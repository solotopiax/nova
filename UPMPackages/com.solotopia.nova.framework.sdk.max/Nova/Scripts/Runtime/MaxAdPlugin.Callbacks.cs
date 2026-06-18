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
        /// <summary>
        /// 注册各格式 MaxSdk 事件回调；由 OnSdkInitializedCallback 在初始化完成后调用。
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

            // 为每个广告位 ID 注册独立 AdUnit 状态机槽位
            foreach (var id in m_RVPlacementIds)
                RegisterAdUnits(AdFormat.Rewarded, new[] { new AdUnitOptions(id) });

            // SDK 回调只注册一次，通过 adUnitId 参数路由到对应 AdUnit
            MaxSdkCallbacks.Rewarded.OnAdLoadedEvent += OnRVLoaded;
            MaxSdkCallbacks.Rewarded.OnAdLoadFailedEvent += OnRVLoadFailed;
            MaxSdkCallbacks.Rewarded.OnAdDisplayedEvent += OnRVDisplayed;
            MaxSdkCallbacks.Rewarded.OnAdDisplayFailedEvent += OnRVDisplayFailed;
            MaxSdkCallbacks.Rewarded.OnAdClickedEvent += OnRVClicked;
            MaxSdkCallbacks.Rewarded.OnAdHiddenEvent += OnRVHidden;
            MaxSdkCallbacks.Rewarded.OnAdReceivedRewardEvent += OnRVReceivedReward;
            MaxSdkCallbacks.Rewarded.OnAdRevenuePaidEvent += OnRVRevenuePaid;
        }

        /// <summary>
        /// 注册插屏广告事件回调；m_InterPlacementIds 为空时跳过。
        /// 广告播放成功后先触发 OnAdDisplayedEvent，关闭后触发 OnAdHiddenEvent；
        /// 播放失败时 OnAdDisplayFailedEvent 必然触发，OnAdHiddenEvent 不一定触发。
        /// </summary>
        private void RegisterInterCallbacks()
        {
            if (m_InterPlacementIds == null || m_InterPlacementIds.Count == 0) return;

            foreach (var id in m_InterPlacementIds)
                RegisterAdUnits(AdFormat.Interstitial, new[] { new AdUnitOptions(id) });

            MaxSdkCallbacks.Interstitial.OnAdLoadedEvent += OnInterLoaded;
            MaxSdkCallbacks.Interstitial.OnAdLoadFailedEvent += OnInterLoadFailed;
            MaxSdkCallbacks.Interstitial.OnAdDisplayedEvent += OnInterDisplayed;
            MaxSdkCallbacks.Interstitial.OnAdDisplayFailedEvent += OnInterDisplayFailed;
            MaxSdkCallbacks.Interstitial.OnAdClickedEvent += OnInterClicked;
            MaxSdkCallbacks.Interstitial.OnAdHiddenEvent += OnInterHidden;
            MaxSdkCallbacks.Interstitial.OnAdRevenuePaidEvent += OnInterRevenuePaid;
        }

        /// <summary>
        /// 注册 Banner 广告事件回调；m_BannerPlacementIds 为空时跳过。
        /// Banner 无展示/关闭生命周期回调，通过 OnAdCollapsedEvent 感知收起状态。
        /// </summary>
        private void RegisterBannerCallbacks()
        {
            if (m_BannerPlacementIds == null || m_BannerPlacementIds.Count == 0) return;

            foreach (var id in m_BannerPlacementIds)
                RegisterAdUnits(AdFormat.Banner, new[] { new AdUnitOptions(id) });

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

            foreach (var id in m_AppOpenPlacementIds)
                RegisterAdUnits(AdFormat.AppOpen, new[] { new AdUnitOptions(id) });

            MaxSdkCallbacks.AppOpen.OnAdLoadedEvent += OnAppOpenLoaded;
            MaxSdkCallbacks.AppOpen.OnAdLoadFailedEvent += OnAppOpenLoadFailed;
            MaxSdkCallbacks.AppOpen.OnAdDisplayedEvent += OnAppOpenDisplayed;
            MaxSdkCallbacks.AppOpen.OnAdDisplayFailedEvent += OnAppOpenDisplayFailed;
            MaxSdkCallbacks.AppOpen.OnAdClickedEvent += OnAppOpenClicked;
            MaxSdkCallbacks.AppOpen.OnAdHiddenEvent += OnAppOpenHidden;
            MaxSdkCallbacks.AppOpen.OnAdRevenuePaidEvent += OnAppOpenRevenuePaid;
        }

        /// <summary>
        /// 反注册所有 MaxSdk 广告事件回调；由 DisposeChannelSDKAsync 调用，防止资源泄露。
        /// 与注册侧对称，同样按格式分组，仅当 placementId 有效时才执行反注册。
        /// </summary>
        protected override UniTask DisposeChannelSDKAsync(CancellationToken ct)
        {
            UnregisterRVCallbacks();
            UnregisterInterCallbacks();
            UnregisterBannerCallbacks();
            UnregisterAppOpenCallbacks();
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
        }
    }
}
