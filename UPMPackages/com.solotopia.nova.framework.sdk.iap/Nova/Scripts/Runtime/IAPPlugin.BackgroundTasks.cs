/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  IAPPlugin.BackgroundTasks.cs
 * author:    yingzheng
 * created:   2026/8/11
 * descrip:   IAPPlugin 后台任务生命周期收口
 ***************************************************************/

using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using NovaFramework.Runtime;

namespace NovaFramework.SDK.IAP.Runtime
{
    /// <summary>
    /// IAPPlugin 后台任务生命周期辅助方法。
    /// 用于收口登录后自动补单等不等待完成的后台任务，避免 Dispose 后继续访问已释放商店。
    /// </summary>
    public sealed partial class IAPPlugin
    {
        /// <summary>
        /// 重建运行期后台任务取消源；初始化开始时调用，确保重复初始化时使用新的取消令牌。
        /// </summary>
        private void ResetRuntimeTaskCancellation()
        {
            DisposeRuntimeTaskCancellation();
            m_RuntimeTaskCts = new CancellationTokenSource();
        }

        /// <summary>
        /// 通过统一入口启动 IAPPlugin 后台任务，自动接入运行期取消令牌并兜底捕获异常。
        /// </summary>
        /// <param name="taskFactory">接收运行期取消令牌并返回后台任务的工厂方法。</param>
        /// <param name="taskName">后台任务名称，用于日志定位。</param>
        private void RunBackgroundTask(Func<CancellationToken, UniTask> taskFactory, string taskName)
        {
            if (taskFactory == null)
            {
                LogWarning($"后台任务启动失败，任务名={taskName}，原因=任务工厂为空。");
                return;
            }

            if (m_RuntimeTaskCts == null || m_RuntimeTaskCts.IsCancellationRequested)
            {
                LogDebug($"后台任务已跳过，IAPPlugin 正在释放或未初始化，任务名={taskName}。");
                return;
            }

            RunBackgroundTaskAsync(taskFactory, taskName, m_RuntimeTaskCts.Token).Forget();
        }

        /// <summary>
        /// 执行 IAPPlugin 后台任务并统一处理取消和异常。
        /// </summary>
        /// <param name="taskFactory">后台任务工厂方法。</param>
        /// <param name="taskName">后台任务名称。</param>
        /// <param name="ct">运行期取消令牌。</param>
        private async UniTaskVoid RunBackgroundTaskAsync(Func<CancellationToken, UniTask> taskFactory, string taskName, CancellationToken ct)
        {
            try
            {
                await taskFactory(ct);
            }
            catch (OperationCanceledException)
            {
                LogDebug($"后台任务已取消，任务名={taskName}。");
            }
            catch (Exception e)
            {
                LogWarning($"后台任务执行异常，任务名={taskName}，详情={e.Message}");
            }
        }

        /// <summary>
        /// 取消 IAPPlugin 运行期后台任务；该方法幂等。
        /// </summary>
        private void CancelRuntimeTasks()
        {
            if (m_RuntimeTaskCts == null || m_RuntimeTaskCts.IsCancellationRequested)
            {
                return;
            }

            m_RuntimeTaskCts.Cancel();
        }

        /// <summary>
        /// 释放 IAPPlugin 后台任务取消源；调用前会先取消。
        /// </summary>
        private void DisposeRuntimeTaskCancellation()
        {
            if (m_RuntimeTaskCts == null)
            {
                return;
            }

            CancelRuntimeTasks();
            m_RuntimeTaskCts.Dispose();
            m_RuntimeTaskCts = null;
        }
    }
}
