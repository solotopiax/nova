/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  IAPLog.cs
 * author:    yingzheng
 * created:   2026/8/31
 * descrip:   IAP 日志统一网关
 ***************************************************************/

using NovaFramework.Runtime;

namespace NovaFramework.SDK.IAP.Runtime
{
    /// <summary>
    /// IAP 模块日志统一网关。
    /// 支付插件、各 Store 与内部服务都应通过本类或 IAPLogOwner 输出日志，
    /// 便于后续统一加日志开关、采样或脱敏策略。
    /// </summary>
    public static class IAPLog
    {
        /// <summary>
        /// IAP 日志总开关；关闭后 Debug / Warning / Error 均不再输出。
        /// </summary>
        private static bool s_IsEnabled = true;

        /// <summary>
        /// 设置 IAP 日志总开关。
        /// </summary>
        /// <param name="enabled">true 表示允许输出，false 表示屏蔽 IAP 日志。</param>
        public static void SetEnabled(bool enabled)
        {
            s_IsEnabled = enabled;
        }

        /// <summary>
        /// 输出指定标签的 Debug 日志。
        /// </summary>
        /// <param name="tag">Nova 日志标签。</param>
        /// <param name="message">日志内容。</param>
        public static void Debug(string tag, string message)
        {
            if (s_IsEnabled)
            {
                Log.Debug(tag, message);
            }
        }

        /// <summary>
        /// 输出指定标签的 Warning 日志。
        /// </summary>
        /// <param name="tag">Nova 日志标签。</param>
        /// <param name="message">日志内容。</param>
        public static void Warning(string tag, string message)
        {
            if (s_IsEnabled)
            {
                Log.Warning(tag, message);
            }
        }

        /// <summary>
        /// 输出指定标签的 Error 日志。
        /// </summary>
        /// <param name="tag">Nova 日志标签。</param>
        /// <param name="message">日志内容。</param>
        public static void Error(string tag, string message)
        {
            if (s_IsEnabled)
            {
                Log.Error(tag, message);
            }
        }
    }
}
