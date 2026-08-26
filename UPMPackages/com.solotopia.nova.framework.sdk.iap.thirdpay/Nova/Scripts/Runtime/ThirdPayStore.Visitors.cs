/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ThirdPayStore.Visitors.cs
 * author:    yingzheng
 * created:   2026/5/20
 * descrip:   ThirdPayStore 常量、字段与属性
 ***************************************************************/

using NovaFramework.Runtime;
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
        /// 获取第三方支付打点使用的固定渠道标识。
        /// </summary>
        protected override string TrackChannel => IAPStoreType.ThirdPay.ToString().ToLowerInvariant();

        /// <summary>
        /// 获取第三方支付日志标签。
        /// </summary>
        protected override string StoreLogTag => LogTag.IAPThirdPay;

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
        /// Android Google 外链政策处理服务。
        /// </summary>
        private ThirdPayGooglePolicyService m_GooglePolicy;

        /// <summary>
        /// 当前支付国家或地区代码。
        /// </summary>
        private string m_CountryCode = string.Empty;

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
