/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ThirdPayWebViewCallbackResolver.cs
 * author:    yingzheng
 * created:   2026/8/3
 * descrip:   第三方支付页回调解析
 ***************************************************************/

using System.Collections.Generic;

namespace NovaFramework.SDK.IAP.ThirdPay.Runtime
{
    /// <summary>
    /// 支付页 pay_callback 返回的支付状态。
    /// </summary>
    internal enum ThirdPayWebViewCallbackStatus
    {
        /// <summary>
        /// 支付成功。
        /// </summary>
        Success = 1,

        /// <summary>
        /// 支付失败。
        /// </summary>
        Failed = 2,

        /// <summary>
        /// 支付成功但渠道信息还未同步成功。
        /// </summary>
        SuccessWithoutChannelSync = 3,
    }

    /// <summary>
    /// 将支付页 Scheme 回调解析为稳定的支付页结果。
    /// </summary>
    internal static class ThirdPayWebViewCallbackResolver
    {
        /// <summary>
        /// 尝试解析支付完成或关闭回调。
        /// </summary>
        /// <param name="path">UniWebView 消息路径。</param>
        /// <param name="args">UniWebView 消息参数。</param>
        /// <param name="result">解析成功后的支付页结果。</param>
        /// <returns>消息属于已知且字段完整的终态回调时返回 true。</returns>
        public static bool TryResolve(string path, IReadOnlyDictionary<string, string> args, out ThirdPayOpenResult result)
        {
            result = ThirdPayOpenResult.Failed;
            if (string.Equals(path, "close_callback", System.StringComparison.Ordinal))
            {
                result = ThirdPayOpenResult.Cancel;
                return true;
            }

            if (!string.Equals(path, "pay_callback", System.StringComparison.Ordinal) || args == null || !args.TryGetValue("orderid", out string orderId) || string.IsNullOrEmpty(orderId) || !args.TryGetValue("status", out string statusText) || !int.TryParse(statusText, out int status))
            {
                return false;
            }

            switch ((ThirdPayWebViewCallbackStatus)status)
            {
                case ThirdPayWebViewCallbackStatus.Success:
                case ThirdPayWebViewCallbackStatus.SuccessWithoutChannelSync:
                    result = ThirdPayOpenResult.Success;
                    return true;
                case ThirdPayWebViewCallbackStatus.Failed:
                    result = ThirdPayOpenResult.Failed;
                    return true;
                default:
                    return false;
            }
        }
    }
}
