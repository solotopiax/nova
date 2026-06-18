/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MaxAdPlugin.RV.cs
 * author:    yingzheng
 * created:   2026/5/15
 * descrip:   MaxAdPlugin 激励视频广告逻辑
 ***************************************************************/

using System.Threading;
using Cysharp.Threading.Tasks;
using NovaFramework.Runtime;
using NovaFramework.SDK.AdPlugin.Runtime;

namespace NovaFramework.SDK.MaxAdPlugin.Runtime
{
    public sealed partial class MaxAdPlugin
    {
        /// <summary>
        /// 发起激励视频加载请求。
        /// </summary>
        /// <param name="placementId">广告位 ID。</param>
        private void RequestRV(string placementId) => MaxSdk.LoadRewardedAd(placementId);

        /// <summary>
        /// 展示激励视频广告，挂起等待关闭或失败回调。
        /// </summary>
        /// <param name="placementId">广告位 ID。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>广告结果，包含是否完成观看及收益数据。</returns>
        private UniTask<AdResult> ShowRVAsync(string placementId, CancellationToken ct)
        {
            m_RVRewarded = false;
            m_RVTcs = new UniTaskCompletionSource<AdResult>();
            MaxSdk.ShowRewardedAd(placementId);
            return m_RVTcs.Task;
        }

        /// <summary>
        /// 激励视频加载成功回调，触发 RaiseAdLoaded。
        /// </summary>
        /// <param name="adUnitId">广告位 ID。</param>
        /// <param name="info">广告信息。</param>
        private void OnRVLoaded(string adUnitId, MaxSdkBase.AdInfo info)
        {
            RaiseAdLoaded(new AdLoadResult
            {
                Success = true,
                Format = AdFormat.Rewarded,
                PlacementId = adUnitId,
                Network = info.NetworkName,
                Revenue = info.Revenue,
                Currency = "USD",
                CustomProps = BuildMaxLoadProps(info),
            });
        }

        /// <summary>
        /// 激励视频加载失败回调，触发 RaiseAdLoadFailed。
        /// </summary>
        /// <param name="adUnitId">广告位 ID。</param>
        /// <param name="err">错误信息。</param>
        private void OnRVLoadFailed(string adUnitId, MaxSdkBase.ErrorInfo err)
        {
            RaiseAdLoadFailed(new AdLoadResult
            {
                Success = false,
                Format = AdFormat.Rewarded,
                PlacementId = adUnitId,
                ErrorCode = (int)err.Code,
                ErrorMessage = err.Message,
            });
        }

        /// <summary>
        /// 激励视频展示成功回调，上报展示打点。
        /// </summary>
        /// <param name="adUnitId">广告位 ID。</param>
        /// <param name="info">广告信息。</param>
        private void OnRVDisplayed(string adUnitId, MaxSdkBase.AdInfo info)
        {
            TrackAdShow(AdFormat.Rewarded, adUnitId);
        }

        /// <summary>
        /// 激励视频展示失败回调，触发 RaiseShowFailed 并结束 Tcs。
        /// </summary>
        /// <param name="adUnitId">广告位 ID。</param>
        /// <param name="err">错误信息。</param>
        /// <param name="info">广告信息。</param>
        private void OnRVDisplayFailed(string adUnitId, MaxSdkBase.ErrorInfo err, MaxSdkBase.AdInfo info)
        {
            var result = new AdResult
            {
                Success = false,
                Format = AdFormat.Rewarded,
                PlacementId = adUnitId,
                Network = info?.NetworkName,
                ErrorMessage = err.Message,
            };
            RaiseShowFailed(result);
            m_RVTcs?.TrySetResult(result);
        }

        /// <summary>
        /// 激励视频点击回调，上报点击打点。
        /// </summary>
        /// <param name="adUnitId">广告位 ID。</param>
        /// <param name="info">广告信息。</param>
        private void OnRVClicked(string adUnitId, MaxSdkBase.AdInfo info)
        {
            TrackAdClick(AdFormat.Rewarded, adUnitId);
        }

        /// <summary>
        /// 激励视频奖励回调，标记 m_RVRewarded。
        /// </summary>
        /// <param name="adUnitId">广告位 ID。</param>
        /// <param name="reward">奖励数据。</param>
        /// <param name="info">广告信息。</param>
        private void OnRVReceivedReward(string adUnitId, MaxSdk.Reward reward, MaxSdkBase.AdInfo info)
        {
            m_RVRewarded = true;
        }

        /// <summary>
        /// 激励视频关闭回调，触发 RaiseAdClosed 并结束 Tcs。
        /// </summary>
        /// <param name="adUnitId">广告位 ID。</param>
        /// <param name="info">广告信息。</param>
        private void OnRVHidden(string adUnitId, MaxSdkBase.AdInfo info)
        {
            var result = new AdResult
            {
                Success = true,
                UserCompleted = m_RVRewarded,
                Format = AdFormat.Rewarded,
                PlacementId = adUnitId,
                Network = info.NetworkName,
                Revenue = info.Revenue,
                Currency = "USD",
            };
            m_RVRewarded = false;
            RaiseAdClosed(result, result.UserCompleted);
            m_RVTcs?.TrySetResult(result);
        }

        /// <summary>
        /// 激励视频收益回调，触发 RaiseRevenue。
        /// </summary>
        /// <param name="adUnitId">广告位 ID。</param>
        /// <param name="info">广告信息。</param>
        private void OnRVRevenuePaid(string adUnitId, MaxSdkBase.AdInfo info)
        {
            RaiseRevenue(new AdEvent
            {
                Format = AdFormat.Rewarded,
                PlacementId = adUnitId,
                Network = info.NetworkName,
                Revenue = info.Revenue,
                Currency = "USD",
                Precision = info.RevenuePrecision,
            });
            TrackMaxRevenue(AdFormat.Rewarded, adUnitId, info);
        }
    }
}
