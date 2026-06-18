/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ThirdPayGoogleExpand.cs
 * author:    yingzheng
 * created:   2026/5/26
 * descrip:   Google Play Billing 外部链接合规扩展（仅 Android + InAppAuto 启用）
 ***************************************************************/

using System;
using Cysharp.Threading.Tasks;
using NovaFramework.Runtime;

namespace NovaFramework.SDK.IAP.ThirdPay.Runtime.Internal
{
    /// <summary>
    /// Google Play Billing 外部链接合规扩展。
    /// 仅 Android + InAppAuto 模式下使用：
    /// 启动期 PrefetchAsync 预取一份 ExternalOfferToken，支付前 ConsumeTokenAsync 取走并立即预取下一份；
    /// 支付成功后 ReportAsync 上报合规交易；上报失败仅 Warning 不阻塞发奖。
    /// Token 不落盘——重启后失效，由启动期重新预取。
    /// </summary>
    internal sealed class ThirdPayGoogleExpand
    {
        /// <summary>
        /// 预缓存的 token；ConsumeTokenAsync 取走后置 null，PrefetchAsync 写入。
        /// </summary>
        private string m_CachedToken;

        /// <summary>
        /// 是否正在拉取 token，防止并发重复请求。
        /// </summary>
        private bool m_IsFetching;

        /// <summary>
        /// 取一份 token：优先返回预缓存值，没有就实时拉一份；返回后异步预取下一份。
        /// </summary>
        /// <returns>ExternalOfferToken；拉取失败时返回 null。</returns>
        public async UniTask<string> ConsumeTokenAsync()
        {
            if (string.IsNullOrEmpty(m_CachedToken))
                await FetchTokenAsync();

            string token = m_CachedToken;
            m_CachedToken = null;
            PrefetchAsync().Forget();
            return token;
        }

        /// <summary>
        /// 启动期 / 支付成功后调用，提前缓存一份 token。
        /// </summary>
        /// <returns>预取完成的异步任务。</returns>
        public UniTask PrefetchAsync() => FetchTokenAsync();

        /// <summary>
        /// 支付成功后向 Google 合规上报。
        /// 失败仅 Warning，不抛异常，不阻塞发奖。
        /// </summary>
        /// <param name="orderId">本地订单 ID。</param>
        /// <param name="token">ConsumeTokenAsync 返回的 token，调用方须自行保留。</param>
        /// <returns>上报完成的异步任务。</returns>
        public async UniTask ReportAsync(string orderId, string token)
        {
            if (string.IsNullOrEmpty(token))
                return;

            try
            {
                await GoogleBillingProxy.ReportExternalOfferTransactionAsync(orderId, token);
            }
            catch (Exception ex)
            {
                Log.Warning(LogTag.IAPThirdPay, $"Google 合规上报失败 orderId={orderId}：{ex.Message}");
            }
        }

        /// <summary>
        /// 内部 token 拉取实现，加锁防并发。
        /// </summary>
        private async UniTask FetchTokenAsync()
        {
            if (m_IsFetching)
                return;

            m_IsFetching = true;
            try
            {
                m_CachedToken = await GoogleBillingProxy.CreateExternalOfferReportingDetailsAsync();
            }
            catch (Exception ex)
            {
                Log.Warning(LogTag.IAPThirdPay, $"Google ExternalOfferToken 拉取失败：{ex.Message}");
                m_CachedToken = null;
            }
            finally
            {
                m_IsFetching = false;
            }
        }
    }

    /// <summary>
    /// Google Play Billing 外部链接合规 API 桥接（仅 Android）。
    /// 非 Android 平台返回空字符串/空任务，不引入 BillingClient 依赖。
    /// </summary>
    internal static class GoogleBillingProxy
    {
        /// <summary>
        /// 调用 BillingClient.createExternalOfferReportingDetails 取一份 ExternalOfferToken。
        /// </summary>
        /// <returns>ExternalOfferToken 字符串；非 Android 或调用失败时返回空字符串。</returns>
        public static UniTask<string> CreateExternalOfferReportingDetailsAsync()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            // TODO（业务层接入 BillingClient 后填写）：通过 AndroidJavaObject 调用
            // BillingClient.createExternalOfferReportingDetails，将返回的 externalTransactionToken 返回。
            // 在业务层完成 BillingClient 桥接前，此处保持空字符串占位（不影响主链路）。
            return UniTask.FromResult(string.Empty);
#else
            return UniTask.FromResult(string.Empty);
#endif
        }

        /// <summary>
        /// 调用 BillingClient.reportExternalOfferTransaction 上报合规交易。
        /// </summary>
        /// <param name="orderId">本地订单 ID。</param>
        /// <param name="token">ExternalOfferToken。</param>
        /// <returns>上报完成的异步任务；非 Android 或调用失败时立即完成。</returns>
        public static UniTask ReportExternalOfferTransactionAsync(string orderId, string token)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            // TODO（业务层接入 BillingClient 后填写）：通过 AndroidJavaObject 调用
            // BillingClient.reportExternalOfferTransaction(orderId, token)。
            return UniTask.CompletedTask;
#else
            return UniTask.CompletedTask;
#endif
        }
    }
}
