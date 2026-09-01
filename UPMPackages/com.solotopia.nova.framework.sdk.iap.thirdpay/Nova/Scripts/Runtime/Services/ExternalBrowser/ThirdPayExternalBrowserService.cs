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
        /// <param name="ct">Cancellation token.</param>
        /// <returns>打开请求是否已提交。</returns>
        UniTask<ThirdPayOpenResult> OpenAsync(string paymentUrl, CancellationToken ct);
    }

    /// <summary>
    /// 基于 Unity Application.OpenURL 的默认外部浏览器打开服务。
    /// </summary>
    internal sealed class ThirdPayExternalBrowserService : ThirdPayLogOwner, IThirdPayExternalBrowserService
    {
        /// <inheritdoc/>
        public UniTask<ThirdPayOpenResult> OpenAsync(string paymentUrl, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (string.IsNullOrEmpty(paymentUrl))
            {
                return UniTask.FromResult(ThirdPayOpenResult.Failed);
            }

            try
            {
                Application.OpenURL(paymentUrl);
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
