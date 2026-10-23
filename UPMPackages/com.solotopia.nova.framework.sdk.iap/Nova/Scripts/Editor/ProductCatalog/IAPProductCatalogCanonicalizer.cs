/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  IAPProductCatalogCanonicalizer.cs
 * author:    Codex
 * created:   2026/10/19
 * descrip:   IAP 商品数组的受限 JCS 规范化与 SHA-256
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace NovaFramework.SDK.IAP.Editor
{
    /// <summary>
    /// 按 RFC 8785 规则规范化 IAP 商品数组并计算 SHA-256。
    /// </summary>
    internal static class IAPProductCatalogCanonicalizer
    {
        /// <summary>
        /// 把商品数组序列化为字段顺序固定且字符串转义符合 JCS 的 JSON。
        /// </summary>
        /// <param name="items">已经按 table_id 排序的商品数组。</param>
        /// <returns>用于计算目录哈希的规范 JSON。</returns>
        internal static string Canonicalize(IReadOnlyList<IAPProductCatalogItem> items)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));

            var result = new StringBuilder();
            result.Append('[');
            for (int i = 0; i < items.Count; i++)
            {
                IAPProductCatalogItem item = items[i] ?? throw new ArgumentException("items 不能包含 null。", nameof(items));
                if (i > 0) result.Append(',');

                result.Append("{\"currency\":");
                AppendJsonString(result, item.Currency);
                result.Append(",\"name\":");
                AppendJsonString(result, item.Name);
                result.Append(",\"price\":");
                AppendJsonString(result, item.Price);
                result.Append(",\"product_id\":");
                AppendJsonString(result, item.ProductId);
                result.Append(",\"product_type\":");
                AppendJsonString(result, item.ProductType);
                result.Append(",\"sub_group_id\":").Append(item.SubGroupId.ToString(CultureInfo.InvariantCulture));
                result.Append(",\"table_id\":");
                AppendJsonString(result, item.TableId);
                result.Append(",\"third_product_id\":");
                AppendJsonString(result, item.ThirdProductId);
                result.Append('}');
            }
            result.Append(']');
            return result.ToString();
        }

        /// <summary>
        /// 计算规范 JSON 的小写十六进制 SHA-256。
        /// </summary>
        /// <param name="canonicalJson">已经规范化的 UTF-16 JSON 字符串。</param>
        /// <returns>UTF-8 字节序列对应的 SHA-256。</returns>
        internal static string ComputeHash(string canonicalJson)
        {
            if (canonicalJson == null) throw new ArgumentNullException(nameof(canonicalJson));

            byte[] bytes = new UTF8Encoding(false).GetBytes(canonicalJson);
            using SHA256 sha256 = SHA256.Create();
            byte[] hash = sha256.ComputeHash(bytes);
            var result = new StringBuilder(hash.Length * 2);
            for (int i = 0; i < hash.Length; i++)
            {
                result.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
            }
            return result.ToString();
        }

        /// <summary>
        /// 按 JCS 使用的 JSON.stringify 转义规则写入字符串，并拒绝非法代理项。
        /// </summary>
        /// <param name="result">接收 JSON 字符串的缓冲区。</param>
        /// <param name="value">待写入的非空字符串。</param>
        private static void AppendJsonString(StringBuilder result, string value)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            if (value == null) throw new ArgumentNullException(nameof(value));

            result.Append('"');
            for (int i = 0; i < value.Length; i++)
            {
                char character = value[i];
                switch (character)
                {
                    case '"':
                        result.Append("\\\"");
                        break;
                    case '\\':
                        result.Append("\\\\");
                        break;
                    case '\b':
                        result.Append("\\b");
                        break;
                    case '\t':
                        result.Append("\\t");
                        break;
                    case '\n':
                        result.Append("\\n");
                        break;
                    case '\f':
                        result.Append("\\f");
                        break;
                    case '\r':
                        result.Append("\\r");
                        break;
                    default:
                        if (character <= '\u001f')
                        {
                            result.Append("\\u").Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
                            break;
                        }
                        if (char.IsHighSurrogate(character))
                        {
                            if (i + 1 >= value.Length || !char.IsLowSurrogate(value[i + 1]))
                            {
                                throw new ArgumentException("JCS 字符串不能包含未配对的 UTF-16 高代理项。", nameof(value));
                            }
                            result.Append(character).Append(value[++i]);
                            break;
                        }
                        if (char.IsLowSurrogate(character))
                        {
                            throw new ArgumentException("JCS 字符串不能包含未配对的 UTF-16 低代理项。", nameof(value));
                        }
                        result.Append(character);
                        break;
                }
            }
            result.Append('"');
        }
    }
}
