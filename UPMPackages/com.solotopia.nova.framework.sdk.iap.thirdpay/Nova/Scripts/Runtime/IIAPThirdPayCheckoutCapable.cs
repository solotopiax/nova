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
        /// 保留旧支付页标题设置入口以兼容源码；当前系统浏览器不应用任意标题文案。
        /// </summary>
        /// <param name="titleText">旧标题文本，当前实现忽略此值。</param>
        void SetThirdPayWebViewTitleText(string titleText);

        /// <summary>
        /// 保留旧支付页关闭文案入口以兼容源码；当前关闭按钮由系统负责。
        /// </summary>
        /// <param name="closeText">旧关闭文本，当前实现忽略此值。</param>
        void SetThirdPayWebViewCloseText(string closeText);
    }
}
