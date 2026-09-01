/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  VoucherLogOwner.cs
 * author:    yingzheng
 * created:   2026/8/31
 * descrip:   Voucher IAP 内部服务日志基类
 ***************************************************************/

using NovaFramework.SDK.IAP.Runtime;

namespace NovaFramework.SDK.IAP.Voucher.Runtime
{
    /// <summary>
    /// Voucher IAP 内部服务日志基类。
    /// 服务继承后可直接调用 LogDebug / LogWarning / LogError，
    /// 日志标签固定解析为 IAP Voucher。
    /// </summary>
    internal abstract class VoucherLogOwner : IAPLogOwner
    {
        /// <summary>
        /// Voucher 服务固定使用代金券日志标签。
        /// </summary>
        protected override string LogTag => NovaFramework.Runtime.LogTag.IAPVoucher;
    }
}
