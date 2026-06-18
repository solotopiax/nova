/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  IAPMobileErrorCode.cs
 * author:    yingzheng
 * created:   2026/6/5
 * descrip:   Mobile IAP store 专属错误码，从 0 起编
 ***************************************************************/

namespace NovaFramework.SDK.IAP.Mobile.Runtime
{
    /// <summary>
    /// Mobile IAP store 专属支付错误码。
    /// 通过 IAPResult.ErrorCode 以 int 形式返回给上层。
    /// </summary>
    public enum IAPMobileErrorCode
    {
        /// <summary>
        /// 无错误。
        /// </summary>
        None = 0,

        /// <summary>
        /// 配置表或平台商品中未找到目标商品。
        /// </summary>
        ProductNotFound = 1,

        /// <summary>
        /// 订阅商品已处于有效订阅期内。
        /// </summary>
        SubscriptionIsReady = 2,

        /// <summary>
        /// 用户取消支付。
        /// </summary>
        UserCancelled = 3,

        /// <summary>
        /// 平台商店不可用或当前无法发起支付。
        /// </summary>
        StoreNotAvailable = 4,

        /// <summary>
        /// 当前已有支付或验单流程正在进行。
        /// </summary>
        AlreadyPurchasing = 5,

        /// <summary>
        /// 网络不可用或网络请求失败。
        /// </summary>
        NetworkError = 6,

        /// <summary>
        /// 服务端验单失败或拒绝订单。
        /// </summary>
        ServerValidationFailed = 7,

        /// <summary>
        /// MobileStore 初始化失败。
        /// </summary>
        StoreInitFailed = 8,

        /// <summary>
        /// Unity IAP 当前不可购买。
        /// </summary>
        PurchaseFailurePurchasingUnavailable = 1000,

        /// <summary>
        /// Unity IAP 已有待处理购买。
        /// </summary>
        PurchaseFailureExistingPurchasePending = 1001,

        /// <summary>
        /// Unity IAP 平台商品不可用。
        /// </summary>
        PurchaseFailureProductUnavailable = 1002,

        /// <summary>
        /// Unity IAP 签名校验失败。
        /// </summary>
        PurchaseFailureSignatureInvalid = 1003,

        /// <summary>
        /// Unity IAP 用户取消购买。
        /// </summary>
        PurchaseFailureUserCancelled = 1004,

        /// <summary>
        /// Unity IAP 支付被拒绝。
        /// </summary>
        PurchaseFailurePaymentDeclined = 1005,

        /// <summary>
        /// Unity IAP 重复交易。
        /// </summary>
        PurchaseFailureDuplicateTransaction = 1006,

        /// <summary>
        /// Unity IAP 交易校验失败。
        /// </summary>
        PurchaseFailureValidationFailure = 1007,

        /// <summary>
        /// Unity IAP 商店未连接。
        /// </summary>
        PurchaseFailureStoreNotConnected = 1008,

        /// <summary>
        /// Unity IAP 平台未返回购买数据。
        /// </summary>
        PurchaseFailurePurchaseMissing = 1009,

        /// <summary>
        /// Unity IAP 返回未知购买失败原因。
        /// </summary>
        PurchaseFailureUnknown = 1010,

        /// <summary>
        /// 验单前网络不可用。
        /// </summary>
        ValidateNetworkUnavailable = 2000,

        /// <summary>
        /// 验单网络请求异常、HTTP 失败或响应包装失败。
        /// </summary>
        ValidateNetworkRequestFailed = 2001,

        /// <summary>
        /// 服务端响应中缺少本次请求对应的订单结果。
        /// </summary>
        ValidateResponseMissing = 2002,

        /// <summary>
        /// 服务端返回订单仍处于待校验或未完成状态。
        /// </summary>
        ValidatePending = 2003,

        /// <summary>
        /// 订单缺少验单所需凭据，例如 Google purchase token。
        /// </summary>
        ValidateCredentialMissing = 2004,

        /// <summary>
        /// 服务端判定订单无效或拒绝订单。
        /// </summary>
        ValidateInvalid = 2005,

        /// <summary>
        /// 验单失败原因未知。
        /// </summary>
        ValidateUnknown = 2999,
    }
}
