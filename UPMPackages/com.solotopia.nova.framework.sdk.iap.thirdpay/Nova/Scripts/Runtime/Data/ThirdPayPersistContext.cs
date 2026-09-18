/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ThirdPayPersistContext.cs
 * author:    yingzheng
 * created:   2026/9/16
 * descrip:   ThirdPay 当前账号持久化上下文
 ***************************************************************/

using System;
using System.Collections.Generic;

namespace NovaFramework.SDK.IAP.ThirdPay.Runtime
{
    /// <summary>
    /// 维护当前账号 ThirdPay 存档和订单仓储，避免 Store 直接散落存档字段写入。
    /// </summary>
    internal sealed class ThirdPayPersistContext
    {
        /// <summary>
        /// 当前账号存档。
        /// </summary>
        private ThirdPayPersistData m_Data;

        /// <summary>
        /// 当前账号订单仓储。
        /// </summary>
        private ThirdPayOrderRepository m_OrderRepository;

        /// <summary>
        /// 完整存档保存回调。
        /// </summary>
        private Action<ThirdPayPersistData> m_Save;

        /// <summary>
        /// 当前账号存档，供需要做账号切换一致性判断的流程读取。
        /// </summary>
        public ThirdPayPersistData Data => m_Data;

        /// <summary>
        /// 当前账号渠道参数。
        /// </summary>
        public string ChannelParams => m_Data?.ChannelParams;

        /// <summary>
        /// 当前账号是否已经持有渠道参数。
        /// </summary>
        public bool HasChannelParams => !string.IsNullOrEmpty(m_Data?.ChannelParams);

        /// <summary>
        /// 切换当前账号存档并重建订单仓储。
        /// </summary>
        /// <param name="data">当前账号存档。</param>
        /// <param name="save">完整存档保存回调。</param>
        public void Reset(ThirdPayPersistData data, Action<ThirdPayPersistData> save)
        {
            m_Data = data ?? throw new ArgumentNullException(nameof(data));
            m_Save = save;
            m_Data.EnsureInitialized();
            m_OrderRepository = new ThirdPayOrderRepository(m_Data, m_Save);
        }

        /// <summary>
        /// 清理当前账号存档和订单仓储引用。
        /// </summary>
        public void Clear()
        {
            m_OrderRepository = null;
            m_Data = null;
            m_Save = null;
        }

        /// <summary>
        /// 手动设置当前账号渠道参数并保存。
        /// </summary>
        /// <param name="channelParams">支付页需要透传的渠道参数。</param>
        public void SetChannelParams(string channelParams)
        {
            if (m_Data == null)
            {
                return;
            }

            m_Data.ChannelParams = channelParams ?? string.Empty;
            Save();
        }

        /// <summary>
        /// 从存档恢复国家码兜底值。
        /// </summary>
        /// <param name="countryState">国家码运行时状态。</param>
        public void RestoreCountryCodes(ThirdPayCountryState countryState)
        {
            countryState?.RestorePersistedCountryCodes(m_Data);
        }

        /// <summary>
        /// 将国家码状态写入存档并保存变化。
        /// </summary>
        /// <param name="countryState">国家码运行时状态。</param>
        public void PersistCountryCodesIfChanged(ThirdPayCountryState countryState)
        {
            if (countryState == null || !countryState.PersistCountryCodesIfChanged(m_Data))
            {
                return;
            }

            Save();
        }

        /// <summary>
        /// 获取当前账号全部待处理订单快照。
        /// </summary>
        /// <returns>订单快照；未初始化时返回 null。</returns>
        public IReadOnlyCollection<ThirdPayOrderRecord> GetAllOrders()
        {
            return m_OrderRepository?.GetAll();
        }

        /// <summary>
        /// 按客户端订单号查找本地订单。
        /// </summary>
        /// <param name="clientOrderId">客户端订单号。</param>
        /// <param name="order">命中的本地订单。</param>
        /// <returns>订单存在时返回 true。</returns>
        public bool TryGetOrder(string clientOrderId, out ThirdPayOrderRecord order)
        {
            order = null;
            return m_OrderRepository != null && m_OrderRepository.TryGet(clientOrderId, out order);
        }

        /// <summary>
        /// 按业务支付键查找本地订单。
        /// </summary>
        /// <param name="tableId">商品表行 ID。</param>
        /// <param name="receiptParam">票据透传参数。</param>
        /// <param name="order">命中的本地订单。</param>
        /// <returns>存在同业务键本地订单时返回 true。</returns>
        public bool TryFindOrderByKey(long tableId, string receiptParam, out ThirdPayOrderRecord order)
        {
            order = null;
            return m_OrderRepository != null && m_OrderRepository.TryFindByOrderKey(tableId, receiptParam, out order);
        }

        /// <summary>
        /// 新增或覆盖本地订单。
        /// </summary>
        /// <param name="order">待保存订单。</param>
        public void UpsertOrder(ThirdPayOrderRecord order)
        {
            m_OrderRepository?.Upsert(order);
        }

        /// <summary>
        /// 移除指定本地订单并立即保存。
        /// </summary>
        /// <param name="clientOrderId">客户端订单号。</param>
        /// <returns>实际移除订单时返回 true。</returns>
        public bool RemoveOrder(string clientOrderId)
        {
            return m_OrderRepository != null && m_OrderRepository.Remove(clientOrderId);
        }

        /// <summary>
        /// 移除指定本地订单，可选择是否立即保存。
        /// </summary>
        /// <param name="clientOrderId">客户端订单号。</param>
        /// <param name="persist">是否立即保存。</param>
        /// <returns>实际移除订单时返回 true。</returns>
        public bool RemoveOrder(string clientOrderId, bool persist)
        {
            return m_OrderRepository != null && m_OrderRepository.Remove(clientOrderId, persist);
        }

        /// <summary>
        /// 保存当前账号完整存档。
        /// </summary>
        public void Save()
        {
            if (m_Data == null)
            {
                return;
            }

            m_Save?.Invoke(m_Data);
        }
    }
}
