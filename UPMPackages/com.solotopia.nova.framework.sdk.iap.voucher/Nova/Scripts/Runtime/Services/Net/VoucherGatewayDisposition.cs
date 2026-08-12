/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  VoucherGatewayDisposition.cs
 * author:    yingzheng
 * created:   2026/8/3
 * descrip:   Voucher 协议扣减结果分类
 ***************************************************************/

namespace NovaFramework.SDK.IAP.Voucher.Runtime
{
    /// <summary>
    /// Voucher 扣减请求的有限结果分类。
    /// </summary>
    internal enum VoucherGatewayDisposition
    {
        /// <summary>
        /// 服务端已确认扣减成功或原订单已经发货。
        /// </summary>
        Succeeded,

        /// <summary>
        /// 服务端明确拒绝本次扣减。
        /// </summary>
        Rejected,

        /// <summary>
        /// 网络或服务端临时失败，可以复用原命令重试。
        /// </summary>
        Retryable,

        /// <summary>
        /// 无法判断服务端是否执行，必须保留原命令恢复。
        /// </summary>
        Unknown,
    }
}
