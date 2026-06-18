/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MobileRestoreCoordinator.cs
 * author:    yingzheng
 * created:   2026/5/26
 * descrip:   Restore 流程协调器，追踪订阅和非消耗品验单完成状态
 ***************************************************************/

namespace NovaFramework.SDK.IAP.Mobile.Runtime
{
    /// <summary>
    /// Restore 流程协调器。
    /// 订阅验单（批量）和非消耗品验单（逐个）均完成时返回 CanFinishRestore() = true。
    /// </summary>
    internal sealed class MobileRestoreCoordinator
    {
        /// <summary>
        /// 当前 Restore 流程中尚未完成验单的订阅商品数量。
        /// </summary>
        private int m_PendingSubscriptionCount;

        /// <summary>
        /// 当前 Restore 流程中尚未完成验单的非消耗品数量。
        /// </summary>
        private int m_PendingNonConsumableCount;

        /// <summary>
        /// 订阅商品验单阶段是否已经完成。
        /// </summary>
        private bool m_SubscriptionFinished;

        /// <summary>
        /// 非消耗品验单阶段是否已经完成。
        /// </summary>
        private bool m_NonConsumableFinished;

        /// <summary>
        /// 重置所有状态，Restore 开始前调用。
        /// </summary>
        internal void Reset()
        {
            m_PendingSubscriptionCount = 0;
            m_PendingNonConsumableCount = 0;
            m_SubscriptionFinished = false;
            m_NonConsumableFinished = false;
        }

        /// <summary>
        /// 设置本次 Restore 待处理的订阅数量和非消耗品数量。
        /// 数量为 0 时对应类别直接标记完成。
        /// </summary>
        internal void SetPending(int subscriptionCount, int nonConsumableCount)
        {
            m_PendingSubscriptionCount = subscriptionCount;
            m_PendingNonConsumableCount = nonConsumableCount;
            if (subscriptionCount == 0)
            {
                m_SubscriptionFinished = true;
            }

            if (nonConsumableCount == 0)
            {
                m_NonConsumableFinished = true;
            }
        }

        /// <summary>
        /// 直接标记订阅验单阶段完成（无订阅商品时使用）。
        /// </summary>
        internal void MarkSubscriptionFinished()
        {
            m_SubscriptionFinished = true;
        }

        /// <summary>
        /// 直接标记非消耗品验单阶段完成（无非消耗品时使用）。
        /// </summary>
        internal void MarkNonConsumableFinished()
        {
            m_NonConsumableFinished = true;
        }

        /// <summary>
        /// 标记一个订阅商品已处理完；全部完成时返回 true。
        /// </summary>
        internal bool MarkSubscriptionItemProcessed()
        {
            m_PendingSubscriptionCount--;
            if (m_PendingSubscriptionCount <= 0)
            {
                m_SubscriptionFinished = true;
                return true;
            }
            return false;
        }

        /// <summary>
        /// 标记一个非消耗品已处理完；全部完成时返回 true。
        /// </summary>
        internal bool MarkNonConsumableItemProcessed()
        {
            m_PendingNonConsumableCount--;
            if (m_PendingNonConsumableCount <= 0)
            {
                m_NonConsumableFinished = true;
                return true;
            }
            return false;
        }

        /// <summary>
        /// 订阅和非消耗品均完成时返回 true。
        /// </summary>
        internal bool CanFinishRestore()
        {
            return m_SubscriptionFinished && m_NonConsumableFinished;
        }
    }
}
