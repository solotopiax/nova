/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ThirdPayGoogleExternalBillingClient.cs
 * author:    yingzheng
 * created:   2026/8/3
 * descrip:   Unity Purchasing Google 外部结算客户端适配器
 ***************************************************************/

using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine.Purchasing.GoogleBilling.Models;
using UnityEngine.Purchasing.Models;

namespace NovaFramework.SDK.IAP.ThirdPay.Runtime
{
    /// <summary>
    /// Unity Purchasing 5.3.1 ExternalBillingProgramClient 的轻量适配器。
    /// </summary>
    internal sealed class ThirdPayGoogleExternalBillingClient : IThirdPayGoogleExternalBillingClient
    {
        /// <summary>
        /// Unity Purchasing 提供的 Google 外部结算客户端。
        /// </summary>
        private readonly ExternalBillingProgramClient m_Client;

        /// <summary>
        /// 连接、资格检查、生成 token 等网络类操作的超时秒数；用户信息页不受此限制。
        /// </summary>
        private readonly double m_TimeoutSeconds;

        /// <summary>
        /// 操作视图共享连接的生命周期所有者；视图不能关闭共享连接。
        /// </summary>
        private readonly ThirdPayGoogleExternalBillingClient m_LifetimeOwner;

        /// <summary>
        /// 当前适配器是否已经释放。
        /// </summary>
        private bool m_Disposed;

        /// <summary>
        /// 使用 Unity Purchasing 默认客户端初始化适配器。
        /// </summary>
        /// <param name="timeoutSeconds">网络类操作超时秒数；非正值回落为 15 秒。</param>
        public ThirdPayGoogleExternalBillingClient(double timeoutSeconds = 15d) : this(new ExternalBillingProgramClient(), timeoutSeconds)
        {
        }

        /// <summary>
        /// 使用指定 Unity Purchasing 客户端初始化适配器。
        /// </summary>
        /// <param name="client">Google 外部结算客户端。</param>
        /// <param name="timeoutSeconds">网络类操作超时秒数；非正值回落为 15 秒。</param>
        internal ThirdPayGoogleExternalBillingClient(ExternalBillingProgramClient client, double timeoutSeconds = 15d)
        {
            m_Client = client ?? throw new ArgumentNullException(nameof(client));
            m_TimeoutSeconds = timeoutSeconds > 0d ? timeoutSeconds : 15d;
        }

        /// <summary>
        /// 创建共享原生连接、仅覆盖本次操作超时的视图。
        /// </summary>
        /// <param name="owner">原生连接所有者。</param>
        /// <param name="timeoutSeconds">已校验的有限正秒数。</param>
        private ThirdPayGoogleExternalBillingClient(ThirdPayGoogleExternalBillingClient owner, double timeoutSeconds)
        {
            m_Client = owner.m_Client;
            m_TimeoutSeconds = timeoutSeconds;
            m_LifetimeOwner = owner;
        }

        /// <summary>
        /// 为本次授权固定网络超时，不修改初始化或其他账号操作的默认超时。
        /// </summary>
        /// <param name="timeoutSeconds">有限正秒数。</param>
        /// <returns>不拥有共享连接的轻量操作视图。</returns>
        internal ThirdPayGoogleExternalBillingClient WithTimeout(double timeoutSeconds)
        {
            ThrowIfDisposed();
            if (double.IsNaN(timeoutSeconds) || double.IsInfinity(timeoutSeconds) || timeoutSeconds <= 0 || timeoutSeconds > int.MaxValue / 1000d)
                throw new ArgumentOutOfRangeException(nameof(timeoutSeconds));
            return new ThirdPayGoogleExternalBillingClient(this, timeoutSeconds);
        }

        /// <summary>
        /// 获取当前客户端连接是否可用。
        /// </summary>
        public bool IsReady => !m_Disposed && !(m_LifetimeOwner?.m_Disposed ?? false) && m_Client.IsReady();

        /// <summary>
        /// 建立 Google 外部结算服务连接。
        /// </summary>
        /// <param name="ct">取消令牌。</param>
        /// <returns>统一的 Google 响应状态。</returns>
        public async UniTask<ThirdPayGoogleResponse> ConnectAsync(CancellationToken ct)
        {
            ThrowIfDisposed();
            ct.ThrowIfCancellationRequested();
            if (m_Client.IsReady())
            {
                return ThirdPayGoogleResponse.Ok;
            }

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(m_TimeoutSeconds));
            var completion = new UniTaskCompletionSource<ThirdPayGoogleResponse>();
            using (timeoutCts.Token.Register(() => completion.TrySetCanceled(timeoutCts.Token)))
            {
                try
                {
                    m_Client.StartConnection(() => completion.TrySetResult(ThirdPayGoogleResponse.Ok), code => completion.TrySetResult(Map(code)));
                }
                catch
                {
                    return ThirdPayGoogleResponse.Failed;
                }

                try
                {
                    return await completion.Task;
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (OperationCanceledException)
                {
                    return ThirdPayGoogleResponse.Timeout;
                }
            }
        }

        /// <summary>
        /// 查询当前用户是否可使用 Google 外部结算计划。
        /// </summary>
        /// <param name="ct">取消令牌。</param>
        /// <returns>统一的 Google 响应状态。</returns>
        public async UniTask<ThirdPayGoogleResponse> CheckAvailabilityAsync(CancellationToken ct)
        {
            ThrowIfDisposed();
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(m_TimeoutSeconds));
            try
            {
                GoogleBillingResponseCode response = await m_Client.IsBillingProgramAvailableAsync().AsUniTask().AttachExternalCancellation(timeoutCts.Token);
                return Map(response);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException)
            {
                return ThirdPayGoogleResponse.Timeout;
            }
            catch
            {
                return ThirdPayGoogleResponse.Failed;
            }
        }

        /// <summary>
        /// 读取 Google Play Billing 当前商店国家或地区代码。
        /// </summary>
        /// <param name="ct">取消令牌。</param>
        /// <returns>ISO 3166-1 alpha-2 代码；读取失败时返回空字符串。</returns>
        public async UniTask<string> GetBillingCountryCodeAsync(CancellationToken ct)
        {
            ThrowIfDisposed();
            ct.ThrowIfCancellationRequested();
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(m_TimeoutSeconds));
            try
            {
                return await ThirdPayGoogleBillingConfigNativeBridge
                    .GetBillingCountryCodeAsync(timeoutCts.Token)
                    .AttachExternalCancellation(timeoutCts.Token);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException)
            {
                return string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// 创建 Google 外部交易上报 token。
        /// </summary>
        /// <param name="ct">取消令牌。</param>
        /// <returns>统一响应状态与外部交易 token。</returns>
        public async UniTask<ThirdPayGoogleTokenResult> CreateTokenAsync(CancellationToken ct)
        {
            ThrowIfDisposed();
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(m_TimeoutSeconds));
            try
            {
                BillingProgramReportingDetails details = await m_Client.CreateBillingProgramReportingDetailsAsync().AsUniTask().AttachExternalCancellation(timeoutCts.Token);
                return new ThirdPayGoogleTokenResult(Map(details.responseCode), details.externalTransactionToken);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException)
            {
                return new ThirdPayGoogleTokenResult(ThirdPayGoogleResponse.Timeout, null);
            }
            catch
            {
                return new ThirdPayGoogleTokenResult(ThirdPayGoogleResponse.Failed, null);
            }
        }

        /// <summary>
        /// 展示 Google 外部链接信息页，由调用方在授权成功后继续打开实际支付页。
        /// </summary>
        /// <param name="url">最终第三方支付 URL。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>统一的 Google 响应状态。</returns>
        public async UniTask<ThirdPayGoogleResponse> LaunchInformationScreenAsync(string url, CancellationToken ct)
        {
            ThrowIfDisposed();
            if (string.IsNullOrEmpty(url))
            {
                throw new ArgumentException("支付 URL 不能为空。", nameof(url));
            }

            try
            {
                GoogleBillingResponseCode response = await m_Client.LaunchExternalLink(url, LinkType.LINK_TO_DIGITAL_CONTENT_OFFER, LaunchMode.CALLER_WILL_LAUNCH_LINK).AsUniTask().AttachExternalCancellation(ct);
                return Map(response);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                return ThirdPayGoogleResponse.Failed;
            }
        }

        /// <summary>
        /// 释放当前适配器；只有连接所有者结束 Google 外部结算连接，操作视图不关闭共享连接。
        /// </summary>
        public void Dispose()
        {
            if (m_Disposed)
            {
                return;
            }

            m_Disposed = true;
            if (m_LifetimeOwner == null)
                m_Client.EndConnection();
        }

        /// <summary>
        /// 在访问已释放客户端时抛出异常。
        /// </summary>
        private void ThrowIfDisposed()
        {
            if (m_Disposed || (m_LifetimeOwner?.m_Disposed ?? false))
            {
                throw new ObjectDisposedException(nameof(ThirdPayGoogleExternalBillingClient));
            }
        }

        /// <summary>
        /// 将 Unity Purchasing 响应码映射为 ThirdPay 内部状态。
        /// </summary>
        /// <param name="response">Unity Purchasing Google 响应码。</param>
        /// <returns>ThirdPay 内部响应状态。</returns>
        private static ThirdPayGoogleResponse Map(GoogleBillingResponseCode response)
        {
            switch (response)
            {
                case GoogleBillingResponseCode.Ok:
                    return ThirdPayGoogleResponse.Ok;
                case GoogleBillingResponseCode.FeatureNotSupported:
                case GoogleBillingResponseCode.BillingUnavailable:
                case GoogleBillingResponseCode.ItemUnavailable:
                    return ThirdPayGoogleResponse.Unavailable;
                case GoogleBillingResponseCode.UserCanceled:
                    return ThirdPayGoogleResponse.UserCancelled;
                default:
                    return ThirdPayGoogleResponse.Failed;
            }
        }
    }
}
