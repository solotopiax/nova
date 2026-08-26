/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MobileValidationQueueCoordinator.cs
 * author:    yingzheng
 * created:   2026/8/11
 * descrip:   移动端官方内购验单队列协调器
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace NovaFramework.SDK.IAP.Mobile.Runtime
{
    /// <summary>
    /// 移动端官方内购验单队列协调器。
    /// 只负责订单键入队去重、批量出队和队列单次执行保护，不持有验单业务规则。
    /// </summary>
    internal sealed class MobileValidationQueueCoordinator
    {
        /// <summary>
        /// 待验单的订单键队列；同一订单键入队前会做去重。
        /// </summary>
        private readonly Queue<string> m_OrderKeyQueue = new Queue<string>();

        /// <summary>
        /// 队列中已有的订单键集合，用于避免重复入队。
        /// </summary>
        private readonly HashSet<string> m_QueuedOrderKeys = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>
        /// 当前批次正在处理的订单键集合，避免异步验单未结束时重复入队。
        /// </summary>
        private readonly HashSet<string> m_ProcessingOrderKeys = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>
        /// 验单队列是否正在处理中，防止并发重入。
        /// </summary>
        internal bool IsProcessing { get; private set; }

        /// <summary>
        /// 判断指定订单键是否已经在队列中。
        /// </summary>
        /// <param name="orderKey">待检查的订单键。</param>
        /// <returns>队列中已经存在该订单键时返回 true。</returns>
        internal bool Contains(string orderKey)
        {
            return MobileOrderKey.IsValid(orderKey) &&
                   (m_QueuedOrderKeys.Contains(orderKey) || m_ProcessingOrderKeys.Contains(orderKey));
        }

        /// <summary>
        /// 将订单键加入验单队列；重复或无效订单键会被忽略。
        /// </summary>
        /// <param name="orderKey">待验单的订单键。</param>
        /// <returns>本次实际入队时返回 true。</returns>
        internal bool Enqueue(string orderKey)
        {
            if (!MobileOrderKey.IsValid(orderKey) || Contains(orderKey))
            {
                return false;
            }

            m_OrderKeyQueue.Enqueue(orderKey);
            m_QueuedOrderKeys.Add(orderKey);
            return true;
        }

        /// <summary>
        /// 串行处理验单队列；每轮批量取出当前队列快照后交给调用方执行验单业务。
        /// </summary>
        /// <param name="processBatchAsync">处理一批订单键的业务委托。</param>
        /// <param name="ct">取消令牌。</param>
        internal async UniTask ProcessAsync(Func<IReadOnlyList<string>, CancellationToken, UniTask> processBatchAsync, CancellationToken ct)
        {
            if (IsProcessing)
            {
                return;
            }

            if (processBatchAsync == null)
            {
                return;
            }

            IsProcessing = true;
            try
            {
                while (m_OrderKeyQueue.Count > 0)
                {
                    ct.ThrowIfCancellationRequested();
                    List<string> orderKeys = DrainQueuedOrderKeys();
                    try
                    {
                        await processBatchAsync(orderKeys, ct);
                    }
                    finally
                    {
                        ReleaseProcessingOrderKeys(orderKeys);
                    }
                }
            }
            finally
            {
                IsProcessing = false;
            }
        }

        /// <summary>
        /// 清空队列并重置处理标记；用于 Store 释放。
        /// </summary>
        internal void Clear()
        {
            m_OrderKeyQueue.Clear();
            m_QueuedOrderKeys.Clear();
            m_ProcessingOrderKeys.Clear();
            IsProcessing = false;
        }

        /// <summary>
        /// 取出当前队列中的全部订单键，并同步清理去重集合。
        /// </summary>
        /// <returns>本轮待处理的订单键列表。</returns>
        private List<string> DrainQueuedOrderKeys()
        {
            var orderKeys = new List<string>();
            while (m_OrderKeyQueue.Count > 0)
            {
                string orderKey = m_OrderKeyQueue.Dequeue();
                m_QueuedOrderKeys.Remove(orderKey);
                m_ProcessingOrderKeys.Add(orderKey);
                orderKeys.Add(orderKey);
            }

            return orderKeys;
        }

        /// <summary>
        /// 释放已完成批次的订单键，使后续新的验单请求可以再次入队。
        /// </summary>
        /// <param name="orderKeys">已完成处理的订单键列表。</param>
        private void ReleaseProcessingOrderKeys(IReadOnlyList<string> orderKeys)
        {
            if (orderKeys == null)
            {
                return;
            }

            foreach (string orderKey in orderKeys)
            {
                m_ProcessingOrderKeys.Remove(orderKey);
            }
        }
    }
}
