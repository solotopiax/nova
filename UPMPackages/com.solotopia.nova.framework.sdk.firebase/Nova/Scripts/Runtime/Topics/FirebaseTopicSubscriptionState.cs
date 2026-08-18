/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  FirebaseTopicSubscriptionState.cs
 * author:    Codex
 * created:   2026/8/13
 * descrip:   Firebase 默认 Topic 订阅状态 DTO
 ***************************************************************/

using System.Collections.Generic;

namespace NovaFramework.SDK.FirebasePlugin.Runtime
{
    /// <summary>
    /// Firebase 基础默认 Topic 订阅状态。
    /// 持久化记录上一次成功订阅的语言、平台、时区和实际 Topic 字符串。
    /// </summary>
    internal sealed class FirebaseTopicSubscriptionState
    {
        /// <summary>
        /// 上一次成功订阅时使用的语言标记。
        /// 来源为 LanguageMetadata.GetFlag(Nova.Localization.Language)。
        /// </summary>
        public string Language { get; set; } = string.Empty;

        /// <summary>
        /// 上一次成功订阅时使用的平台标记。
        /// 当前仅记录 iOS 或 Android，其他平台为空。
        /// </summary>
        public string Platform { get; set; } = string.Empty;

        /// <summary>
        /// 上一次成功订阅时使用的 UTC 时区偏移标记。
        /// 格式为 utc_plus_08、utc_minus_05 或含分钟的 utc_plus_05_30。
        /// </summary>
        public string Timezone { get; set; } = string.Empty;

        /// <summary>
        /// 上一次成功订阅的完整 Topic 列表。
        /// 存实际 Topic 字符串，确保规则变化后仍能准确退订旧 Topic。
        /// </summary>
        public List<string> Topics { get; set; } = new List<string>();
    }

    /// <summary>
    /// Firebase 国家默认 Topic 订阅状态。
    /// 国家码由广告聚合层发布，可能晚于 Firebase 初始化，因此和基础状态分开持久化。
    /// </summary>
    internal sealed class FirebaseCountryTopicSubscriptionState
    {
        /// <summary>
        /// 上一次成功订阅国家 Topic 时使用的国家或地区代码。
        /// 保存前统一转换为大写，空值和 IV 不会进入该状态。
        /// </summary>
        public string Country { get; set; } = string.Empty;

        /// <summary>
        /// 上一次成功订阅的完整国家 Topic。
        /// 存实际 Topic 字符串，确保规则变化后仍能准确退订旧 Topic。
        /// </summary>
        public string Topic { get; set; } = string.Empty;
    }

    /// <summary>
    /// Firebase Topic 订阅差异。
    /// 调用方先退订 UnsubscribeTopics，再订阅 SubscribeTopics，全部成功后才可保存新状态。
    /// </summary>
    internal sealed class FirebaseTopicSubscriptionDiff
    {
        /// <summary>
        /// 初始化订阅差异。
        /// </summary>
        /// <param name="unsubscribeTopics">需要退订的旧 Topic 列表。</param>
        /// <param name="subscribeTopics">需要订阅的新 Topic 列表。</param>
        public FirebaseTopicSubscriptionDiff(IReadOnlyList<string> unsubscribeTopics, IReadOnlyList<string> subscribeTopics)
        {
            UnsubscribeTopics = unsubscribeTopics ?? new List<string>();
            SubscribeTopics = subscribeTopics ?? new List<string>();
        }

        /// <summary>
        /// 需要退订的旧 Topic 列表。
        /// </summary>
        public IReadOnlyList<string> UnsubscribeTopics { get; }

        /// <summary>
        /// 需要订阅的新 Topic 列表。
        /// </summary>
        public IReadOnlyList<string> SubscribeTopics { get; }

        /// <summary>
        /// 是否没有任何订阅或退订操作。
        /// </summary>
        public bool IsEmpty => UnsubscribeTopics.Count == 0 && SubscribeTopics.Count == 0;
    }
}
