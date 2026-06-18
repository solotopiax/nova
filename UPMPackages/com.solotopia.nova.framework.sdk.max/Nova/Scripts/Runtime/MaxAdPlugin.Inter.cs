/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MaxAdPlugin.Inter.cs
 * author:    yingzheng
 * created:   2026/5/15
 * descrip:   MaxAdPlugin 插屏广告逻辑
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
        /// 发起插屏广告加载请求。
        /// </summary>
        /// <param name="placementId">广告位 ID。</param>
        private void RequestInter(string placementId) => MaxSdk.LoadInterstitial(placementId);

        /// <summary>
        /// 展示插屏广告，挂起等待关闭或失败回调。
        /// </summary>
        /// <param name="placementId">广告位 ID。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>广告结果，包含展示状态及收益数据。</returns>
        private UniTask<AdResult> ShowInterAsync(string placementId, CancellationToken ct)
        {
            m_InterTcs = new UniTaskCompletionSource<AdResult>();
            MaxSdk.ShowInterstitial(placementId);
            return m_InterTcs.Task;
        }

        /// <summary>
        /// 插屏加载成功回调，触发 RaiseAdLoaded。
        /// </summary>
        /// <param name="adUnitId">广告位 ID。</param>
        /// <param name="info">广告信息。</param>
        private void OnInterLoaded(string adUnitId, MaxSdkBase.AdInfo info)
        {
            RaiseAdLoaded(new AdLoadResult
            {
                Success = true,
                Format = AdFormat.Interstitial,
                PlacementId = adUnitId,
                Network = info.NetworkName,
                Revenue = info.Revenue,
                Currency = "USD",
                CustomProps = BuildMaxLoadProps(info),
            });
        }

        /// <summary>
        /// 插屏加载失败回调，触发 RaiseAdLoadFailed。
        /// </summary>
        /// <param name="adUnitId">广告位 ID。</param>
        /// <param name="err">错误信息。</param>
        private void OnInterLoadFailed(string adUnitId, MaxSdkBase.ErrorInfo err)
        {
            RaiseAdLoadFailed(new AdLoadResult
            {
                Success = false,
                Format = AdFormat.Interstitial,
                PlacementId = adUnitId,
                ErrorCode = (int)err.Code,
                ErrorMessage = err.Message,
            });
        }

        /// <summary>
        /// 插屏展示成功回调，上报展示打点。
        /// </summary>
        /// <param name="adUnitId">广告位 ID。</param>
        /// <param name="info">广告信息。</param>
        private void OnInterDisplayed(string adUnitId, MaxSdkBase.AdInfo info)
        {
            TrackAdShow(AdFormat.Interstitial, adUnitId);
        }

        /// <summary>
        /// 插屏展示失败回调，触发 RaiseShowFailed 并结束 Tcs。
        /// </summary>
        /// <param name="adUnitId">广告位 ID。</param>
        /// <param name="err">错误信息。</param>
        /// <param name="info">广告信息。</param>
        private void OnInterDisplayFailed(string adUnitId, MaxSdkBase.ErrorInfo err, MaxSdkBase.AdInfo info)
        {
            var result = new AdResult
            {
                Success = false,
                Format = AdFormat.Interstitial,
                PlacementId = adUnitId,
                Network = info?.NetworkName,
                ErrorMessage = err.Message,
            };
            RaiseShowFailed(result);
            m_InterTcs?.TrySetResult(result);
        }

        /// <summary>
        /// 插屏点击回调，上报点击打点。
        /// </summary>
        /// <param name="adUnitId">广告位 ID。</param>
        /// <param name="info">广告信息。</param>
        private void OnInterClicked(string adUnitId, MaxSdkBase.AdInfo info)
        {
            TrackAdClick(AdFormat.Interstitial, adUnitId);
        }

        /// <summary>
        /// 插屏关闭回调，触发 RaiseAdClosed 并结束 Tcs。
        /// </summary>
        /// <param name="adUnitId">广告位 ID。</param>
        /// <param name="info">广告信息。</param>
        private void OnInterHidden(string adUnitId, MaxSdkBase.AdInfo info)
        {
            var result = new AdResult
            {
                Success = true,
                Format = AdFormat.Interstitial,
                PlacementId = adUnitId,
                Network = info.NetworkName,
                Revenue = info.Revenue,
                Currency = "USD",
                RewardGranted = true,
            };
            RaiseAdClosed(result, true);
            m_InterTcs?.TrySetResult(result);
        }

        /// <summary>
        /// 插屏收益回调，触发 RaiseRevenue。
        /// </summary>
        /// <param name="adUnitId">广告位 ID。</param>
        /// <param name="info">广告信息。</param>
        private void OnInterRevenuePaid(string adUnitId, MaxSdkBase.AdInfo info)
        {
            RaiseRevenue(new AdEvent
            {
                Format = AdFormat.Interstitial,
                PlacementId = adUnitId,
                Network = info.NetworkName,
                Revenue = info.Revenue,
                Currency = "USD",
                Precision = info.RevenuePrecision,
            });
            TrackMaxRevenue(AdFormat.Interstitial, adUnitId, info);
        }
    }
}
