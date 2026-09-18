/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ThirdPayStore.Visitors.cs
 * author:    yingzheng
 * created:   2026/5/20
 * descrip:   ThirdPayStore 常量、字段与属性
 ***************************************************************/

using NovaFramework.SDK.IAP.Runtime;

namespace NovaFramework.SDK.IAP.ThirdPay.Runtime
{
    public sealed partial class ThirdPayStore
    {
        /// <summary>
        /// 服务端验单失败后的重试间隔，单位为秒。
        /// </summary>
        private static readonly float[] s_ValidateRetryIntervals = { 0.2f, 0.5f, 1f, 2f, 4f, 8f };

        /// <summary>
        /// 外部浏览器支付返回 App 后的默认自动验单延迟，单位为秒。
        /// </summary>
        private const float c_DefaultExternalBrowserReturnValidateDelaySeconds = 2.5f;

        /// <summary>
        /// 获取第三方支付打点使用的固定渠道标识。
        /// </summary>
        protected override string TrackChannel => IAPStoreType.ThirdPay.ToString().ToLowerInvariant();

        /// <summary>
        /// 当前 Store 使用的 Nova 日志标签。
        /// </summary>
        protected override string LogTag => NovaFramework.Runtime.LogTag.IAPThirdPay;

        /// <summary>
        /// 获取当前 Store 是否已具备基础配置且必需配置项齐备。
        /// </summary>
        protected override bool IsStoreReady => m_Config != null && m_ConfigReady;

        /// <summary>
        /// 第三方支付 Store 配置。
        /// </summary>
        private ThirdPayStoreConfig m_Config;

        /// <summary>
        /// 第三方支付协议服务。
        /// </summary>
        private ThirdIapNetService m_NetService;

        /// <summary>
        /// 按 GameUID 合并登录预取与支付等待的渠道参数加载器。
        /// </summary>
        private ThirdPayChannelParamsLoader m_ChannelParamsLoader;

        /// <summary>
        /// 框架内应用内支付页服务。
        /// </summary>
        private IThirdPayWebViewService m_WebViewService;

        /// <summary>
        /// 系统外部浏览器打开服务。
        /// </summary>
        private IThirdPayExternalBrowserService m_ExternalBrowserService;

        /// <summary>
        /// Android Google 外链政策处理服务。
        /// </summary>
        private ThirdPayGooglePolicyService m_GooglePolicy;

        /// <summary>
        /// 第三方支付国家码运行时状态。
        /// </summary>
        private readonly ThirdPayCountryState m_CountryState = new ThirdPayCountryState();

        /// <summary>
        /// 第三方支付商品快照与在途请求状态。
        /// </summary>
        private readonly ThirdPayProductCatalogState m_ProductCatalogState = new ThirdPayProductCatalogState();

        /// <summary>
        /// 当前账号持久化和订单仓储上下文。
        /// </summary>
        private readonly ThirdPayPersistContext m_PersistContext = new ThirdPayPersistContext();

        /// <summary>
        /// 外部浏览器支付会话状态。
        /// </summary>
        private readonly ThirdPayExternalBrowserSessionState m_ExternalBrowserSessionState = new ThirdPayExternalBrowserSessionState();

        /// <summary>
        /// 当前是否跳过 Google 第三方支付信息页。
        /// </summary>
        private bool m_SkipPaymentInformationScreen;

        /// <summary>
        /// 必需的 Store 配置项是否齐备，决定 Store 是否就绪接受支付。
        /// </summary>
        private bool m_ConfigReady;

        /// <summary>
        /// 第三方支付页 URL 基址，初始化或首次支付时解析并缓存。
        /// </summary>
        private string m_PayUrlBase;
    }
}
