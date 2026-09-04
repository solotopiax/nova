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
    }

    /// <summary>
    /// 基于平台能力的外部支付页打开服务。
    /// </summary>
    internal sealed class ThirdPayExternalBrowserService : ThirdPayLogOwner, IThirdPayExternalBrowserService
    {
        /// <inheritdoc/>
        public UniTask<ThirdPayOpenResult> OpenAsync(string paymentUrl, Func<string> openUrlFallbackFactory, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (string.IsNullOrEmpty(paymentUrl))
            {
                LogWarning("第三方支付外部支付页打开失败：URL 为空。");
                return UniTask.FromResult(ThirdPayOpenResult.Failed);
            }

            try
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                LogDebug("第三方支付 Android 外部支付页准备通过 Auth Tab / Custom Tabs 打开。");
                ThirdPayExternalBrowserLaunchMode launchMode = ThirdPayCustomTabsNativeBridge.OpenUrlPreferAuthTab(paymentUrl);
                if (launchMode == ThirdPayExternalBrowserLaunchMode.Failed)
                {
                    LogWarning("第三方支付 Android Auth Tab / Custom Tabs 打开失败，将回退 Application.OpenURL。");
                    string fallbackPaymentUrl = openUrlFallbackFactory == null ? paymentUrl : openUrlFallbackFactory();
                    if (string.IsNullOrEmpty(fallbackPaymentUrl))
                    {
                        LogWarning("第三方支付 Android Application.OpenURL 兜底打开失败：fallback URL 为空。");
                        return UniTask.FromResult(ThirdPayOpenResult.Failed);
                    }

                    Application.OpenURL(fallbackPaymentUrl);
                    LogDebug("第三方支付 Android 外部支付页已通过 Application.OpenURL 兜底打开。");
                    return UniTask.FromResult(ThirdPayOpenResult.Success);
                }

                LogDebug($"第三方支付 Android 外部支付页已打开：Mode={launchMode}");
#else
                Application.OpenURL(paymentUrl);
                LogDebug("第三方支付外部浏览器已打开。");
#endif
                return UniTask.FromResult(ThirdPayOpenResult.Success);
            }
            catch (Exception ex)
            {
                LogWarning($"打开外部浏览器失败：{ex.Message}");
                return UniTask.FromResult(ThirdPayOpenResult.Failed);
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
        }
    }
}
