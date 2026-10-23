/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MaxAdPlugin.Inter.cs
 * author:    yingzheng
 * created:   2026/5/15
 * descrip:   MaxAdPlugin 插屏广告逻辑
 ***************************************************************/

using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using NovaFramework.Runtime;
using NovaFramework.SDK.AdPlugin.Runtime;

namespace NovaFramework.SDK.MaxAdPlugin.Runtime
{
    public sealed partial class MaxAdPlugin
    {
#if NOVA_APPLOVIN_MAX
        /// <summary>
        /// 由基类分组门发起插屏的当前真实 ID 加载，完成任务只表示调用已发出。
        /// </summary>
        /// <param name="placementId">当前组正在尝试的真实广告位 ID。</param>
        private void RequestInter(string placementId)
        {
            MaxSdk.LoadInterstitial(placementId);
        }

        /// <summary>
        /// 展示插屏并等待原厂终态；全屏互斥与业务取消隔离由基类保证。
        /// </summary>
        /// <param name="placementId">基类已选中的真实广告位。</param>
        /// <param name="ct">渠道生命周期令牌，业务取消由基类隔离。</param>
        /// <returns>匹配关闭或展示失败后的结果；静默跳过时继续等待原生终态。</returns>
        private UniTask<AdResult> ShowInterAsync(string placementId, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            // 基类已预留全屏占用；此处只建立本次原厂展示的终态等待。
            var completion = new UniTaskCompletionSource<AdResult>();
            m_InterTcs = completion;
            m_ActiveFullscreenPlacementId = placementId;
            try
            {
                TrackAdShow(AdFormat.Interstitial, placementId, BeginFullscreenImpression(AdFormat.Interstitial, placementId));
                Log.Debug(LogTag.Max, $"Interstitial 发起原厂展示：format={AdFormat.Interstitial}，placement={placementId}，等待展示与关闭回调。");
                MaxSdk.ShowInterstitial(placementId);
            }
            catch (Exception ex)
            {
                FinishShowException(AdFormat.Interstitial, placementId, ex);
            }
            return completion.Task;
        }

        /// <summary>
        /// 复制插屏加载结果后切回主线程，真实 Ready 的复查和组推进由基类处理。
        /// </summary>
        /// <param name="adUnitId">加载回调真实 ID。</param>
        /// <param name="info">厂商响应，返回前复制所需字段。</param>
        private void OnInterLoaded(string adUnitId, MaxSdkBase.AdInfo info)
        {
            Log.Debug(LogTag.Max, $"Interstitial 加载成功回调：format={AdFormat.Interstitial}，placement={adUnitId}，network={info?.NetworkName}，revenueUSD={FormatRevenue((decimal)(info?.Revenue ?? 0d))}，precision={info?.RevenuePrecision}。");
            if (!AcceptMaxCallback(AdFormat.Interstitial, adUnitId, nameof(OnInterLoaded))) return;
            var snapshot = new MaxAdInfoSnapshot(info);
            var result = new AdLoadResult
            {
                Success = true,
                Format = AdFormat.Interstitial,
                PlacementId = adUnitId,
                Network = snapshot.NetworkName,
                Revenue = snapshot.Revenue,
                Currency = "USD",
                CustomProps = BuildMaxLoadProps(snapshot),
            };
            PostAdCallbackToMainThread(() => RaiseAdLoaded(result));
        }

        /// <summary>
        /// 复制插屏加载失败结果后排入主线程，只由基类当前组接收终态。
        /// </summary>
        /// <param name="adUnitId">失败的真实 ID。</param>
        /// <param name="err">厂商错误，保留原始错误码和消息。</param>
        private void OnInterLoadFailed(string adUnitId, MaxSdkBase.ErrorInfo err)
        {
            Log.Debug(LogTag.Max, $"Interstitial 加载失败回调：format={AdFormat.Interstitial}，placement={adUnitId}，code={(err == null ? -1 : (int)err.Code)}，message={err?.Message}。");
            if (!AcceptMaxCallback(AdFormat.Interstitial, adUnitId, nameof(OnInterLoadFailed))) return;
            var result = new AdLoadResult
            {
                Success = false,
                Format = AdFormat.Interstitial,
                PlacementId = adUnitId,
                ErrorCode = err == null ? -1 : (int)err.Code,
                ErrorMessage = err?.Message ?? "MAX 未提供加载错误。",
            };
            PostAdCallbackToMainThread(() => RaiseAdLoadFailed(result));
        }

        /// <summary>
        /// 匹配插屏当前展示后上报 displayed，重复通知与展示诊断由基类处理。
        /// </summary>
        /// <param name="adUnitId">展示的真实 ID。</param>
        /// <param name="info">厂商展示响应。</param>
        private void OnInterDisplayed(string adUnitId, MaxSdkBase.AdInfo info)
        {
            Log.Debug(LogTag.Max, $"Interstitial 展示成功回调：format={AdFormat.Interstitial}，placement={adUnitId}，network={info?.NetworkName}。");
            var snapshot = new MaxAdInfoSnapshot(info);
            PostAdCallbackToMainThread(() =>
            {
                if (!AcceptMaxCallback(AdFormat.Interstitial, adUnitId, nameof(OnInterDisplayed)) || !IsCurrentFullscreenShow(AdFormat.Interstitial, adUnitId))
                {
                    return;
                }
                RaiseShowCompleted(new AdResult
                {
                    Success = true,
                    RewardGranted = true,
                    Format = AdFormat.Interstitial,
                    PlacementId = adUnitId,
                    Network = snapshot.NetworkName,
                    Revenue = snapshot.Revenue,
                    Currency = "USD",
                }, MarkFullscreenDisplayed(AdFormat.Interstitial, adUnitId));
            });
        }

        /// <summary>
        /// 匹配插屏展示失败后先领取终态，避免 hidden 或重复失败再次收尾和续杯。
        /// </summary>
        /// <param name="adUnitId">失败的真实 ID。</param>
        /// <param name="err">厂商展示错误。</param>
        /// <param name="info">厂商广告响应。</param>
        private void OnInterDisplayFailed(string adUnitId, MaxSdkBase.ErrorInfo err, MaxSdkBase.AdInfo info)
        {
            Log.Debug(LogTag.Max, $"Interstitial 展示失败回调：format={AdFormat.Interstitial}，placement={adUnitId}，network={info?.NetworkName}，code={(err == null ? -1 : (int)err.Code)}，message={err?.Message}。");
            var snapshot = new MaxAdInfoSnapshot(info);
            string message = err?.Message ?? "MAX 未提供展示错误。";
            PostAdCallbackToMainThread(() =>
            {
                if (!AcceptMaxCallback(AdFormat.Interstitial, adUnitId, nameof(OnInterDisplayFailed)) || !TryFinishFullscreenShow(AdFormat.Interstitial, adUnitId, out var completion))
                {
                    return;
                }
                var showProps = AbandonFullscreenImpression(AdFormat.Interstitial, adUnitId);
                var result = new AdResult
                {
                    Success = false,
                    Format = AdFormat.Interstitial,
                    PlacementId = adUnitId,
                    Network = snapshot.NetworkName,
                    ErrorMessage = message,
                };
                try
                {
                    RaiseShowFailed(result, showProps);
                }
                finally
                {
                    completion.TrySetResult(result);
                }
            });
        }

        /// <summary>
        /// 仅在匹配的插屏原生展示中于主线程上报点击。
        /// </summary>
        /// <param name="adUnitId">点击的真实 ID。</param>
        /// <param name="info">厂商响应，此处不保留。</param>
        private void OnInterClicked(string adUnitId, MaxSdkBase.AdInfo info)
        {
            Log.Debug(LogTag.Max, $"Interstitial 点击回调：format={AdFormat.Interstitial}，placement={adUnitId}，network={info?.NetworkName}。");
            PostAdCallbackToMainThread(() =>
            {
                if (!AcceptMaxCallback(AdFormat.Interstitial, adUnitId, nameof(OnInterClicked)) || !IsCurrentFullscreenShow(AdFormat.Interstitial, adUnitId))
                {
                    return;
                }
                TrackAdClick(AdFormat.Interstitial, adUnitId);
            });
        }

        /// <summary>
        /// 匹配插屏关闭后只领取一次终态，保留曝光关联并交由基类补充全部组。
        /// </summary>
        /// <param name="adUnitId">关闭的真实 ID。</param>
        /// <param name="info">关闭响应，返回前复制收益与网络。</param>
        private void OnInterHidden(string adUnitId, MaxSdkBase.AdInfo info)
        {
            Log.Debug(LogTag.Max, $"Interstitial 关闭回调：format={AdFormat.Interstitial}，placement={adUnitId}，network={info?.NetworkName}。");
            var snapshot = new MaxAdInfoSnapshot(info);
            PostAdCallbackToMainThread(() =>
            {
                if (!AcceptMaxCallback(AdFormat.Interstitial, adUnitId, nameof(OnInterHidden)) || !TryFinishFullscreenShow(AdFormat.Interstitial, adUnitId, out var completion))
                {
                    return;
                }
                var result = new AdResult
                {
                    Success = true,
                    RewardGranted = true,
                    Format = AdFormat.Interstitial,
                    PlacementId = adUnitId,
                    Network = snapshot.NetworkName,
                    Revenue = snapshot.Revenue,
                    Currency = "USD",
                };
                try
                {
                    RaiseAdClosed(result, true, GetFullscreenImpressionProps(AdFormat.Interstitial, adUnitId));
                }
                finally
                {
                    completion.TrySetResult(result);
                }
            });
        }

        /// <summary>
        /// 插屏收益纯打点保留即时路径，槽位收益与业务通知由基类排入主线程。
        /// </summary>
        /// <param name="adUnitId">收益的真实 ID。</param>
        /// <param name="info">厂商收益响应，立即复制后使用。</param>
        private void OnInterRevenuePaid(string adUnitId, MaxSdkBase.AdInfo info)
        {
            Log.Debug(LogTag.Max, $"Interstitial 收益回调：format={AdFormat.Interstitial}，placement={adUnitId}，network={info?.NetworkName}，revenueUSD={FormatRevenue((decimal)(info?.Revenue ?? 0d))}，precision={info?.RevenuePrecision}。");
            if (!AcceptMaxCallback(AdFormat.Interstitial, adUnitId, nameof(OnInterRevenuePaid))) return;
            var snapshot = new MaxAdInfoSnapshot(info);
            RaiseRevenueImmediately(new AdEvent
            {
                Format = AdFormat.Interstitial,
                PlacementId = adUnitId,
                Network = snapshot.NetworkName,
                Revenue = snapshot.Revenue,
                Currency = "USD",
                Precision = snapshot.RevenuePrecision,
            }, () => TrackMaxRevenue(AdFormat.Interstitial, adUnitId, snapshot, RecordFullscreenRevenue(AdFormat.Interstitial, adUnitId)));
        }

        /// <summary>
        /// 复制插屏过期替换缓存的收益；只由基类安全采纳，不结束主动加载或展示。
        /// </summary>
        /// <param name="adUnitId">过期替换的真实 ID。</param>
        /// <param name="expiredInfo">已过期广告信息，不用于新缓存收益。</param>
        /// <param name="newInfo">替换广告信息，收益从此响应复制。</param>
        private void OnInterExpiredAdReloaded(string adUnitId, MaxSdkBase.AdInfo expiredInfo, MaxSdkBase.AdInfo newInfo)
        {
            Log.Debug(LogTag.Max, $"Interstitial 过期广告重载回调：format={AdFormat.Interstitial}，placement={adUnitId}，expiredNetwork={expiredInfo?.NetworkName}，expiredRevenueUSD={FormatRevenue((decimal)(expiredInfo?.Revenue ?? 0d))}，expiredPrecision={expiredInfo?.RevenuePrecision}，network={newInfo?.NetworkName}，revenueUSD={FormatRevenue((decimal)(newInfo?.Revenue ?? 0d))}，precision={newInfo?.RevenuePrecision}。");
            if (!AcceptMaxCallback(AdFormat.Interstitial, adUnitId, nameof(OnInterExpiredAdReloaded))) return;
            double revenue = newInfo?.Revenue ?? 0d;
            PostAdCallbackToMainThread(() => RaiseAdCacheReplaced(AdFormat.Interstitial, adUnitId, revenue));
        }
#endif
    }
}
