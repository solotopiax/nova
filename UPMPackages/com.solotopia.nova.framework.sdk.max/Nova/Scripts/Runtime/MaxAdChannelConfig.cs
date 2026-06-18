/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MaxAdChannelConfig.cs
 * author:    yingzheng
 * created:   2026/5/14
 * descrip:   AppLovin MAX 渠道配置，存储 AppKey 与各广告位 ID
 ***************************************************************/

using System;
using System.Collections.Generic;
using NovaFramework.Runtime;
using NovaFramework.SDK.AdPlugin.Runtime;
using UnityEngine;

namespace NovaFramework.SDK.MaxAdPlugin.Runtime
{
    /// <summary>
    /// AppLovin MAX 渠道配置。
    /// 实现 IAdChannelConfig，存储 MAX SDK 所需的 AppKey 与各广告位 ID。
    /// 在 AdPluginConfig 的渠道配置列表中以 [SerializeReference] 多态存储。
    /// </summary>
    [Serializable]
    public sealed class MaxAdChannelConfig : IAdChannelConfig
    {
        /// <summary>
        /// 渠道类型标识。
        /// </summary>
        public AdChannelType Channel => AdChannelType.MAX;

        /// <summary>
        /// 对应的渠道插件类型，AdPlugin 用于反射创建实例。
        /// </summary>
        public Type PluginType => typeof(MaxAdPlugin);

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
        /// AppLovin MAX SDK AppKey。
        /// </summary>
        [SerializeField, Tooltip("AppLovin MAX SDK AppKey。")]
        private string m_AppKey;
        /// <summary>
        /// AppLovin MAX SDK AppKey 只读属性。
        /// </summary>
        public string AppKey => m_AppKey;

        /// <summary>
        /// AdMob Android App ID（构建时由 MaxAdPluginBuildProcessor 写入 AppLovinSettings.AdMobAndroidAppId）。
        /// </summary>
        [SerializeField, Tooltip("AdMob Android App ID，构建时写入 AppLovinSettings.AdMobAndroidAppId。")]
        private string m_AdMobAppIdAndroid;
        /// <summary>
        /// AdMob Android App ID 只读属性。
        /// </summary>
        public string AdMobAppIdAndroid => m_AdMobAppIdAndroid;

        /// <summary>
        /// AdMob iOS App ID（构建时由 MaxAdPluginBuildProcessor 写入 AppLovinSettings.AdMobIosAppId）。
        /// </summary>
        [SerializeField, Tooltip("AdMob iOS App ID，构建时写入 AppLovinSettings.AdMobIosAppId。")]
        private string m_AdMobAppIdIOS;
        /// <summary>
        /// AdMob iOS App ID 只读属性。
        /// </summary>
        public string AdMobAppIdIOS => m_AdMobAppIdIOS;

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

        /// <summary>
        /// 是否开启 MAX SDK 详细日志输出；正式发布时建议关闭。
        /// </summary>
        [SerializeField, Tooltip("是否开启 MAX SDK 详细日志，正式发布建议关闭。")]
        private bool m_LogEnable;
        /// <summary>
        /// 是否开启 MAX SDK 详细日志的只读属性。
        /// </summary>
        public bool LogEnable => m_LogEnable;

        /// <summary>
        /// 是否开启 MAX Creative Debugger；正式发布时建议关闭。
        /// </summary>
        [SerializeField, Tooltip("是否开启 MAX Creative Debugger，正式发布建议关闭。")]
        private bool m_CreativeDebuggerEnabled;
        /// <summary>
        /// 是否开启 MAX Creative Debugger 的只读属性。
        /// </summary>
        public bool CreativeDebuggerEnabled => m_CreativeDebuggerEnabled;

        /// <summary>
        /// 是否在初始化成功后显示 MAX 调解调试器界面；测试环境建议开启，正式发布时关闭。
        /// </summary>
        [SerializeField, Tooltip("是否在初始化完成后显示 MAX Mediation Debugger 界面，测试时建议开启。")]
        private bool m_MediationDebuggerEnabled;
        /// <summary>
        /// 是否显示 MAX 调解调试器界面的只读属性。
        /// </summary>
        public bool MediationDebuggerEnabled => m_MediationDebuggerEnabled;

    }
}
