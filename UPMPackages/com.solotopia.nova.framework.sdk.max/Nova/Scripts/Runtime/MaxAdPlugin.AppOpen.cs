/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MaxAdPlugin.AppOpen.cs
 * author:    yingzheng
 * created:   2026/5/15
 * descrip:   MaxAdPlugin 开屏广告逻辑
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
        /// 发起开屏广告加载请求。
        /// </summary>
        /// <param name="placementId">广告位 ID。</param>
        private void RequestAppOpen(string placementId) => MaxSdk.LoadAppOpenAd(placementId);

        /// <summary>
        /// 展示开屏广告，挂起等待关闭或失败回调。
        /// </summary>
        /// <param name="placementId">广告位 ID。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>广告结果，包含展示状态及收益数据。</returns>
        private UniTask<AdResult> ShowAppOpenAsync(string placementId, CancellationToken ct)
        {
            m_AppOpenTcs = new UniTaskCompletionSource<AdResult>();
            MaxSdk.ShowAppOpenAd(placementId);
            return m_AppOpenTcs.Task;
        }

        /// <summary>
        /// 开屏加载成功回调，触发 RaiseAdLoaded。
        /// </summary>
        /// <param name="adUnitId">广告位 ID。</param>
        /// <param name="info">广告信息。</param>
        private void OnAppOpenLoaded(string adUnitId, MaxSdkBase.AdInfo info)
        {
            RaiseAdLoaded(new AdLoadResult
            {
                Success = true,
                Format = AdFormat.AppOpen,
                PlacementId = adUnitId,
                Network = info.NetworkName,
                Revenue = info.Revenue,
                Currency = "USD",
                CustomProps = BuildMaxLoadProps(info),
            });
        }

        /// <summary>
        /// 开屏加载失败回调，触发 RaiseAdLoadFailed。
        /// </summary>
        /// <param name="adUnitId">广告位 ID。</param>
        /// <param name="err">错误信息。</param>
        private void OnAppOpenLoadFailed(string adUnitId, MaxSdkBase.ErrorInfo err)
        {
            RaiseAdLoadFailed(new AdLoadResult
            {
                Success = false,
                Format = AdFormat.AppOpen,
                PlacementId = adUnitId,
                ErrorCode = (int)err.Code,
                ErrorMessage = err.Message,
            });
        }

        /// <summary>
        /// 开屏展示成功回调，上报展示打点。
        /// </summary>
        /// <param name="adUnitId">广告位 ID。</param>
        /// <param name="info">广告信息。</param>
        private void OnAppOpenDisplayed(string adUnitId, MaxSdkBase.AdInfo info)
        {
            TrackAdShow(AdFormat.AppOpen, adUnitId);
        }

        /// <summary>
        /// 开屏展示失败回调，触发 RaiseShowFailed 并结束 Tcs。
        /// </summary>
        /// <param name="adUnitId">广告位 ID。</param>
        /// <param name="err">错误信息。</param>
        /// <param name="info">广告信息。</param>
        private void OnAppOpenDisplayFailed(string adUnitId, MaxSdkBase.ErrorInfo err, MaxSdkBase.AdInfo info)
        {
            var result = new AdResult
            {
                Success = false,
                Format = AdFormat.AppOpen,
                PlacementId = adUnitId,
                Network = info?.NetworkName,
                ErrorMessage = err.Message,
            };
            RaiseShowFailed(result);
            m_AppOpenTcs?.TrySetResult(result);
        }

        /// <summary>
        /// 开屏点击回调，上报点击打点。
        /// </summary>
        /// <param name="adUnitId">广告位 ID。</param>
        /// <param name="info">广告信息。</param>
        private void OnAppOpenClicked(string adUnitId, MaxSdkBase.AdInfo info)
        {
            TrackAdClick(AdFormat.AppOpen, adUnitId);
        }

        /// <summary>
        /// 开屏关闭回调，触发 RaiseAdClosed 并结束 Tcs。
        /// </summary>
        /// <param name="adUnitId">广告位 ID。</param>
        /// <param name="info">广告信息。</param>
        private void OnAppOpenHidden(string adUnitId, MaxSdkBase.AdInfo info)
        {
            var result = new AdResult
            {
                Success = true,
                Format = AdFormat.AppOpen,
                PlacementId = adUnitId,
                Network = info.NetworkName,
                Revenue = info.Revenue,
                Currency = "USD",
            };
            RaiseAdClosed(result);
            m_AppOpenTcs?.TrySetResult(result);
        }

        /// <summary>
        /// 开屏收益回调，触发 RaiseRevenue。
        /// </summary>
        /// <param name="adUnitId">广告位 ID。</param>
        /// <param name="info">广告信息。</param>
        private void OnAppOpenRevenuePaid(string adUnitId, MaxSdkBase.AdInfo info)
        {
            RaiseRevenue(new AdEvent
            {
                Format = AdFormat.AppOpen,
                PlacementId = adUnitId,
                Network = info.NetworkName,
                Revenue = info.Revenue,
                Currency = "USD",
                Precision = info.RevenuePrecision,
            });
            TrackMaxRevenue(AdFormat.AppOpen, adUnitId, info);
        }
    }
}
