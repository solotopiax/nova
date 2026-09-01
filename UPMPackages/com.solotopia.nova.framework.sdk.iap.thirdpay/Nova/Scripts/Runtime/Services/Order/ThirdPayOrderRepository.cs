/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ThirdPayOrderRepository.cs
 * author:    yingzheng
 * created:   2026/8/3
 * descrip:   第三方支付待处理订单仓储
 ***************************************************************/

using System;
using System.Collections.Generic;

namespace NovaFramework.SDK.IAP.ThirdPay.Runtime
{
    /// <summary>
    /// 第三方支付待处理订单仓储，所有变更都以完整账号存档为单位立即保存。
    /// </summary>
    internal sealed class ThirdPayOrderRepository
    {
        /// <summary>
        /// 当前账号的第三方支付存档。
        /// </summary>
        private readonly ThirdPayPersistData m_Data;

        /// <summary>
        /// 完整存档保存回调。
        /// </summary>
        private readonly Action<ThirdPayPersistData> m_Save;

        /// <summary>
        /// 初始化当前账号的订单仓储。
        /// </summary>
        /// <param name="data">当前账号存档。</param>
        /// <param name="save">完整存档保存回调。</param>
        public ThirdPayOrderRepository(ThirdPayPersistData data, Action<ThirdPayPersistData> save)
        {
            m_Data = data ?? throw new ArgumentNullException(nameof(data));
            m_Save = save;
            m_Data.EnsureInitialized();
        }

        /// <summary>
        /// 获取当前账号全部待处理订单的快照。
        /// </summary>
        /// <returns>与内部字典解耦的订单集合。</returns>
        public IReadOnlyCollection<ThirdPayOrderRecord> GetAll()
        {
            return new List<ThirdPayOrderRecord>(m_Data.Orders.Values);
        }

        /// <summary>
        /// 按客户端订单号读取待处理本地订单。
        /// </summary>
        /// <param name="clientOrderId">客户端订单号。</param>
        /// <param name="order">匹配到的订单记录。</param>
        /// <returns>订单存在时返回 true。</returns>
        public bool TryGet(string clientOrderId, out ThirdPayOrderRecord order)
        {
            order = null;
            return !string.IsNullOrEmpty(clientOrderId) && m_Data.Orders.TryGetValue(clientOrderId, out order);
        }

        /// <summary>
        /// 新增或覆盖客户端订单号相同的待处理订单，并立即保存账号存档。
        /// </summary>
        /// <param name="order">待保存订单。</param>
        public void Upsert(ThirdPayOrderRecord order)
        {
            if (order == null)
            {
                throw new ArgumentNullException(nameof(order));
            }

            if (string.IsNullOrEmpty(order.ClientOrderId))
            {
                throw new ArgumentException("ClientOrderId 不能为空。", nameof(order));
            }

            m_Data.Orders[order.ClientOrderId] = order;
            m_Save?.Invoke(m_Data);
        }

        /// <summary>
        /// 移除指定客户端订单并立即保存账号存档。
        /// </summary>
        /// <param name="clientOrderId">客户端订单号。</param>
        /// <returns>实际移除订单时返回 true。</returns>
        public bool Remove(string clientOrderId)
        {
            return Remove(clientOrderId, true);
        }

        /// <summary>
        /// 移除指定客户端订单，可选择是否立即落盘。
        /// 批量验单场景应传入 persist=false 逐笔移除，末尾统一调用 <see cref="Save"/> 合并为单次写入。
        /// </summary>
        /// <param name="clientOrderId">客户端订单号。</param>
        /// <param name="persist">是否在本次移除后立即保存账号存档。</param>
        /// <returns>实际移除订单时返回 true。</returns>
        public bool Remove(string clientOrderId, bool persist)
        {
            if (string.IsNullOrEmpty(clientOrderId) || !m_Data.Orders.Remove(clientOrderId))
            {
                return false;
            }

            if (persist)
            {
                m_Save?.Invoke(m_Data);
            }

            return true;
        }

        /// <summary>
        /// 将当前账号存档整体落盘一次，供批量变更后合并写入。
        /// </summary>
        public void Save()
        {
            m_Save?.Invoke(m_Data);
        }
    }
}
