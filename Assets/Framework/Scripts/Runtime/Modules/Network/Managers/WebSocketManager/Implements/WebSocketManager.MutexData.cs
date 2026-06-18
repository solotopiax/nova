/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  WebSocketManager.MutexData.cs
 * author:    taoye
 * created:   2026/3/9
 * descrip:   WebSocket管理器 —— 跨线程消息分发
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Threading;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// WebSocket 管理器。
    /// </summary>
    internal sealed partial class WebSocketManager : WebSocketManagerBase, WebSocketScope.IWebSocketManagerBridge
    {
        /// <summary>
        /// 跨线程待执行委托队列（子线程写入）。
        /// </summary>
        private Queue<Action> m_ActionQueueOnMultiThread = new Queue<Action>();

        /// <summary>
        /// 主线程执行委托队列（每帧 Update 时从多线程队列交换过来后执行）。
        /// </summary>
        private Queue<Action> m_ActionExecuteQueueOnMainThread = new Queue<Action>();

        /// <summary>
        /// 轻量级互斥锁，保护 m_ActionQueueOnMultiThread 的并发访问。
        /// </summary>
        private readonly object m_Lock = new object();

        /// <summary>
        /// 每帧主线程调用：将多线程队列平移到主线程队列后逐一执行。
        /// </summary>
        private void MutexDataUpdate()
        {
            lock (m_Lock)
            {
                if (m_ActionQueueOnMultiThread.Count == 0)
                {
                    return;
                }

                while (m_ActionQueueOnMultiThread.Count > 0)
                {
                    m_ActionExecuteQueueOnMainThread.Enqueue(m_ActionQueueOnMultiThread.Dequeue());
                }
            }

            while (m_ActionExecuteQueueOnMainThread.Count > 0)
            {
                m_ActionExecuteQueueOnMainThread.Dequeue()?.Invoke();
            }
        }

        /// <summary>
        /// 在子线程中调用，将委托加锁入队，等待下一帧主线程执行。
        /// </summary>
        /// <param name="action">待主线程执行的委托。</param>
        public void LazyToQueueOnMainThread(Action action)
        {
            lock (m_Lock)
            {
                m_ActionQueueOnMultiThread.Enqueue(action);
            }
        }

        /// <summary>
        /// 在主线程中调用，向线程池投递任务；任务完成后通过 LazyToQueueOnMainThread 回调到主线程。
        /// </summary>
        /// <param name="actionOnSubThread">子线程执行的委托（参数为 state object）。</param>
        /// <param name="actionOnMainThread">任务完成后在主线程执行的回调。</param>
        public void QueueOnSubThread(Action<object> actionOnSubThread, Action actionOnMainThread)
        {
            QueueOnSubThread(actionOnSubThread, null, actionOnMainThread);
        }

        /// <summary>
        /// 在主线程中调用，携带 state 对象向线程池投递任务；任务完成后通过 LazyToQueueOnMainThread 回调到主线程。
        /// </summary>
        /// <param name="actionOnSubThread">子线程执行的委托（参数为 state object）。</param>
        /// <param name="state">传递给子线程委托的参数对象。</param>
        /// <param name="actionOnMainThread">任务完成后在主线程执行的回调。</param>
        public void QueueOnSubThread(Action<object> actionOnSubThread, object state, Action actionOnMainThread)
        {
            ThreadPool.QueueUserWorkItem((_) =>
            {
                actionOnSubThread?.Invoke(state);
                LazyToQueueOnMainThread(actionOnMainThread);
            });
        }
    }
}
