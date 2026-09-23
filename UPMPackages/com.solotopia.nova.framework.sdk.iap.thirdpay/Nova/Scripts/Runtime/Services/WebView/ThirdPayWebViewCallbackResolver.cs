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
        /// 未收到或无法识别支付回调状态。
        /// </summary>
        Unknown = 0,

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
    /// 支付页终态回调的强类型解析结果。
    /// </summary>
    internal readonly struct ThirdPayWebViewCallback
    {
        /// <summary>
        /// 创建支付页终态回调结果。
        /// </summary>
        /// <param name="path">回调路径。</param>
        /// <param name="orderId">客户端订单号；关闭回调为空。</param>
        /// <param name="status">支付状态；关闭回调使用默认值。</param>
        /// <param name="rawStatus">支付页返回的原始状态整数。</param>
        /// <param name="result">支付页结果。</param>
        public ThirdPayWebViewCallback(string path, string orderId, ThirdPayWebViewCallbackStatus status, int rawStatus, ThirdPayOpenResult result)
        {
            Path = path ?? string.Empty;
            OrderId = orderId ?? string.Empty;
            Status = status;
            RawStatus = rawStatus;
            Result = result;
        }

        /// <summary>
        /// 回调路径。
        /// </summary>
        public string Path { get; }

        /// <summary>
        /// 客户端订单号；关闭回调为空。
        /// </summary>
        public string OrderId { get; }

        /// <summary>
        /// 支付状态；关闭回调使用默认值。
        /// </summary>
        public ThirdPayWebViewCallbackStatus Status { get; }

        /// <summary>
        /// 支付页回调中的原始状态整数；未提供时为 0。
        /// </summary>
        public int RawStatus { get; }

        /// <summary>
        /// 支付页结果。
        /// </summary>
        public ThirdPayOpenResult Result { get; }
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
            if (TryResolveCallback(path, args, out ThirdPayWebViewCallback callback))
            {
                result = callback.Result;
                return true;
            }

            result = ThirdPayOpenResult.Failed;
            return false;
        }

        /// <summary>
        /// 尝试解析支付完成或关闭回调，并保留订单号与状态供外部浏览器会话校验。
        /// </summary>
        /// <param name="path">UniWebView 消息路径。</param>
        /// <param name="args">UniWebView 消息参数。</param>
        /// <param name="callback">解析成功后的强类型回调。</param>
        /// <returns>消息属于已知且字段完整的终态回调时返回 true。</returns>
        public static bool TryResolveCallback(string path, IReadOnlyDictionary<string, string> args, out ThirdPayWebViewCallback callback)
        {
            return ResolveCallback(path, args, out callback) == ThirdPayCallbackResolveResult.Valid;
        }

        /// <summary>
        /// 解析支付回调并明确区分非支付 Deep Link、参数错误和未知支付状态。
        /// </summary>
        /// <param name="path">UniWebView 消息路径。</param>
        /// <param name="args">UniWebView 消息参数。</param>
        /// <param name="callback">可读取的支付回调信息；无效参数时可能只保留路径和原始状态。</param>
        /// <returns>支付回调解析分类。</returns>
        public static ThirdPayCallbackResolveResult ResolveCallback(string path, IReadOnlyDictionary<string, string> args, out ThirdPayWebViewCallback callback)
        {
            callback = default;
            if (string.Equals(path, "close_callback", System.StringComparison.Ordinal))
            {
                callback = new ThirdPayWebViewCallback(path, string.Empty, ThirdPayWebViewCallbackStatus.Unknown, 0, ThirdPayOpenResult.Cancel);
                return ThirdPayCallbackResolveResult.Valid;
            }

            if (!string.Equals(path, "pay_callback", System.StringComparison.Ordinal))
            {
                return ThirdPayCallbackResolveResult.NotPaymentCallback;
            }

            if (args == null || !args.TryGetValue("orderid", out string orderId) || string.IsNullOrEmpty(orderId) || !args.TryGetValue("status", out string statusText) || !int.TryParse(statusText, out int status))
            {
                return ThirdPayCallbackResolveResult.InvalidPayload;
            }

            ThirdPayWebViewCallbackStatus callbackStatus = (ThirdPayWebViewCallbackStatus)status;
            switch (callbackStatus)
            {
                case ThirdPayWebViewCallbackStatus.Success:
                case ThirdPayWebViewCallbackStatus.SuccessWithoutChannelSync:
                    callback = new ThirdPayWebViewCallback(path, orderId, callbackStatus, status, ThirdPayOpenResult.Success);
                    return ThirdPayCallbackResolveResult.Valid;
                case ThirdPayWebViewCallbackStatus.Failed:
                    callback = new ThirdPayWebViewCallback(path, orderId, callbackStatus, status, ThirdPayOpenResult.Failed);
                    return ThirdPayCallbackResolveResult.Valid;
                default:
                    callback = new ThirdPayWebViewCallback(path, orderId, ThirdPayWebViewCallbackStatus.Unknown, status, ThirdPayOpenResult.Failed);
                    return ThirdPayCallbackResolveResult.UnknownStatus;
            }
        }
    }
}
