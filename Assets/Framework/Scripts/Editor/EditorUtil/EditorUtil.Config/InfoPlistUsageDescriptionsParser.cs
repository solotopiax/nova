/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  InfoPlistUsageDescriptionsParser.cs
 * author:    taoye
 * created:   2026/9/29
 * descrip:   iOS Info.plist 权限用途说明解析与校验
 ***************************************************************/

using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NovaFramework.Editor
{
    /// <summary>
    /// 解析 iOS Info.plist 的 UsageDescription 字符串映射；供配置校验与构建注入共用。
    /// </summary>
    internal static class InfoPlistUsageDescriptionsParser
    {
        private const string c_KeySuffix = "UsageDescription";

        /// <summary>
        /// 解析用途说明 JSON；空配置视为不注入，已声明的键必须有非空用户文案。
        /// </summary>
        /// <param name="json">ConfigWindow 中填写的 JSON。</param>
        /// <param name="descriptions">解析后的 Info.plist 键和值。</param>
        /// <param name="error">校验失败时的人读原因。</param>
        /// <returns>JSON 结构及全部条目有效时为 true。</returns>
        internal static bool TryParse(string json, out Dictionary<string, string> descriptions, out string error)
        {
            descriptions = new Dictionary<string, string>(StringComparer.Ordinal);
            error = null;
            if (string.IsNullOrWhiteSpace(json)) return true;

            try
            {
                if (JToken.Parse(json) is not JObject root)
                {
                    error = "根节点必须是 JSON 对象。";
                    return false;
                }

                foreach (JProperty property in root.Properties())
                {
                    if (!IsUsageDescriptionKey(property.Name))
                    {
                        error = $"{property.Name} 不是有效的 UsageDescription 键。";
                        return false;
                    }

                    if (property.Value.Type != JTokenType.String || string.IsNullOrWhiteSpace(property.Value.Value<string>()))
                    {
                        error = $"{property.Name} 必须填写非空的用户用途说明。";
                        return false;
                    }

                    descriptions.Add(property.Name, property.Value.Value<string>().Trim());
                }

                return true;
            }
            catch (JsonException exception)
            {
                error = $"JSON 格式无效：{exception.Message}";
                return false;
            }
        }

        /// <summary>
        /// 仅接受字母数字前缀加 UsageDescription 后缀的 Info.plist 键，避免把任意配置写入应用权限区。
        /// </summary>
        /// <param name="key">待校验的 plist 键。</param>
        /// <returns>键符合用途说明命名形态时为 true。</returns>
        private static bool IsUsageDescriptionKey(string key)
        {
            if (key == null || key.Length <= c_KeySuffix.Length || !key.EndsWith(c_KeySuffix, StringComparison.Ordinal))
                return false;

            for (int i = 0; i < key.Length - c_KeySuffix.Length; i++)
            {
                char character = key[i];
                bool isAsciiLetterOrDigit = (character >= 'A' && character <= 'Z')
                                            || (character >= 'a' && character <= 'z')
                                            || (character >= '0' && character <= '9');
                if (!isAsciiLetterOrDigit)
                    return false;
            }

            return true;
        }
    }
}
