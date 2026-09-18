/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ThirdPayExternalBrowserSessionState.cs
 * author:    yingzheng
 * created:   2026/9/16
 * descrip:   ThirdPay 外部浏览器会话状态入口
 ***************************************************************/

namespace NovaFramework.SDK.IAP.ThirdPay.Runtime
{
    /// <summary>
    /// 维护当前外部浏览器支付会话引用和匹配规则，具体 UI/验单副作用仍由 Store 编排。
    /// </summary>
    internal sealed class ThirdPayExternalBrowserSessionState
    {
        /// <summary>
        /// 当前外部浏览器支付会话。
        /// </summary>
        public ThirdPayExternalBrowserPaySession Current { get; private set; }

        /// <summary>
        /// 根据本地订单创建新的外部浏览器支付会话。
        /// </summary>
        /// <param name="order">已保存的本地订单。</param>
        /// <returns>新建会话；订单无效时返回 null。</returns>
        public ThirdPayExternalBrowserPaySession Begin(ThirdPayOrderRecord order)
        {
            if (order == null || string.IsNullOrEmpty(order.ClientOrderId))
            {
                Current = null;
                return null;
            }

            Current = new ThirdPayExternalBrowserPaySession(order.ClientOrderId, order.TableId, order.UserId, order.CustomData, order.ReceiptParam);
            return Current;
        }

        /// <summary>
        /// 取出并清空当前会话引用。
        /// </summary>
        /// <returns>清理前的当前会话。</returns>
        public ThirdPayExternalBrowserPaySession Detach()
        {
            ThirdPayExternalBrowserPaySession session = Current;
            Current = null;
            return session;
        }

        /// <summary>
        /// 判断当前会话是否匹配指定订单和返回版本。
        /// </summary>
        /// <param name="clientOrderId">客户端订单号。</param>
        /// <param name="version">返回 App 版本号。</param>
        /// <param name="session">命中的当前会话。</param>
        /// <returns>会话仍匹配时返回 true。</returns>
        public bool TryGetMatching(string clientOrderId, int version, out ThirdPayExternalBrowserPaySession session)
        {
            session = Current;
            return session != null
                && string.Equals(session.ClientOrderId, clientOrderId, System.StringComparison.Ordinal)
                && session.ReturnVersion == version;
        }
    }
}
