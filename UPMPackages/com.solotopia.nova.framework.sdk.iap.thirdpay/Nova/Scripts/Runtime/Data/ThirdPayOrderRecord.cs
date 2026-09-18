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
    /// 存档以客户端订单号为唯一键；支付发起前通过业务键复用未完成订单。
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

        /// <summary>
        /// 本地订单状态，仅用于区分创建、验单中和待后续补单。
        /// </summary>
        public string State = ThirdPayLocalOrderState.Created;
    }

    /// <summary>
    /// ThirdPay 本地订单状态常量。
    /// </summary>
    internal static class ThirdPayLocalOrderState
    {
        /// <summary>
        /// 本地订单已创建，支付页尚未返回明确验单结果。
        /// </summary>
        public const string Created = "Created";

        /// <summary>
        /// 本地订单正在发起服务端验单。
        /// </summary>
        public const string Validating = "Validating";

        /// <summary>
        /// 服务端验单尚未终结，订单保留等待后续补单。
        /// </summary>
        public const string PendingValidation = "PendingValidation";
    }

    /// <summary>
    /// ThirdPay 本地订单业务键工具。
    /// </summary>
    internal static class ThirdPayOrderKey
    {
        /// <summary>
        /// 存储键内部字段分隔符。
        /// </summary>
        private const string c_Separator = "|";

        /// <summary>
        /// 根据商品表行 ID 和票据透传参数生成稳定业务键。
        /// </summary>
        /// <param name="tableId">商品表行 ID。</param>
        /// <param name="receiptParam">票据透传参数。</param>
        /// <returns>可用于同业务支付去重的稳定键。</returns>
        internal static string Build(long tableId, string receiptParam)
        {
            return tableId <= 0L ? string.Empty : $"{tableId}{c_Separator}{NormalizeReceiptParam(receiptParam)}";
        }

        /// <summary>
        /// 根据本地订单记录生成稳定业务键。
        /// </summary>
        /// <param name="record">本地订单记录。</param>
        /// <returns>可用于同业务支付去重的稳定键。</returns>
        internal static string Build(ThirdPayOrderRecord record)
        {
            return record == null ? string.Empty : Build(record.TableId, record.ReceiptParam);
        }

        /// <summary>
        /// 判断本地订单记录是否匹配指定业务键。
        /// </summary>
        /// <param name="record">待检查本地订单记录。</param>
        /// <param name="tableId">商品表行 ID。</param>
        /// <param name="receiptParam">票据透传参数。</param>
        /// <returns>业务键一致时返回 true。</returns>
        internal static bool Matches(ThirdPayOrderRecord record, long tableId, string receiptParam)
        {
            return string.Equals(Build(record), Build(tableId, receiptParam), StringComparison.Ordinal);
        }

        /// <summary>
        /// 将票据透传参数归一化为业务键使用的形式。
        /// </summary>
        /// <param name="receiptParam">原始票据透传参数。</param>
        /// <returns>归一化后的票据透传参数。</returns>
        internal static string NormalizeReceiptParam(string receiptParam)
        {
            return string.IsNullOrEmpty(receiptParam) ? string.Empty : receiptParam.ToUpperInvariant();
        }
    }
}
