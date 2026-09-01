/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  FirebaseDefaultTopicBuilder.cs
 * author:    Codex
 * created:   2026/8/13
 * descrip:   Firebase 默认推送 Topic 构建器
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using NovaFramework.Runtime;

namespace NovaFramework.SDK.FirebasePlugin.Runtime
{
    /// <summary>
    /// Firebase 默认推送 Topic 构建器。
    /// 只处理纯字符串规则，不直接调用 Firebase SDK 或 Nova 运行时组件，便于单元测试和复用。
    /// </summary>
    internal static class FirebaseDefaultTopicBuilder
    {
        /// <summary>
        /// 所有 Nova 默认 Firebase Topic 的通用前缀根。
        /// </summary>
        private const string c_TopicPrefixRoot = "top_";

        /// <summary>
        /// 广告 SDK 表示国家未知或无效时可能返回的占位值。
        /// </summary>
        private const string c_InvalidCountryCode = "IV";

        /// <summary>
        /// 构建基础默认 Topic 订阅状态。
        /// 包含 all、语言、平台和时区四类 Topic，并对 Topic 做去重和安全字符清洗。
        /// </summary>
        /// <param name="developMode">默认 Topic 使用的开发模式，用于区分 Debug / Release Topic 前缀。</param>
        /// <param name="language">语言标记，通常来自 LanguageMetadata.GetFlag。</param>
        /// <param name="platform">平台标记，例如 iOS 或 Android。</param>
        /// <param name="utcOffset">当前设备的 UTC 偏移。</param>
        /// <returns>基础默认 Topic 订阅状态。</returns>
        public static FirebaseTopicSubscriptionState BuildBaseState(DevelopMode developMode, string language, string platform, TimeSpan utcOffset)
        {
            string normalizedLanguage = SanitizeTopicSegment(language);
            string normalizedPlatform = SanitizeTopicSegment(platform);
            string timezone = FormatUtcOffset(utcOffset);
            List<string> topics = new List<string>
            {
                BuildTopic(developMode, "all"),
            };

            if (!string.IsNullOrEmpty(normalizedLanguage))
            {
                topics.Add(BuildTopic(developMode, "lang_" + normalizedLanguage));
            }

            if (!string.IsNullOrEmpty(normalizedPlatform))
            {
                topics.Add(BuildTopic(developMode, "platform_" + normalizedPlatform));
            }

            topics.Add(BuildTopic(developMode, "timezone_" + timezone));

            return new FirebaseTopicSubscriptionState
            {
                Language = normalizedLanguage,
                Platform = normalizedPlatform,
                Timezone = timezone,
                Topics = Deduplicate(topics),
            };
        }

        /// <summary>
        /// 尝试构建国家默认 Topic 订阅状态。
        /// 空国家码和 IV 占位值会返回 false，不会生成可订阅状态。
        /// </summary>
        /// <param name="developMode">默认 Topic 使用的开发模式，用于区分 Debug / Release Topic 前缀。</param>
        /// <param name="countryCode">广告 SDK 返回的国家或地区代码。</param>
        /// <param name="state">构建出的国家 Topic 状态。</param>
        /// <returns>国家码有效并成功构建返回 true，否则返回 false。</returns>
        public static bool TryBuildCountryState(DevelopMode developMode, string countryCode, out FirebaseCountryTopicSubscriptionState state)
        {
            state = null;
            if (!TryNormalizeCountryCode(countryCode, out string normalizedCountryCode))
            {
                return false;
            }

            state = new FirebaseCountryTopicSubscriptionState
            {
                Country = normalizedCountryCode,
                Topic = BuildTopic(developMode, "country_" + normalizedCountryCode),
            };
            return true;
        }

        /// <summary>
        /// 规范化 Firebase 上报协议使用的国家或地区代码。
        /// 空值和 IV 占位值会统一上报为空字符串。
        /// </summary>
        /// <param name="countryCode">原始国家或地区代码。</param>
        /// <returns>规范化后的国家或地区代码；不可用或无效时返回空字符串。</returns>
        public static string NormalizeReportCountryCode(string countryCode)
        {
            return TryNormalizeCountryCode(countryCode, out string normalizedCountryCode)
                ? normalizedCountryCode
                : string.Empty;
        }

        /// <summary>
        /// 按 Topic 集合计算订阅差异。
        /// 旧集合独有项进入退订列表，新集合独有项进入订阅列表，相同项不重复处理。
        /// </summary>
        /// <param name="oldTopics">上一次成功订阅的 Topic 集合。</param>
        /// <param name="currentTopics">本次应订阅的 Topic 集合。</param>
        /// <returns>退订和订阅差异。</returns>
        public static FirebaseTopicSubscriptionDiff BuildTopicDiff(IEnumerable<string> oldTopics, IEnumerable<string> currentTopics)
        {
            List<string> oldList = NormalizeTopicList(oldTopics);
            List<string> currentList = NormalizeTopicList(currentTopics);
            HashSet<string> oldSet = new HashSet<string>(oldList, StringComparer.Ordinal);
            HashSet<string> currentSet = new HashSet<string>(currentList, StringComparer.Ordinal);
            List<string> unsubscribeTopics = new List<string>();
            List<string> subscribeTopics = new List<string>();

            for (int i = 0; i < oldList.Count; i++)
            {
                string topic = oldList[i];
                if (!currentSet.Contains(topic))
                {
                    unsubscribeTopics.Add(topic);
                }
            }

            for (int i = 0; i < currentList.Count; i++)
            {
                string topic = currentList[i];
                if (!oldSet.Contains(topic))
                {
                    subscribeTopics.Add(topic);
                }
            }

            return new FirebaseTopicSubscriptionDiff(unsubscribeTopics, subscribeTopics);
        }

        /// <summary>
        /// 将 UTC 偏移格式化为 Firebase Topic 安全的时区标记。
        /// utc 固定小写，整点时区省略分钟，非整点时区保留两位分钟。
        /// </summary>
        /// <param name="utcOffset">UTC 偏移。</param>
        /// <returns>时区标记，例如 utc_plus_08 或 utc_minus_03_30。</returns>
        public static string FormatUtcOffset(TimeSpan utcOffset)
        {
            int totalMinutes = (int)Math.Round(utcOffset.TotalMinutes, MidpointRounding.AwayFromZero);
            string sign = totalMinutes < 0 ? "minus" : "plus";
            int absoluteMinutes = Math.Abs(totalMinutes);
            int hours = absoluteMinutes / 60;
            int minutes = absoluteMinutes % 60;

            if (minutes == 0)
            {
                return string.Format(CultureInfo.InvariantCulture, "utc_{0}_{1:00}", sign, hours);
            }

            return string.Format(CultureInfo.InvariantCulture, "utc_{0}_{1:00}_{2:00}", sign, hours, minutes);
        }

        /// <summary>
        /// 将 UTC 偏移格式化为服务端协议使用的可读时区标记。
        /// 该格式包含 Firebase Topic 不支持的 + 和 :，只能用于上报协议，不能用于 Topic 名称。
        /// </summary>
        /// <param name="utcOffset">UTC 偏移。</param>
        /// <returns>时区偏移，例如 +08:00 或 -03:30。</returns>
        public static string FormatReportTimezoneOffset(TimeSpan utcOffset)
        {
            int totalMinutes = (int)Math.Round(utcOffset.TotalMinutes, MidpointRounding.AwayFromZero);
            string sign = totalMinutes < 0 ? "-" : "+";
            int absoluteMinutes = Math.Abs(totalMinutes);
            int hours = absoluteMinutes / 60;
            int minutes = absoluteMinutes % 60;
            return string.Format(CultureInfo.InvariantCulture, "{0}{1:00}:{2:00}", sign, hours, minutes);
        }

        /// <summary>
        /// 尝试规范化国家或地区代码。
        /// 该方法与 AdPlugin 发布规则保持一致：空值和 IV 不视为有效国家码。
        /// </summary>
        /// <param name="countryCode">原始国家或地区代码。</param>
        /// <param name="normalizedCountryCode">规范化后的国家或地区代码。</param>
        /// <returns>国家码有效返回 true，否则返回 false。</returns>
        private static bool TryNormalizeCountryCode(string countryCode, out string normalizedCountryCode)
        {
            normalizedCountryCode = string.IsNullOrWhiteSpace(countryCode)
                ? string.Empty
                : SanitizeTopicSegment(countryCode.Trim().ToUpper(CultureInfo.InvariantCulture));

            return normalizedCountryCode.Length > 0
                   && !string.Equals(normalizedCountryCode, c_InvalidCountryCode, StringComparison.Ordinal);
        }

        /// <summary>
        /// 构建带环境前缀的完整 Firebase Topic。
        /// 输入片段会先按 Firebase Topic 安全字符规则清洗。
        /// </summary>
        /// <param name="developMode">默认 Topic 使用的开发模式。</param>
        /// <param name="topicSegment">不含环境前缀的 Topic 片段。</param>
        /// <returns>完整 Topic。</returns>
        private static string BuildTopic(DevelopMode developMode, string topicSegment)
        {
            return BuildTopicPrefix(developMode) + SanitizeTopicSegment(topicSegment);
        }

        /// <summary>
        /// 按开发模式构建默认 Topic 前缀。
        /// Debug 与未知值统一走调试分群，避免配置未加载时误进入正式分群。
        /// </summary>
        /// <param name="developMode">默认 Topic 使用的开发模式。</param>
        /// <returns>完整 Topic 前缀。</returns>
        private static string BuildTopicPrefix(DevelopMode developMode)
        {
            return developMode == DevelopMode.Release
                ? c_TopicPrefixRoot + "release_"
                : c_TopicPrefixRoot + "debug_";
        }

        /// <summary>
        /// 清洗 Topic 片段。
        /// Firebase Topic 支持字母、数字、连字符、下划线、点、波浪号和百分号；其他字符统一替换为下划线。
        /// </summary>
        /// <param name="segment">待清洗片段。</param>
        /// <returns>清洗后的片段。</returns>
        private static string SanitizeTopicSegment(string segment)
        {
            if (string.IsNullOrWhiteSpace(segment))
            {
                return string.Empty;
            }

            string trimmed = segment.Trim();
            StringBuilder builder = new StringBuilder(trimmed.Length);
            for (int i = 0; i < trimmed.Length; i++)
            {
                char c = trimmed[i];
                builder.Append(IsTopicSafeChar(c) ? c : '_');
            }

            return builder.ToString().Trim('_');
        }

        /// <summary>
        /// 判断字符是否可安全放入 Firebase Topic。
        /// </summary>
        /// <param name="c">待判断字符。</param>
        /// <returns>字符安全返回 true，否则返回 false。</returns>
        private static bool IsTopicSafeChar(char c)
        {
            return (c >= 'a' && c <= 'z')
                   || (c >= 'A' && c <= 'Z')
                   || (c >= '0' && c <= '9')
                   || c == '-'
                   || c == '_'
                   || c == '.'
                   || c == '~'
                   || c == '%';
        }

        /// <summary>
        /// 规范化 Topic 列表，移除空值并保留首次出现顺序。
        /// </summary>
        /// <param name="topics">待规范化 Topic 集合。</param>
        /// <returns>去重后的 Topic 列表。</returns>
        private static List<string> NormalizeTopicList(IEnumerable<string> topics)
        {
            if (topics == null)
            {
                return new List<string>();
            }

            return Deduplicate(topics);
        }

        /// <summary>
        /// 按首次出现顺序对字符串集合去重。
        /// 空字符串和 null 会被跳过。
        /// </summary>
        /// <param name="values">待去重集合。</param>
        /// <returns>去重后的字符串列表。</returns>
        private static List<string> Deduplicate(IEnumerable<string> values)
        {
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            List<string> result = new List<string>();
            foreach (string value in values)
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                string normalizedValue = value.Trim();
                if (seen.Add(normalizedValue))
                {
                    result.Add(normalizedValue);
                }
            }

            return result;
        }
    }
}
