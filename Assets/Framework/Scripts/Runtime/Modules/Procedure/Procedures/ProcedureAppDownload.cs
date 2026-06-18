/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ProcedureAppDownload.cs
 * author:    taoye
 * created:   2026/5/15
 * descrip:   大版本下载提示流程
 *            职责：弹出强制 / 推荐更新弹窗，按 DownloadRoute 跳商店或下载 APK；
 *            强制更新取消时退出，推荐更新取消时回到热更 / 启动链继续执行。
 ***************************************************************/

using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using ProcedureOwner = NovaFramework.Runtime.IFsm<NovaFramework.Runtime.IProcedureManager>;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 大版本下载提示流程。
    /// 1. 根据 AppVersionResult 弹出 ForcedDownload / RecommendedDownload 弹窗。
    /// 2. 确认后按 AppManagerConfig.DownloadRoute 选择跳商店或下载 APK。
    /// 3. 操作结束（无论成败）重新显示弹窗，直到用户选择退出或下载完成。
    /// 4. 推荐更新取消后，按 HasAssetPatch 回到热更或 DLL 启动流程。
    /// </summary>
    public sealed class ProcedureAppDownload : ProcedureBase
    {
        /// <summary>
        /// 流程是否完成（可跳出弹窗循环）。
        /// </summary>
        private bool m_Complete;

        /// <summary>
        /// 本次弹窗对应的大版本结果。
        /// </summary>
        private AppVersionResult m_AppResult;

        /// <summary>
        /// 推荐更新取消后是否还需要继续热更。
        /// </summary>
        private bool m_HasAssetPatch;

        /// <summary>
        /// 是否已有更新动作正在执行，防止确认按钮重入。
        /// </summary>
        private bool m_IsOperationInProgress;

        /// <summary>
        /// 进入流程时调用。
        /// </summary>
        /// <param name="procedureOwner">流程持有者。</param>
        protected internal override void OnEnter(ProcedureOwner procedureOwner)
        {
            base.OnEnter(procedureOwner);

            m_Complete = false;
            m_AppResult = procedureOwner.GetData<AppVersionResult>(ProcedureDataKeys.AppVersionResult);
            m_HasAssetPatch = procedureOwner.GetData<bool>(ProcedureDataKeys.HasAssetPatch);
            m_IsOperationInProgress = false;

            Log.Debug(LogTag.Procedure, Txt.Format("ProcedureAppDownload — 显示大版本更新弹窗。Result={0} HasAssetPatch={1}", m_AppResult, m_HasAssetPatch));
            ShowDialog();
        }

        /// <summary>
        /// 流程轮询时调用。
        /// </summary>
        /// <param name="procedureOwner">流程持有者。</param>
        protected internal override void OnUpdate(ProcedureOwner procedureOwner)
        {
            base.OnUpdate(procedureOwner);

            if (!m_Complete)
            {
                return;
            }

            if (m_AppResult == AppVersionResult.RecommendedDownload && m_HasAssetPatch)
            {
                ChangeState<ProcedureHotfix>(procedureOwner);
                return;
            }

            ChangeState<ProcedureLoadDll>(procedureOwner);
        }

        /// <summary>
        /// 离开流程时调用。
        /// </summary>
        /// <param name="procedureOwner">流程持有者。</param>
        /// <param name="isShutdown">是否因流程管理器关闭而离开。</param>
        protected internal override void OnLeave(ProcedureOwner procedureOwner, bool isShutdown)
        {
            base.OnLeave(procedureOwner, isShutdown);
            procedureOwner.RemoveData(ProcedureDataKeys.AppVersionResult);
            procedureOwner.RemoveData(ProcedureDataKeys.HasAssetPatch);
            m_IsOperationInProgress = false;

            if (isShutdown)
            {
                LauncherUIController.DestroyDialog();
            }
        }

        /// <summary>
        /// 显示大版本更新弹窗。
        /// 确认回调：执行下载/跳商店操作。
        /// 取消回调：强制更新直接退出；推荐更新继续后续热更 / 启动流程。
        /// </summary>
        private void ShowDialog()
        {
            if (m_Complete)
            {
                return;
            }

            LauncherUIController.DestroyDialog();
            LauncherDialogType dialogType = ResolveDialogType();
            Action onConfirm = () => OnConfirm().Forget();
            Action onCancel = m_AppResult == AppVersionResult.ForcedDownload
                ? (Action)(() =>
                {
                    LauncherUIController.DestroyDialog();
                    Log.Debug(LogTag.Procedure, "用户取消强制更新，应用退出。");
                    Nova.Self.QuitApplication();
                })
                : () =>
                {
                    LauncherUIController.DestroyDialog();
                    Log.Debug(LogTag.Procedure, "用户取消推荐更新，继续后续启动流程。");
                    m_Complete = true;
                };

            LauncherUIController.ShowDialog(dialogType, onConfirm, onCancel);
        }

        /// <summary>
        /// 确认按钮回调：按 DownloadRoute 执行跳商店或下载 APK。
        /// 操作结束后（无论成败）重新调用 ShowDialog 等待用户再次操作。
        /// </summary>
        /// <returns>UniTask。</returns>
        private async UniTaskVoid OnConfirm()
        {
            if (m_Complete || m_IsOperationInProgress || CancellationToken.IsCancellationRequested)
            {
                return;
            }

            m_IsOperationInProgress = true;
            LauncherUIController.DestroyDialog();

            AppComponent appComponent = FrameworkComponentsGroup.GetComponent<AppComponent>();
            AppDownloadRoute route = appComponent.DownloadRoute;
            Log.Debug(LogTag.Procedure, Txt.Format("用户确认大版本更新，Result={0} 路由={1}", m_AppResult, route));

            try
            {
                if (route == AppDownloadRoute.Store)
                {
                    await appComponent.OpenStoreAsync(CancellationToken);
                }
                else
                {
                    await appComponent.DownloadAsync(CancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception e)
            {
                Log.Warning(LogTag.Procedure, Txt.Format("大版本更新操作异常: {0}", e.Message));
            }
            finally
            {
                m_IsOperationInProgress = false;

                if (!m_Complete && !CancellationToken.IsCancellationRequested)
                {
                    ShowDialog();
                }
            }
        }

        /// <summary>
        /// 根据 AppVersionResult 选择弹窗类型。
        /// </summary>
        /// <returns>弹窗类型。</returns>
        private LauncherDialogType ResolveDialogType()
        {
            return m_AppResult == AppVersionResult.ForcedDownload
                ? LauncherDialogType.ForcedDownload
                : LauncherDialogType.RecommendedDownload;
        }
    }
}
