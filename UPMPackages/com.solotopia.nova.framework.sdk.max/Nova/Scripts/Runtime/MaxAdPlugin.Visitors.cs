/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MaxAdPlugin.Visitors.cs
 * author:    yingzheng
 * created:   2026/5/15
 * descrip:   MaxAdPlugin 私有字段
 ***************************************************************/

using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using NovaFramework.Runtime;
using NovaFramework.SDK.AdPlugin.Runtime;

namespace NovaFramework.SDK.MaxAdPlugin.Runtime
{
    public sealed partial class MaxAdPlugin
    {

        /// <summary>
        /// 插件唯一名称。
        /// </summary>
        public override string Name => "Max";

        /// <summary>
        /// 当前渠道枚举标识，返回 AppLovin MAX。
        /// </summary>
        public override AdChannelType Channel => AdChannelType.MAX;
#if NOVA_APPLOVIN_MAX

        /// <summary>
        /// 激励视频广告位 ID 列表，InitChannelSDKAsync 从 MaxAdChannelConfig 缓存。
        /// </summary>
        private IReadOnlyList<string> m_RVPlacementIds;

        /// <summary>
        /// 插屏广告位 ID 列表，InitChannelSDKAsync 从 MaxAdChannelConfig 缓存。
        /// </summary>
        private IReadOnlyList<string> m_InterPlacementIds;

        /// <summary>
        /// Banner 广告位 ID 列表，InitChannelSDKAsync 从 MaxAdChannelConfig 缓存。
        /// </summary>
        private IReadOnlyList<string> m_BannerPlacementIds;
        /// <summary>
        /// Banner 操作用的首个广告位 ID；列表为空时返回 null。
        /// </summary>
        private string BannerPlacementId => m_BannerPlacementIds?.Count > 0 ? m_BannerPlacementIds[0] : null;

        /// <summary>
        /// Banner 自动刷新间隔，初始化时从 MaxAdChannelConfig 缓存，单位为秒。
        /// </summary>
        private int m_BannerAutoRefreshIntervalSeconds = 10;

        /// <summary>
        /// AppOpen 广告位 ID 列表，InitChannelSDKAsync 从 MaxAdChannelConfig 缓存。
        /// </summary>
        private IReadOnlyList<string> m_AppOpenPlacementIds;

        /// <summary>
        /// 激励视频展示挂起句柄；OnRVHidden / OnRVDisplayFailed 时 TrySetResult。
        /// </summary>
        private UniTaskCompletionSource<AdResult> m_RVTcs;

        /// <summary>
        /// 插屏展示挂起句柄；OnInterHidden / OnInterDisplayFailed 时 TrySetResult。
        /// </summary>
        private UniTaskCompletionSource<AdResult> m_InterTcs;

        /// <summary>
        /// AppOpen 展示挂起句柄；OnAppOpenHidden / OnAppOpenDisplayFailed 时 TrySetResult。
        /// </summary>
        private UniTaskCompletionSource<AdResult> m_AppOpenTcs;

        /// <summary>
        /// RV 奖励标记：OnRVReceivedReward 置 true，OnRVHidden 读取后清零。
        /// </summary>
        private bool m_RVRewarded;

        /// <summary>
        /// Banner 当前停靠位置，默认 BottomCenter。
        /// </summary>
        private MaxSdkBase.AdViewPosition m_BannerPosition = MaxSdkBase.AdViewPosition.BottomCenter;

        /// <summary>
        /// 已创建 native Banner view 的广告位集合；Destroy 后移除，允许后续重新创建。
        /// </summary>
        private readonly HashSet<string> m_CreatedBannerPlacementIds = new HashSet<string>();

        /// <summary>
        /// 业务层当前是否期望 Banner 保持可见；用于加载失败后再次成功时恢复显示。
        /// </summary>
        private bool m_BannerDesiredVisible;

        /// <summary>
        /// MAX SDK 返回的国家代码，InitializedCallback 中赋值，用于调试和数据上报。
        /// </summary>
        private string m_CountryCode;

        /// <summary>
        /// MAX 初始化完成时缓存的用户是否已作出广告隐私授权决定。
        /// </summary>
        private bool m_IsUserConsentSet;

        /// <summary>
        /// MAX 初始化完成时缓存的用户广告隐私授权结果；仅在 m_IsUserConsentSet 为 true 时有明确语义。
        /// </summary>
        private bool m_HasUserConsent;

        /// <summary>
        /// MAX 初始化期间 Consent Flow 的业务等待信号；初始化完成、取消或失败时结束等待。
        /// </summary>
        private readonly UniTaskCompletionSource m_PrivacyFlowCompletionSource = new UniTaskCompletionSource();

        /// <summary>
        /// 收益回调即时打点用的变现插件引用，初始化阶段在主线程缓存。
        /// </summary>
        private IMonetizeTrackPlugin m_RevenueMonetizeTracker;

        /// <summary>
        /// 收益回调即时打点用的归因插件引用，初始化阶段在主线程缓存。
        /// </summary>
        private IAttributionPlugin m_RevenueAttributionTracker;

        /// <summary>
        /// 收益回调即时打点用的通用埋点插件引用，初始化阶段在主线程缓存。
        /// </summary>
        private ITrackPlugin m_RevenueEventTracker;
#endif

    }
}
