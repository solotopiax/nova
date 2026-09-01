/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  IAPLogOwner.cs
 * author:    yingzheng
 * created:   2026/8/31
 * descrip:   IAP 日志持有者基类
 ***************************************************************/

namespace NovaFramework.SDK.IAP.Runtime
{
    /// <summary>
    /// IAP 日志持有者基类。
    /// Store 与各渠道内部服务继承后可直接调用 LogDebug / LogWarning / LogError，
    /// 无需在调用点重复传入 LogTag。
    /// </summary>
    public abstract class IAPLogOwner
    {
        /// <summary>
        /// 当前日志持有者使用的 Nova 日志标签。
        /// </summary>
        protected abstract string LogTag { get; }

        /// <summary>
        /// 输出 Debug 级别日志。
        /// </summary>
        /// <param name="message">日志内容。</param>
        protected virtual void LogDebug(string message)
        {
            IAPLog.Debug(LogTag, message);
        }

        /// <summary>
        /// 输出 Warning 级别日志。
        /// </summary>
        /// <param name="message">日志内容。</param>
        protected virtual void LogWarning(string message)
        {
            IAPLog.Warning(LogTag, message);
        }

        /// <summary>
        /// 输出 Error 级别日志。
        /// </summary>
        /// <param name="message">日志内容。</param>
        protected virtual void LogError(string message)
        {
            IAPLog.Error(LogTag, message);
        }
    }
}
