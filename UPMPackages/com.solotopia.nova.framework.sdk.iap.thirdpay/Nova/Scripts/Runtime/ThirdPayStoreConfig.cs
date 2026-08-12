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
using NovaFramework.SDK.IAP.Runtime;
using UnityEngine;
using UnityEngine.Serialization;

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

        [SerializeField, Tooltip("ISO 3166-1 alpha-2 国家/地区代码")]
        private string m_CountryCode = string.Empty;

        [SerializeField, Tooltip("拉取第三方商品列表的 NetCmd 名称")]
        private string m_GetProductListCmdName = "ThirdGetProductList";

        [FormerlySerializedAs("m_ValidateSuccessOrderListCmdName")]
        [SerializeField, Tooltip("查询支付成功但客户端尚未校验订单的 NetCmd 名称")]
        private string m_QueryPendingOrderCmdName = "ThirdQueryPendingOrder";

        [SerializeField, Tooltip("拉取第三方支付渠道参数的 NetCmd 名称")]
        private string m_PayChannelParamsCmdName = "ThirdGetPayChannelParams";

        [FormerlySerializedAs("m_CheckOrderCmdName")]
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
        /// 获取默认国家或地区代码。
        /// </summary>
        public string CountryCode => m_CountryCode;

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
