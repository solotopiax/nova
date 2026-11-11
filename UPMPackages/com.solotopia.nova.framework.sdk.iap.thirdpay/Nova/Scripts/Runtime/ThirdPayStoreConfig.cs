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
        /// <summary>
        /// 默认是否启用 ThirdPay Store。
        /// </summary>
        [SerializeField, Tooltip("默认是否启用 ThirdPay Store")]
        private bool m_Enabled = true;

        /// <summary>
        /// Android 重复购买时是否先查询支付成功但尚未校验的订单。
        /// </summary>
        [SerializeField, InspectorName("启用安卓重复点击补单查询"), Tooltip("启用安卓第三方支付重复点击补单查询；查询失败时不会创建新订单")]
        private bool m_AndroidOrderRecoveryEnabled = true;

        /// <summary>
        /// Android 购买前补单查询窗口，单位为分钟；零表示跳过查询。
        /// </summary>
        [SerializeField, InspectorName("安卓补单查询窗口（分钟）"), Range(0, 30), Tooltip("安卓第三方支付补单查询窗口（分钟），0 表示不查询，最大 30 分钟")]
        private int m_AndroidOrderRecoveryWindowMinutes = 30;

        /// <summary>
        /// 是否跳过 Google 第三方支付信息页。
        /// </summary>
        [SerializeField, Tooltip("是否跳过 Google 第三方支付信息页，直接进入 ThirdPay 支付页")]
        [FormerlySerializedAs("m_SkipPaymentInformationScreen")]
        private bool m_AndroidSkipPaymentInformationScreen = false;

        /// <summary>
        /// 外部浏览器返回应用后自动验单前的等待秒数。
        /// </summary>
        [SerializeField, Tooltip("外部浏览器支付返回 App 后自动验单前的等待秒数")]
        [FormerlySerializedAs("m_ExternalBrowserReturnValidateDelaySeconds")]
        private float m_AndroidExternalBrowserReturnValidateDelaySeconds = 2.5f;

        /// <summary>
        /// 获取统一第三方支付配置的协议名。
        /// </summary>
        [SerializeField, Tooltip("获取第三方支付配置的 NetCmd 名称")]
        private string m_PaymentConfigCmdName = "ThirdGetPaymentConfig";

        /// <summary>
        /// 查询服务端待验订单的协议名。
        /// </summary>
        [SerializeField, Tooltip("查询支付成功但客户端尚未校验订单的 NetCmd 名称")]
        private string m_QueryPendingOrderCmdName = "ThirdQueryPendingOrder";

        /// <summary>
        /// 验证第三方支付订单的协议名。
        /// </summary>
        [SerializeField, Tooltip("验证第三方订单的 NetCmd 名称")]
        private string m_VerifyIapCmdName = "ThirdVerifyIap";

        /// <summary>
        /// Google 外链结算网络操作的超时秒数。
        /// </summary>
        [SerializeField, Tooltip("Google 外链结算网络类操作（连接/资格/生成 token）的超时秒数，用户信息页不受此限制")]
        [FormerlySerializedAs("m_GoogleApiTimeoutSeconds")]
        private double m_AndroidGoogleApiTimeoutSeconds = 15d;

        /// <summary>
        /// 获取当前配置对应的 Store 类型。
        /// </summary>
        public IAPStoreType StoreType => IAPStoreType.ThirdPay;

        /// <summary>
        /// 获取是否默认启用 ThirdPay Store。
        /// </summary>
        public bool Enabled => m_Enabled;

        /// <summary>
        /// 获取 Android 购买前补单查询开关；非 Android 不改变原购买流程。
        /// </summary>
        public bool AndroidOrderRecoveryEnabled => m_AndroidOrderRecoveryEnabled;

        /// <summary>
        /// 获取限制在零至三十分钟的 Android 购买前查询窗口。
        /// </summary>
        public int AndroidOrderRecoveryWindowMinutes => Math.Max(0, Math.Min(30, m_AndroidOrderRecoveryWindowMinutes));

        /// <summary>
        /// 获取是否默认跳过 Google 第三方支付信息页。
        /// </summary>
        public bool AndroidSkipPaymentInformationScreen => m_AndroidSkipPaymentInformationScreen;

        /// <summary>
        /// 获取外部浏览器支付返回后的自动验单延迟秒数。
        /// </summary>
        public float AndroidExternalBrowserReturnValidateDelaySeconds => m_AndroidExternalBrowserReturnValidateDelaySeconds > 0f ? m_AndroidExternalBrowserReturnValidateDelaySeconds : 2.5f;

        /// <summary>
        /// 获取第三方支付配置协议的 NetCmd 名称。
        /// </summary>
        public string PaymentConfigCmdName => string.IsNullOrEmpty(m_PaymentConfigCmdName) ? "ThirdGetPaymentConfig" : m_PaymentConfigCmdName;

        /// <summary>
        /// 获取未校验订单查询协议的 NetCmd 名称。
        /// </summary>
        public string QueryPendingOrderCmdName => m_QueryPendingOrderCmdName;

        /// <summary>
        /// 获取第三方订单验单协议的 NetCmd 名称。
        /// </summary>
        public string VerifyIapCmdName => m_VerifyIapCmdName;

        /// <summary>
        /// 获取 Google 外链结算网络类操作的超时秒数；非正值时回落为 15 秒。
        /// </summary>
        public double AndroidGoogleApiTimeoutSeconds => m_AndroidGoogleApiTimeoutSeconds > 0d ? m_AndroidGoogleApiTimeoutSeconds : 15d;
    }
}
