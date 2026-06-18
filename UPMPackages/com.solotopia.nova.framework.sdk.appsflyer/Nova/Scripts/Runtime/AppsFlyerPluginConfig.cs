/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AppsFlyerPluginConfig.cs
 * author:    yingzheng
 * created:   2026/4/20
 * descrip:   AppsFlyer 插件运行期初始化配置；作为 ISDKPluginConfig 由 ConfigMasterSO
 *            静态配置，SDKManager 按 RequiredConfigType 自动注入给
 *            AppsFlyerPlugin.OnInitializeAsync。
 ***************************************************************/

#if !UNITY_WEBGL
using System;
using NovaFramework.Runtime;
using UnityEngine;

namespace NovaFramework.SDK.AppsFlyerPlugin.Runtime
{
    /// <summary>
    /// AppsFlyer 插件初始化所需数据：DevKey / LogEnable 来自 TbAppsFlyerConfigs，AppId 来自 TbCommonConfigs 的 MGAppID。
    /// 标注 [Serializable] 以便被 ConfigWindow SDKPluginScanner 扫描到，并可作为
    /// PlatformChannelEntry.SDKConfigsByMode 的 [SerializeReference] 条目持久化。
    /// 由 Editor 面板直接编辑字段值；Game 层亦可通过带参构造器手工生成。
    /// </summary>
    [Serializable]
    public sealed class AppsFlyerPluginConfig : ISDKPluginConfig
    {
        /// <summary>
        /// AppsFlyer Dev Key 序列化字段，对应 AFDevKey。
        /// </summary>
        [SerializeField, Tooltip("AppsFlyer Dev Key。填写 AppsFlyer 后台为当前应用提供的鉴权密钥。")]
        private string m_DevKey;

        /// <summary>
        /// 商店或平台应用 ID 序列化字段，对应 TbCommonConfigs.MGAppID。
        /// </summary>
        [SerializeField, Tooltip("应用商店 ID。填写当前平台商店中的应用 ID；iOS 使用 App Store App ID。")]
        private string m_AppId;

        /// <summary>
        /// 是否开启 AppsFlyer 调试日志序列化字段，对应 AFLogEnable。
        /// </summary>
        [SerializeField, Tooltip("是否输出 AppsFlyer 调试日志。排查问题时开启，正式发布建议关闭。")]
        private bool m_LogEnable;

        /// <summary>
        /// AF OneLink 自定义链接 Host 序列化字段，对应 AFOneLink；构建时注入 AndroidManifest intent-filter 与 iOS Associated Domains。
        /// </summary>
        [SerializeField, Tooltip("OneLink 回流域名。填写 AppsFlyer 后台配置的 OneLink Host，用于唤起 App 并接收深链回流。")]
        private string m_OneLinkHost;

        /// <summary>
        /// AF OneLink URI scheme 备选方案名称序列化字段，对应 AFOneLinkFallbackName；构建时注入 AndroidManifest intent-filter scheme 与 iOS CFBundleURLSchemes。
        /// </summary>
        [SerializeField, Tooltip("OneLink 备用唤起 scheme。填写为 App 配置的自定义 scheme 名称，用于部分场景下的回流唤起。")]
        private string m_OneLinkFallbackName;

        /// <summary>
        /// AF OneLink URI scheme 路径前缀序列化字段，对应 AFOneLinkPathPrefix；构建时注入 AndroidManifest intent-filter pathPrefix。
        /// </summary>
        [SerializeField, Tooltip("OneLink 回流路径前缀。填写与 OneLink 配置一致的 pathPrefix，用于匹配对应的唤起链接。")]
        private string m_OneLinkPathPrefix;

        /// <summary>
        /// 上报 AppsFlyer 标识协议 NetCmd 指令名序列化字段与属性。
        /// </summary>
        [SerializeField, Tooltip("用于向业务服务器上报 AppsFlyer 设备标识的协议名。填写 NetCmd 表中的名称，如 AppsFlyerReport。")]
        private string m_ReportCmdName = "AppsFlyerReport";

        /// <summary>
        /// AppsFlyer Dev Key，对应 AFDevKey。
        /// </summary>
        public string DevKey => m_DevKey;

        /// <summary>
        /// 商店或平台应用 ID，对应 TbCommonConfigs.MGAppID。
        /// </summary>
        public string AppId => m_AppId;

        /// <summary>
        /// 是否开启 AppsFlyer 调试日志，对应 AFLogEnable。
        /// </summary>
        public bool LogEnable => m_LogEnable;

        /// <summary>
        /// AF OneLink 自定义链接 Host，对应 AFOneLink。
        /// </summary>
        public string OneLinkHost => m_OneLinkHost;

        /// <summary>
        /// AF OneLink URI scheme 备选方案名称，对应 AFOneLinkFallbackName。
        /// </summary>
        public string OneLinkFallbackName => m_OneLinkFallbackName;

        /// <summary>
        /// AF OneLink URI scheme 路径前缀，对应 AFOneLinkPathPrefix。
        /// </summary>
        public string OneLinkPathPrefix => m_OneLinkPathPrefix;

        /// <summary>
        /// 上报 AppsFlyer 标识协议 NetCmd 指令名。
        /// </summary>
        public string ReportCmdName => m_ReportCmdName;

        /// <summary>
        /// ConfigWindow 左树展示的中文名称。
        /// </summary>
        public string DisplayName => "AppsFlyer 归因";

        /// <summary>
        /// 无参构造器；供 ConfigWindow SDKPluginScanner 通过 Activator 创建空实例使用。
        /// </summary>
        public AppsFlyerPluginConfig() { }

        /// <summary>
        /// 构造 AppsFlyerPluginConfig 实例。
        /// </summary>
        /// <param name="devKey">AppsFlyer Dev Key。</param>
        /// <param name="appId">商店或平台应用 ID。</param>
        /// <param name="logEnable">是否开启调试日志。</param>
        /// <param name="oneLinkHost">AF OneLink 自定义链接 Host。</param>
        /// <param name="oneLinkFallbackName">AF OneLink URI scheme 备选方案名称。</param>
        /// <param name="oneLinkPathPrefix">AF OneLink URI scheme 路径前缀。</param>
        public AppsFlyerPluginConfig(string devKey, string appId, bool logEnable = false, string oneLinkHost = "", string oneLinkFallbackName = "", string oneLinkPathPrefix = "")
        {
            m_DevKey = devKey;
            m_AppId = appId;
            m_LogEnable = logEnable;
            m_OneLinkHost = oneLinkHost;
            m_OneLinkFallbackName = oneLinkFallbackName;
            m_OneLinkPathPrefix = oneLinkPathPrefix;
        }
    }
}
#endif
