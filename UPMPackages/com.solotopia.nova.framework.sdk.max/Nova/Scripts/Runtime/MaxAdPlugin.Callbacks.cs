/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MaxAdPlugin.Callbacks.cs
 * author:    yingzheng
 * created:   2026/5/15
 * descrip:   MaxSdk 全局事件回调注册与反注册
 ***************************************************************/

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
        /// 订阅各格式 MaxSdk 回调；计划注册完成后、SDK 初始化成功后调用，不创建组或槽位。
        /// 每种格式独立判断 placementId 是否有效，无效则跳过该格式的注册。
        /// </summary>
        private void RegisterCallbacks()
        {
            RegisterRVCallbacks();
            RegisterInterCallbacks();
            RegisterBannerCallbacks();
            RegisterAppOpenCallbacks();
        }

        /// <summary>
        /// 注册激励视频广告事件回调；m_RVPlacementIds 为空时跳过。
        /// 广告播放成功后先触发 OnAdDisplayedEvent，关闭后触发 OnAdHiddenEvent；
        /// 播放失败时 OnAdDisplayFailedEvent 必然触发，OnAdHiddenEvent 不一定触发。
        /// </summary>
        private void RegisterRVCallbacks()
        {
            if (m_RVPlacementIds == null || m_RVPlacementIds.Count == 0) return;

            // SDK 回调只注册一次，通过 adUnitId 参数路由到对应 AdUnit
            MaxSdkCallbacks.Rewarded.OnAdLoadedEvent += OnRVLoaded;
            MaxSdkCallbacks.Rewarded.OnAdLoadFailedEvent += OnRVLoadFailed;
            MaxSdkCallbacks.Rewarded.OnAdDisplayedEvent += OnRVDisplayed;
            MaxSdkCallbacks.Rewarded.OnAdDisplayFailedEvent += OnRVDisplayFailed;
            MaxSdkCallbacks.Rewarded.OnAdClickedEvent += OnRVClicked;
            MaxSdkCallbacks.Rewarded.OnAdHiddenEvent += OnRVHidden;
            MaxSdkCallbacks.Rewarded.OnAdReceivedRewardEvent += OnRVReceivedReward;
            MaxSdkCallbacks.Rewarded.OnAdRevenuePaidEvent += OnRVRevenuePaid;
            MaxSdkCallbacks.Rewarded.OnExpiredAdReloadedEvent += OnRVExpiredAdReloaded;
        }

        /// <summary>
        /// 注册插屏广告事件回调；m_InterPlacementIds 为空时跳过。
        /// 广告播放成功后先触发 OnAdDisplayedEvent，关闭后触发 OnAdHiddenEvent；
        /// 播放失败时 OnAdDisplayFailedEvent 必然触发，OnAdHiddenEvent 不一定触发。
        /// </summary>
        private void RegisterInterCallbacks()
        {
            if (m_InterPlacementIds == null || m_InterPlacementIds.Count == 0) return;

            MaxSdkCallbacks.Interstitial.OnAdLoadedEvent += OnInterLoaded;
            MaxSdkCallbacks.Interstitial.OnAdLoadFailedEvent += OnInterLoadFailed;
            MaxSdkCallbacks.Interstitial.OnAdDisplayedEvent += OnInterDisplayed;
            MaxSdkCallbacks.Interstitial.OnAdDisplayFailedEvent += OnInterDisplayFailed;
            MaxSdkCallbacks.Interstitial.OnAdClickedEvent += OnInterClicked;
            MaxSdkCallbacks.Interstitial.OnAdHiddenEvent += OnInterHidden;
            MaxSdkCallbacks.Interstitial.OnAdRevenuePaidEvent += OnInterRevenuePaid;
            MaxSdkCallbacks.Interstitial.OnExpiredAdReloadedEvent += OnInterExpiredAdReloaded;
        }

        /// <summary>
        /// 注册 Banner 广告事件回调；m_BannerPlacementIds 为空时跳过。
        /// Banner 无展示/关闭生命周期回调，通过 OnAdCollapsedEvent 感知收起状态。
        /// </summary>
        private void RegisterBannerCallbacks()
        {
            if (m_BannerPlacementIds == null || m_BannerPlacementIds.Count == 0) return;

            MaxSdkCallbacks.Banner.OnAdLoadedEvent += OnBannerLoaded;
            MaxSdkCallbacks.Banner.OnAdLoadFailedEvent += OnBannerLoadFailed;
            MaxSdkCallbacks.Banner.OnAdClickedEvent += OnBannerClicked;
            MaxSdkCallbacks.Banner.OnAdRevenuePaidEvent += OnBannerRevenuePaid;
            MaxSdkCallbacks.Banner.OnAdCollapsedEvent += OnBannerCollapsed;
        }

        /// <summary>
        /// 注册开屏广告事件回调；m_AppOpenPlacementIds 为空时跳过。
        /// 广告播放成功后先触发 OnAdDisplayedEvent，关闭后触发 OnAdHiddenEvent；
        /// 播放失败时 OnAdDisplayFailedEvent 必然触发，OnAdHiddenEvent 不一定触发。
        /// </summary>
        private void RegisterAppOpenCallbacks()
        {
            if (m_AppOpenPlacementIds == null || m_AppOpenPlacementIds.Count == 0) return;

            MaxSdkCallbacks.AppOpen.OnAdLoadedEvent += OnAppOpenLoaded;
            MaxSdkCallbacks.AppOpen.OnAdLoadFailedEvent += OnAppOpenLoadFailed;
            MaxSdkCallbacks.AppOpen.OnAdDisplayedEvent += OnAppOpenDisplayed;
            MaxSdkCallbacks.AppOpen.OnAdDisplayFailedEvent += OnAppOpenDisplayFailed;
            MaxSdkCallbacks.AppOpen.OnAdClickedEvent += OnAppOpenClicked;
            MaxSdkCallbacks.AppOpen.OnAdHiddenEvent += OnAppOpenHidden;
            MaxSdkCallbacks.AppOpen.OnAdRevenuePaidEvent += OnAppOpenRevenuePaid;
            MaxSdkCallbacks.AppOpen.OnExpiredAdReloadedEvent += OnAppOpenExpiredAdReloaded;
        }

        /// <summary>
        /// 反注册所有 MaxSdk 广告事件回调；由 DisposeChannelSDKAsync 调用，防止资源泄露。
        /// 结束隐私与原生展示等待并销毁本渠道创建的 Banner；已取消 ct 不跳过清理。
        /// 基类取消生命周期后，初始化方法通过自身 finally 解绑初始化事件。
        /// 清除国家代码与授权缓存，使已释放实例的查询恢复默认值。
        /// </summary>
        /// <param name="ct">调用方取消令牌；清理不因令牌已取消而跳过。</param>
        /// <returns>全部同步清理步骤执行后的完成任务。</returns>
        protected override UniTask DisposeChannelSDKAsync(CancellationToken ct)
        {
            m_IsSdkInitialized = false;
            m_PrivacyFlowCompletionSource.TrySetResult();
            // 先清空成员，再取消局部完成源，释放续体不会读取到旧展示句柄。
            var rvCompletion = m_RVTcs;
            var interCompletion = m_InterTcs;
            var appOpenCompletion = m_AppOpenTcs;
            m_RVTcs = null;
            m_InterTcs = null;
            m_AppOpenTcs = null;
            m_ActiveFullscreenPlacementId = null;
            m_RVRewarded = false;
            rvCompletion?.TrySetCanceled();
            interCompletion?.TrySetCanceled();
            appOpenCompletion?.TrySetCanceled();
            lock (m_FullscreenImpressionLock) m_FullscreenImpressions.Clear();
            ClearMaxRevenueTrackers();
            UnregisterRVCallbacks();
            UnregisterInterCallbacks();
            UnregisterBannerCallbacks();
            UnregisterAppOpenCallbacks();
            foreach (string placementId in m_CreatedBannerPlacementIds)
            {
                try { MaxSdk.DestroyBanner(placementId); }
                catch (System.Exception ex) { Log.Warning(LogTag.Max, $"MAX Banner 释放失败：placement={placementId}，{ex.Message}"); }
            }
            m_CreatedBannerPlacementIds.Clear();
            m_BannerDesiredVisible = false;
            m_CountryCode = null;
            m_IsUserConsentSet = false;
            m_HasUserConsent = false;
            return UniTask.CompletedTask;
        }


        /// <summary>
        /// 反注册激励视频广告事件回调；m_RVPlacementIds 为空时跳过。
        /// </summary>
        private void UnregisterRVCallbacks()
        {
            if (m_RVPlacementIds == null || m_RVPlacementIds.Count == 0) return;

            // 反注册激励视频加载成功回调
            MaxSdkCallbacks.Rewarded.OnAdLoadedEvent -= OnRVLoaded;
            // 反注册激励视频加载失败回调
            MaxSdkCallbacks.Rewarded.OnAdLoadFailedEvent -= OnRVLoadFailed;
            // 反注册激励视频展示成功回调
            MaxSdkCallbacks.Rewarded.OnAdDisplayedEvent -= OnRVDisplayed;
            // 反注册激励视频展示失败回调
            MaxSdkCallbacks.Rewarded.OnAdDisplayFailedEvent -= OnRVDisplayFailed;
            // 反注册激励视频点击回调
            MaxSdkCallbacks.Rewarded.OnAdClickedEvent -= OnRVClicked;
            // 反注册激励视频关闭回调
            MaxSdkCallbacks.Rewarded.OnAdHiddenEvent -= OnRVHidden;
            // 反注册激励视频奖励发放回调
            MaxSdkCallbacks.Rewarded.OnAdReceivedRewardEvent -= OnRVReceivedReward;
            // 反注册激励视频收益回调
            MaxSdkCallbacks.Rewarded.OnAdRevenuePaidEvent -= OnRVRevenuePaid;
            MaxSdkCallbacks.Rewarded.OnExpiredAdReloadedEvent -= OnRVExpiredAdReloaded;
        }

        /// <summary>
        /// 反注册插屏广告事件回调；m_InterPlacementIds 为空时跳过。
        /// </summary>
        private void UnregisterInterCallbacks()
        {
            if (m_InterPlacementIds == null || m_InterPlacementIds.Count == 0) return;

            // 反注册插屏广告加载成功回调
            MaxSdkCallbacks.Interstitial.OnAdLoadedEvent -= OnInterLoaded;
            // 反注册插屏广告加载失败回调
            MaxSdkCallbacks.Interstitial.OnAdLoadFailedEvent -= OnInterLoadFailed;
            // 反注册插屏广告展示成功回调
            MaxSdkCallbacks.Interstitial.OnAdDisplayedEvent -= OnInterDisplayed;
            // 反注册插屏广告展示失败回调
            MaxSdkCallbacks.Interstitial.OnAdDisplayFailedEvent -= OnInterDisplayFailed;
            // 反注册插屏广告点击回调
            MaxSdkCallbacks.Interstitial.OnAdClickedEvent -= OnInterClicked;
            // 反注册插屏广告关闭回调
            MaxSdkCallbacks.Interstitial.OnAdHiddenEvent -= OnInterHidden;
            // 反注册插屏广告收益回调
            MaxSdkCallbacks.Interstitial.OnAdRevenuePaidEvent -= OnInterRevenuePaid;
            MaxSdkCallbacks.Interstitial.OnExpiredAdReloadedEvent -= OnInterExpiredAdReloaded;
        }

        /// <summary>
        /// 反注册 Banner 广告事件回调；m_BannerPlacementIds 为空时跳过。
        /// </summary>
        private void UnregisterBannerCallbacks()
        {
            if (m_BannerPlacementIds == null || m_BannerPlacementIds.Count == 0) return;

            // 反注册 Banner 加载成功回调
            MaxSdkCallbacks.Banner.OnAdLoadedEvent -= OnBannerLoaded;
            // 反注册 Banner 加载失败回调
            MaxSdkCallbacks.Banner.OnAdLoadFailedEvent -= OnBannerLoadFailed;
            // 反注册 Banner 点击回调
            MaxSdkCallbacks.Banner.OnAdClickedEvent -= OnBannerClicked;
            // 反注册 Banner 收益回调
            MaxSdkCallbacks.Banner.OnAdRevenuePaidEvent -= OnBannerRevenuePaid;
            // 反注册 Banner 收起回调
            MaxSdkCallbacks.Banner.OnAdCollapsedEvent -= OnBannerCollapsed;
        }

        /// <summary>
        /// 反注册开屏广告事件回调；m_AppOpenPlacementIds 为空时跳过。
        /// </summary>
        private void UnregisterAppOpenCallbacks()
        {
            if (m_AppOpenPlacementIds == null || m_AppOpenPlacementIds.Count == 0) return;

            // 反注册开屏广告加载成功回调
            MaxSdkCallbacks.AppOpen.OnAdLoadedEvent -= OnAppOpenLoaded;
            // 反注册开屏广告加载失败回调
            MaxSdkCallbacks.AppOpen.OnAdLoadFailedEvent -= OnAppOpenLoadFailed;
            // 反注册开屏广告展示成功回调
            MaxSdkCallbacks.AppOpen.OnAdDisplayedEvent -= OnAppOpenDisplayed;
            // 反注册开屏广告展示失败回调
            MaxSdkCallbacks.AppOpen.OnAdDisplayFailedEvent -= OnAppOpenDisplayFailed;
            // 反注册开屏广告点击回调
            MaxSdkCallbacks.AppOpen.OnAdClickedEvent -= OnAppOpenClicked;
            // 反注册开屏广告关闭回调
            MaxSdkCallbacks.AppOpen.OnAdHiddenEvent -= OnAppOpenHidden;
            // 反注册开屏广告收益回调
            MaxSdkCallbacks.AppOpen.OnAdRevenuePaidEvent -= OnAppOpenRevenuePaid;
            MaxSdkCallbacks.AppOpen.OnExpiredAdReloadedEvent -= OnAppOpenExpiredAdReloaded;
        }
#endif
    }
}
