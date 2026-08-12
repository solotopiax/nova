/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  IAPErrorSource.cs
 * author:    yingzheng
 * created:   2026/8/12
 * descrip:   IAP 错误码来源，标识 ErrorCode 所属的错误码枚举，消除跨渠道歧义
 ***************************************************************/

namespace NovaFramework.Runtime
{
    /// <summary>
    /// IAP 错误码来源。
    /// ErrorCode 是各来源私有错误码枚举强转的 int，不同来源同一 int 含义不同
    /// （例如 ErrorCode=4 在 Mobile 为 StoreNotAvailable、在 ThirdPay 为 ServerValidationFailed）。
    /// 业务层须结合 (ErrorSource, ErrorCode) 才能唯一解码，从而精确定位问题。
    /// </summary>
    public enum IAPErrorSource
    {
        /// <summary>
        /// 无来源。支付成功（ErrorCode=0）时取此值。
        /// </summary>
        None = 0,

        /// <summary>
        /// 官方移动内购渠道（Google Play / App Store）。ErrorCode 属于 IAPMobileErrorCode。
        /// </summary>
        Mobile = 1,

        /// <summary>
        /// 第三方支付渠道（WebView / 系统浏览器）。ErrorCode 属于 IAPThirdPayErrorCode。
        /// </summary>
        ThirdPay = 2,

        /// <summary>
        /// 代金券与金币虚拟货币渠道。ErrorCode 属于 IAPVoucherErrorCode。
        /// </summary>
        Voucher = 3,

        /// <summary>
        /// 未进入具体 store 之前的路由/前置校验层。ErrorCode 属于 IAPPluginErrorCode。
        /// </summary>
        PluginRouter = 4,
    }
}
