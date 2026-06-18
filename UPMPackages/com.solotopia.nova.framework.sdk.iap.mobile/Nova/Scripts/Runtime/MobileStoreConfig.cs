/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MobileStoreConfig.cs
 * author:    yingzheng
 * created:   2026/5/26
 * descrip:   移动端官方内购 store 专属配置，实现 IIAPStoreConfig
 ***************************************************************/

using System;
using UnityEngine;
using NovaFramework.SDK.IAP.Runtime;

using NovaFramework.Runtime;
namespace NovaFramework.SDK.IAP.Mobile.Runtime
{
    /// <summary>
    /// 移动端官方内购（Google Play / iOS App Store）store 专属配置。
    /// 在 IAPPluginConfig Inspector 中以 [SerializeReference] 多态条目添加。
    /// </summary>
    [Serializable]
    public sealed class MobileStoreConfig : IIAPStoreConfig
    {
        /// <summary>
        /// 当前配置对应的 store 渠道类型，固定为 Mobile。
        /// </summary>
        public IAPStoreType StoreType => IAPStoreType.Mobile;

        /// <summary>
        /// 当前 store 是否启用；false 时 IAPPlugin 跳过初始化。
        /// </summary>
        [SerializeField, Tooltip("默认是否启用 Mobile Store；运行时可通过 IAPPlugin.SetStoreEnabled 覆盖")]
        private bool m_Enabled = true;

        // ── Google Play 协议 ──────────────────────────────────────────────
        [SerializeField, Tooltip("用于查询谷歌未完成(已支付)订单列表的协议名。填写 NetCmd 表中的名称，如 IAPGoogleQueryPendingOrder。")]
        private string m_GoogleQueryPendingOrderCmdName = "IAPGoogleQueryPendingOrder";

        [SerializeField, Tooltip("用于谷歌普通内购验单的协议名。填写 NetCmd 表中的名称，如 IAPGoogleVerify。")]
        private string m_GoogleVerifyCmdName = "IAPGoogleVerify";

        [SerializeField, Tooltip("用于谷歌订阅内购验单的协议名。填写 NetCmd 表中的名称，如 IAPGoogleVerifySubscription。")]
        private string m_GoogleVerifySubscriptionCmdName = "IAPGoogleVerifySubscription";

        // ── Apple App Store 协议 ──────────────────────────────────────────
        [SerializeField, Tooltip("用于查询苹果未完成(已支付)订单列表的协议名。填写 NetCmd 表中的名称，如 IAPAppleQueryPendingOrder。")]
        private string m_AppleQueryPendingOrderCmdName = "IAPAppleQueryPendingOrder";

        [SerializeField, Tooltip("用于苹果普通内购验单的协议名。填写 NetCmd 表中的名称，如 IAPAppleVerify。")]
        private string m_AppleVerifyCmdName = "IAPAppleVerify";

        [SerializeField, Tooltip("用于苹果订阅内购验单的协议名。填写 NetCmd 表中的名称，如 IAPAppleVerifySubscription。")]
        private string m_AppleVerifySubscriptionCmdName = "IAPAppleVerifySubscription";


        /// <summary>
        /// 当前 store 是否启用。
        /// </summary>
        public bool Enabled => m_Enabled;

        /// <summary>
        /// 谷歌-查询未完成订单协议名。
        /// </summary>
        public string GoogleQueryPendingOrderCmdName => m_GoogleQueryPendingOrderCmdName;

        /// <summary>
        /// 谷歌-普通内购验单协议名。
        /// </summary>
        public string GoogleVerifyCmdName => m_GoogleVerifyCmdName;

        /// <summary>
        /// 谷歌-订阅内购验单协议名。
        /// </summary>
        public string GoogleVerifySubscriptionCmdName => m_GoogleVerifySubscriptionCmdName;

        /// <summary>
        /// 苹果-查询未完成订单协议名。
        /// </summary>
        public string AppleQueryPendingOrderCmdName => m_AppleQueryPendingOrderCmdName;

        /// <summary>
        /// 苹果-普通内购验单协议名。
        /// </summary>
        public string AppleVerifyCmdName => m_AppleVerifyCmdName;

        /// <summary>
        /// 苹果-订阅内购验单协议名。
        /// </summary>
        public string AppleVerifySubscriptionCmdName => m_AppleVerifySubscriptionCmdName;
    }
}
