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
        /// 设置是否跳过 Google 第三方支付信息页，启用后直接进入 ThirdPay 支付页。
        /// </summary>
        /// <param name="skip">是否跳过信息页。</param>
        void SetSkipPaymentInformationScreen(bool skip);

        /// <summary>
        /// 获取当前是否跳过 Google 第三方支付信息页。
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
