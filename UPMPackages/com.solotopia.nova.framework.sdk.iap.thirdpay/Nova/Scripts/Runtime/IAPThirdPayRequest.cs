/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  IAPThirdPayRequest.cs
 * author:    yingzheng
 * created:   2026/5/20
 * descrip:   第三方支付渠道请求（含支付方式与 WebView 适配区域）
 ***************************************************************/

using UnityEngine;

namespace NovaFramework.SDK.IAP.Runtime
{
    /// <summary>
    /// 第三方支付渠道支付请求。
    /// 适用于非 Google/iOS 官方商店的外部支付（如网页支付、聚合支付平台等）。
    /// store 层将通过 WebView 或外部跳转完成支付，并监听回调。
    /// </summary>
    public sealed class IAPThirdPayRequest : IAPRequest
    {
        /// <summary>
        /// 当前请求对应的渠道类型，固定为 ThirdPay。
        /// </summary>
        public override IAPStoreType StoreType => IAPStoreType.ThirdPay;

        /// <summary>
        /// 支付类型 ID，对应配置表中定义的第三方支付渠道枚举值。
        /// </summary>
        public int PayTypeId;

        /// <summary>
        /// 支付方式标识字符串（如 "alipay"、"wechat"），传递给支付网关。
        /// </summary>
        public string PayMethod;

        /// <summary>
        /// WebView 适配锚点，WebView 将根据此 RectTransform 确定弹出区域。
        /// 为 null 时 store 使用默认全屏布局。
        /// </summary>
        public RectTransform AdaptRectTransform;
    }
}
