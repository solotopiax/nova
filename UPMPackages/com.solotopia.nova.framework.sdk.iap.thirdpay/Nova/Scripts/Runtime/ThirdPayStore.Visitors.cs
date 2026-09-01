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
        /// 应用内第三方支付页对应的固定 NetCmd 名称。
        /// </summary>
        private const string c_OpenUrlCmdName = "ThirdOpenURL";

        /// <summary>
        /// 服务端验单失败后的重试间隔，单位为秒。
        /// </summary>
        private static readonly float[] s_ValidateRetryIntervals = { 0.2f, 0.5f, 1f, 2f, 4f };

        /// <summary>
        /// 外部浏览器支付返回 App 后的默认自动验单延迟，单位为秒。
        /// </summary>
        private const float c_DefaultExternalBrowserReturnValidateDelaySeconds = 2.5f;

        /// <summary>
        /// 第三方支付国家码兜底值，和 Solar 保持一致。
        /// </summary>
        private const string c_DefaultCountryCode = "US";

        /// <summary>
        /// 广告或平台侧可能返回的无效国家码，ThirdPay 按 Solar 规则映射为 US。
        /// </summary>
        private const string c_InvalidCountryCode = "IV";

        /// <summary>
        /// iOS 原生层在无法识别商店国家码时返回的占位值。
        /// </summary>
        private const string c_UnknownCountryCode = "UNKNOWN";

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
        /// 最近一次成功拉取的第三方商品列表。
        /// </summary>
        private PbNetThirdProductListResp m_ProductList;

        /// <summary>
        /// 当前账号的第三方支付存档容器。
        /// </summary>
        private ThirdPayPersistData m_PersistData;

        /// <summary>
        /// 当前账号待处理订单仓储。
        /// </summary>
        private ThirdPayOrderRepository m_OrderRepository;

        /// <summary>
        /// 框架内应用内支付页服务。
        /// </summary>
        private IThirdPayWebViewService m_WebViewService;

        /// <summary>
        /// 系统外部浏览器打开服务。
        /// </summary>
        private IThirdPayExternalBrowserService m_ExternalBrowserService;

        /// <summary>
        /// 当前外部浏览器支付会话；为空表示没有浏览器支付等待返回验单。
        /// </summary>
        private ThirdPayExternalBrowserPaySession m_ExternalBrowserPaySession;

        /// <summary>
        /// Android Google 外链政策处理服务。
        /// </summary>
        private ThirdPayGooglePolicyService m_GooglePolicy;

        /// <summary>
        /// Debug 覆盖用国家或地区代码，优先级高于所有运行时自动解析来源。
        /// </summary>
        private string m_DebugCountryCode = string.Empty;

        /// <summary>
        /// 首次自动解析后锁定的国家码，避免同一运行期商品国家反复漂移。
        /// </summary>
        private string m_LockCountryCode = string.Empty;

        /// <summary>
        /// Google Play Billing 返回的商店国家码。
        /// </summary>
        private string m_BillingCountryCode = string.Empty;

        /// <summary>
        /// iOS StoreKit storefront 返回的商店国家码。
        /// </summary>
        private string m_NativeCountryCode = string.Empty;

        /// <summary>
        /// iOS StoreKit storefront 返回的商店区域标识。
        /// </summary>
        private string m_NativeStorefrontIdentifier = string.Empty;

        /// <summary>
        /// 广告模块返回或缓存的国家码，用作 Billing 与原生国家码之后的兜底来源。
        /// </summary>
        private string m_AdCountryCode = string.Empty;

        /// <summary>
        /// 商品列表请求版本号，用于忽略旧国家或旧账号返回的过期响应。
        /// </summary>
        private int m_ProductListRequestVersion;

        /// <summary>
        /// 当前是否跳过 Google 第三方支付信息页。
        /// </summary>
        private bool m_SkipPaymentInformationScreen;

        /// <summary>
        /// 必需的 Store 配置项是否齐备，决定 Store 是否就绪接受支付。
        /// </summary>
        private bool m_ConfigReady;

        /// <summary>
        /// 支付参数加密使用的 AES 密钥，初始化或首次支付时解析并缓存。
        /// </summary>
        private string m_AesKey;

        /// <summary>
        /// 支付参数加密使用的 AES 向量，初始化或首次支付时解析并缓存。
        /// </summary>
        private string m_AesIv;

        /// <summary>
        /// 第三方支付页 URL 基址，初始化或首次支付时解析并缓存。
        /// </summary>
        private string m_PayUrlBase;
    }
}
