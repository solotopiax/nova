/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  Log.cs
 * author:    taoye
 * created:   2025/12/1
 * descrip:   日志工具：门面类，提供统一静态入口
 ***************************************************************/

using System;
using System.Diagnostics;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 日志工具。
    /// 提供统一静态入口，按级别分发到 ILogHelper 实现（Debug/Info/Warning/Error/Fatal）。
    /// 各级别方法通过 Conditional 编译开关控制，运行时零开销。
    /// </summary>
    public static partial class Log
    {
        /// <summary>
        /// 日志辅助器实例，由外部通过 SetHelper 注入。
        /// </summary>
        private static ILogHelper s_LogHelper = null;

        /// <summary>
        /// 设置框架日志辅助器。
        /// </summary>
        /// <param name="logHelper">要设置的框架日志辅助器，不能为 null。</param>
        public static void SetHelper(ILogHelper logHelper)
        {
            s_LogHelper = logHelper;
        }
    }
}
