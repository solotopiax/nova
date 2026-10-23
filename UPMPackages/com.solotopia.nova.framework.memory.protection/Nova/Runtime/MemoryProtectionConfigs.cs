/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MemoryProtectionConfigs.cs
 * author:    taoye
 * created:   2026/10/21
 * descrip:   独立内存防护包的隐私配置数据
 ***************************************************************/

using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NovaFramework.Runtime
{
    /// <summary>由内存防护包拥有的配置，通过主框架通用隐私扩展数据保存。</summary>
    [Serializable]
    public sealed class MemoryProtectionConfigs
    {
        /// <summary>隐私扩展 JSON 中本包的唯一键。</summary>
        public const string ExtensionKey = "com.solotopia.nova.framework.memory.protection";
        /// <summary>是否启用内存篡改检测；缺省开启，无可用后端时使用普通数值。</summary>
        public bool EnableMemoryTamperDetection = true;
        /// <summary>是否定期轮换内存密钥；仅实际启用防护时生效。</summary>
        public bool EnableMemoryKeyRotation = true;

        /// <summary>读取本包配置；未配置使用默认值，损坏配置使用普通数值兼容并记录脱敏提示。</summary>
        public static MemoryProtectionConfigs FromPrivacy(PrivacyConfigs privacy)
        {
            if (TryRead(privacy, out var config)) return config;
            Log.Warning(LogTag.Config, "内存防护配置格式无效，当前使用普通数值兼容模式；请检查隐私配置。");
            return new MemoryProtectionConfigs { EnableMemoryTamperDetection = false, EnableMemoryKeyRotation = false };
        }

        /// <summary>仅解析本包扩展，不修改主配置；格式错误返回 false，缺省字段仍为开启。</summary>
        public static bool TryRead(PrivacyConfigs privacy, out MemoryProtectionConfigs config)
        {
            config = new MemoryProtectionConfigs();
            string json = privacy?.ExtensionConfigsJson;
            if (string.IsNullOrWhiteSpace(json)) return true;
            try
            {
                var root = JObject.Parse(json);
                JToken value = root[ExtensionKey];
                if (value == null) return true;
                if (!(value is JObject entry)) return false;
                JToken detection = entry[nameof(EnableMemoryTamperDetection)];
                JToken rotation = entry[nameof(EnableMemoryKeyRotation)];
                if (detection != null && detection.Type != JTokenType.Boolean) return false;
                if (rotation != null && rotation.Type != JTokenType.Boolean) return false;
                if (detection != null) config.EnableMemoryTamperDetection = detection.Value<bool>();
                if (rotation != null) config.EnableMemoryKeyRotation = rotation.Value<bool>();
                return true;
            }
            catch (JsonException)
            {
                return false;
            }
        }

        /// <summary>合并本包配置并保留其他扩展；原 JSON 损坏时抛错，防止覆盖其他包的数据。</summary>
        public string WriteToPrivacyJson(string currentJson)
        {
            var root = string.IsNullOrWhiteSpace(currentJson) ? new JObject() : JObject.Parse(currentJson);
            var entry = root[ExtensionKey] as JObject ?? new JObject();
            entry[nameof(EnableMemoryTamperDetection)] = EnableMemoryTamperDetection;
            entry[nameof(EnableMemoryKeyRotation)] = EnableMemoryKeyRotation;
            root[ExtensionKey] = entry;
            return root.ToString(Formatting.None);
        }
    }
}
