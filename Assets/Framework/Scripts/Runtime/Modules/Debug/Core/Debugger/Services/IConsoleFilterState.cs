/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  IConsoleFilterState.cs
 * author:    yingzheng
 * created:   2026/6/12
 * descrip:   Nova Runtime Debugger 运行时代码。
 ***************************************************************/
using UnityEngine;
using System.Collections.Generic;

namespace NovaFramework.Runtime
{
    public delegate void ConsoleStateChangedEventHandler(LogType logType, bool newState);
    public delegate void ConsoleTextFilterChangedEventHandler(string newFilter);
    /// <summary>
    /// Tag 过滤集合变更委托。
    /// </summary>
    /// <param name="tags">变更后当前生效的 Tag 过滤集合。</param>
    public delegate void ConsoleTagFilterChangedEventHandler(IReadOnlyCollection<string> tags);
    /// <summary>
    /// Nova 专用日志级别过滤状态变更委托。
    /// </summary>
    public delegate void NovaConsoleStateChangedEventHandler(NovaLogLevel level, bool newState);

    public interface IConsoleFilterState
    {
        event ConsoleStateChangedEventHandler FilterStateChange;
        event ConsoleTextFilterChangedEventHandler TextFilterChange;
        /// <summary>
        /// Nova 专用级别（Debug / Fatal）过滤状态变更事件。
        /// </summary>
        event NovaConsoleStateChangedEventHandler NovaFilterStateChange;

        /// <summary>
        /// Set whether log messages with <paramref name="logType"/> severity
        /// should be displayed in the RuntimeDebugger console.
        /// </summary>
        /// <param name="logType">Type of message (only Error/Warning/Log are used. <see cref="LogType.Exception"/> and <see cref="LogType.Assert"/> will redirect to <see cref="LogType.Error"/></param>
        /// <param name="enabled">True to display the log type, false to hide.</param>
        void SetConsoleFilterState(LogType logType, bool enabled);

        /// <summary>
        /// Get whether log messages with <paramref name="logType"/> severity are
        /// being displayed in the RuntimeDebugger console.
        /// </summary>
        /// <param name="logType">Type of message (only Error/Warning/Log are used. <see cref="LogType.Exception"/> and <see cref="LogType.Assert"/> will redirect to <see cref="LogType.Error"/></param>
        /// <returns>True if the log type is displayed.</returns>
        bool GetConsoleFilterState(LogType logType);

        /// <summary>
        /// 设置 Nova 日志级别的过滤开关。支持全部五个级别：Info/Warning/Error 桥接到 SetConsoleFilterState；Debug/Fatal 使用独立开关。
        /// </summary>
        /// <param name="level">Nova 日志级别。</param>
        /// <param name="enabled">true 表示显示，false 表示隐藏。</param>
        void SetNovaFilterState(NovaLogLevel level, bool enabled);

        /// <summary>
        /// 获取 Nova 日志级别的过滤开关。支持全部五个级别：Info/Warning/Error 桥接到 GetConsoleFilterState；Debug/Fatal 使用独立开关。
        /// </summary>
        /// <param name="level">Nova 日志级别。</param>
        /// <returns>true 表示当前显示，false 表示当前隐藏。</returns>
        bool GetNovaFilterState(NovaLogLevel level);

        /// <summary>
        /// What text is the console log being filtered by.
        /// </summary>
        string TextFilter { get; set; }
        /// <summary>
        /// Tag 过滤变更事件，TagFilter 集合发生变化时触发。
        /// </summary>
        event ConsoleTagFilterChangedEventHandler TagFilterChange;
        /// <summary>
        /// 当前选中的 Tag 集合，空集合表示不过滤。
        /// </summary>
        IReadOnlyCollection<string> TagFilter { get; }
        /// <summary>
        /// 设置 Tag 过滤集合，传入空集合或 null 表示清除过滤。
        /// </summary>
        /// <param name="tags">要过滤的标签集合（如 "[SDK]"、"[Network][Http]"）。</param>
        void SetTagFilter(IEnumerable<string> tags);
    }
}
