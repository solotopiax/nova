/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MaxAdPlugin.AppOpen.cs
 * author:    yingzheng
 * created:   2026/5/15
 * descrip:   MaxAdPlugin 开屏广告逻辑
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
        /// 由基类分组门发起开屏的当前真实 ID 加载，完成任务只表示调用已发出。
        /// </summary>
        /// <param name="placementId">当前组正在尝试的真实广告位 ID。</param>
        private void RequestAppOpen(string placementId)
        {
            MaxSdk.LoadAppOpenAd(placementId);
        }

        /// <summary>
        /// 展示开屏并等待原厂终态；全屏互斥与业务取消隔离由基类保证。
        /// </summary>
        /// <param name="placementId">基类已选中的真实广告位。</param>
        /// <param name="ct">渠道生命周期令牌，业务取消由基类隔离。</param>
        /// <returns>匹配关闭或展示失败后的结果；静默跳过时继续等待原生终态。</returns>
        private UniTask<AdResult> ShowAppOpenAsync(string placementId, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            // 基类已预留全屏占用；此处只建立本次原厂展示的终态等待。
            var completion = new UniTaskCompletionSource<AdResult>();
            m_AppOpenTcs = completion;
            m_ActiveFullscreenPlacementId = placementId;
            try
            {
                TrackAdShow(AdFormat.AppOpen, placementId, BeginFullscreenImpression(AdFormat.AppOpen, placementId));
                Log.Debug(LogTag.Max, $"AppOpen 发起原厂展示：format={AdFormat.AppOpen}，placement={placementId}，等待展示与关闭回调。");
                MaxSdk.ShowAppOpenAd(placementId);
            }
            catch (Exception ex)
            {
                FinishShowException(AdFormat.AppOpen, placementId, ex);
            }
            return completion.Task;
        }

        /// <summary>
        /// 复制开屏加载结果后切回主线程，真实 Ready 的复查和组推进由基类处理。
        /// </summary>
        /// <param name="adUnitId">加载回调真实 ID。</param>
        /// <param name="info">厂商响应，返回前复制所需字段。</param>
        private void OnAppOpenLoaded(string adUnitId, MaxSdkBase.AdInfo info)
        {
            Log.Debug(LogTag.Max, $"AppOpen 加载成功回调：format={AdFormat.AppOpen}，placement={adUnitId}，network={info?.NetworkName}，revenueUSD={FormatRevenue((decimal)(info?.Revenue ?? 0d))}，precision={info?.RevenuePrecision}。");
            if (!AcceptMaxCallback(AdFormat.AppOpen, adUnitId, nameof(OnAppOpenLoaded))) return;
            var snapshot = new MaxAdInfoSnapshot(info);
            var result = new AdLoadResult
            {
                Success = true,
                Format = AdFormat.AppOpen,
                PlacementId = adUnitId,
                Network = snapshot.NetworkName,
                Revenue = snapshot.Revenue,
                Currency = "USD",
                CustomProps = BuildMaxLoadProps(snapshot),
            };
            PostAdCallbackToMainThread(() => RaiseAdLoaded(result));
        }

        /// <summary>
        /// 复制开屏加载失败结果后排入主线程，只由基类当前组接收终态。
        /// </summary>
        /// <param name="adUnitId">失败的真实 ID。</param>
        /// <param name="err">厂商错误，保留原始错误码和消息。</param>
        private void OnAppOpenLoadFailed(string adUnitId, MaxSdkBase.ErrorInfo err)
        {
            Log.Debug(LogTag.Max, $"AppOpen 加载失败回调：format={AdFormat.AppOpen}，placement={adUnitId}，code={(err == null ? -1 : (int)err.Code)}，message={err?.Message}。");
            if (!AcceptMaxCallback(AdFormat.AppOpen, adUnitId, nameof(OnAppOpenLoadFailed))) return;
            var result = new AdLoadResult
            {
                Success = false,
                Format = AdFormat.AppOpen,
                PlacementId = adUnitId,
                ErrorCode = err == null ? -1 : (int)err.Code,
                ErrorMessage = err?.Message ?? "MAX 未提供加载错误。",
            };
            PostAdCallbackToMainThread(() => RaiseAdLoadFailed(result));
        }

        /// <summary>
        /// 匹配开屏当前展示后上报 displayed，重复通知与展示诊断由基类处理。
        /// </summary>
        /// <param name="adUnitId">展示的真实 ID。</param>
        /// <param name="info">厂商展示响应。</param>
        private void OnAppOpenDisplayed(string adUnitId, MaxSdkBase.AdInfo info)
        {
            Log.Debug(LogTag.Max, $"AppOpen 展示成功回调：format={AdFormat.AppOpen}，placement={adUnitId}，network={info?.NetworkName}。");
            var snapshot = new MaxAdInfoSnapshot(info);
            PostAdCallbackToMainThread(() =>
            {
                if (!AcceptMaxCallback(AdFormat.AppOpen, adUnitId, nameof(OnAppOpenDisplayed)) || !IsCurrentFullscreenShow(AdFormat.AppOpen, adUnitId))
                {
                    return;
                }
                RaiseShowCompleted(new AdResult
                {
                    Success = true,
                    Format = AdFormat.AppOpen,
                    PlacementId = adUnitId,
                    Network = snapshot.NetworkName,
                    Revenue = snapshot.Revenue,
                    Currency = "USD",
                }, MarkFullscreenDisplayed(AdFormat.AppOpen, adUnitId));
            });
        }

        /// <summary>
        /// 匹配开屏展示失败后先领取终态，避免 hidden 或重复失败再次收尾和续杯。
        /// </summary>
        /// <param name="adUnitId">失败的真实 ID。</param>
        /// <param name="err">厂商展示错误。</param>
        /// <param name="info">厂商广告响应。</param>
        private void OnAppOpenDisplayFailed(string adUnitId, MaxSdkBase.ErrorInfo err, MaxSdkBase.AdInfo info)
        {
            Log.Debug(LogTag.Max, $"AppOpen 展示失败回调：format={AdFormat.AppOpen}，placement={adUnitId}，network={info?.NetworkName}，code={(err == null ? -1 : (int)err.Code)}，message={err?.Message}。");
            var snapshot = new MaxAdInfoSnapshot(info);
            string message = err?.Message ?? "MAX 未提供展示错误。";
            PostAdCallbackToMainThread(() =>
            {
                if (!AcceptMaxCallback(AdFormat.AppOpen, adUnitId, nameof(OnAppOpenDisplayFailed)) || !TryFinishFullscreenShow(AdFormat.AppOpen, adUnitId, out var completion))
                {
                    return;
                }
                var showProps = AbandonFullscreenImpression(AdFormat.AppOpen, adUnitId);
                var result = new AdResult
                {
                    Success = false,
                    Format = AdFormat.AppOpen,
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
        /// 仅在匹配的开屏原生展示中于主线程上报点击。
        /// </summary>
        /// <param name="adUnitId">点击的真实 ID。</param>
        /// <param name="info">厂商响应，此处不保留。</param>
        private void OnAppOpenClicked(string adUnitId, MaxSdkBase.AdInfo info)
        {
            Log.Debug(LogTag.Max, $"AppOpen 点击回调：format={AdFormat.AppOpen}，placement={adUnitId}，network={info?.NetworkName}。");
            PostAdCallbackToMainThread(() =>
            {
                if (!AcceptMaxCallback(AdFormat.AppOpen, adUnitId, nameof(OnAppOpenClicked)) || !IsCurrentFullscreenShow(AdFormat.AppOpen, adUnitId))
                {
                    return;
                }
                TrackAdClick(AdFormat.AppOpen, adUnitId);
            });
        }

        /// <summary>
        /// 匹配开屏关闭后只领取一次终态，保留曝光关联并交由基类补充全部组。
        /// </summary>
        /// <param name="adUnitId">关闭的真实 ID。</param>
        /// <param name="info">关闭响应，返回前复制收益与网络。</param>
        private void OnAppOpenHidden(string adUnitId, MaxSdkBase.AdInfo info)
        {
            Log.Debug(LogTag.Max, $"AppOpen 关闭回调：format={AdFormat.AppOpen}，placement={adUnitId}，network={info?.NetworkName}。");
            var snapshot = new MaxAdInfoSnapshot(info);
            PostAdCallbackToMainThread(() =>
            {
                if (!AcceptMaxCallback(AdFormat.AppOpen, adUnitId, nameof(OnAppOpenHidden)) || !TryFinishFullscreenShow(AdFormat.AppOpen, adUnitId, out var completion))
                {
                    return;
                }
                var result = new AdResult
                {
                    Success = true,
                    Format = AdFormat.AppOpen,
                    PlacementId = adUnitId,
                    Network = snapshot.NetworkName,
                    Revenue = snapshot.Revenue,
                    Currency = "USD",
                };
                try
                {
                    RaiseAdClosed(result, false, GetFullscreenImpressionProps(AdFormat.AppOpen, adUnitId));
                }
                finally
                {
                    completion.TrySetResult(result);
                }
            });
        }

        /// <summary>
        /// 开屏收益纯打点保留即时路径，槽位收益与业务通知由基类排入主线程。
        /// </summary>
        /// <param name="adUnitId">收益的真实 ID。</param>
        /// <param name="info">厂商收益响应，立即复制后使用。</param>
        private void OnAppOpenRevenuePaid(string adUnitId, MaxSdkBase.AdInfo info)
        {
            Log.Debug(LogTag.Max, $"AppOpen 收益回调：format={AdFormat.AppOpen}，placement={adUnitId}，network={info?.NetworkName}，revenueUSD={FormatRevenue((decimal)(info?.Revenue ?? 0d))}，precision={info?.RevenuePrecision}。");
            if (!AcceptMaxCallback(AdFormat.AppOpen, adUnitId, nameof(OnAppOpenRevenuePaid))) return;
            var snapshot = new MaxAdInfoSnapshot(info);
            RaiseRevenueImmediately(new AdEvent
            {
                Format = AdFormat.AppOpen,
                PlacementId = adUnitId,
                Network = snapshot.NetworkName,
                Revenue = snapshot.Revenue,
                Currency = "USD",
                Precision = snapshot.RevenuePrecision,
            }, () => TrackMaxRevenue(AdFormat.AppOpen, adUnitId, snapshot, RecordFullscreenRevenue(AdFormat.AppOpen, adUnitId)));
        }

        /// <summary>
        /// 复制开屏过期替换缓存的收益；只由基类安全采纳，不结束主动加载或展示。
        /// </summary>
        /// <param name="adUnitId">过期替换的真实 ID。</param>
        /// <param name="expiredInfo">已过期广告信息，不用于新缓存收益。</param>
        /// <param name="newInfo">替换广告信息，收益从此响应复制。</param>
        private void OnAppOpenExpiredAdReloaded(string adUnitId, MaxSdkBase.AdInfo expiredInfo, MaxSdkBase.AdInfo newInfo)
        {
            Log.Debug(LogTag.Max, $"AppOpen 过期广告重载回调：format={AdFormat.AppOpen}，placement={adUnitId}，expiredNetwork={expiredInfo?.NetworkName}，expiredRevenueUSD={FormatRevenue((decimal)(expiredInfo?.Revenue ?? 0d))}，expiredPrecision={expiredInfo?.RevenuePrecision}，network={newInfo?.NetworkName}，revenueUSD={FormatRevenue((decimal)(newInfo?.Revenue ?? 0d))}，precision={newInfo?.RevenuePrecision}。");
            if (!AcceptMaxCallback(AdFormat.AppOpen, adUnitId, nameof(OnAppOpenExpiredAdReloaded))) return;
            double revenue = newInfo?.Revenue ?? 0d;
            PostAdCallbackToMainThread(() => RaiseAdCacheReplaced(AdFormat.AppOpen, adUnitId, revenue));
        }
#endif
    }
}
