/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ThirdPayOrderRecord.cs
 * author:    yingzheng
 * created:   2026/5/20
 * descrip:   第三方支付待处理订单记录
 ***************************************************************/

using System;

namespace NovaFramework.SDK.IAP.ThirdPay.Runtime
{
    /// <summary>
    /// 本地保存的第三方支付订单。
    /// 订单以客户端订单号为唯一键，允许同一商品同时存在多笔待处理订单。
    /// </summary>
    [Serializable]
    internal sealed class ThirdPayOrderRecord
    {
        /// <summary>
        /// 客户端生成的唯一订单号。
        /// </summary>
        public string ClientOrderId = string.Empty;

        /// <summary>
        /// 支付商品表行 ID。
        /// </summary>
        public long TableId;

        /// <summary>
        /// 创建订单时的用户 UID。
        /// </summary>
        public string UserId = string.Empty;

        /// <summary>
        /// 业务层透传数据。
        /// </summary>
        public string CustomData = string.Empty;

        /// <summary>
        /// 随第三方支付票据往返的业务透传参数。
        /// </summary>
        public string ReceiptParam = string.Empty;
    }
}
