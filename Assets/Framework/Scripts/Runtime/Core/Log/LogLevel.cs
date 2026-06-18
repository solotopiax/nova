/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  LogLevel.cs
 * author:    taoye
 * created:   2025/12/1
 * descrip:   日志等级枚举，用于标识日志的严重性
 ***************************************************************/

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 日志等级枚举。
    /// 用于标识日志信息的严重程度，从调试信息到致命错误。
    /// </summary>
    public enum LogLevel : byte
    {
        /// <summary>
        /// 调试信息，通常用于开发阶段排查问题。
        /// </summary>
        Debug = 0,

        /// <summary>
        /// 普通信息，用于记录程序运行状态或重要事件。
        /// </summary>
        Info,

        /// <summary>
        /// 警告信息，表示可能存在潜在问题，需要关注。
        /// </summary>
        Warning,

        /// <summary>
        /// 错误信息，表示程序发生错误，但不影响整个系统运行。
        /// </summary>
        Error,

        /// <summary>
        /// 致命错误，表示程序发生严重异常，可能导致程序崩溃或数据丢失。
        /// </summary>
        Fatal
    }   
}
