/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ProcedureCheckVersion.cs
 * author:    taoye
 * created:   2026/3/12
 * descrip:   版本检查流程
 *            职责：大版本检查 → 资源清单加载 → 补丁差异检查 → 路由跳转。
 ***************************************************************/

using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using ProcedureOwner = NovaFramework.Runtime.IFsm<NovaFramework.Runtime.IProcedureManager>;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 版本检查流程。
    /// 1. await IAppManager.CheckAsync 获取大版本检查结果。
    /// 2. ForcedDownload → 直接跳 ProcedureAppDownload，跳过资源检查。
    /// 3. 非强更且 EnableHotfix=true → LoadManifestAsync 加载清单 → HasPatchAsync 判断补丁。
    /// 4. 非强更且 EnableHotfix=false → 跳过资源热更检查，直接继续后续启动链。
    /// 4. 路由：RecommendedDownload → ProcedureAppDownload | hasPatch → ProcedureHotfix | 否则 → ProcedureLoadDll。
    /// </summary>
    public sealed class ProcedureCheckVersion : ProcedureBase
    {
        /// <summary>
        /// 版本检查是否完成。
        /// </summary>
        private bool m_CheckComplete;

        /// <summary>
        /// App 大版本检查结果。
        /// </summary>
        private AppVersionResult m_AppResult;

        /// <summary>
        /// 是否存在资源补丁。
        /// </summary>
        private bool m_HasAssetPatch;

        /// <summary>
        /// 检查过程是否发生不可恢复异常（用于异常保护跳转）。
        /// </summary>
        private bool m_HasError;

        /// <summary>
        /// 进入流程时调用。
        /// </summary>
        /// <param name="procedureOwner">流程持有者。</param>
        protected internal override void OnEnter(ProcedureOwner procedureOwner)
        {
            base.OnEnter(procedureOwner);

            m_CheckComplete = false;
            m_AppResult = AppVersionResult.NoDownload;
            m_HasAssetPatch = false;
            m_HasError = false;

            Log.Debug(LogTag.Procedure, "ProcedureCheckVersion — 开始版本检查。");
            RunCheckAsync(procedureOwner, CancellationToken).Forget();
        }

        /// <summary>
        /// 流程轮询时调用。
        /// </summary>
        /// <param name="procedureOwner">流程持有者。</param>
        protected internal override void OnUpdate(ProcedureOwner procedureOwner)
        {
            base.OnUpdate(procedureOwner);

            if (!m_CheckComplete)
            {
                return;
            }

            if (m_HasError)
            {
                Log.Warning(LogTag.Procedure, "ProcedureCheckVersion 检查异常，降级跳 ProcedureLoadDll。");
                ChangeState<ProcedureLoadDll>(procedureOwner);
                return;
            }

            procedureOwner.SetData(ProcedureDataKeys.AppVersionResult, m_AppResult);
            procedureOwner.SetData(ProcedureDataKeys.HasAssetPatch, m_HasAssetPatch);
            Log.Debug(LogTag.Procedure, Txt.Format("版本检查完成 AppResult={0} HasAssetPatch={1}", m_AppResult, m_HasAssetPatch));

            if (m_AppResult == AppVersionResult.ForcedDownload)
            {
                ChangeState<ProcedureAppDownload>(procedureOwner);
                return;
            }

            if (m_AppResult == AppVersionResult.RecommendedDownload)
            {
                ChangeState<ProcedureAppDownload>(procedureOwner);
                return;
            }

            if (m_HasAssetPatch)
            {
                ChangeState<ProcedureHotfix>(procedureOwner);
                return;
            }

            ChangeState<ProcedureLoadDll>(procedureOwner);
        }

        /// <summary>
        /// 异步执行版本检查完整流程：大版本检查 → 清单加载 → 补丁判断。
        /// ForcedDownload 时提前跳出，不执行资源检查。
        /// EnableHotfix=false 时仅跳过资产热更检查，不影响 App 大版本检测。
        /// </summary>
        /// <param name="procedureOwner">流程持有者。</param>
        /// <param name="ct">取消令牌。</param>
        private async UniTaskVoid RunCheckAsync(ProcedureOwner procedureOwner, CancellationToken ct)
        {
            try
            {
                IAppManager appManager = FrameworkManagersGroup.GetManager<IAppManager>();
                m_AppResult = await appManager.CheckAsync(ct);
                Log.Debug(LogTag.Procedure, Txt.Format("大版本检查结果: {0}", m_AppResult));

                if (m_AppResult == AppVersionResult.ForcedDownload)
                {
                    Log.Debug(LogTag.Procedure, "命中强更条件，跳转大版本下载。");
                    m_HasAssetPatch = false;
                    m_CheckComplete = true;
                    return;
                }

                AssetComponent assetComponent = FrameworkComponentsGroup.GetComponent<AssetComponent>();
                if (!assetComponent.EnableHotfix)
                {
                    Log.Debug(LogTag.Procedure, "EnableHotfix=false，跳过资源热更检查。");
                    m_HasAssetPatch = false;
                    m_CheckComplete = true;
                    return;
                }

                IAssetManager assetManager = FrameworkManagersGroup.GetManager<IAssetManager>();
                await assetManager.BootstrapAsync(ct);
                ct.ThrowIfCancellationRequested();
                await assetManager.LoadManifestAsync(null, ct);
                m_HasAssetPatch = await assetManager.HasPatchAsync(null, ct);
                Log.Debug(LogTag.Procedure, Txt.Format("资源补丁检查: HasPatch={0}", m_HasAssetPatch));
            }
            catch (OperationCanceledException)
            {
                Log.Debug(LogTag.Procedure, "ProcedureCheckVersion 已取消。");
                return;
            }
            catch (Exception e)
            {
                Log.Warning(LogTag.Procedure, Txt.Format("ProcedureCheckVersion 异常：{0}", e.Message));
                m_HasError = true;
            }

            m_CheckComplete = true;
        }
    }
}
