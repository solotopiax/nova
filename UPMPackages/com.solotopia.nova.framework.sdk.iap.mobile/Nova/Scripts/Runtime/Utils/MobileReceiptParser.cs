/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MobileReceiptParser.cs
 * author:    yingzheng
 * created:   2026/5/26
 * descrip:   解析 Unity IAP 5.x order.Info.Receipt JSON，提取 orderId / googleToken
 ***************************************************************/

using System.Collections.Generic;
using Newtonsoft.Json;

namespace NovaFramework.SDK.IAP.Mobile.Runtime
{
    /// <summary>
    /// 解析后的票据信息。
    /// </summary>
    internal sealed class MobileReceiptInfo
    {
        /// <summary>
        /// 商店类型（GooglePlay 或 AppleAppStore）。
        /// </summary>
        [JsonProperty("Store")]
        public string Store { get; set; }

        /// <summary>
        /// 平台交易 ID。
        /// </summary>
        [JsonProperty("TransactionID")]
        public string TransactionID { get; set; }

        /// <summary>
        /// 平台原始 Payload 字符串。
        /// </summary>
        [JsonProperty("Payload")]
        public string Payload { get; set; }

        /// <summary>
        /// 原始 receipt JSON，用于缓存比对，避免重复解析。
        /// </summary>
        public string ReceiptJson;

        /// <summary>
        /// Google Play Payload 解析结果。
        /// </summary>
        public MobileGooglePlayload GooglePayload;

        /// <summary>
        /// 订单号：Google = PayloadJson.OrderId；Apple = TransactionID。
        /// </summary>
        public string OrderId
        {
            get
            {
                if (Store == MobileReceiptParser.GoogleStore)
                {
                    return GooglePayload?.PayloadJson?.OrderId ?? string.Empty;
                }

                if (Store == MobileReceiptParser.AppleStore)
                {
                    return TransactionID ?? string.Empty;
                }

                return string.Empty;
            }
        }

        /// <summary>
        /// Google Play 购买 token：Google = PayloadJson.PurchaseToken；Apple 为空。
        /// </summary>
        public string GoogleToken
        {
            get
            {
                if (Store == MobileReceiptParser.GoogleStore)
                {
                    return GooglePayload?.PayloadJson?.PurchaseToken ?? string.Empty;
                }

                return string.Empty;
            }
        }
    }

    /// <summary>
    /// Google Play Payload 反序列化结构。
    /// </summary>
    internal sealed class MobileGooglePlayload
    {
        /// <summary>
        /// 购买数据 JSON 字符串（inappPurchaseData）。
        /// </summary>
        [JsonProperty("json")]
        public string Json { get; set; }

        /// <summary>
        /// 购买签名字符串（inappDataSignature）。
        /// </summary>
        [JsonProperty("signature")]
        public string Signature { get; set; }

        /// <summary>
        /// 解析 Json 字段后的结构化对象。
        /// </summary>
        public MobileGooglePayloadJson PayloadJson;
    }

    /// <summary>
    /// Google Play Payload 中 json 字段的反序列化结构。
    /// </summary>
    internal sealed class MobileGooglePayloadJson
    {
        /// <summary>
        /// 订单号。
        /// </summary>
        public string OrderId { get; set; }

        /// <summary>
        /// 应用包名。
        /// </summary>
        public string PackageName { get; set; }

        /// <summary>
        /// 商品 SKU ID。
        /// </summary>
        public string ProductId { get; set; }

        /// <summary>
        /// 购买令牌。
        /// </summary>
        public string PurchaseToken { get; set; }
    }

    /// <summary>
    /// 票据解析工具，带缓存避免重复解析相同票据。
    /// </summary>
    internal static class MobileReceiptParser
    {
        /// <summary>
        /// Google Play 商店标识常量。
        /// </summary>
        internal const string GoogleStore = "GooglePlay";

        /// <summary>
        /// Apple App Store 商店标识常量。
        /// </summary>
        internal const string AppleStore = "AppleAppStore";

        /// <summary>
        /// 已解析过的票据缓存，key = productId。
        /// </summary>
        private static readonly Dictionary<string, MobileReceiptInfo> s_Cache = new Dictionary<string, MobileReceiptInfo>();

        /// <summary>
        /// 根据 productId 解析票据 JSON，命中缓存且票据未变化时直接返回缓存结果。
        /// </summary>
        /// <param name="productId">商品 ID，用作缓存键。</param>
        /// <param name="receiptJson">order.Info.Receipt 原始 JSON 字符串。</param>
        /// <returns>解析结果；失败时返回 null。</returns>
        internal static MobileReceiptInfo Parse(string productId, string receiptJson)
        {
            if (string.IsNullOrEmpty(receiptJson))
            {
                return null;
            }

            if (s_Cache.TryGetValue(productId, out MobileReceiptInfo cached) && cached.ReceiptJson == receiptJson)
            {
                return cached;
            }

            MobileReceiptInfo info = ParseInternal(receiptJson);
            if (info != null)
            {
                s_Cache[productId] = info;
            }

            return info;
        }

        /// <summary>
        /// 清除所有缓存（用于 store 销毁时）。
        /// </summary>
        internal static void ClearCache()
        {
            s_Cache.Clear();
        }

        /// <summary>
        /// 实际执行 JSON 解析，不走缓存；解析失败时返回 null。
        /// </summary>
        /// <param name="receiptJson">原始 receipt JSON 字符串。</param>
        /// <returns>解析成功返回 MobileReceiptInfo，失败返回 null。</returns>
        private static MobileReceiptInfo ParseInternal(string receiptJson)
        {
            MobileReceiptInfo info = JsonConvert.DeserializeObject<MobileReceiptInfo>(receiptJson);
            if (info == null)
            {
                return null;
            }

            info.ReceiptJson = receiptJson;
            if (info.Store == GoogleStore && !string.IsNullOrEmpty(info.Payload))
            {
                info.GooglePayload = JsonConvert.DeserializeObject<MobileGooglePlayload>(info.Payload);
                if (info.GooglePayload != null && !string.IsNullOrEmpty(info.GooglePayload.Json))
                {
                    info.GooglePayload.PayloadJson = JsonConvert.DeserializeObject<MobileGooglePayloadJson>(info.GooglePayload.Json);
                }
            }

            return info;
        }
    }
}
