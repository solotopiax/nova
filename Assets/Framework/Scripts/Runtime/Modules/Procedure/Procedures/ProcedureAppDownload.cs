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
    /// 3. APK 保存返回路径，独立安装开启时等待系统结果；重试复用文件，文件交付后不再重复下载。
    /// 4. 推荐更新取消后，按启动资源工作状态回到热更/Warmup 或 DLL 启动流程。
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
        /// 推荐更新取消后是否还需要继续下载或 WebGL Warmup。
        /// </summary>
        private bool m_RequiresStartupAssetWork;

        /// <summary>
        /// 是否已有更新动作正在执行，防止确认按钮重入。
        /// </summary>
        private bool m_IsOperationInProgress;

        /// <summary>本流程已完成下载的本地路径；安装失败、拒绝或权限跳转后重试不重新下载。</summary>
        private string m_DownloadedApkPath;

        /// <summary>没有启用可选安装时展示文件交付结果，确认后推荐续行/强制退出。</summary>
        private bool m_IsFileDelivered;

        /// <summary>离开或重新进入时使旧异步操作失效，不依赖成员令牌被下一轮替换后的状态。</summary>
        private int m_OperationVersion;

        /// <summary>
        /// 进入流程时调用。
        /// </summary>
        /// <param name="procedureOwner">流程持有者。</param>
        protected internal override void OnEnter(ProcedureOwner procedureOwner)
        {
            base.OnEnter(procedureOwner);

            m_Complete = false;
            m_AppResult = procedureOwner.GetData<AppVersionResult>(ProcedureDataKeys.AppVersionResult);
            m_RequiresStartupAssetWork = procedureOwner.GetData<bool>(ProcedureDataKeys.RequiresStartupAssetWork);
            m_IsOperationInProgress = false;
            m_DownloadedApkPath = null;
            m_IsFileDelivered = false;
            m_OperationVersion++;

            if (m_AppResult != AppVersionResult.ForcedDownload
                && m_AppResult != AppVersionResult.RecommendedDownload)
            {
                Log.Warning(LogTag.Procedure, Txt.Format("ProcedureAppDownload 收到无效结果，跳过下载提示。Result={0}", m_AppResult));
                m_Complete = true;
                return;
            }

            Log.Debug(LogTag.Procedure, Txt.Format(
                "ProcedureAppDownload — 显示大版本更新弹窗。Result={0} RequiresStartupAssetWork={1}",
                m_AppResult,
                m_RequiresStartupAssetWork));
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

            if (m_AppResult == AppVersionResult.RecommendedDownload && m_RequiresStartupAssetWork)
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
            m_OperationVersion++;
            procedureOwner.RemoveData(ProcedureDataKeys.AppVersionResult);
            procedureOwner.RemoveData(ProcedureDataKeys.HasAssetPatch);
            procedureOwner.RemoveData(ProcedureDataKeys.RequiresStartupAssetWork);
            m_IsOperationInProgress = false;

            LauncherUIController.DestroyDialog();
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
            if (m_IsFileDelivered)
            {
                LauncherUIController.ShowDialog(LauncherDialogType.AppDownloaded, () =>
                {
                    LauncherUIController.DestroyDialog();
                    if (m_AppResult == AppVersionResult.ForcedDownload) Nova.Self.QuitApplication();
                    else m_Complete = true;
                });
                return;
            }
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
                    AppComponent appComponent = FrameworkComponentsGroup.GetComponent<AppComponent>();
                    appComponent.RecordRecommendedDownloadDismissed();
                    Log.Debug(LogTag.Procedure, "用户取消推荐更新，继续后续启动流程。");
                    m_Complete = true;
                };

            LauncherUIController.ShowDialog(dialogType, onConfirm, onCancel);
        }

        /// <summary>
        /// 确认按钮回调：按 DownloadRoute 执行跳商店或下载 APK。
        /// 冻结本轮令牌与版本；APK 路径保存后只重试独立安装，不重新下载或将系统界面当安装成功。
        /// </summary>
        /// <returns>UniTask。</returns>
        private async UniTaskVoid OnConfirm()
        {
            if (m_Complete || m_IsOperationInProgress || CancellationToken.IsCancellationRequested)
            {
                return;
            }

            m_IsOperationInProgress = true;
            CancellationToken operationToken = CancellationToken;
            int operationVersion = m_OperationVersion;
            LauncherUIController.DestroyDialog();

            AppComponent appComponent = FrameworkComponentsGroup.GetComponent<AppComponent>();
            AppDownloadRoute route = appComponent.DownloadRoute;
            Log.Debug(LogTag.Procedure, Txt.Format("用户确认大版本更新，Result={0} 路由={1}", m_AppResult, route));

            try
            {
                if (route == AppDownloadRoute.Store)
                {
                    await appComponent.OpenStoreAsync(operationToken);
                }
                else
                {
                    if (string.IsNullOrEmpty(m_DownloadedApkPath))
                    {
                        string downloaded = await appComponent.DownloadAsync(operationToken);
                        operationToken.ThrowIfCancellationRequested();
                        if (operationVersion != m_OperationVersion) return;
                        m_DownloadedApkPath = downloaded;
                        Log.Debug(LogTag.Procedure, "APK 文件已保存：{0}", downloaded);
                    }
                    NativeComponent native = FrameworkComponentsGroup.GetComponent<NativeComponent>();
                    if (native == null || !native.IsAppInstallEnabled)
                    {
                        m_IsFileDelivered = true;
                    }
                    else
                    {
                        NativeAppInstallResult result = await native.InstallApkAsync(m_DownloadedApkPath, operationToken);
                        operationToken.ThrowIfCancellationRequested();
                        if (operationVersion != m_OperationVersion) return;
                        if (result.Status == NativeAppInstallStatus.Unsupported || result.Status == NativeAppInstallStatus.Disabled)
                            m_IsFileDelivered = true;
                        else if (result.Status == NativeAppInstallStatus.PermissionRequired)
                        {
                            await native.OpenPackageInstallSettingsAsync();
                            Log.Warning(LogTag.Procedure, "请允许当前应用的安装来源，返回后可确认重试；APK 文件已保留。");
                        }
                        else if (result.IsInstalled)
                        {
                            // 自身升级通常由系统自然替换并终止旧进程；框架不主动 kill，也不自动重启。
                            m_IsFileDelivered = true;
                            Log.Debug(LogTag.Procedure, "系统报告 APK 安装完成。");
                        }
                        else Log.Warning(LogTag.Procedure, "APK 安装未完成，状态={0}：{1}", result.Status, result.ErrorMessage);
                    }
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
                if (operationVersion == m_OperationVersion)
                {
                    m_IsOperationInProgress = false;
                    if (!m_Complete && !operationToken.IsCancellationRequested) ShowDialog();
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
