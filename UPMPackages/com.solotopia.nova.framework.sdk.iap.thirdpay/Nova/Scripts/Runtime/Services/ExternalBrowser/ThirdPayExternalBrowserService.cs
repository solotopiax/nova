/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ThirdPayExternalBrowserService.cs
 * author:    yingzheng
 * created:   2026/8/27
 * descrip:   ThirdPay 外部浏览器打开服务
 ***************************************************************/

using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace NovaFramework.SDK.IAP.ThirdPay.Runtime
{
    /// <summary>
    /// ThirdPay 外部浏览器打开服务契约。
    /// </summary>
    internal interface IThirdPayExternalBrowserService : IDisposable
    {
        /// <summary>
        /// 使用平台外部浏览器打开 URL。
        /// </summary>
        /// <param name="paymentUrl">ThirdPay 支付 URL。</param>
        /// <param name="openUrlFallbackFactory">Android 回退 Application.OpenURL 时使用的支付 URL 构造器。</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>打开请求是否已提交。</returns>
        UniTask<ThirdPayOpenResult> OpenAsync(string paymentUrl, Func<string> openUrlFallbackFactory, CancellationToken ct);

        /// <summary>
        /// 使用平台外部浏览器打开 URL，并返回实际打开方式和失败详情。
        /// </summary>
        /// <param name="paymentUrl">ThirdPay 支付 URL。</param>
        /// <param name="openUrlFallbackFactory">Android 回退 Application.OpenURL 时使用的支付 URL 构造器。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>外部支付页结构化打开结果。</returns>
        UniTask<ThirdPayPaymentPageResult> OpenWithResultAsync(string paymentUrl, Func<string> openUrlFallbackFactory, CancellationToken ct);
    }

    /// <summary>
    /// 基于平台能力的外部支付页打开服务。
    /// </summary>
    internal sealed class ThirdPayExternalBrowserService : ThirdPayLogOwner, IThirdPayExternalBrowserService
    {
        /// <inheritdoc/>
        public UniTask<ThirdPayOpenResult> OpenAsync(string paymentUrl, Func<string> openUrlFallbackFactory, CancellationToken ct)
        {
            return OpenResultAsync(paymentUrl, openUrlFallbackFactory, ct);
        }

        /// <summary>
        /// 使用兼容返回值打开外部支付页。
        /// </summary>
        /// <param name="paymentUrl">ThirdPay 支付 URL。</param>
        /// <param name="openUrlFallbackFactory">Android 回退 Application.OpenURL 时使用的支付 URL 构造器。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>旧版三态支付页结果。</returns>
        private async UniTask<ThirdPayOpenResult> OpenResultAsync(string paymentUrl, Func<string> openUrlFallbackFactory, CancellationToken ct)
        {
            return (await OpenWithResultAsync(paymentUrl, openUrlFallbackFactory, ct)).Result;
        }

        /// <summary>
        /// 使用平台外部浏览器打开 URL，并返回实际打开方式和失败详情。
        /// </summary>
        /// <param name="paymentUrl">ThirdPay 支付 URL。</param>
        /// <param name="openUrlFallbackFactory">Android 回退 Application.OpenURL 时使用的支付 URL 构造器。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>外部支付页结构化打开结果。</returns>
        public UniTask<ThirdPayPaymentPageResult> OpenWithResultAsync(string paymentUrl, Func<string> openUrlFallbackFactory, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (string.IsNullOrEmpty(paymentUrl))
            {
                LogWarning("第三方支付外部支付页打开失败：URL 为空。");
                return UniTask.FromResult(ThirdPayPaymentPageResult.Completed(ThirdPayOpenResult.Failed, ThirdPayExternalBrowserLaunchMode.Failed, ThirdPayWebViewCallbackStatus.Unknown, ThirdPayCloseReason.Unknown, ThirdPayPaymentFailureReason.PaymentPageOpenFailed, 0, "PaymentUrlEmpty", false));
            }

            ThirdPayExternalBrowserLaunchMode launchMode = ThirdPayExternalBrowserLaunchMode.Failed;
            try
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                LogDebug("第三方支付 Android 外部支付页准备通过 Auth Tab / Custom Tabs 打开。");
                launchMode = ThirdPayCustomTabsNativeBridge.OpenUrlPreferAuthTab(paymentUrl);
                if (launchMode == ThirdPayExternalBrowserLaunchMode.Failed)
                {
                    LogWarning("第三方支付 Android Auth Tab / Custom Tabs 打开失败，将回退 Application.OpenURL。");
                    string fallbackPaymentUrl = openUrlFallbackFactory == null ? paymentUrl : openUrlFallbackFactory();
                    if (string.IsNullOrEmpty(fallbackPaymentUrl))
                    {
                        LogWarning("第三方支付 Android Application.OpenURL 兜底打开失败：fallback URL 为空。");
                        return UniTask.FromResult(ThirdPayPaymentPageResult.Completed(ThirdPayOpenResult.Failed, ThirdPayExternalBrowserLaunchMode.Failed, ThirdPayWebViewCallbackStatus.Unknown, ThirdPayCloseReason.Unknown, ThirdPayPaymentFailureReason.SystemBrowserFallbackUrlEmpty, 0, "SystemBrowserFallbackUrlEmpty", false));
                    }

                    Application.OpenURL(fallbackPaymentUrl);
                    LogDebug("第三方支付 Android 已提交 Application.OpenURL 系统浏览器兜底请求，无法确认浏览器展示结果。");
                    return UniTask.FromResult(ThirdPayPaymentPageResult.Opened(ThirdPayExternalBrowserLaunchMode.SystemBrowserFallback));
                }

                LogDebug($"第三方支付 Android 外部支付页已打开：Mode={launchMode}");
#else
                Application.OpenURL(paymentUrl);
                launchMode = ThirdPayExternalBrowserLaunchMode.SystemBrowserFallback;
                LogDebug("第三方支付已提交系统浏览器打开请求，无法确认浏览器展示结果。");
#endif
                return UniTask.FromResult(ThirdPayPaymentPageResult.Opened(launchMode));
            }
            catch (Exception ex)
            {
                LogWarning($"打开外部浏览器失败：{ex.Message}");
                return UniTask.FromResult(ThirdPayPaymentPageResult.Completed(ThirdPayOpenResult.Failed, ThirdPayExternalBrowserLaunchMode.Failed, ThirdPayWebViewCallbackStatus.Unknown, ThirdPayCloseReason.PaymentPageException, ThirdPayPaymentFailureReason.PaymentPageOpenException, 0, ex.Message, false));
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
        }
    }
}
