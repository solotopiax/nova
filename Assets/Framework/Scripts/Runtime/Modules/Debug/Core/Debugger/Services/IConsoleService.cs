/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  IConsoleService.cs
 * author:    yingzheng
 * created:   2026/6/12
 * descrip:   Nova Runtime Debugger 运行时代码。
 ***************************************************************/
using System;
using System.Collections.Generic;
using System.Text;

namespace NovaFramework.Runtime
{
    using UnityEngine;

    /// <summary>
    /// Nova 框架日志级别，与 LogHelper.PrintInternal 中的枚举对应。
    /// </summary>
    public enum NovaLogLevel
    {
        /// <summary>
        /// 调试级别，开发阶段详细流程日志。
        /// </summary>
        Debug = 0,
        /// <summary>
        /// 信息级别，常规运行时信息。
        /// </summary>
        Info = 1,
        /// <summary>
        /// 警告级别，可恢复异常或降级情况。
        /// </summary>
        Warning = 2,
        /// <summary>
        /// 错误级别，不可恢复错误。
        /// </summary>
        Error = 3,
        /// <summary>
        /// 致命级别，通过 Debug.LogException 发出。
        /// </summary>
        Fatal = 4
    }

    public delegate void ConsoleUpdatedEventHandler(IConsoleService console);

    public interface IConsoleService
    {
        /// <summary>
        /// Error / Assert / Exception 级别日志的计数（不含 Fatal）。
        /// </summary>
        int ErrorCount { get; }
        /// <summary>
        /// Warning 级别日志的计数。
        /// </summary>
        int WarningCount { get; }
        /// <summary>
        /// Info 级别日志的计数（含未携带 Nova 前缀的普通 Log）。
        /// </summary>
        int InfoCount { get; }
        /// <summary>
        /// Nova Debug 级别日志的计数。
        /// </summary>
        int DebugCount { get; }
        /// <summary>
        /// Nova Fatal 级别日志的计数。
        /// </summary>
        int FatalCount { get; }

        /// <summary>
        /// List of ConsoleEntry objects since the last clear.
        /// </summary>
        IReadOnlyList<ConsoleEntry> Entries { get; }

        /// <summary>
        /// List of all ConsoleEntry objects, regardless of clear.
        /// </summary>
        IReadOnlyList<ConsoleEntry> AllEntries { get; }

        event ConsoleUpdatedEventHandler Updated;

        event ConsoleUpdatedEventHandler Error;

        bool LoggingEnabled { get; set; }

        bool LogHandlerIsOverriden { get; }

        void Clear();
    }

    public class ConsoleEntry
    {
        private const int MessagePreviewLength = 180;
        private const int StackTracePreviewLength = 120;
        private string _messagePreview;
        private string _stackTracePreview;
        private int _messagePreviewLength = -1;
        private int _stackTracePreviewLength = -1;

        /// <summary>
        /// Number of times this log entry has occured (if collapsing is enabled)
        /// </summary>
        public int Count = 1;

        public LogType LogType;
        public string Message;
        public string StackTrace;
        /// <summary>
        /// Nova 自定义日志级别（仅 Nova 格式日志有值，非 Nova 日志为 null）。
        /// </summary>
        public NovaLogLevel? NovaLogLevel;
        /// <summary>
        /// 日志到达时间，用于 UI 前缀显示。
        /// </summary>
        public DateTime Timestamp;
        public ConsoleEntry() {}

        public ConsoleEntry(ConsoleEntry other)
        {
            Message = other.Message;
            StackTrace = other.StackTrace;
            LogType = other.LogType;
            Count = other.Count;
            NovaLogLevel = other.NovaLogLevel;
            Timestamp = other.Timestamp;
        }

        public string MessagePreview
        {
            get
            {
                return GetMessagePreview(MessagePreviewLength);
            }
        }

        public string StackTracePreview
        {
            get
            {
                return GetStackTracePreview(StackTracePreviewLength);
            }
        }

        public string GetMessagePreview(int maxVisibleLength)
        {
            if (_messagePreview != null && _messagePreviewLength == maxVisibleLength)
            {
                return _messagePreview;
            }
            if (string.IsNullOrEmpty(Message))
            {
                return "";
            }

            _messagePreview = CreateRichTextPreview(Message, maxVisibleLength);
            _messagePreviewLength = maxVisibleLength;
            return _messagePreview;
        }

        public string GetStackTracePreview(int maxVisibleLength)
        {
            if (_stackTracePreview != null && _stackTracePreviewLength == maxVisibleLength)
            {
                return _stackTracePreview;
            }
            if (string.IsNullOrEmpty(StackTrace))
            {
                return "";
            }

            _stackTracePreview = CreateRichTextPreview(StackTrace, maxVisibleLength);
            _stackTracePreviewLength = maxVisibleLength;
            return _stackTracePreview;
        }

        private static string CreateRichTextPreview(string value, int maxLength)
        {
            maxLength = Mathf.Max(1, maxLength);
            var firstLine = value.Split('\n')[0].TrimEnd('\r');
            var hasMoreLines = value.IndexOf('\n') >= 0;
            var visibleLength = GetRichTextVisibleLength(firstLine);
            if (visibleLength <= maxLength && !hasMoreLines && HasBalancedRichTextTags(firstLine))
            {
                return firstLine;
            }

            return TruncateRichText(firstLine, Math.Min(visibleLength, maxLength), true);
        }

        private static int GetRichTextVisibleLength(string value)
        {
            var length = 0;
            for (var i = 0; i < value.Length;)
            {
                if (TryReadRichTextTag(value, i, out _, out _, out var nextIndex))
                {
                    i = nextIndex;
                    continue;
                }

                length++;
                i++;
            }

            return length;
        }

        private static string TruncateRichText(string value, int maxLength, bool appendEllipsis)
        {
            var builder = new StringBuilder(value.Length);
            var closingTags = new List<string>();
            var visibleLength = 0;

            for (var i = 0; i < value.Length;)
            {
                if (TryReadRichTextTag(value, i, out var tag, out var tagName, out var nextIndex))
                {
                    builder.Append(tag);
                    TrackRichTextTag(tag, tagName, closingTags);
                    i = nextIndex;
                    continue;
                }

                if (visibleLength >= maxLength)
                {
                    break;
                }

                builder.Append(value[i]);
                visibleLength++;
                i++;
            }

            if (appendEllipsis)
            {
                builder.Append("...");
            }

            for (var i = closingTags.Count - 1; i >= 0; i--)
            {
                builder.Append(closingTags[i]);
            }

            return builder.ToString();
        }

        private static bool HasBalancedRichTextTags(string value)
        {
            var closingTags = new List<string>();
            for (var i = 0; i < value.Length;)
            {
                if (TryReadRichTextTag(value, i, out var tag, out var tagName, out var nextIndex))
                {
                    TrackRichTextTag(tag, tagName, closingTags);
                    i = nextIndex;
                    continue;
                }

                i++;
            }

            return closingTags.Count == 0;
        }

        private static bool TryReadRichTextTag(string value, int index, out string tag, out string tagName, out int nextIndex)
        {
            tag = null;
            tagName = null;
            nextIndex = index;

            if (value[index] != '<')
            {
                return false;
            }

            var endIndex = value.IndexOf('>', index);
            if (endIndex < 0)
            {
                return false;
            }

            var contentStart = index + 1;
            var contentLength = endIndex - contentStart;
            if (contentLength <= 0)
            {
                return false;
            }

            var content = value.Substring(contentStart, contentLength).Trim();
            var nameStart = content.StartsWith("/", StringComparison.Ordinal) ? 1 : 0;
            var nameEnd = nameStart;
            while (nameEnd < content.Length && content[nameEnd] != '=' && !char.IsWhiteSpace(content[nameEnd]))
            {
                nameEnd++;
            }

            if (nameEnd <= nameStart)
            {
                return false;
            }

            tagName = content.Substring(nameStart, nameEnd - nameStart);
            if (!IsSupportedRichTextTag(tagName))
            {
                return false;
            }

            tag = value.Substring(index, endIndex - index + 1);
            nextIndex = endIndex + 1;
            return true;
        }

        private static void TrackRichTextTag(string tag, string tagName, List<string> closingTags)
        {
            if (tag.StartsWith("</", StringComparison.Ordinal))
            {
                var closingTag = "</" + tagName + ">";
                for (var i = closingTags.Count - 1; i >= 0; i--)
                {
                    if (string.Equals(closingTags[i], closingTag, StringComparison.OrdinalIgnoreCase))
                    {
                        closingTags.RemoveAt(i);
                        break;
                    }
                }

                return;
            }

            if (tag.StartsWith("<quad", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            closingTags.Add("</" + tagName + ">");
        }

        private static bool IsSupportedRichTextTag(string tagName)
        {
            return string.Equals(tagName, "b", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(tagName, "i", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(tagName, "size", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(tagName, "color", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(tagName, "material", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(tagName, "quad", StringComparison.OrdinalIgnoreCase);
        }

        public bool Matches(ConsoleEntry other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }
            if (ReferenceEquals(this, other))
            {
                return true;
            }
            return string.Equals(Message, other.Message) && string.Equals(StackTrace, other.StackTrace) &&
                   LogType == other.LogType && NovaLogLevel == other.NovaLogLevel;
        }
    }
}
