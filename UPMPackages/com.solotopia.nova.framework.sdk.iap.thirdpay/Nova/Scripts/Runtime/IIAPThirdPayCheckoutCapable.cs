/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  IIAPThirdPayCheckoutCapable.cs
 * author:    yingzheng
 * created:   2026/9/17
 * descrip:   第三方支付结算展示能力接口
 ***************************************************************/

using NovaFramework.SDK.IAP.Runtime;

namespace NovaFramework.SDK.IAP.ThirdPay.Runtime
{
    /// <summary>
    /// 第三方支付结算流程与支付页展示配置能力。
    /// </summary>
    public interface IIAPThirdPayCheckoutCapable : IIAPCapable
    {
        /// <summary>
        /// 设置 Google 信息页的本地跳过开关；服务端明确配置时优先使用服务端值。
        /// </summary>
        /// <param name="skip">是否跳过信息页。</param>
        void SetSkipPaymentInformationScreen(bool skip);

        /// <summary>
        /// 获取本地信息页跳过开关，不包含本次支付快照的服务端覆盖值。
        /// </summary>
        bool IsPaymentInformationScreenSkipped { get; }

        /// <summary>
        /// 设置第三方支付 WebView 导航栏左上角的显示名称。
        /// </summary>
        /// <param name="titleText">标题文本；空值时恢复应用名称。</param>
        void SetThirdPayWebViewTitleText(string titleText);

        /// <summary>
        /// 设置第三方支付 WebView 导航栏右上角的关闭文本。
        /// </summary>
        /// <param name="closeText">关闭文本；空值时恢复为 close。</param>
        void SetThirdPayWebViewCloseText(string closeText);
    }
}
