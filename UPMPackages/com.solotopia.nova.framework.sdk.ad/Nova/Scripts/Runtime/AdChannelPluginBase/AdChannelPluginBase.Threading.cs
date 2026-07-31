/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AdChannelPluginBase.Threading.cs
 * author:    Codex
 * created:   2026/7/17
 * descrip:   AdChannelPluginBase SDK callback threading helpers
 ***************************************************************/

using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using NovaFramework.Runtime;

namespace NovaFramework.SDK.AdPlugin.Runtime
{
    public abstract partial class AdChannelPluginBase
    {
        /// <summary>
        /// 将广告 SDK 生命周期回调排入 Unity 主线程，保持回调到达顺序。
        /// 业务事件和 UI 相关回调必须走此入口；状态机、批次通知和纯打点逻辑可以在 SDK 原始回调线程立即执行。
        /// </summary>
        /// <param name="action">需要在 Unity 主线程执行的回调逻辑。</param>
        protected void PostAdCallbackToMainThread(Action action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));

            lock (m_AdCallbackQueueLock)
            {
                m_AdCallbackQueue.Enqueue(action);
                if (m_AdCallbackDrainScheduled) return;
                m_AdCallbackDrainScheduled = true;
            }

            DrainAdCallbackQueueAsync().Forget();
        }

        /// <summary>
        /// 收益回调即时入口：immediateAction 在 SDK 原始回调线程立刻执行，Nova 收益事件排入 Unity 主线程。
        /// </summary>
        /// <param name="e">广告收益事件载荷。</param>
        /// <param name="immediateAction">不依赖 Unity 主线程的即时收益处理。</param>
        protected void RaiseRevenueImmediately(AdEvent e, Action immediateAction = null)
        {
            if (e == null) throw new ArgumentNullException(nameof(e));

            if (immediateAction != null)
            {
                try { immediateAction(); }
                catch (Exception ex) { Log.Error(LogTag.AD, $"[{Name}] 即时收益处理失败: {ex}"); }
            }

            RaiseRevenue(e);
        }

        private async UniTaskVoid DrainAdCallbackQueueAsync()
        {
            await UniTask.SwitchToMainThread();
            await UniTask.Yield();

            while (true)
            {
                Action action;
                lock (m_AdCallbackQueueLock)
                {
                    if (m_AdCallbackQueue.Count == 0)
                    {
                        m_AdCallbackDrainScheduled = false;
                        return;
                    }

                    action = m_AdCallbackQueue.Dequeue();
                }

                try { action(); }
                catch (Exception ex) { Log.Error(LogTag.AD, $"[{Name}] 主线程广告回调执行失败: {ex}"); }
            }
        }
    }
}
