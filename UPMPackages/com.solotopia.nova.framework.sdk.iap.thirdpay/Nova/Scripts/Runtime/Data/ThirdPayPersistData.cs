/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ThirdPayPersistData.cs
 * author:    yingzheng
 * created:   2026/5/26
 * descrip:   第三方支付本地存档统一容器
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NovaFramework.SDK.IAP.Runtime;

namespace NovaFramework.SDK.IAP.ThirdPay.Runtime
{
    /// <summary>
    /// 第三方支付本地存档统一容器。
    /// 通过 IAPStoreBase 统一模板按 classify=iap_thirdpay / item=data_{uid} 单原子读写。
    /// </summary>
    [Serializable]
    internal sealed class ThirdPayPersistData : IIAPStorePersistData
    {
        /// <summary>
        /// 进行中的第三方订单字典：key = 客户端订单号，value = 订单上下文。
        /// 同一个商品可以同时存在多个待处理订单。
        /// </summary>
        public Dictionary<string, ThirdPayOrderRecord> Orders;

        /// <summary>
        /// 当前账号按 TableId + ReceiptParam 保存的首次建单 UTC 秒；重复点击不刷新。
        /// 与订单分开保留，正常验单删单后仍可在剩余窗口内查询服务端。
        /// </summary>
        [JsonConverter(typeof(ThirdPayOrderRecoveryTimesConverter))]
        public Dictionary<string, long> OrderRecoveryCreateTimes;

        /// <summary>
        /// 当前账号最近一次有效的广告国家码，作为 Billing 与 iOS Storefront 均不可用时的 ThirdPay 兜底来源。
        /// </summary>
        public string AdCountryCode;

        /// <summary>
        /// 当前账号最近一次有效的 Google Play Billing 国家码，作为 ThirdPay 优先兜底来源。
        /// </summary>
        public string BillingCountryCode;

        /// <summary>
        /// 当前账号最近一次有效的 iOS StoreKit storefront 国家码，作为 Billing 不可用时的 ThirdPay 兜底来源。
        /// </summary>
        public string IosStorefrontCountryCode;

        /// <summary>
        /// 反序列化后或新建空容器后由 IAPStoreBase 调用一次，确保引用类型字段非 null。
        /// </summary>
        public void EnsureInitialized()
        {
            if (Orders == null)
            {
                Orders = new Dictionary<string, ThirdPayOrderRecord>();
            }

            if (OrderRecoveryCreateTimes == null)
            {
                OrderRecoveryCreateTimes = new Dictionary<string, long>();
            }

            if (AdCountryCode == null)
            {
                AdCountryCode = string.Empty;
            }

            if (BillingCountryCode == null)
            {
                BillingCountryCode = string.Empty;
            }

            if (IosStorefrontCountryCode == null)
            {
                IosStorefrontCountryCode = string.Empty;
            }
        }
    }

    /// <summary>
    /// 单独容错读取查询时间，防止时间字段损坏导致整个账号订单存档被放弃。
    /// </summary>
    [UnityEngine.Scripting.Preserve]
    internal sealed class ThirdPayOrderRecoveryTimesConverter : JsonConverter
    {
        /// <summary>
        /// 写入沿用普通字典格式，保持存档结构稳定。
        /// </summary>
        public override bool CanWrite => false;

        /// <summary>
        /// 仅接管业务键到 UTC 秒的时间映射。
        /// </summary>
        /// <param name="objectType">待转换的字段类型。</param>
        /// <returns>字段为时间映射时返回 true。</returns>
        public override bool CanConvert(Type objectType) => objectType == typeof(Dictionary<string, long>);

        /// <summary>
        /// 读取有效正整数时间，字段形状或条目损坏时忽略，不影响订单字段。
        /// </summary>
        /// <param name="reader">当前 JSON 读取器。</param>
        /// <param name="objectType">时间映射类型。</param>
        /// <param name="existingValue">原有映射，不参与合并。</param>
        /// <param name="serializer">当前序列化器。</param>
        /// <returns>仅包含有效时间的独立映射。</returns>
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var result = new Dictionary<string, long>();
            if (JToken.Load(reader) is JObject times)
            {
                foreach (JProperty entry in times.Properties())
                {
                    if (!string.IsNullOrEmpty(entry.Name) && entry.Value.Type == JTokenType.Integer
                        && long.TryParse(entry.Value.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out long seconds) && seconds > 0)
                    {
                        result[entry.Name] = seconds;
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// 禁止调用自定义写入；CanWrite 为 false 时由默认序列化器写入。
        /// </summary>
        /// <param name="writer">当前写入器。</param>
        /// <param name="value">待写入映射。</param>
        /// <param name="serializer">当前序列化器。</param>
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotSupportedException("查询时间使用默认字典序列化。");
        }
    }
}
