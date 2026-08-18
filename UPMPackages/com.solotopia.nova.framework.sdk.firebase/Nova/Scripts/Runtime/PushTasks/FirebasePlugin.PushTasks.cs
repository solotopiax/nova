/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  FirebasePlugin.PushTasks.cs
 * author:    Codex
 * created:   2026/8/13
 * descrip:   FirebasePlugin push task bridge methods
 ***************************************************************/

#if !UNITY_WEBGL
using System.Threading;
using Cysharp.Threading.Tasks;
using NovaFramework.Runtime;

namespace NovaFramework.SDK.FirebasePlugin.Runtime
{
    public sealed partial class FirebasePlugin
    {
        /// <summary>
        /// 初始化 push task 调度器。
        /// OnInitializeAsync 早期调用，保证业务在 Firebase 真正完成初始化前也可以先写入本地缓存。
        /// </summary>
        private void InitializePushTaskServices()
        {
            EnsurePushTaskDispatcher();
            m_PushTaskDispatcher.Initialize(m_RuntimeConfig);
        }

        /// <summary>
        /// 释放 push task 后台任务。
        /// </summary>
        private void CancelPushTaskFlush()
        {
            m_PushTaskDispatcher?.Dispose();
            m_PushTaskDispatcher = null;
        }

        /// <summary>
        /// 写入或覆盖 push task 缓存。
        /// </summary>
        /// <param name="task">待推送任务。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>缓存成功返回 true。</returns>
        private UniTask<bool> QueuePushTaskInternalAsync(FirebasePushTask task, CancellationToken ct)
        {
            EnsurePushTaskDispatcher();
            return m_PushTaskDispatcher.QueueAsync(task, ct);
        }

        /// <summary>
        /// 从后台恢复前台时请求立即发送当前 push task 缓存。
        /// 实际发送仍受 Firebase 初始化和用户身份就绪门槛保护。
        /// </summary>
        private void RequestPushTaskFlushOnForeground()
        {
            if (m_PushTaskDispatcher == null)
            {
                Log.Info(LogTag.Firebase, "Firebase push task 前台恢复发送请求已跳过：调度器尚未初始化。");
                return;
            }

            Log.Info(LogTag.Firebase, "Firebase push task 前台恢复，主动请求发送本地缓存。");
            m_PushTaskDispatcher.FlushAllCachedTasks();
        }

        /// <summary>
        /// 确保 push task 调度器已经创建。
        /// </summary>
        private void EnsurePushTaskDispatcher()
        {
            if (m_PushTaskDispatcher == null)
            {
                m_PushTaskDispatcher = new FirebasePushTaskDispatcher();
            }
        }
    }
}
#endif
