/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ProcedureLoadDll.cs
 * author:    taoye
 * created:   2026/5/6
 * descrip:   HybridCLR 业务 DLL 加载流程
 *            职责：加载 Manifest → IConfigManager.LoadAsync（幂等）→ 补 AOT metadata
 *                  → 加载业务 DLL → 刷新程序集缓存 → 扫描并延迟注册业务 Procedure → 跳转业务入口。
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using ProcedureOwner = NovaFramework.Runtime.IFsm<NovaFramework.Runtime.IProcedureManager>;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// HybridCLR 业务 DLL 加载流程。
    /// 执行顺序：
    /// 1. 加载 Manifest（DLL 以 AB 包形式存储，Manifest 是所有 AB 加载的前置条件）。
    /// 2. IConfigManager.LoadAsync（幂等；已加载则直接返回）。
    /// 3. 补 AOT metadata（IL2CPP 下生效，Editor no-op）。
    /// 4. 加载业务 DLL（IL2CPP 下生效，Editor 源码模式 no-op）。
    /// 5. 刷新 Util.Assembly 的程序集缓存。
    /// 6. 扫描业务程序集中的 ProcedureBase 子类，Activator.CreateInstance 后
    ///    调用 RegisterAdditionalProcedures 批量注册。
    /// 7. 按 IConfigManager.Namespace + IConfigManager.GameEntranceProcedureName
    ///    拼出业务入口全名，ChangeState 跳转。
    /// 通过 FrameworkManagersGroup.GetManager<IConfigManager>() 获取配置，不依赖 Nova.Config 公开 API。
    /// </summary>
    [Serializable]
    public sealed class ProcedureLoadDll : ProcedureBase
    {
        /// <summary>
        /// 加载是否完成（RunLoadAsync 完成后置 true）。
        /// </summary>
        private bool m_LoadComplete;

        /// <summary>
        /// 加载过程中缓存的异常，由 OnUpdate 抛出到主线程。
        /// </summary>
        private Exception m_LoadException;

        /// <summary>
        /// 异常是否已向主线程抛出过一次；防止 OnUpdate 每帧重复抛出同一异常刷屏。
        /// </summary>
        private bool m_ExceptionThrown;

        /// <summary>
        /// 加载完成后需要跳转的目标 Procedure 类型。
        /// </summary>
        private Type m_EntranceType;

        /// <summary>
        /// DLL 加载是否已完成（Inspector 只读展示）。
        /// </summary>
        internal bool LoadComplete => m_LoadComplete;

        /// <summary>
        /// DLL 加载过程中产生的异常（Inspector 只读展示，null 表示无错误）。
        /// </summary>
        internal Exception LoadException => m_LoadException;

        /// <summary>
        /// 加载完成后的业务入口 Procedure 类型（Inspector 只读展示）。
        /// </summary>
        internal Type EntranceType => m_EntranceType;

        /// <summary>
        /// 进入流程时调用；重置状态并启动异步 DLL 加载任务。
        /// </summary>
        /// <param name="procedureOwner">流程持有者。</param>
        protected internal override void OnEnter(ProcedureOwner procedureOwner)
        {
            base.OnEnter(procedureOwner);
            m_LoadComplete = false;
            m_LoadException = null;
            m_ExceptionThrown = false;
            m_EntranceType = null;
            RunLoadAsync(procedureOwner).Forget();
        }

        /// <summary>
        /// 流程轮询时调用；等待异步加载完成后执行跳转或抛出异常。
        /// </summary>
        /// <param name="procedureOwner">流程持有者。</param>
        protected internal override void OnUpdate(ProcedureOwner procedureOwner)
        {
            base.OnUpdate(procedureOwner);
            if (!m_LoadComplete)
            {
                return;
            }

            if (m_LoadException != null)
            {
                if (m_ExceptionThrown)
                {
                    // 异常已抛出一次，后续帧保持停滞，不再重复抛出。
                    return;
                }
                m_ExceptionThrown = true;
                throw m_LoadException;
            }

            Log.Debug(LogTag.Procedure, Txt.Format("跳转业务入口 {0}", m_EntranceType.FullName));
            ChangeState(procedureOwner, m_EntranceType);
        }

        /// <summary>
        /// 异步执行完整的 DLL 加载时序：
        /// Manifest → Config → AOT metadata → 业务 DLL → 刷新缓存 → 扫描注册 → 跳转。
        /// 完成后将结果写入运行时字段，由 OnUpdate 驱动跳转。
        /// </summary>
        /// <param name="procedureOwner">流程持有者，用于延迟注册业务 Procedure。</param>
        private async UniTaskVoid RunLoadAsync(ProcedureOwner procedureOwner)
        {
            // 强制脱离 OnEnter 同步调用栈，确保 m_IsChangingState 回落 false 后再执行。
            // Editor 下各 LoadXxxAsync 均为同步 no-op，若不 Yield 则 RegisterAdditionalProcedures
            // 会在 OnEnter 调用栈内触发 Fsm.AddStates，命中守卫抛 InvalidOperationException。
            await UniTask.Yield();

            try
            {
                IConfigManager configManager = FrameworkManagersGroup.GetManager<IConfigManager>();
                if (configManager == null)
                {
                    throw new InvalidOperationException("[ProcedureLoadDll] IConfigManager 未就绪。");
                }

                // 1. 启动 Asset 模块并加载清单。
                // BootstrapAsync 注册包/创建解密器/初始化底层资源系统；
                // LoadManifestAsync 拉版本号并加载清单，幂等——ProcedureCheckVersion 已加载时直接返回。
                Log.Debug(LogTag.Procedure, "[ProcedureLoadDll] BootstrapAsync + LoadManifestAsync。");
                IAssetManager assetManager = FrameworkManagersGroup.GetManager<IAssetManager>();
                await assetManager.BootstrapAsync(CancellationToken);
                CancellationToken.ThrowIfCancellationRequested();
                await assetManager.LoadManifestAsync(null, CancellationToken);
                CancellationToken.ThrowIfCancellationRequested();

                // 2. 加载 Config（幂等；已加载则直接返回，无二次 AB 加载开销）。
                Log.Debug(LogTag.Procedure, "[ProcedureLoadDll] 加载 ConfigRuntimeSO。");
                await configManager.LoadAsync();
                CancellationToken.ThrowIfCancellationRequested();

                // 3. 并行发起所有 AOT metadata 加载（IL2CPP 下生效，Editor no-op）。
                // LoadMetadataForAOTAssembly 多次调用无顺序依赖，UniTask 调度仍在主线程，
                // HashSet 写入由 LoadAotMetadataAsync 内部原子化守卫保护。
                // 注意：AOT metadata 必须在业务 DLL 之前全部加载完成。
                IReadOnlyList<DllAssetEntry> aotList = configManager.AotMetadataDlls;
                int aotCount = aotList != null ? aotList.Count : 0;
                if (aotCount > 0)
                {
                    UniTask[] aotTasks = new UniTask[aotCount];
                    for (int i = 0; i < aotCount; i++)
                    {
                        DllAssetEntry entry = aotList[i];
                        aotTasks[i] = Util.HybridCLR.LoadAotMetadataAsync(entry.AssetLocation);
                    }
                    await UniTask.WhenAll(aotTasks);
                    CancellationToken.ThrowIfCancellationRequested();
                }

                // 4. 加载业务 DLL（IL2CPP 下生效，Editor no-op）。
                IReadOnlyList<DllAssetEntry> dllList = configManager.GameDlls;
                int dllCount = dllList != null ? dllList.Count : 0;
                for (int i = 0; i < dllCount; i++)
                {
                    DllAssetEntry entry = dllList[i];
                    await Util.HybridCLR.LoadGameAssemblyAsync(entry.AssetLocation);
                    CancellationToken.ThrowIfCancellationRequested();
                }

                // 5. 刷新程序集缓存，让 Util.Assembly 反射视图能看见新加载的程序集。
                Util.Assembly.RefreshAssemblies();

                // 6. 扫描业务程序集中的 ProcedureBase 子类并批量注册。
                // 前置校验：Namespace 不可为空，否则 GetAssembly("") 返回的程序集无意义且后续拼全名失败。
                string businessAssemblyName = configManager.Namespace;
                if (string.IsNullOrWhiteSpace(businessAssemblyName))
                {
                    throw new InvalidOperationException("[ProcedureLoadDll] ConfigRuntimeSO.Namespace 未配置，请通过 ConfigWindow → HybridCLR 配置 面板填写并重新导出。");
                }
                System.Reflection.Assembly businessAsm = Util.Assembly.GetAssembly(businessAssemblyName);
                if (businessAsm == null)
                {
                    throw new InvalidOperationException(
                        Txt.Format("[ProcedureLoadDll] 业务程序集 '{0}' 未加载，请检查 ConfigRuntimeSO.GameDlls 配置。", businessAssemblyName));
                }

                Type[] procTypes = businessAsm.GetTypes()
                    .Where(t => !t.IsAbstract && t.IsSubclassOf(typeof(ProcedureBase)))
                    .ToArray();
                ProcedureBase[] procs = procTypes
                    .Select(t => (ProcedureBase)Activator.CreateInstance(t))
                    .ToArray();
                procedureOwner.Owner.RegisterAdditionalProcedures(procs);

                Log.Debug(LogTag.Procedure, Txt.Format("[ProcedureLoadDll] 注册业务 Procedure 数量：{0}", procs.Length));

                // 7. 定位业务入口 Procedure 类型。
                string gameEntranceProcedureName = configManager.GameEntranceProcedureName;
                if (string.IsNullOrWhiteSpace(gameEntranceProcedureName))
                {
                    throw new InvalidOperationException("[ProcedureLoadDll] ConfigRuntimeSO.GameEntranceProcedureName 未配置，请通过 ConfigWindow → HybridCLR 配置 面板填写并重新导出。");
                }
                string entranceFullName = Txt.Format("{0}.{1}", businessAssemblyName, gameEntranceProcedureName);
                Type entrance = businessAsm.GetType(entranceFullName);
                if (entrance == null)
                {
                    string loaded = string.Join(",", procTypes.Select(t => t.Name));
                    throw new InvalidOperationException(
                        Txt.Format("[ProcedureLoadDll] 入口 Procedure '{0}' 未找到。已加载类型：[{1}]", entranceFullName, loaded));
                }

                Log.Debug(LogTag.Procedure, Txt.Format("业务入口定位完成: {0}", entranceFullName));
                m_EntranceType = entrance;
                m_LoadComplete = true;
            }
            catch (OperationCanceledException)
            {
                // 流程已离开，静默退出。
            }
            catch (Exception e)
            {
                m_LoadException = e;
                m_LoadComplete = true;
            }
        }
    }
}
