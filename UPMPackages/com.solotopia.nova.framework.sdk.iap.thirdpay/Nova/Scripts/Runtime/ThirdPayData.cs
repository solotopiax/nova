/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ThirdPayData.cs
 * author:    yingzheng
 * created:   2026/5/20
 * descrip:   第三方支付 store 数据结构（本地存档）
 ***************************************************************/

namespace NovaFramework.SDK.IAP.ThirdPay.Runtime
{
    /// <summary>
    /// 本地存档的第三方进行中订单数据，用于跨会话补单。
    /// </summary>
    public sealed class ThirdOrderData
    {
        /// <summary>
        /// 商品配置表行 ID。
        /// </summary>
        public long TableId;

        /// <summary>
        /// 用户 UID（string 形式，与 IAPStoreBase.m_GameUID 类型一致）。
        /// </summary>
        public string Uid = string.Empty;

        /// <summary>
        /// 客户端生成的订单 ID（client_order_id），用于服务端验单 ClientOrderIds 字段对应。
        /// </summary>
        public string ClientOrderId;

        /// <summary>
        /// 透传的业务自定义数据。
        /// </summary>
        public string CustomString;
    }
}
