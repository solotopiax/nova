/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  PrivacyInfoConfigParser.cs
 * author:    taoye
 * created:   2026/9/28
 * descrip:   iOS Required Reason API 配置解析与校验
 ***************************************************************/

using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NovaFramework.Editor
{
    /// <summary>
    /// 解析 iOS 隐私清单的 API 类别与理由码；供配置导出校验和构建注入共用。
    /// </summary>
    internal static class PrivacyInfoConfigParser
    {
        /// <summary>
        /// 解析类别到理由码数组的 JSON 映射；空配置视为不声明。
        /// </summary>
        /// <param name="json">ConfigWindow 中填写的 JSON。</param>
        /// <param name="reasonsByCategory">解析后的非空类别映射。</param>
        /// <param name="error">校验失败时的人读原因。</param>
        /// <returns>JSON 结构及所有字段有效时为 true。</returns>
        internal static bool TryParse(
            string json,
            out Dictionary<string, List<string>> reasonsByCategory,
            out string error)
        {
            reasonsByCategory = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            error = null;
            if (string.IsNullOrWhiteSpace(json)) return true;

            try
            {
                JToken token = JToken.Parse(json);
                if (token is not JObject root)
                {
                    error = "根节点必须是 JSON 对象。";
                    return false;
                }

                foreach (JProperty property in root.Properties())
                {
                    if (!property.Name.StartsWith("NSPrivacyAccessedAPICategory", StringComparison.Ordinal))
                    {
                        error = $"{property.Name} 不是 Required Reason API 类别。";
                        return false;
                    }

                    if (property.Value is not JArray reasonArray)
                    {
                        error = $"{property.Name} 的值必须是理由码字符串数组。";
                        return false;
                    }

                    List<string> reasons = new List<string>();
                    foreach (JToken reasonToken in reasonArray)
                    {
                        if (reasonToken.Type != JTokenType.String || string.IsNullOrWhiteSpace(reasonToken.Value<string>()))
                        {
                            error = $"{property.Name} 中存在空值或非字符串理由码。";
                            return false;
                        }

                        string reason = reasonToken.Value<string>().Trim();
                        if (!reasons.Contains(reason)) reasons.Add(reason);
                    }

                    if (reasons.Count > 0) reasonsByCategory.Add(property.Name, reasons);
                }

                return true;
            }
            catch (JsonException exception)
            {
                error = $"JSON 格式无效：{exception.Message}";
                return false;
            }
        }
    }
}
