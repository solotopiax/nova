/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ConsoleFilterStateService.cs
 * author:    yingzheng
 * created:   2026/6/12
 * descrip:   Nova Runtime Debugger 运行时代码。
 ***************************************************************/
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NovaFramework.Runtime
{
    [Service(typeof(IConsoleFilterState))]
    public sealed class ConsoleFilterStateService : IConsoleFilterState
    {
        public event ConsoleStateChangedEventHandler FilterStateChange;
        public event ConsoleTextFilterChangedEventHandler TextFilterChange;
        /// <summary>
        /// Nova 专用级别（Debug / Fatal）过滤状态变更事件。
        /// </summary>
        public event NovaConsoleStateChangedEventHandler NovaFilterStateChange;

        private readonly bool[] _states;
        private string _textFilter = "";
        private bool m_DebugEnabled = true;
        private bool m_FatalEnabled = true;
        /// <summary>
        /// Tag 过滤标签集合，使用有序大小写敏感比较。
        /// </summary>
        private readonly HashSet<string> m_TagFilter = new HashSet<string>(StringComparer.Ordinal);
        /// <summary>
        /// Tag 过滤集合变更事件。
        /// </summary>
        public event ConsoleTagFilterChangedEventHandler TagFilterChange;

        public ConsoleFilterStateService()
        {
            _states = new bool[Enum.GetValues(typeof(LogType)).Length];
            for (var i = 0; i < _states.Length; i++)
            {
                _states[i] = true;
            }
        }

        public void SetConsoleFilterState(LogType type, bool newState)
        {
            type = GetType(type);
            if (_states[(int)type] == newState)
            {
                return;
            }

            _states[(int)type] = newState;
            FilterStateChange?.Invoke(type, newState);
        }

        public bool GetConsoleFilterState(LogType type)
        {
            type = GetType(type);
            return _states[(int)type];
        }

        /// <summary>
        /// 设置 Nova 日志级别的过滤开关。Info/Warning/Error 桥接到 SetConsoleFilterState；Debug/Fatal 使用独立开关。
        /// </summary>
        /// <param name="level">Nova 日志级别。</param>
        /// <param name="enabled">true 表示显示，false 表示隐藏。</param>
        public void SetNovaFilterState(NovaLogLevel level, bool enabled)
        {
            switch (level)
            {
                case NovaLogLevel.Debug:
                    if (m_DebugEnabled == enabled)
                        return;
                    m_DebugEnabled = enabled;
                    NovaFilterStateChange?.Invoke(NovaLogLevel.Debug, enabled);
                    break;
                case NovaLogLevel.Fatal:
                    if (m_FatalEnabled == enabled)
                        return;
                    m_FatalEnabled = enabled;
                    NovaFilterStateChange?.Invoke(NovaLogLevel.Fatal, enabled);
                    break;
                case NovaLogLevel.Info:
                    SetConsoleFilterState(LogType.Log, enabled);
                    break;
                case NovaLogLevel.Warning:
                    SetConsoleFilterState(LogType.Warning, enabled);
                    break;
                case NovaLogLevel.Error:
                    SetConsoleFilterState(LogType.Error, enabled);
                    break;
            }
        }

        /// <summary>
        /// 获取 Nova 日志级别的过滤开关。Info/Warning/Error 桥接到 GetConsoleFilterState；Debug/Fatal 使用独立开关。
        /// </summary>
        /// <param name="level">Nova 日志级别。</param>
        /// <returns>true 表示当前显示，false 表示当前隐藏。</returns>
        public bool GetNovaFilterState(NovaLogLevel level)
        {
            switch (level)
            {
                case NovaLogLevel.Debug:
                    return m_DebugEnabled;
                case NovaLogLevel.Fatal:
                    return m_FatalEnabled;
                case NovaLogLevel.Info:
                    return GetConsoleFilterState(LogType.Log);
                case NovaLogLevel.Warning:
                    return GetConsoleFilterState(LogType.Warning);
                case NovaLogLevel.Error:
                    return GetConsoleFilterState(LogType.Error);
                default:
                    return true;
            }
        }

        /// <summary>
        /// 当前选中的 Tag 集合只读视图。
        /// </summary>
        public IReadOnlyCollection<string> TagFilter => m_TagFilter;

        /// <summary>
        /// 设置 Tag 过滤集合，传入空集合或 null 清除过滤。
        /// </summary>
        /// <param name="tags">要过滤的标签集合。</param>
        public void SetTagFilter(IEnumerable<string> tags)
        {
            m_TagFilter.Clear();
            if (tags != null)
            {
                foreach (var tag in tags)
                {
                    if (!string.IsNullOrEmpty(tag))
                        m_TagFilter.Add(tag);
                }
            }
            TagFilterChange?.Invoke(m_TagFilter);
        }

        public string TextFilter
        {
            get => _textFilter;
            set {
                if (value != _textFilter)
                {
                    _textFilter = value;
                    TextFilterChange?.Invoke(value);
                }
            }
        }

        private static LogType GetType(LogType type)
        {
            switch (type)
            {
                case LogType.Error:
                case LogType.Assert:
                case LogType.Exception:
                    return LogType.Error;
                default:
                    return type;
            }
        }
    }
}
