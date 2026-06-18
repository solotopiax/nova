/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AppsFlyerPlugin.Visitors.cs
 * author:    yingzheng
 * created:   2026/4/20
 * descrip:   AppsFlyerPlugin字段、属性、常量定义
 ***************************************************************/

#if !UNITY_WEBGL
using System;
using System.Collections.Generic;
using NovaFramework.Runtime;

namespace NovaFramework.SDK.AppsFlyerPlugin.Runtime
{
    public sealed partial class AppsFlyerPlugin
    {
        /// <summary>
        /// 获取插件友好名称，用于诊断日志与 Inspector 显示。
        /// </summary>
        public override string Name => "AppsFlyer";

        /// <summary>
        /// 获取插件初始化优先级。
        /// </summary>
        public override int Priority => 90;

         /// <summary>
        /// 声明本插件所需的配置类型；SDKManager 据此从 IConfigManager 拉取 AppsFlyerPluginConfig 注入 OnInitializeAsync。
        /// </summary>
        protected override Type ConfigType => typeof(AppsFlyerPluginConfig);

        /// <summary>
        /// 归因数据缓存，由onConversionDataSuccess回调填充。
        /// </summary>
        private Dictionary<string, object> m_ConversionData;

        /// <summary>
        /// 深度链接数据缓存，由ParseDeepLinkData方法填充。
        /// </summary>
        private Dictionary<string, object> m_DeepLinkData;

        /// <summary>
        /// 解析并缓存后的归因数据；由BuildAndPublishAttribution填充，非null时GetAttributionAsync立即返回。
        /// </summary>
        private AttributionData m_Attribution;

        /// <summary>
        /// IAttributionPlugin.OnAttributionResolved事件的内部委托链；归因数据就绪时触发。
        /// </summary>
        private Action<AttributionData> m_OnAttributionResolved;

        /// <summary>
        /// 挂载在独立GameObject上的AF转化回调监听器；由OnInitializeAsync创建并绑定。
        /// </summary>
        private AppsFlyerConversionListener m_ConversionListener;

        /// <summary>
        /// 事件管理器引用，用于订阅/退订 SDKEventData.UserLogin。
        /// </summary>
        private IEventManager m_EventManager;

        /// <summary>
        /// AppsFlyer 标识上报 NetService 实例；OnInitializeAsync 入口处由 Plugin 自行 new 出。
        /// </summary>
        private AppsFlyerReportNetService m_ReportNetService;

        /// <summary>
        /// 由 SDKManager 注入并在初始化期缓存的运行时配置；事件回调（如 OnUserLogin）需读取协议名等字段时使用。
        /// </summary>
        private AppsFlyerPluginConfig m_RuntimeConfig;
    }
}
#endif
