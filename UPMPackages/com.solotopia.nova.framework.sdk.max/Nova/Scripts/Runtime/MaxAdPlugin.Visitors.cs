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
        /// MAX SDK 返回的国家代码，InitializedCallback 中赋值，用于调试和数据上报。
        /// </summary>
        private string m_CountryCode;

    }
}
