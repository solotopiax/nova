/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  Util.Placeholder.cs
 * author:    Codex
 * created:   2026/7/24
 * descrip:   Editor/Runtime 共用的文本占位符解析器
 ***************************************************************/

using System;
using System.Globalization;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 文本占位符解析所需的显式值快照；数据来源由 Editor、Runtime 或导出调用方决定。
    /// </summary>
    public readonly struct PlaceholderContext
    {
        public PlaceholderContext(
            PlatformType platform,
            ChannelType channel,
            string package,
            string version,
            DateTime time)
        {
            Platform = platform;
            Channel = channel;
            Package = package;
            Version = version;
            Time = time;
        }

        public PlatformType Platform { get; }
        public ChannelType Channel { get; }
        public string Package { get; }
        public string Version { get; }
        public DateTime Time { get; }
    }

    public static partial class Util
    {
        /// <summary>
        /// Editor、Runtime 与导出链共用的文本占位符解析入口。
        /// </summary>
        public static class Placeholder
        {
            public const string TimeFormat = "yyyy-MM-dd-HH-mm-ss";

            /// <summary>
            /// 替换标准占位符；未知占位符保持原样，空字符串值替换为空文本。
            /// </summary>
            public static string Resolve(string template, PlaceholderContext context)
            {
                if (template == null) return null;

                return template
                    .Replace("{Platform}", context.Platform.ToString())
                    .Replace("{Channel}", context.Channel.ToString())
                    .Replace("{Package}", context.Package ?? string.Empty)
                    .Replace("{Version}", context.Version ?? string.Empty)
                    .Replace("{Time}", context.Time.ToString(TimeFormat, CultureInfo.InvariantCulture));
            }

            /// <summary>
            /// 从已导出的 ConfigRuntimeSO 构造运行时上下文；包名由具体消费者显式提供。
            /// </summary>
            public static PlaceholderContext FromRuntimeConfig(
                ConfigRuntimeSO config,
                string package,
                string version,
                DateTime time)
            {
                if (config == null) throw new ArgumentNullException(nameof(config));
                return new PlaceholderContext(config.Platform, config.Channel, package, version, time);
            }
        }
    }
}
