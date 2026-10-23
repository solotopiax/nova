/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ThirdPayIosStorefrontCountryBridge.cs
 * author:    yingzheng
 * created:   2026/8/28
 * descrip:   ThirdPay iOS StoreKit storefront 区域桥接
 ***************************************************************/

using System;
using System.Collections.Generic;

#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
using AOT;
#endif

namespace NovaFramework.SDK.IAP.ThirdPay.Runtime
{
    /// <summary>
    /// 将 StoreKit 返回的 ISO 3166-1 alpha-3 国家码转换为统一的 alpha-2 国家码。
    /// </summary>
    internal static class ThirdPayStorefrontRegionCode
    {
        private static readonly Dictionary<string, string> s_Alpha3ToAlpha2 = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "ABW", "AW" },
            { "AFG", "AF" },
            { "AGO", "AO" },
            { "AIA", "AI" },
            { "ALA", "AX" },
            { "ALB", "AL" },
            { "AND", "AD" },
            { "ARE", "AE" },
            { "ARG", "AR" },
            { "ARM", "AM" },
            { "ASM", "AS" },
            { "ATA", "AQ" },
            { "ATF", "TF" },
            { "ATG", "AG" },
            { "AUS", "AU" },
            { "AUT", "AT" },
            { "AZE", "AZ" },
            { "BDI", "BI" },
            { "BEL", "BE" },
            { "BEN", "BJ" },
            { "BES", "BQ" },
            { "BFA", "BF" },
            { "BGD", "BD" },
            { "BGR", "BG" },
            { "BHR", "BH" },
            { "BHS", "BS" },
            { "BIH", "BA" },
            { "BLM", "BL" },
            { "BLR", "BY" },
            { "BLZ", "BZ" },
            { "BMU", "BM" },
            { "BOL", "BO" },
            { "BRA", "BR" },
            { "BRB", "BB" },
            { "BRN", "BN" },
            { "BTN", "BT" },
            { "BVT", "BV" },
            { "BWA", "BW" },
            { "CAF", "CF" },
            { "CAN", "CA" },
            { "CCK", "CC" },
            { "CHE", "CH" },
            { "CHL", "CL" },
            { "CHN", "CN" },
            { "CIV", "CI" },
            { "CMR", "CM" },
            { "COD", "CD" },
            { "COG", "CG" },
            { "COK", "CK" },
            { "COL", "CO" },
            { "COM", "KM" },
            { "CPV", "CV" },
            { "CRI", "CR" },
            { "CUB", "CU" },
            { "CUW", "CW" },
            { "CXR", "CX" },
            { "CYM", "KY" },
            { "CYP", "CY" },
            { "CZE", "CZ" },
            { "DEU", "DE" },
            { "DJI", "DJ" },
            { "DMA", "DM" },
            { "DNK", "DK" },
            { "DOM", "DO" },
            { "DZA", "DZ" },
            { "ECU", "EC" },
            { "EGY", "EG" },
            { "ERI", "ER" },
            { "ESH", "EH" },
            { "ESP", "ES" },
            { "EST", "EE" },
            { "ETH", "ET" },
            { "FIN", "FI" },
            { "FJI", "FJ" },
            { "FLK", "FK" },
            { "FRA", "FR" },
            { "FRO", "FO" },
            { "FSM", "FM" },
            { "GAB", "GA" },
            { "GBR", "GB" },
            { "GEO", "GE" },
            { "GGY", "GG" },
            { "GHA", "GH" },
            { "GIB", "GI" },
            { "GIN", "GN" },
            { "GLP", "GP" },
            { "GMB", "GM" },
            { "GNB", "GW" },
            { "GNQ", "GQ" },
            { "GRC", "GR" },
            { "GRD", "GD" },
            { "GRL", "GL" },
            { "GTM", "GT" },
            { "GUF", "GF" },
            { "GUM", "GU" },
            { "GUY", "GY" },
            { "HKG", "HK" },
            { "HMD", "HM" },
            { "HND", "HN" },
            { "HRV", "HR" },
            { "HTI", "HT" },
            { "HUN", "HU" },
            { "IDN", "ID" },
            { "IMN", "IM" },
            { "IND", "IN" },
            { "IOT", "IO" },
            { "IRL", "IE" },
            { "IRN", "IR" },
            { "IRQ", "IQ" },
            { "ISL", "IS" },
            { "ISR", "IL" },
            { "ITA", "IT" },
            { "JAM", "JM" },
            { "JEY", "JE" },
            { "JOR", "JO" },
            { "JPN", "JP" },
            { "KAZ", "KZ" },
            { "KEN", "KE" },
            { "KGZ", "KG" },
            { "KHM", "KH" },
            { "KIR", "KI" },
            { "KNA", "KN" },
            { "KOR", "KR" },
            { "KWT", "KW" },
            { "LAO", "LA" },
            { "LBN", "LB" },
            { "LBR", "LR" },
            { "LBY", "LY" },
            { "LCA", "LC" },
            { "LIE", "LI" },
            { "LKA", "LK" },
            { "LSO", "LS" },
            { "LTU", "LT" },
            { "LUX", "LU" },
            { "LVA", "LV" },
            { "MAC", "MO" },
            { "MAF", "MF" },
            { "MAR", "MA" },
            { "MCO", "MC" },
            { "MDA", "MD" },
            { "MDG", "MG" },
            { "MDV", "MV" },
            { "MEX", "MX" },
            { "MHL", "MH" },
            { "MKD", "MK" },
            { "MLI", "ML" },
            { "MLT", "MT" },
            { "MMR", "MM" },
            { "MNE", "ME" },
            { "MNG", "MN" },
            { "MNP", "MP" },
            { "MOZ", "MZ" },
            { "MRT", "MR" },
            { "MSR", "MS" },
            { "MTQ", "MQ" },
            { "MUS", "MU" },
            { "MWI", "MW" },
            { "MYS", "MY" },
            { "MYT", "YT" },
            { "NAM", "NA" },
            { "NCL", "NC" },
            { "NER", "NE" },
            { "NFK", "NF" },
            { "NGA", "NG" },
            { "NIC", "NI" },
            { "NIU", "NU" },
            { "NLD", "NL" },
            { "NOR", "NO" },
            { "NPL", "NP" },
            { "NRU", "NR" },
            { "NZL", "NZ" },
            { "OMN", "OM" },
            { "PAK", "PK" },
            { "PAN", "PA" },
            { "PCN", "PN" },
            { "PER", "PE" },
            { "PHL", "PH" },
            { "PLW", "PW" },
            { "PNG", "PG" },
            { "POL", "PL" },
            { "PRI", "PR" },
            { "PRK", "KP" },
            { "PRT", "PT" },
            { "PRY", "PY" },
            { "PSE", "PS" },
            { "PYF", "PF" },
            { "QAT", "QA" },
            { "REU", "RE" },
            { "ROU", "RO" },
            { "RUS", "RU" },
            { "RWA", "RW" },
            { "SAU", "SA" },
            { "SDN", "SD" },
            { "SEN", "SN" },
            { "SGP", "SG" },
            { "SGS", "GS" },
            { "SHN", "SH" },
            { "SJM", "SJ" },
            { "SLB", "SB" },
            { "SLE", "SL" },
            { "SLV", "SV" },
            { "SMR", "SM" },
            { "SOM", "SO" },
            { "SPM", "PM" },
            { "SRB", "RS" },
            { "SSD", "SS" },
            { "STP", "ST" },
            { "SUR", "SR" },
            { "SVK", "SK" },
            { "SVN", "SI" },
            { "SWE", "SE" },
            { "SWZ", "SZ" },
            { "SXM", "SX" },
            { "SYC", "SC" },
            { "SYR", "SY" },
            { "TCA", "TC" },
            { "TCD", "TD" },
            { "TGO", "TG" },
            { "THA", "TH" },
            { "TJK", "TJ" },
            { "TKL", "TK" },
            { "TKM", "TM" },
            { "TLS", "TL" },
            { "TON", "TO" },
            { "TTO", "TT" },
            { "TUN", "TN" },
            { "TUR", "TR" },
            { "TUV", "TV" },
            { "TWN", "TW" },
            { "TZA", "TZ" },
            { "UGA", "UG" },
            { "UKR", "UA" },
            { "UMI", "UM" },
            { "URY", "UY" },
            { "USA", "US" },
            { "UZB", "UZ" },
            { "VAT", "VA" },
            { "VCT", "VC" },
            { "VEN", "VE" },
            { "VGB", "VG" },
            { "VIR", "VI" },
            { "VNM", "VN" },
            { "VUT", "VU" },
            { "WLF", "WF" },
            { "WSM", "WS" },
            { "YEM", "YE" },
            { "ZAF", "ZA" },
            { "ZMB", "ZM" },
            { "ZWE", "ZW" },
        };

        /// <summary>
        /// 将 Storefront 国家码规范化为 ISO 3166-1 alpha-2。
        /// </summary>
        /// <param name="countryCode">StoreKit 国家码或 Locale 回退国家码。</param>
        /// <returns>规范化后的两位国家码；输入无效或无法识别时返回空字符串。</returns>
        internal static string ToAlpha2(string countryCode)
        {
            if (string.IsNullOrWhiteSpace(countryCode))
            {
                return string.Empty;
            }

            string normalized = countryCode.Trim().ToUpperInvariant();
            if (normalized.Length == 2)
            {
                return normalized;
            }

            return s_Alpha3ToAlpha2.TryGetValue(normalized, out string alpha2) ? alpha2 : string.Empty;
        }
    }

    /// <summary>
    /// 桥接 iOS StoreKit storefront 国家码查询，并向托管层转发查询结果。
    /// </summary>
    internal static class ThirdPayIosStorefrontCountryBridge
    {
#if UNITY_IOS && !UNITY_EDITOR
        /// <summary>
        /// iOS StoreKit storefront 原生回调。
        /// </summary>
        /// <param name="countryCode">App Store 国家或地区代码。</param>
        /// <param name="identifier">App Store storefront 标识。</param>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void StorefrontRegionCallback(string countryCode, string identifier);

        /// <summary>
        /// 传递给 iOS 原生层的固定回调，避免委托被 GC。
        /// </summary>
        private static readonly StorefrontRegionCallback s_Callback = OnStorefrontRegion;

        /// <summary>
        /// 当前等待 iOS storefront 结果的托管回调。
        /// </summary>
        private static Action<string, string> s_ResultHandler;

        /// <summary>
        /// 调用 ThirdPay 包内 iOS 插件读取 StoreKit storefront。
        /// </summary>
        /// <param name="callback">原生层读取完成后的回调。</param>
        [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
        private static extern void NovaThirdPayGetStorefrontRegion(StorefrontRegionCallback callback);

        /// <summary>
        /// 请求 iOS 原生层读取 App Store storefront。
        /// </summary>
        /// <param name="resultHandler">读取完成后的托管回调。</param>
        internal static void Request(Action<string, string> resultHandler)
        {
            s_ResultHandler = resultHandler;
            try
            {
                NovaThirdPayGetStorefrontRegion(s_Callback);
            }
            catch
            {
                s_ResultHandler = null;
                throw;
            }
        }

        /// <summary>
        /// 接收 iOS 原生层返回的 storefront 信息。
        /// </summary>
        /// <param name="countryCode">App Store 国家或地区代码。</param>
        /// <param name="identifier">App Store storefront 标识。</param>
        [MonoPInvokeCallback(typeof(StorefrontRegionCallback))]
        private static void OnStorefrontRegion(string countryCode, string identifier)
        {
            Action<string, string> resultHandler = s_ResultHandler;
            s_ResultHandler = null;
            resultHandler?.Invoke(ThirdPayStorefrontRegionCode.ToAlpha2(countryCode), identifier ?? string.Empty);
        }
#else
        /// <summary>
        /// 非 iOS 平台直接返回空 storefront 信息。
        /// </summary>
        /// <param name="resultHandler">读取完成后的托管回调。</param>
        internal static void Request(Action<string, string> resultHandler)
        {
            resultHandler?.Invoke(string.Empty, string.Empty);
        }
#endif
    }
}
