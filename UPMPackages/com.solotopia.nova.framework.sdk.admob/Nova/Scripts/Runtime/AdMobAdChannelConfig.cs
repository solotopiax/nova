/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AdMobAdChannelConfig.cs
 * author:    yingzheng
 * created:   2026/5/14
 * descrip:   Google AdMob 渠道配置，存储 AppId 与各广告位 ID
 ***************************************************************/

using System;
using System.Collections.Generic;
using NovaFramework.Runtime;
using NovaFramework.SDK.AdPlugin.Runtime;
using UnityEngine;

namespace NovaFramework.SDK.AdMobAdPlugin.Runtime
{
    /// <summary>
    /// Google AdMob 渠道配置。
    /// 实现 IAdChannelConfig，存储 AdMob SDK 所需的 AppId 与各广告位 ID。
    /// 在 AdPluginConfig 的渠道配置列表中以 [SerializeReference] 多态存储。
    /// </summary>
    [Serializable]
    public sealed class AdMobAdChannelConfig : IAdChannelConfig
    {
        /// <summary>
        /// 渠道类型标识。
        /// </summary>
        public AdChannelType Channel => AdChannelType.AdMob;

        /// <summary>
        /// 对应的渠道插件类型，AdPlugin 用于反射创建实例。
        /// </summary>
        public Type PluginType => typeof(AdMobAdPlugin);

        /// <summary>
        /// 是否启用此渠道。
        /// </summary>
        [SerializeField, Tooltip("是否开启此渠道。")]
        private bool m_Enabled = true;
        /// <summary>
        /// 是否启用此渠道的只读属性。
        /// </summary>
        public bool Enabled => m_Enabled;

        /// <summary>
        /// Android 平台 App ID。
        /// </summary>
        [SerializeField, Tooltip("Android 平台 App ID，格式 ca-app-pub-XXXXXXXXXXXXXXXX~XXXXXXXXXX。")]
        private string m_AppIdAndroid;
        /// <summary>
        /// Android 平台 App ID 只读属性。
        /// </summary>
        public string AppIdAndroid => m_AppIdAndroid;

        /// <summary>
        /// iOS 平台 App ID。
        /// </summary>
        [SerializeField, Tooltip("iOS 平台 App ID，格式 ca-app-pub-XXXXXXXXXXXXXXXX~XXXXXXXXXX。")]
        private string m_AppIdIOS;
        /// <summary>
        /// iOS 平台 App ID 只读属性。
        /// </summary>
        public string AppIdIOS => m_AppIdIOS;

        /// <summary>
        /// 激励视频广告位 ID 列表；空列表表示不启用激励视频。
        /// </summary>
        [SerializeField, Tooltip("激励视频广告位 ID 列表，支持多个并发请求。")]
        private List<string> m_RVPlacementIds = new List<string>();
        /// <summary>
        /// 激励视频广告位 ID 列表只读属性。
        /// </summary>
        public IReadOnlyList<string> RVPlacementIds => m_RVPlacementIds;

        /// <summary>
        /// 插屏广告位 ID 列表；空列表表示不启用插屏。
        /// </summary>
        [SerializeField, Tooltip("插屏广告位 ID 列表，支持多个并发请求。")]
        private List<string> m_InterPlacementIds = new List<string>();
        /// <summary>
        /// 插屏广告位 ID 列表只读属性。
        /// </summary>
        public IReadOnlyList<string> InterPlacementIds => m_InterPlacementIds;

        /// <summary>
        /// Banner 广告位 ID 列表；空列表表示不启用 Banner。
        /// </summary>
        [SerializeField, Tooltip("Banner 广告位 ID 列表，支持多个并发请求。")]
        private List<string> m_BannerPlacementIds = new List<string>();
        /// <summary>
        /// Banner 广告位 ID 列表只读属性。
        /// </summary>
        public IReadOnlyList<string> BannerPlacementIds => m_BannerPlacementIds;

        /// <summary>
        /// AppOpen 广告位 ID 列表；空列表表示不启用开屏广告。
        /// </summary>
        [SerializeField, Tooltip("AppOpen 广告位 ID 列表，支持多个并发请求。")]
        private List<string> m_AppOpenPlacementIds = new List<string>();
        /// <summary>
        /// AppOpen 广告位 ID 列表只读属性。
        /// </summary>
        public IReadOnlyList<string> AppOpenPlacementIds => m_AppOpenPlacementIds;
    }
}
