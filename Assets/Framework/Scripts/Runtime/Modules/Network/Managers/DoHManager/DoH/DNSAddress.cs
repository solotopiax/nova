/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DNSAddress.cs
 * author:    taoye
 * created:   2026/3/9
 * descrip:   DNS地址
 ***************************************************************/

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 预定义 DoH 端点地址常量。
    /// </summary>
    public static class DNSAddress
    {
        /// <summary>
        /// Cloudflare DoH 端点地址。
        /// </summary>
        public static class Cloudflare
        {
            /// <summary>
            /// Cloudflare DoH URL（域名版）。
            /// </summary>
            public const string c_URL = "https://cloudflare-dns.com/dns-query";

            /// <summary>
            /// Cloudflare IPv4 DoH 端点。
            /// </summary>
            public static class IPv4
            {
                /// <summary>
                /// 主 IPv4 端点。
                /// </summary>
                public const string c_Primary = "https://1.1.1.1/dns-query";

                /// <summary>
                /// 备 IPv4 端点。
                /// </summary>
                public const string c_Secondary = "https://1.0.0.1/dns-query";
            }

            /// <summary>
            /// Cloudflare IPv6 DoH 端点。
            /// </summary>
            public static class IPv6
            {
                /// <summary>
                /// 主 IPv6 端点。
                /// </summary>
                public const string c_Primary = "https://[2606:4700:4700::1111]/dns-query";

                /// <summary>
                /// 备 IPv6 端点。
                /// </summary>
                public const string c_Secondary = "https://[2606:4700:4700::1001]/dns-query";
            }
        }

        /// <summary>
        /// Google DoH 端点地址。
        /// </summary>
        public static class Google
        {
            /// <summary>
            /// Google DoH URL（域名版）。
            /// </summary>
            public const string c_URL = "https://dns.google/resolve";

            /// <summary>
            /// Google IPv4 DoH 端点。
            /// </summary>
            public static class IPv4
            {
                /// <summary>
                /// 主 IPv4 端点。
                /// </summary>
                public const string c_Primary = "https://8.8.8.8/resolve";

                /// <summary>
                /// 备 IPv4 端点。
                /// </summary>
                public const string c_Secondary = "https://8.8.4.4/resolve";
            }

            /// <summary>
            /// Google IPv6 DoH 端点。
            /// </summary>
            public static class IPv6
            {
                /// <summary>
                /// 主 IPv6 端点。
                /// </summary>
                public const string c_Primary = "https://[2001:4860:4860::8888]/resolve";

                /// <summary>
                /// 备 IPv6 端点。
                /// </summary>
                public const string c_Secondary = "https://[2001:4860:4860::8844]/resolve";
            }
        }
    }
}
