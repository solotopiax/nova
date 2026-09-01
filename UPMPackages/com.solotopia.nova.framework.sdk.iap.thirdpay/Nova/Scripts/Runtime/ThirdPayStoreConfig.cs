/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ThirdPayStoreConfig.cs
 * author:    yingzheng
 * created:   2026/5/20
 * descrip:   应用内第三方支付 Store 配置
 ***************************************************************/

using System;
using System.Collections.Generic;
using NovaFramework.SDK.IAP.Runtime;
using UnityEngine;

namespace NovaFramework.SDK.IAP.ThirdPay.Runtime
{
    /// <summary>
    /// InAppAuto 第三方支付配置。
    /// </summary>
    [Serializable]
    public sealed class ThirdPayStoreConfig : IIAPStoreConfig
    {
        [SerializeField, Tooltip("默认是否启用 ThirdPay Store")]
        private bool m_Enabled = true;

        [SerializeField, Tooltip("Debug 覆盖用 ISO 3166-1 alpha-2 国家/地区代码；生产留空以使用 Billing/Native/广告/默认兜底")]
        private string m_CountryCode = string.Empty;

        [SerializeField, Tooltip("是否跳过 Google 第三方支付信息页，直接进入 ThirdPay 支付页")]
        private bool m_SkipPaymentInformationScreen = false;

        [SerializeField, Tooltip("命中后使用系统外部浏览器支付的 ISO 3166-1 alpha-2 国家/地区代码")]
        private List<string> m_ExternalBrowserCountryCodes = new List<string>();

        [SerializeField, Tooltip("外部浏览器支付返回 App 后自动验单前的等待秒数")]
        private float m_ExternalBrowserReturnValidateDelaySeconds = 2.5f;

        [SerializeField, Tooltip("拉取第三方商品列表的 NetCmd 名称")]
        private string m_GetProductListCmdName = "ThirdGetProductList";

        [SerializeField, Tooltip("查询支付成功但客户端尚未校验订单的 NetCmd 名称")]
        private string m_QueryPendingOrderCmdName = "ThirdQueryPendingOrder";

        [SerializeField, Tooltip("拉取第三方支付渠道参数的 NetCmd 名称")]
        private string m_PayChannelParamsCmdName = "ThirdGetPayChannelParams";

        [SerializeField, Tooltip("验证第三方订单的 NetCmd 名称")]
        private string m_VerifyIapCmdName = "ThirdVerifyIap";

        [SerializeField, Tooltip("Google 外链结算网络类操作（连接/资格/生成 token）的超时秒数，用户信息页不受此限制")]
        private double m_GoogleApiTimeoutSeconds = 15d;

        /// <summary>
        /// 获取当前配置对应的 Store 类型。
        /// </summary>
        public IAPStoreType StoreType => IAPStoreType.ThirdPay;

        /// <summary>
        /// 获取是否默认启用 ThirdPay Store。
        /// </summary>
        public bool Enabled => m_Enabled;

        /// <summary>
        /// 获取 Debug 覆盖用国家或地区代码；生产环境通常留空。
        /// </summary>
        public string CountryCode => m_CountryCode;

        /// <summary>
        /// 获取是否默认跳过 Google 第三方支付信息页。
        /// </summary>
        public bool SkipPaymentInformationScreen => m_SkipPaymentInformationScreen;

        /// <summary>
        /// 获取需要使用系统外部浏览器打开 ThirdPay 支付页的国家或地区代码。
        /// </summary>
        public IReadOnlyList<string> ExternalBrowserCountryCodes => m_ExternalBrowserCountryCodes;

        /// <summary>
        /// 获取外部浏览器支付返回后的自动验单延迟秒数。
        /// </summary>
        public float ExternalBrowserReturnValidateDelaySeconds => m_ExternalBrowserReturnValidateDelaySeconds > 0f ? m_ExternalBrowserReturnValidateDelaySeconds : 2.5f;

        /// <summary>
        /// 获取第三方商品列表协议的 NetCmd 名称。
        /// </summary>
        public string GetProductListCmdName => m_GetProductListCmdName;

        /// <summary>
        /// 获取未校验订单查询协议的 NetCmd 名称。
        /// </summary>
        public string QueryPendingOrderCmdName => m_QueryPendingOrderCmdName;

        /// <summary>
        /// 获取第三方支付渠道参数协议的 NetCmd 名称。
        /// </summary>
        public string PayChannelParamsCmdName => m_PayChannelParamsCmdName;

        /// <summary>
        /// 获取第三方订单验单协议的 NetCmd 名称。
        /// </summary>
        public string VerifyIapCmdName => m_VerifyIapCmdName;

        /// <summary>
        /// 获取 Google 外链结算网络类操作的超时秒数；非正值时回落为 15 秒。
        /// </summary>
        public double GoogleApiTimeoutSeconds => m_GoogleApiTimeoutSeconds > 0d ? m_GoogleApiTimeoutSeconds : 15d;
    }
}
