/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ProcedureHotfix.cs
 * author:    taoye
 * created:   2026/3/12
 * descrip:   资源补丁下载流程
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using ProcedureOwner = NovaFramework.Runtime.IFsm<NovaFramework.Runtime.IProcedureManager>;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 资源补丁下载流程。
    /// 1. 显示进度面板（Hotfix 阶段）。
    /// 2. 非 WebGL 使用 Downloader；WebGL TagsOnLaunch / AllOnLaunch 使用 WarmupGroup。
    /// 3. 失败时弹出 HotfixFailed 弹窗：确认重试，取消按 QuitOnFailedOrCancel 强退或跳过补丁进入游戏。
    /// 4. 成功后若 AutoClearUnusedCacheOnHotfix 为 true 则执行清缓存（失败不阻断）。
    /// 5. m_Complete=true 时 OnUpdate 跳转 ProcedureLoadDll。
    /// </summary>
    public sealed class ProcedureHotfix : ProcedureBase
    {
        /// <summary>
        /// 是否完成（m_Complete=true 时 OnUpdate 触发跳转）。
        /// </summary>
        private bool m_Complete;

        /// <summary>
        /// 本轮下载或 WebGL 预热是否成功。
        /// </summary>
        private bool m_Success;

        /// <summary>
        /// 当前是否执行 WebGL Warmup，用于区分计数进度与下载字节进度。
        /// </summary>
        private bool m_IsWarmup;

        /// <summary>
        /// 是否已有下载或 Warmup 轮次正在执行，防止重试按钮重复回调并发启动。
        /// </summary>
        private bool m_AttemptInProgress;

        /// <summary>
        /// 用户手动点击重试的累计次数（仅用于日志，不设上限）。
        /// </summary>
        private int m_UserRetryCount;

        /// <summary>
        /// 当前轮次下载所用的 CancellationTokenSource（链接到流程生命周期 ct）。
        /// OnLeave 或 OnRetryClicked 时取消并置 null。
        /// </summary>
        private CancellationTokenSource m_DownloadCts;

        /// <summary>
        /// 上次输出进度日志时的整数百分比（-1 表示尚未输出）。
        /// </summary>
        private int m_LastLoggedPercent;

        /// <summary>
        /// 最近一次进度回调缓存（已完成文件数）。
        /// </summary>
        private int m_LastFinishedCount;

        /// <summary>
        /// 最近一次进度回调缓存（总文件数）。
        /// </summary>
        private int m_LastTotalCount;

        /// <summary>
        /// 最近一次进度回调缓存（已完成字节数）。
        /// </summary>
        private long m_LastFinishedBytes;

        /// <summary>
        /// 最近一次进度回调缓存（总字节数）。
        /// </summary>
        private long m_LastTotalBytes;

        /// <summary>
        /// 最近一次进度回调缓存（百分比，0~100）。
        /// </summary>
        private int m_LastPercent;

        /// <summary>
        /// 进入流程时调用。重置所有状态并启动第一轮下载。
        /// </summary>
        /// <param name="procedureOwner">流程持有者。</param>
        protected internal override void OnEnter(ProcedureOwner procedureOwner)
        {
            base.OnEnter(procedureOwner);

            m_Complete = false;
            m_Success = false;
            m_IsWarmup = false;
            m_AttemptInProgress = false;
            m_UserRetryCount = 0;
            m_DownloadCts = null;
            ResetProgressCache();

            procedureOwner.RemoveData(ProcedureDataKeys.AppVersionResult);
            procedureOwner.RemoveData(ProcedureDataKeys.HasAssetPatch);
            procedureOwner.RemoveData(ProcedureDataKeys.RequiresStartupAssetWork);

            Log.Debug(LogTag.Procedure, "触发资源热更新。");
            StartDownloadAttempt(CancellationToken);
        }

        /// <summary>
        /// 流程轮询时调用。m_Complete=true 时跳转 ProcedureLoadDll。
        /// </summary>
        /// <param name="procedureOwner">流程持有者。</param>
        protected internal override void OnUpdate(ProcedureOwner procedureOwner)
        {
            base.OnUpdate(procedureOwner);

            if (!m_Complete)
            {
                return;
            }

            if (m_IsWarmup)
            {
                Log.Debug(LogTag.Procedure,
                    Txt.Format("WebGL 资源预热进度 {0}% [完成 {1}/{2}]", m_LastPercent, m_LastFinishedCount, m_LastTotalCount));
            }
            else
            {
                Log.Debug(LogTag.Procedure,
                    Txt.Format("热更新进度 {0}% [字节 {1}/{2}] [完成 {3}/{4}]", m_LastPercent, m_LastFinishedBytes, m_LastTotalBytes, m_LastFinishedCount, m_LastTotalCount));
            }
            Log.Debug(LogTag.Procedure, Txt.Format("热更新完毕 成功={0}", m_Success));
            ChangeState<ProcedureLoadDll>(procedureOwner);
        }

        /// <summary>
        /// 离开流程时调用。
        /// 进度面板跨流程存活，正常启动路径由业务入口统一回收；FSM 关闭时在本流程兜底销毁。
        /// </summary>
        /// <param name="procedureOwner">流程持有者。</param>
        /// <param name="isShutdown">是否因流程管理器关闭而离开。</param>
        protected internal override void OnLeave(ProcedureOwner procedureOwner, bool isShutdown)
        {
            base.OnLeave(procedureOwner, isShutdown);

            m_DownloadCts?.Cancel();
            m_DownloadCts?.Dispose();
            m_DownloadCts = null;

            if (isShutdown)
            {
                LauncherUIController.DestroyProgress();
                LauncherUIController.DestroyDialog();
            }
        }

        /// <summary>
        /// 执行一轮补丁下载。
        /// 根据 LaunchHotfixTags 选择整包或切片下载；下载失败后弹出重试弹窗而非直接决策。
        /// </summary>
        /// <param name="ct">流程生命周期取消令牌。</param>
        private async UniTask TryDownloadAsync(CancellationToken ct)
        {
            if (m_AttemptInProgress || m_Complete || ct.IsCancellationRequested)
            {
                return;
            }

            m_AttemptInProgress = true;
            try
            {
                await RunDownloadAttemptAsync(ct);
            }
            catch (OperationCanceledException)
            {
                // 流程离开或框架关闭时由生命周期令牌统一终止当前轮次。
            }
            catch (Exception e)
            {
                Log.Warning(LogTag.Procedure, Txt.Format("资源下载轮次异常: {0}", e.Message));
                if (!ct.IsCancellationRequested)
                {
                    AssetComponent assetComponent = FrameworkComponentsGroup.GetComponent<AssetComponent>();
                    ShowRetryDialog(assetComponent.QuitOnFailedOrCancel, ct);
                }
            }
            finally
            {
                m_AttemptInProgress = false;
            }
        }

        /// <summary>
        /// 执行一次实际下载或 WebGL Warmup；外层统一负责互斥和未预期异常兜底。
        /// </summary>
        /// <param name="ct">流程生命周期取消令牌。</param>
        private async UniTask RunDownloadAttemptAsync(CancellationToken ct)
        {
            AssetComponent assetComponent = FrameworkComponentsGroup.GetComponent<AssetComponent>();
            IAssetManager assetManager = FrameworkManagersGroup.GetManager<IAssetManager>();

            if (assetManager is IAssetStartupWarmupController warmupController
                && warmupController.RequiresLaunchWarmup)
            {
                m_IsWarmup = true;
                await TryWarmupAsync(assetComponent, assetManager, warmupController, ct);
                return;
            }

            m_IsWarmup = false;

            int concurrency = assetComponent.MaxDownloadConcurrency;
            // retry 表示下载重试次数；每次重试都会重新走完主备候选与配置轮数，
            // AssetManager 会在创建 YooAsset 下载器时换算为物理重试次数。
            int logicalRetryCount = assetComponent.RetryDownloadCount;

            List<string> hotfixTags = assetComponent.LaunchHotfixTags;
            IAssetDownloader downloader;

            if (hotfixTags == null || hotfixTags.Count == 0)
            {
                Log.Debug(LogTag.Procedure, "LaunchHotfixTags 为空，执行整包下载。");
                downloader = assetManager.CreateDownloader(package: null, concurrency: concurrency, retry: logicalRetryCount);
            }
            else
            {
                Log.Debug(LogTag.Procedure, Txt.Format("LaunchHotfixTags 非空（{0} 个 Tag），执行切片下载。", hotfixTags.Count));
                downloader = assetManager.CreateDownloaderByTags(hotfixTags.ToArray(), package: null, concurrency: concurrency, retry: logicalRetryCount);
            }

            Log.Debug(LogTag.Procedure, Txt.Format("开始热更新 文件数={0} 总字节={1}", downloader.TotalCount, downloader.TotalBytes));

            if (downloader.IsEmpty)
            {
                Log.Debug(LogTag.Procedure, "无补丁需要下载，直接进入 LoadDll。");
                assetManager.CommitBootableVersion();
                m_Success = true;
                m_Complete = true;
                return;
            }

            LauncherUIController.ShowProgress(LauncherStage.Hotfix);

            downloader.OnProgress += (finished, total, finishedBytes, totalBytes) =>
            {
                LauncherUIController.UpdateProgress(downloader.Progress);
                int percent = (int)(downloader.Progress * 100f);
                m_LastFinishedCount = finished;
                m_LastTotalCount = total;
                m_LastFinishedBytes = finishedBytes;
                m_LastTotalBytes = totalBytes;
                m_LastPercent = percent;
                if (percent / 10 != m_LastLoggedPercent / 10)
                {
                    m_LastLoggedPercent = percent;
                    Log.Debug(LogTag.Procedure, Txt.Format("热更新进度 {0}% [字节 {1}/{2}] [完成 {3}/{4}]", percent, finishedBytes, totalBytes, finished, total));
                }
            };

            downloader.OnFileStarted += (bundle, file, size) =>
            {
                Log.Debug(LogTag.Procedure, Txt.Format("热更新文件 [{0}] size={1}B → {2}", bundle, size, file));
            };

            bool ok;
            m_DownloadCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            try
            {
                ok = await downloader.RunAsync(m_DownloadCts.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception e)
            {
                Log.Warning(LogTag.Procedure, Txt.Format("补丁下载异常: {0}", e.Message));
                ok = false;
            }
            finally
            {
                m_DownloadCts?.Dispose();
                m_DownloadCts = null;
            }

            if (ok)
            {
                assetManager.CommitBootableVersion();
                if (assetComponent.AutoClearUnusedCacheOnHotfix)
                {
                    try
                    {
                        Log.Debug(LogTag.Procedure, "热更完成，开始清理资源系统冗余磁盘缓存。");
                        await assetManager.ClearUnusedCacheAsync(ct: ct);
                        Log.Debug(LogTag.Procedure, "资源系统冗余缓存清理完成。");
                    }
                    catch (Exception e)
                    {
                        Log.Warning(LogTag.Procedure, Txt.Format("资源系统缓存清理异常（不阻断进游戏）: {0}", e.Message));
                    }
                }

                m_Success = true;
                m_Complete = true;
            }
            else
            {
                ShowRetryDialog(assetComponent.QuitOnFailedOrCancel, ct);
            }
        }

        /// <summary>
        /// 执行一轮 WebGL 启动预热；失败或取消后释放本轮全部 Handle，重试会创建新组。
        /// </summary>
        /// <param name="assetComponent">资源组件，用于读取失败策略。</param>
        /// <param name="assetManager">资源管理器，用于提交可启动版本。</param>
        /// <param name="warmupController">启动预热控制器。</param>
        /// <param name="ct">流程生命周期取消令牌。</param>
        private async UniTask TryWarmupAsync(
            AssetComponent assetComponent,
            IAssetManager assetManager,
            IAssetStartupWarmupController warmupController,
            CancellationToken ct)
        {
            IAssetWarmupGroup warmupGroup = null;
            bool ok = false;
            m_DownloadCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            CancellationToken runToken = m_DownloadCts.Token;
            try
            {
                warmupGroup = warmupController.CreateLaunchWarmupGroup();
                Log.Debug(LogTag.Procedure,
                    Txt.Format("开始 WebGL 资源预热 Scope={0} 总项数={1}", warmupGroup.Scope, warmupGroup.TotalCount));

                if (warmupGroup.TotalCount > 0)
                {
                    LauncherUIController.ShowProgress(LauncherStage.Hotfix);
                }
                warmupGroup.OnProgress += OnWarmupProgress;
                ok = await warmupGroup.RunAsync(runToken);
                warmupGroup.OnProgress -= OnWarmupProgress;

                string error = warmupGroup.Error;
                IAssetWarmupGroup completedGroup = warmupGroup;
                warmupGroup = null;
                await warmupController.CompleteLaunchWarmupAsync(completedGroup, ok, runToken);

                if (!ok)
                {
                    Log.Warning(LogTag.Procedure, Txt.Format("WebGL 资源预热失败: {0}", error));
                    ShowRetryDialog(assetComponent.QuitOnFailedOrCancel, ct);
                    return;
                }

                assetManager.CommitBootableVersion();
                m_Success = true;
                m_Complete = true;
            }
            catch (OperationCanceledException)
            {
                warmupGroup?.Release();
            }
            catch (Exception e)
            {
                Log.Warning(LogTag.Procedure, Txt.Format("WebGL 资源预热异常: {0}", e.Message));
                if (warmupGroup != null)
                {
                    try
                    {
                        await warmupController.CompleteLaunchWarmupAsync(warmupGroup, false, ct);
                    }
                    catch (Exception cleanupException)
                    {
                        Log.Warning(LogTag.Procedure,
                            Txt.Format("WebGL 资源预热失败后的清理异常: {0}", cleanupException.Message));
                    }
                }
                ShowRetryDialog(assetComponent.QuitOnFailedOrCancel, ct);
            }
            finally
            {
                m_DownloadCts?.Dispose();
                m_DownloadCts = null;
            }
        }

        /// <summary>
        /// 接收 WebGL Warmup 的计数进度并更新启动进度 UI，不伪造字节进度。
        /// </summary>
        /// <param name="finished">已完成项数。</param>
        /// <param name="total">总项数。</param>
        private void OnWarmupProgress(int finished, int total)
        {
            float progress = total <= 0 ? 1f : (float)finished / total;
            LauncherUIController.UpdateProgress(progress);
            int percent = (int)(progress * 100f);
            m_LastFinishedCount = finished;
            m_LastTotalCount = total;
            m_LastFinishedBytes = 0L;
            m_LastTotalBytes = 0L;
            m_LastPercent = percent;
            if (percent / 10 != m_LastLoggedPercent / 10)
            {
                m_LastLoggedPercent = percent;
                Log.Debug(LogTag.Procedure,
                    Txt.Format("WebGL 资源预热进度 {0}% [完成 {1}/{2}]", percent, finished, total));
            }
        }

        /// <summary>
        /// 显示热更重试弹窗（HotfixFailed 类型）。
        /// 确认回调触发 OnRetryClicked；取消回调触发 OnCancelClicked。
        /// </summary>
        /// <param name="quitOnCancel">用户取消时是否强退应用。</param>
        /// <param name="ct">流程生命周期取消令牌。</param>
        private void ShowRetryDialog(bool quitOnCancel, CancellationToken ct)
        {
            LauncherUIController.ShowDialog(
                LauncherDialogType.HotfixFailed,
                onConfirm: () => OnRetryClicked(ct),
                onCancel: () => OnCancelClicked(quitOnCancel));
        }

        /// <summary>
        /// 用户点击重试按钮。
        /// 自卫检查已完成或 ct 已取消；销毁弹窗，重置进度缓存，重新启动下载。
        /// </summary>
        /// <param name="ct">流程生命周期取消令牌。</param>
        private void OnRetryClicked(CancellationToken ct)
        {
            if (m_AttemptInProgress || m_Complete || ct.IsCancellationRequested)
            {
                return;
            }

            m_UserRetryCount++;
            Log.Debug(LogTag.Procedure, Txt.Format("用户点击重试热更新，累计重试次数={0}。", m_UserRetryCount));

            LauncherUIController.DestroyDialog();
            ResetProgressCache();

            StartDownloadAttempt(ct);
        }

        /// <summary>
        /// 尝试启动一轮下载；已有轮次执行中时直接忽略重复触发。
        /// </summary>
        /// <param name="ct">流程生命周期取消令牌。</param>
        private void StartDownloadAttempt(CancellationToken ct)
        {
            TryDownloadAsync(ct).Forget();
        }

        /// <summary>
        /// 用户点击取消按钮。
        /// 自卫检查已完成；根据 quitOnCancel 决定强退或跳过补丁进入游戏。
        /// </summary>
        /// <param name="quitOnCancel">是否强退应用。</param>
        private void OnCancelClicked(bool quitOnCancel)
        {
#if UNITY_WEBGL
            // 浏览器环境不执行应用强退，取消后按跳过本次资源准备继续启动。
            quitOnCancel = false;
#endif
            if (m_Complete)
            {
                return;
            }

            LauncherUIController.DestroyDialog();

            if (quitOnCancel)
            {
                Log.Error(LogTag.Procedure, "用户取消热更新且配置为强制退出，应用退出。");
                Nova.Self.QuitApplication();
                return;
            }

            Log.Warning(LogTag.Procedure, "用户取消热更新，跳过补丁进入游戏，资源可能不完整。");
            m_Success = false;
            m_Complete = true;
        }

        /// <summary>
        /// 重置进度缓存字段（不清 m_UserRetryCount）。
        /// </summary>
        private void ResetProgressCache()
        {
            m_LastLoggedPercent = -1;
            m_LastFinishedCount = 0;
            m_LastTotalCount = 0;
            m_LastFinishedBytes = 0L;
            m_LastTotalBytes = 0L;
            m_LastPercent = 0;
        }
    }
}
