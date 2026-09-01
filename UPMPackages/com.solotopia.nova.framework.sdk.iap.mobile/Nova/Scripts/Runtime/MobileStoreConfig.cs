/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MobileStoreConfig.cs
 * author:    yingzheng
 * created:   2026/5/26
 * descrip:   移动端官方内购商店专属配置，实现 IIAPStoreConfig
 ***************************************************************/

using System;
using System.Collections.Generic;
using UnityEngine;
using NovaFramework.Runtime;
using NovaFramework.SDK.IAP.Runtime;
namespace NovaFramework.SDK.IAP.Mobile.Runtime
{
    /// <summary>
    /// 移动端官方内购（Google Play / iOS App Store）商店专属配置。
    /// 在 IAPPluginConfig Inspector 中以 [SerializeReference] 多态条目添加。
    /// </summary>
    [Serializable]
    public sealed class MobileStoreConfig : IIAPStoreConfig
    {
        /// <summary>
        /// 商品拉取失败后的默认自动重试延迟表，单位毫秒；默认三次重试：2 秒、5 秒、10 秒。
        /// </summary>
        private static readonly int[] s_DefaultProductFetchRetryDelaysMs = { 2000, 5000, 10000 };

        /// <summary>
        /// 当前配置对应的商店渠道类型，固定为 Mobile。
        /// </summary>
        public IAPStoreType StoreType => IAPStoreType.Mobile;

        /// <summary>
        /// 当前商店是否启用；false 时 IAPPlugin 跳过初始化。
        /// </summary>
        [SerializeField, Tooltip("默认是否启用移动端官方内购商店；运行时可通过 IAPPlugin.SetStoreEnabled 覆盖")]
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

        // ── 商品拉取重试 ────────────────────────────────────────────────
        /// <summary>
        /// 商品拉取失败后的自动重试延迟，单位毫秒；默认 2 秒、5 秒、10 秒。
        /// </summary>
        [SerializeField, Tooltip("商品拉取失败后的自动重试延迟，单位毫秒；默认 2000/5000/10000，表示最多重试 3 次。")]
        private int[] m_ProductFetchRetryDelaysMs = { 2000, 5000, 10000 };


        /// <summary>
        /// 当前商店是否启用。
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

        /// <summary>
        /// 商品拉取失败后的自动重试延迟表，单位毫秒；非法配置会回落到默认 2 秒、5 秒、10 秒。
        /// </summary>
        public IReadOnlyList<int> ProductFetchRetryDelaysMs => SanitizeProductFetchRetryDelaysMs(m_ProductFetchRetryDelaysMs);

        /// <summary>
        /// 规整商品拉取重试延迟配置；空列表或非正数都会回落默认延迟表。
        /// </summary>
        /// <param name="retryDelaysMs">Inspector 配置的重试延迟表，单位毫秒。</param>
        /// <returns>可用于运行时商品拉取协调器的重试延迟表。</returns>
        private static IReadOnlyList<int> SanitizeProductFetchRetryDelaysMs(int[] retryDelaysMs)
        {
            if (retryDelaysMs == null || retryDelaysMs.Length == 0)
            {
                IAPLog.Warning(NovaFramework.Runtime.LogTag.IAPMobile, "商品拉取重试延迟配置为空，已回落到默认 2s/5s/10s。");
                return s_DefaultProductFetchRetryDelaysMs;
            }

            for (int i = 0; i < retryDelaysMs.Length; i++)
            {
                if (retryDelaysMs[i] <= 0)
                {
                    IAPLog.Warning(NovaFramework.Runtime.LogTag.IAPMobile, $"商品拉取重试延迟配置包含非法值，索引={i}，值={retryDelaysMs[i]}，已回落到默认 2s/5s/10s。");
                    return s_DefaultProductFetchRetryDelaysMs;
                }
            }

            return retryDelaysMs;
        }
    }
}
