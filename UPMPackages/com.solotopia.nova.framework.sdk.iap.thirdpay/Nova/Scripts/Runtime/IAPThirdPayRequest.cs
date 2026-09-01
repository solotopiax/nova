/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  IAPThirdPayRequest.cs
 * author:    yingzheng
 * created:   2026/5/20
 * descrip:   第三方支付渠道请求
 ***************************************************************/

namespace NovaFramework.SDK.IAP.Runtime
{
    /// <summary>
    /// 第三方支付渠道支付请求。
    /// 适用于非 Google/iOS 官方商店的外部支付（如网页支付、聚合支付平台等）。
    /// Store 层负责创建并管理应用内支付页。
    /// </summary>
    public sealed class IAPThirdPayRequest : IAPRequest
    {
        /// <summary>
        /// 当前请求对应的渠道类型，固定为 ThirdPay。
        /// </summary>
        public override IAPStoreType StoreType => IAPStoreType.ThirdPay;
    }
}
