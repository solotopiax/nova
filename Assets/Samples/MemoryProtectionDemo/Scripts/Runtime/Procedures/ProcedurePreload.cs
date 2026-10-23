/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ProcedurePreload.cs
 * author:    taoye
 * created:   2026/3/12
 * descrip:   内存防护演示的必要资源预加载流程
 *            职责：初始化运行时模块 -> 加载业务资源。
 ***************************************************************/

using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using NovaFramework.Runtime;
using ProcedureOwner = NovaFramework.Runtime.IFsm<NovaFramework.Runtime.IProcedureManager>;

namespace NovaFramework.Memory.Protection.Samples.Runtime
{
    /// <summary>
    /// 预加载流程。
    /// 1. 显示进度面板（Preload 阶段）。
    /// 2. 按步骤加载：Persist -> Localization -> UI（共 3 步）。
    /// 3. 全部完成后跳转 ProcedurePlaying。
    /// </summary>
    public class ProcedurePreload : ProcedureBase
    {
        /// <summary>
        /// 预加载总步骤数。
        /// </summary>
        private const int c_TotalSteps = 3;

        /// <summary>
        /// 已完成步骤数。
        /// </summary>
        private int m_CompletedSteps;

        /// <summary>
        /// 是否全部加载完成。
        /// </summary>
        private bool m_AllLoaded;

        /// <summary>
        /// 当前进入代际，防止旧流程任务与回调修改重入后的状态。
        /// </summary>
        private int m_EnterGeneration;

        /// <summary>
        /// 当前预加载尝试代际，重试后使旧弹窗回调失效。
        /// </summary>
        private int m_AttemptGeneration;

        /// <summary>
        /// 当前进入是否已有预加载任务，避免重复重试并发执行。
        /// </summary>
        private bool m_PreloadRunning;

        /// <summary>
        /// 进入流程时创建本次任务身份，显示进度并启动预加载。
        /// </summary>
        /// <param name="procedureOwner">流程持有者。</param>
        protected override void OnEnter(ProcedureOwner procedureOwner)
        {
            base.OnEnter(procedureOwner);

            m_EnterGeneration++;
            m_AttemptGeneration = 0;
            m_PreloadRunning = false;
            m_CompletedSteps = 0;
            m_AllLoaded = false;

            LauncherUIController.ShowProgress(LauncherStage.Preload);
            Log.Debug(LogTag.Procedure, "ProcedurePreload — 开始预加载。");
            StartPreload(CancellationToken, m_EnterGeneration, m_AttemptGeneration);
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

            ChangeState<ProcedurePlaying>(procedureOwner);
        }

        /// <summary>
        /// 离开流程时取消任务、使旧回调失效并销毁启动面板。
        /// </summary>
        /// <param name="procedureOwner">流程持有者。</param>
        /// <param name="isShutdown">是否因流程管理器关闭而离开。</param>
        protected override void OnLeave(ProcedureOwner procedureOwner, bool isShutdown)
        {
            base.OnLeave(procedureOwner, isShutdown);

            m_EnterGeneration++;
            m_PreloadRunning = false;
            LauncherUIController.DestroyAll();
        }

        /// <summary>
        /// 为当前进入启动唯一预加载尝试；重试从零进度开始，过期与重复请求忽略。
        /// </summary>
        /// <param name="cancellationToken">本次进入时冻结的流程取消令牌。</param>
        /// <param name="enterGeneration">本次进入的代际。</param>
        /// <param name="expectedAttemptGeneration">触发回调所属的尝试代际。</param>
        private void StartPreload(CancellationToken cancellationToken, int enterGeneration, int expectedAttemptGeneration)
        {
            if (!IsCurrentPreload(cancellationToken, enterGeneration, expectedAttemptGeneration)
                || m_PreloadRunning || m_AllLoaded)
            {
                return;
            }

            m_PreloadRunning = true;
            int attemptGeneration = ++m_AttemptGeneration;
            m_CompletedSteps = 0;
            LauncherUIController.HideDialog();
            LauncherUIController.UpdateProgress(0f);
            RunPreloadAsync(cancellationToken, enterGeneration, attemptGeneration).Forget();
        }

        /// <summary>
        /// 判断令牌、进入代际与尝试代际是否仍属于当前有效预加载任务。
        /// </summary>
        /// <param name="cancellationToken">任务启动时冻结的流程取消令牌。</param>
        /// <param name="enterGeneration">任务所属的进入代际。</param>
        /// <param name="attemptGeneration">任务所属的尝试代际。</param>
        /// <returns>任务仍属于当前进入且未被取消时返回 true。</returns>
        private bool IsCurrentPreload(CancellationToken cancellationToken, int enterGeneration, int attemptGeneration)
        {
            return !cancellationToken.IsCancellationRequested
                && enterGeneration == m_EnterGeneration
                && attemptGeneration == m_AttemptGeneration;
        }

        /// <summary>
        /// 在关键等待后检查本次令牌与任务身份，阻止旧任务继续产生副作用。
        /// </summary>
        /// <param name="cancellationToken">任务启动时冻结的流程取消令牌。</param>
        /// <param name="enterGeneration">任务所属的进入代际。</param>
        /// <param name="attemptGeneration">任务所属的尝试代际。</param>
        private void ThrowIfPreloadObsolete(CancellationToken cancellationToken, int enterGeneration, int attemptGeneration)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsCurrentPreload(cancellationToken, enterGeneration, attemptGeneration))
            {
                throw new OperationCanceledException(cancellationToken);
            }
        }

        /// <summary>
        /// 仅预加载演示需要的存储、本地化和 UI；所有等待、失败和重试均绑定当前身份。
        /// </summary>
        /// <param name="cancellationToken">本次进入时冻结的流程取消令牌。</param>
        /// <param name="enterGeneration">本次进入的代际。</param>
        /// <param name="attemptGeneration">本次预加载尝试的代际。</param>
        private async UniTaskVoid RunPreloadAsync(CancellationToken cancellationToken, int enterGeneration, int attemptGeneration)
        {
            try
            {
                ThrowIfPreloadObsolete(cancellationToken, enterGeneration, attemptGeneration);
                Log.Debug(LogTag.Procedure, "Preload Step 1/3 — 加载持久化数据。");
                await Nova.Persist.LoadAsync();
                ThrowIfPreloadObsolete(cancellationToken, enterGeneration, attemptGeneration);
                OnStepComplete();

                Log.Debug(LogTag.Procedure, "Preload Step 2/3 — 加载本地化数据。");
                await Nova.Localization.LoadAsync();
                ThrowIfPreloadObsolete(cancellationToken, enterGeneration, attemptGeneration);
                await Nova.Localization.InitCurrentLanguageAsync();
                ThrowIfPreloadObsolete(cancellationToken, enterGeneration, attemptGeneration);
                OnStepComplete();

                Log.Debug(LogTag.Procedure, "Preload Step 3/3 — 加载UI配置。");
                await Nova.UI.LoadAsync();
                ThrowIfPreloadObsolete(cancellationToken, enterGeneration, attemptGeneration);
                OnStepComplete();

                Log.Debug(LogTag.Procedure, "预加载全部完成。");
                m_AllLoaded = true;
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception e)
            {
                if (!IsCurrentPreload(cancellationToken, enterGeneration, attemptGeneration))
                {
                    return;
                }

                Log.Error(LogTag.Procedure, Txt.Format("预加载异常: {0}", e.Message));
                LauncherUIController.ShowDialog(
                    LauncherDialogType.PreloadFailed,
                    () => StartPreload(cancellationToken, enterGeneration, attemptGeneration),
                    () =>
                    {
                        if (IsCurrentPreload(cancellationToken, enterGeneration, attemptGeneration))
                        {
                            UnityEngine.Application.Quit();
                        }
                    });
            }
            finally
            {
                if (IsCurrentPreload(cancellationToken, enterGeneration, attemptGeneration))
                {
                    m_PreloadRunning = false;
                }
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
