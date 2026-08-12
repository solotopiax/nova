/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ThirdPayUrlRewriteRules.cs
 * author:    yingzheng
 * created:   2026/8/3
 * descrip:   第三方支付页 URL Scheme 重写规则
 ***************************************************************/

using System;
using System.Collections.Generic;

namespace NovaFramework.SDK.IAP.ThirdPay.Runtime
{
    /// <summary>
    /// 保存 ThirdPay 支付页需要监听和重写的 URL Scheme。
    /// </summary>
    internal static class ThirdPayUrlRewriteRules
    {
        private const string c_AlipayConnectSource = "alipayconnect://platformapi/alipayconnectcode.htm";
        private const string c_AlipayConnectTarget = "https://psp.ac.alipay.com/page/simulation-wallet/acwallet/alipayconnectcode.html";
        private static readonly string[] s_Schemes = { "alipayconnect" };

        /// <summary>
        /// 获取支付页需要注册的自定义 Scheme。
        /// </summary>
        /// <returns>只读 Scheme 列表。</returns>
        public static IReadOnlyList<string> GetSchemes()
        {
            return s_Schemes;
        }

        /// <summary>
        /// 尝试把 AlipayConnect Scheme 重写为兼容的 HTTPS 地址，并保留原始 Query。
        /// </summary>
        /// <param name="url">支付页请求打开的原始 URL。</param>
        /// <param name="rewrittenUrl">命中规则后的 HTTPS URL。</param>
        /// <returns>命中已知重写规则时返回 true。</returns>
        public static bool TryRewrite(string url, out string rewrittenUrl)
        {
            rewrittenUrl = null;
            if (string.IsNullOrEmpty(url) || !url.StartsWith(c_AlipayConnectSource, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (url.Length > c_AlipayConnectSource.Length && url[c_AlipayConnectSource.Length] != '?')
            {
                return false;
            }

            int queryIndex = url.IndexOf('?');
            rewrittenUrl = queryIndex < 0 ? c_AlipayConnectTarget : c_AlipayConnectTarget + url.Substring(queryIndex);
            return true;
        }
    }
}
