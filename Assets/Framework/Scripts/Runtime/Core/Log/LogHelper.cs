/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  LogHelper.cs
 * author:    taoye
 * created:   2025/12/1
 * descrip:   Unity 平台默认日志实现
 *            提供 ILogger 接口具体实现
 *            支持主线程安全日志打印，可跨线程调用
 *            减少 GC 开销，提升高频日志性能
 ***************************************************************/

using System;
using System.Threading;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// Unity 默认日志实现，提供 ILogHelper 接口具体实现，支持日志分级输出及跨线程安全打印。
    /// </summary>
    internal sealed class LogHelper : ILogHelper
    {
#if !UNITY_WEBGL
        /// <summary>
        /// Unity 主线程同步上下文。
        /// </summary>
        private SynchronizationContext m_SynchronizationContext;

        /// <summary>
        /// Unity 主线程 ID。
        /// </summary>
        private int m_UnityMainThreadId;
#endif

        /// <summary>
        /// Debug 日志颜色。
        /// </summary>
        private const string c_ColorDebug = "#00E7FF";

        /// <summary>
        /// Info 日志颜色。
        /// </summary>
        private const string c_ColorInfo = "#00BF0F";

        /// <summary>
        /// Warning 日志颜色。
        /// </summary>
        private const string c_ColorWarning = "#FDFF00";

        /// <summary>
        /// Error 日志颜色。
        /// </summary>
        private const string c_ColorError = "#FF0000";

        /// <summary>
        /// Fatal 日志颜色。
        /// </summary>
        private const string c_ColorFatal = "#FF00BF";

        /// <summary>
        /// Tag 日志颜色。
        /// </summary>
        private const string c_ColorTag = "#6D6D6D";

        /// <summary>
        /// 初始化日志辅助器，捕获主线程上下文供跨线程调用使用。
        /// </summary>
        public void Initialize()
        {
#if !UNITY_WEBGL
            m_SynchronizationContext = SynchronizationContext.Current;
            m_UnityMainThreadId = Thread.CurrentThread.ManagedThreadId;
#endif
        }

        /// <summary>
        /// 输出日志，自动处理跨线程调用，非主线程消息 Post 到主线程执行。
        /// </summary>
        /// <param name="level">日志等级。</param>
        /// <param name="tag">日志标签。</param>
        /// <param name="message">日志内容。</param>
        public void Print(LogLevel level, string tag, object message)
        {
#if !UNITY_WEBGL
            if (m_SynchronizationContext == null || Thread.CurrentThread.ManagedThreadId == m_UnityMainThreadId)
            {
                PrintInternal(level, tag, message);
            }
            else
            {
                // 跨线程必须 Post 到主线程，此处闭包分配不可避免
                var capturedLevel = level;
                var capturedTag = tag;
                var capturedMessage = message;
                m_SynchronizationContext.Post(_ => PrintInternal(capturedLevel, capturedTag, capturedMessage), null);
            }
#else
            PrintInternal(level, tag, message);
#endif
        }

        /// <summary>
        /// 内部日志输出（直接调用 Unity 日志接口，无闭包分配）。
        /// </summary>
        /// <param name="level">日志等级。</param>
        /// <param name="tag">日志标签。</param>
        /// <param name="message">日志内容。</param>
        private void PrintInternal(LogLevel level, string tag, object message)
        {
            switch (level)
            {
                case LogLevel.Debug:
                    UnityEngine.Debug.Log(FormatMessage("Debug", c_ColorDebug, tag, message));
                    break;

                case LogLevel.Info:
                    UnityEngine.Debug.Log(FormatMessage("Info", c_ColorInfo, tag, message));
                    break;

                case LogLevel.Warning:
                    UnityEngine.Debug.LogWarning(FormatMessage("Warning", c_ColorWarning, tag, message));
                    break;

                case LogLevel.Error:
                    UnityEngine.Debug.LogError(FormatMessage("Error", c_ColorError, tag, message));
                    break;

                case LogLevel.Fatal:
                    UnityEngine.Debug.LogException(new Exception(FormatMessage("Fatal", c_ColorFatal, tag, message)));
                    break;

                default:
                    throw new InvalidOperationException(message.ToString());
            }
        }

        /// <summary>
        /// 格式化日志输出字符串。
        /// </summary>
        /// <param name="level">日志等级名称。</param>
        /// <param name="color">消息颜色。</param>
        /// <param name="tag">模块标签。</param>
        /// <param name="message">日志内容。</param>
        /// <returns>格式化字符串。</returns>
        private string FormatMessage(string level, string color, string tag, object message)
        {
            return Txt.Format("<color={0}>[Nova][{1}]</color><color={2}>{3} {4}</color>", c_ColorTag, level, color, tag, message);
        }
    }
}
