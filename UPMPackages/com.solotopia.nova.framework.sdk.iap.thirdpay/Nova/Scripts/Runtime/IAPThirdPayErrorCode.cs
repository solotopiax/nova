/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  IAPThirdPayErrorCode.cs
 * author:    yingzheng
 * created:   2026/6/5
 * descrip:   ThirdPay store 专属错误码，从 0 起编
 ***************************************************************/

namespace NovaFramework.SDK.IAP.ThirdPay.Runtime
{
    /// <summary>
    /// 第三方支付 Store 专属错误码。
    /// </summary>
    public enum IAPThirdPayErrorCode
    {
        /// <summary>
        /// 无错误。
        /// </summary>
        None = 0,

        /// <summary>
        /// Store 尚未完成初始化。
        /// </summary>
        StoreInitFailed = 1,

        /// <summary>
        /// 用户主动取消支付。
        /// </summary>
        UserCancelled = 2,

        /// <summary>
        /// 支付网络请求失败。
        /// </summary>
        NetworkError = 3,

        /// <summary>
        /// 服务端验单失败。
        /// </summary>
        ServerValidationFailed = 4,

        /// <summary>
        /// 第三方支付 Store 当前不可用。
        /// </summary>
        StoreNotAvailable = 5,

        /// <summary>
        /// 应用内支付页关闭或打开失败。
        /// </summary>
        WebViewClosed = 6,

        /// <summary>
        /// Google 外部结算能力尚未就绪。
        /// </summary>
        BillingNotReady = 7,

        /// <summary>
        /// 订单需要业务侧手动发货。
        /// </summary>
        ManualDelivery = 8,

        /// <summary>
        /// 服务端订单仍在处理中。
        /// </summary>
        OrderPending = 9,
    }
}
