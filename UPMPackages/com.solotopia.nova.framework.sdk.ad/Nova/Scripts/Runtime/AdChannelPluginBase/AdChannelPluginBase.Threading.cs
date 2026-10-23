/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AdChannelPluginBase.Threading.cs
 * author:    Codex
 * created:   2026/7/17
 * descrip:   广告回调主线程串行入口与载荷快照
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
        /// 当前是否在正式主线程回调drain内；派生层先Post后Raise可同步收口再完成原厂TCS。
        /// </summary>
        private bool m_IsDrainingAdCallbacks;

        /// <summary>
        /// 广告状态API只允许Unity主线程调用，避免对原厂Ready/Load/Show进行跨线程查询。
        /// </summary>
        private static void RequireAdMainThread()
        {
            if (!PlayerLoopHelper.IsMainThread) throw new InvalidOperationException("广告状态API必须在Unity主线程调用。");
        }

        /// <summary>
        /// 将SDK回调排入FIFO主线程队列，执行前再次检查本实例释放版本。
        /// </summary>
        /// <param name="action">主线程状态及业务通知逻辑。</param>
        protected void PostAdCallbackToMainThread(Action action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            lock (m_AdCallbackQueueLock)
            {
                if (m_IsDisposed) return;
                int generation = m_AdCallbackGeneration;
                m_AdCallbackQueue.Enqueue(() => { if (!m_IsDisposed && generation == m_AdCallbackGeneration) action(); });
                if (m_AdCallbackDrainScheduled) return;
                m_AdCallbackDrainScheduled = true;
            }
            DrainAdCallbackQueueAsync().Forget();
        }

        /// <summary>
        /// 正式队列内嵌套Raise立即收口；其他入口排队，不在SDK发Load栈中递归推进。
        /// </summary>
        /// <param name="action">已快照的状态动作。</param>
        private void DispatchAdStateCallback(Action action)
        {
            if (m_IsDisposed) return;
            if (PlayerLoopHelper.IsMainThread && m_IsDrainingAdCallbacks) action();
            else PostAdCallbackToMainThread(action);
        }

        /// <summary>
        /// 纯收益上报仍在原SDK线程即时执行，槽位及业务事件由RaiseRevenue主线程衔接。
        /// </summary>
        /// <param name="e">收益载荷。</param>
        /// <param name="immediateAction">不访问Unity状态的纯上报操作。</param>
        protected void RaiseRevenueImmediately(AdEvent e, Action immediateAction = null)
        {
            if (e == null) throw new ArgumentNullException(nameof(e));
            if (m_IsDisposed) return;
            try { immediateAction?.Invoke(); }
            catch (Exception ex) { Log.Error(LogTag.AD, $"[{Name}] 即时收益处理失败：{ex}"); }
            RaiseRevenue(e);
        }

        /// <summary>
        /// 复制属性字典，冻结每次SDK回调或轮次启动的字段，不承诺深复制业务对象。
        /// </summary>
        /// <param name="props">可空属性。</param>
        /// <returns>独立字典结构或null。</returns>
        private static Dictionary<string, object> SnapshotAdProps(Dictionary<string, object> props)
            => props == null ? null : new Dictionary<string, object>(props);

        /// <summary>
        /// 冻结加载载荷，SDK/调用者后续复用对象不会修改已排队结果。
        /// </summary>
        /// <param name="e">加载载荷。</param>
        /// <returns>字段与属性快照。</returns>
        private static AdLoadResult SnapshotLoadResult(AdLoadResult e)
            => new AdLoadResult { Success = e.Success, Format = e.Format, PlacementId = e.PlacementId, Network = e.Network, Revenue = e.Revenue, Currency = e.Currency, ErrorCode = e.ErrorCode, ErrorMessage = e.ErrorMessage, CustomProps = SnapshotAdProps(e.CustomProps) };

        /// <summary>
        /// 冻结展示载荷，奖励资格原样保留，由渠道官方回调决定。
        /// </summary>
        /// <param name="e">展示载荷。</param>
        /// <returns>独立展示结果。</returns>
        private static AdResult SnapshotShowResult(AdResult e)
            => new AdResult { Success = e.Success, Format = e.Format, PlacementId = e.PlacementId, UserCompleted = e.UserCompleted, RewardGranted = e.RewardGranted, ErrorMessage = e.ErrorMessage, Network = e.Network, Revenue = e.Revenue, Currency = e.Currency };

        /// <summary>
        /// 冻结收益载荷，状态写入与业务派发使用同一快照。
        /// </summary>
        /// <param name="e">收益载荷。</param>
        /// <returns>独立收益事件。</returns>
        private static AdEvent SnapshotRevenueEvent(AdEvent e)
            => new AdEvent { EventType = e.EventType, Format = e.Format, PlacementId = e.PlacementId, Network = e.Network, Revenue = e.Revenue, Currency = e.Currency, Precision = e.Precision, Timestamp = e.Timestamp };

        /// <summary>
        /// 等待Unity主线程后串行处理回调；释放时丢弃旧动作，单回调异常不截断队列。
        /// </summary>
        private async UniTask DrainAdCallbackQueueAsync()
        {
            await UniTask.SwitchToMainThread();
            await UniTask.Yield();
            try
            {
                m_IsDrainingAdCallbacks = true;
                while (true)
                {
                    Action action;
                    lock (m_AdCallbackQueueLock)
                    {
                        if (m_IsDisposed || m_AdCallbackQueue.Count == 0)
                        {
                            m_AdCallbackDrainScheduled = false;
                            return;
                        }
                        action = m_AdCallbackQueue.Dequeue();
                    }
                    try { action(); }
                    catch (Exception ex) { Log.Error(LogTag.AD, $"[{Name}] 主线程广告回调执行失败：{ex}"); }
                }
            }
            finally
            {
                m_IsDrainingAdCallbacks = false;

            }
        }
    }
}
