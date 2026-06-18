/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ProcedurePreload.cs
 * author:    taoye
 * created:   2026/3/12
 * descrip:   预加载流程（含 SDK 初始化）
 *            职责：加载基础配置 -> 初始化 SDK -> 加载业务资源。
 ***************************************************************/

using System;
using Cysharp.Threading.Tasks;
using NovaFramework.Runtime;
using ProcedureOwner = NovaFramework.Runtime.IFsm<NovaFramework.Runtime.IProcedureManager>;

namespace NovaFramework.Sdk.Appsflyer.Samples.Runtime
{
    /// <summary>
    /// 预加载流程。
    /// 1. 显示进度面板（Preload 阶段）。
    /// 2. 按步骤加载：Persist -> configs -> SDK -> 数据表 -> 业务资源（共 9 步）。
    /// 3. 全部完成后跳转 ProcedureLogin。
    /// </summary>
    public class ProcedurePreload : ProcedureBase
    {
        /// <summary>
        /// 预加载总步骤数。
        /// </summary>
        private const int c_TotalSteps = 9;

        /// <summary>
        /// 已完成步骤数。
        /// </summary>
        private int m_CompletedSteps;

        /// <summary>
        /// 是否全部加载完成。
        /// </summary>
        private bool m_AllLoaded;

        /// <summary>
        /// 进入流程时调用。
        /// </summary>
        /// <param name="procedureOwner">流程持有者。</param>
        protected override void OnEnter(ProcedureOwner procedureOwner)
        {
            base.OnEnter(procedureOwner);

            m_CompletedSteps = 0;
            m_AllLoaded = false;

            LauncherUIController.ShowProgress(LauncherStage.Preload);
            Log.Debug(LogTag.Procedure, "ProcedurePreload — 开始预加载。");
            RunPreloadAsync().Forget();
        }

        /// <summary>
        /// 流程轮询时调用。
        /// </summary>
        /// <param name="procedureOwner">流程持有者。</param>
        protected override void OnUpdate(ProcedureOwner procedureOwner)
        {
            base.OnUpdate(procedureOwner);

            if (!m_AllLoaded)
            {
                return;
            }

            ChangeState<ProcedureLogin>(procedureOwner);
        }

        /// <summary>
        /// 离开流程时调用。
        /// </summary>
        /// <param name="procedureOwner">流程持有者。</param>
        /// <param name="isShutdown">是否因流程管理器关闭而离开。</param>
        protected override void OnLeave(ProcedureOwner procedureOwner, bool isShutdown)
        {
            base.OnLeave(procedureOwner, isShutdown);

            LauncherUIController.DestroyAll();
        }

        /// <summary>
        /// 按步骤顺序预加载：configs -> SDK -> 数据表 -> 业务资源。
        /// </summary>
        private async UniTaskVoid RunPreloadAsync()
        {
            try
            {
                // 初始化本地存储 AES 默认 Key/IV（示例值；正式项目请替换为自有 16 字节 Key/IV）
                Util.Encrypt.AES.Configure("NovaSampleKey_16", "NovaSampleIv__16");

                Log.Debug(LogTag.Procedure, "Preload Step 1/9 — 初始化持久化后端。");
                await Nova.Persist.LoadAsync();
                CancellationToken.ThrowIfCancellationRequested();
                OnStepComplete();

                Log.Debug(LogTag.Procedure, "Preload Step 2/9 — 加载基础配置。");
                await Nova.Config.LoadAsync();
                CancellationToken.ThrowIfCancellationRequested();
                OnStepComplete();

                Log.Debug(LogTag.Procedure, "Preload Step 3/9 — 加载网络配置。");
                await Nova.Network.LoadAsync();
                CancellationToken.ThrowIfCancellationRequested();
                OnStepComplete();

                Log.Debug(LogTag.Procedure, "Preload Step 4/9 — 初始化 SDK。");
                await Nova.SDK.InitializeTask;
                CancellationToken.ThrowIfCancellationRequested();
                OnStepComplete();

                Log.Debug(LogTag.Procedure, "Preload Step 5/9 — 加载数据表。");
                await Nova.Table.LoadAsync();
                CancellationToken.ThrowIfCancellationRequested();
                OnStepComplete();

                Log.Debug(LogTag.Procedure, "Preload Step 6/9 — 加载UI配置。");
                await Nova.UI.LoadAsync();
                CancellationToken.ThrowIfCancellationRequested();
                OnStepComplete();

                Log.Debug(LogTag.Procedure, "Preload Step 7/9 — 加载声音数据。");
                await Nova.Sound.LoadAsync();
                CancellationToken.ThrowIfCancellationRequested();
                OnStepComplete();

                Log.Debug(LogTag.Procedure, "Preload Step 8/9 — 加载振动数据。");
                await Nova.Vibrate.LoadAsync();
                CancellationToken.ThrowIfCancellationRequested();
                OnStepComplete();

                Log.Debug(LogTag.Procedure, "Preload Step 9/9 — 加载本地化数据。");
                await Nova.Localization.LoadAsync();
                CancellationToken.ThrowIfCancellationRequested();
                await Nova.Localization.InitCurrentLanguageAsync();
                CancellationToken.ThrowIfCancellationRequested();
                OnStepComplete();

                Log.Debug(LogTag.Procedure, "预加载全部完成。");
                m_AllLoaded = true;
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception e)
            {
                Log.Error(LogTag.Procedure, Txt.Format("预加载异常: {0}", e.Message));
                LauncherUIController.ShowDialog(
                    LauncherDialogType.PreloadFailed,
                    () => RunPreloadAsync().Forget(),
                    () => UnityEngine.Application.Quit());
            }
        }

        /// <summary>
        /// 单步完成时更新进度。
        /// </summary>
        private void OnStepComplete()
        {
            m_CompletedSteps++;
            LauncherUIController.UpdateProgress((float)m_CompletedSteps / c_TotalSteps);
        }
    }
}
