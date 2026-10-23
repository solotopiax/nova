/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MaxAdPlugin.RV.cs
 * author:    yingzheng
 * created:   2026/5/15
 * descrip:   MaxAdPlugin 激励视频广告逻辑
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
        /// 由基类分组门发起激励视频的当前真实 ID 加载，完成任务只表示调用已发出。
        /// </summary>
        /// <param name="placementId">当前组正在尝试的真实广告位 ID。</param>
        private void RequestRV(string placementId)
        {
            MaxSdk.LoadRewardedAd(placementId);
        }

        /// <summary>
        /// 展示激励视频并等待原厂终态；全屏互斥与业务取消隔离由基类保证。
        /// </summary>
        /// <param name="placementId">基类已选中的真实广告位。</param>
        /// <param name="ct">渠道生命周期令牌，业务取消由基类隔离。</param>
        /// <returns>匹配关闭或展示失败后的结果；静默跳过时继续等待原生终态。</returns>
        private UniTask<AdResult> ShowRVAsync(string placementId, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            // 基类已预留全屏占用；此处只建立本次原厂展示的终态等待。
            var completion = new UniTaskCompletionSource<AdResult>();
            m_RVTcs = completion;
            m_ActiveFullscreenPlacementId = placementId;
            m_RVRewarded = false;
            try
            {
                TrackAdShow(AdFormat.Rewarded, placementId, BeginFullscreenImpression(AdFormat.Rewarded, placementId));
                Log.Debug(LogTag.Max, $"Rewarded 发起原厂展示：format={AdFormat.Rewarded}，placement={placementId}，等待展示、奖励与关闭回调。");
                MaxSdk.ShowRewardedAd(placementId);
            }
            catch (Exception ex)
            {
                FinishShowException(AdFormat.Rewarded, placementId, ex);
            }
            return completion.Task;
        }

        /// <summary>
        /// 复制激励视频加载结果后切回主线程，真实 Ready 的复查和组推进由基类处理。
        /// </summary>
        /// <param name="adUnitId">加载回调真实 ID。</param>
        /// <param name="info">厂商响应，返回前复制所需字段。</param>
        private void OnRVLoaded(string adUnitId, MaxSdkBase.AdInfo info)
        {
            Log.Debug(LogTag.Max, $"Rewarded 加载成功回调：format={AdFormat.Rewarded}，placement={adUnitId}，network={info?.NetworkName}，revenueUSD={FormatRevenue((decimal)(info?.Revenue ?? 0d))}，precision={info?.RevenuePrecision}。");
            if (!AcceptMaxCallback(AdFormat.Rewarded, adUnitId, nameof(OnRVLoaded))) return;
            var snapshot = new MaxAdInfoSnapshot(info);
            var result = new AdLoadResult
            {
                Success = true,
                Format = AdFormat.Rewarded,
                PlacementId = adUnitId,
                Network = snapshot.NetworkName,
                Revenue = snapshot.Revenue,
                Currency = "USD",
                CustomProps = BuildMaxLoadProps(snapshot),
            };
            PostAdCallbackToMainThread(() => RaiseAdLoaded(result));
        }

        /// <summary>
        /// 复制激励视频加载失败结果后排入主线程，只由基类当前组接收终态。
        /// </summary>
        /// <param name="adUnitId">失败的真实 ID。</param>
        /// <param name="err">厂商错误，保留原始错误码和消息。</param>
        private void OnRVLoadFailed(string adUnitId, MaxSdkBase.ErrorInfo err)
        {
            Log.Debug(LogTag.Max, $"Rewarded 加载失败回调：format={AdFormat.Rewarded}，placement={adUnitId}，code={(err == null ? -1 : (int)err.Code)}，message={err?.Message}。");
            if (!AcceptMaxCallback(AdFormat.Rewarded, adUnitId, nameof(OnRVLoadFailed))) return;
            var result = new AdLoadResult
            {
                Success = false,
                Format = AdFormat.Rewarded,
                PlacementId = adUnitId,
                ErrorCode = err == null ? -1 : (int)err.Code,
                ErrorMessage = err?.Message ?? "MAX 未提供加载错误。",
            };
            PostAdCallbackToMainThread(() => RaiseAdLoadFailed(result));
        }

        /// <summary>
        /// 匹配激励视频当前展示后上报 displayed，重复通知与展示诊断由基类处理。
        /// </summary>
        /// <param name="adUnitId">展示的真实 ID。</param>
        /// <param name="info">厂商展示响应。</param>
        private void OnRVDisplayed(string adUnitId, MaxSdkBase.AdInfo info)
        {
            Log.Debug(LogTag.Max, $"Rewarded 展示成功回调：format={AdFormat.Rewarded}，placement={adUnitId}，network={info?.NetworkName}。");
            var snapshot = new MaxAdInfoSnapshot(info);
            PostAdCallbackToMainThread(() =>
            {
                if (!AcceptMaxCallback(AdFormat.Rewarded, adUnitId, nameof(OnRVDisplayed)) || !IsCurrentFullscreenShow(AdFormat.Rewarded, adUnitId))
                {
                    return;
                }
                RaiseShowCompleted(new AdResult
                {
                    Success = true,
                    Format = AdFormat.Rewarded,
                    PlacementId = adUnitId,
                    Network = snapshot.NetworkName,
                    Revenue = snapshot.Revenue,
                    Currency = "USD",
                }, MarkFullscreenDisplayed(AdFormat.Rewarded, adUnitId));
            });
        }

        /// <summary>
        /// 匹配激励视频展示失败后先领取终态，避免 hidden 或重复失败再次收尾和续杯。
        /// </summary>
        /// <param name="adUnitId">失败的真实 ID。</param>
        /// <param name="err">厂商展示错误。</param>
        /// <param name="info">厂商广告响应。</param>
        private void OnRVDisplayFailed(string adUnitId, MaxSdkBase.ErrorInfo err, MaxSdkBase.AdInfo info)
        {
            Log.Debug(LogTag.Max, $"Rewarded 展示失败回调：format={AdFormat.Rewarded}，placement={adUnitId}，network={info?.NetworkName}，code={(err == null ? -1 : (int)err.Code)}，message={err?.Message}。");
            var snapshot = new MaxAdInfoSnapshot(info);
            string message = err?.Message ?? "MAX 未提供展示错误。";
            PostAdCallbackToMainThread(() =>
            {
                if (!AcceptMaxCallback(AdFormat.Rewarded, adUnitId, nameof(OnRVDisplayFailed)) || !TryFinishFullscreenShow(AdFormat.Rewarded, adUnitId, out var completion))
                {
                    return;
                }
                var showProps = AbandonFullscreenImpression(AdFormat.Rewarded, adUnitId);
                var result = new AdResult
                {
                    Success = false,
                    Format = AdFormat.Rewarded,
                    PlacementId = adUnitId,
                    Network = snapshot.NetworkName,
                    ErrorMessage = message,
                };
                m_RVRewarded = false;
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
        /// 仅在匹配的激励视频原生展示中于主线程上报点击。
        /// </summary>
        /// <param name="adUnitId">点击的真实 ID。</param>
        /// <param name="info">厂商响应，此处不保留。</param>
        private void OnRVClicked(string adUnitId, MaxSdkBase.AdInfo info)
        {
            Log.Debug(LogTag.Max, $"Rewarded 点击回调：format={AdFormat.Rewarded}，placement={adUnitId}，network={info?.NetworkName}。");
            PostAdCallbackToMainThread(() =>
            {
                if (!AcceptMaxCallback(AdFormat.Rewarded, adUnitId, nameof(OnRVClicked)) || !IsCurrentFullscreenShow(AdFormat.Rewarded, adUnitId))
                {
                    return;
                }
                TrackAdClick(AdFormat.Rewarded, adUnitId);
            });
        }

        /// <summary>
        /// 激励奖励只在匹配的原生展示中于主线程采纳，未知 ID 不改变奖励资格。
        /// </summary>
        /// <param name="adUnitId">奖励回调广告位。</param>
        /// <param name="reward">原厂奖励载荷，业务资格以收到此回调为准。</param>
        /// <param name="info">原厂广告载荷，此处不保留。</param>
        private void OnRVReceivedReward(string adUnitId, MaxSdk.Reward reward, MaxSdkBase.AdInfo info)
        {
            Log.Debug(LogTag.Max, $"Rewarded 奖励回调：format={AdFormat.Rewarded}，placement={adUnitId}，network={info?.NetworkName}。");
            PostAdCallbackToMainThread(() =>
            {
                if (!AcceptMaxCallback(AdFormat.Rewarded, adUnitId, nameof(OnRVReceivedReward)) || !IsCurrentFullscreenShow(AdFormat.Rewarded, adUnitId))
                {
                    return;
                }
                m_RVRewarded = true;
            });
        }

        /// <summary>
        /// 匹配激励视频关闭后只领取一次终态，保留曝光关联并交由基类补充全部组。
        /// </summary>
        /// <param name="adUnitId">关闭的真实 ID。</param>
        /// <param name="info">关闭响应，返回前复制收益与网络。</param>
        private void OnRVHidden(string adUnitId, MaxSdkBase.AdInfo info)
        {
            Log.Debug(LogTag.Max, $"Rewarded 关闭回调：format={AdFormat.Rewarded}，placement={adUnitId}，network={info?.NetworkName}，rewarded={m_RVRewarded}。");
            var snapshot = new MaxAdInfoSnapshot(info);
            PostAdCallbackToMainThread(() =>
            {
                if (!AcceptMaxCallback(AdFormat.Rewarded, adUnitId, nameof(OnRVHidden)) || !TryFinishFullscreenShow(AdFormat.Rewarded, adUnitId, out var completion))
                {
                    return;
                }
                var result = new AdResult
                {
                    Success = true,
                    UserCompleted = m_RVRewarded,
                    RewardGranted = m_RVRewarded,
                    Format = AdFormat.Rewarded,
                    PlacementId = adUnitId,
                    Network = snapshot.NetworkName,
                    Revenue = snapshot.Revenue,
                    Currency = "USD",
                };
                m_RVRewarded = false;
                try
                {
                    RaiseAdClosed(result, result.UserCompleted, GetFullscreenImpressionProps(AdFormat.Rewarded, adUnitId));
                }
                finally
                {
                    completion.TrySetResult(result);
                }
            });
        }

        /// <summary>
        /// 激励视频收益纯打点保留即时路径，槽位收益与业务通知由基类排入主线程。
        /// </summary>
        /// <param name="adUnitId">收益的真实 ID。</param>
        /// <param name="info">厂商收益响应，立即复制后使用。</param>
        private void OnRVRevenuePaid(string adUnitId, MaxSdkBase.AdInfo info)
        {
            Log.Debug(LogTag.Max, $"Rewarded 收益回调：format={AdFormat.Rewarded}，placement={adUnitId}，network={info?.NetworkName}，revenueUSD={FormatRevenue((decimal)(info?.Revenue ?? 0d))}，precision={info?.RevenuePrecision}。");
            if (!AcceptMaxCallback(AdFormat.Rewarded, adUnitId, nameof(OnRVRevenuePaid))) return;
            var snapshot = new MaxAdInfoSnapshot(info);
            RaiseRevenueImmediately(new AdEvent
            {
                Format = AdFormat.Rewarded,
                PlacementId = adUnitId,
                Network = snapshot.NetworkName,
                Revenue = snapshot.Revenue,
                Currency = "USD",
                Precision = snapshot.RevenuePrecision,
            }, () => TrackMaxRevenue(AdFormat.Rewarded, adUnitId, snapshot, RecordFullscreenRevenue(AdFormat.Rewarded, adUnitId)));
        }

        /// <summary>
        /// 复制激励视频过期替换缓存的收益；只由基类安全采纳，不结束主动加载或展示。
        /// </summary>
        /// <param name="adUnitId">过期替换的真实 ID。</param>
        /// <param name="expiredInfo">已过期广告信息，不用于新缓存收益。</param>
        /// <param name="newInfo">替换广告信息，收益从此响应复制。</param>
        private void OnRVExpiredAdReloaded(string adUnitId, MaxSdkBase.AdInfo expiredInfo, MaxSdkBase.AdInfo newInfo)
        {
            Log.Debug(LogTag.Max, $"Rewarded 过期广告重载回调：format={AdFormat.Rewarded}，placement={adUnitId}，expiredNetwork={expiredInfo?.NetworkName}，expiredRevenueUSD={FormatRevenue((decimal)(expiredInfo?.Revenue ?? 0d))}，expiredPrecision={expiredInfo?.RevenuePrecision}，network={newInfo?.NetworkName}，revenueUSD={FormatRevenue((decimal)(newInfo?.Revenue ?? 0d))}，precision={newInfo?.RevenuePrecision}。");
            if (!AcceptMaxCallback(AdFormat.Rewarded, adUnitId, nameof(OnRVExpiredAdReloaded))) return;
            double revenue = newInfo?.Revenue ?? 0d;
            PostAdCallbackToMainThread(() => RaiseAdCacheReplaced(AdFormat.Rewarded, adUnitId, revenue));
        }
#endif
    }
}
