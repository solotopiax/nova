/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AdPlugin.Visitors.cs
 * author:    yingzheng
 * created:   2026/5/13
 * descrip:   AdPlugin 字段与属性
 ***************************************************************/

using System.Collections.Generic;
using NovaFramework.Runtime;

namespace NovaFramework.SDK.AdPlugin.Runtime
{
    public sealed partial class AdPlugin
    {
        /// <summary>
        /// 插件友好名。
        /// </summary>
        public override string Name => "AdPlugin";

        /// <summary>
        /// 初始化优先级为 80；在现有收益打点插件之后初始化，确保广告渠道能缓存可用的打点实例。
        /// </summary>
        public override int Priority => 80;

        /// <summary>
        /// 广告国家码上次成功缓存的持久化分类名。
        /// </summary>
        private const string c_CountryCodePersistClassify = "AdCountryCode";

        /// <summary>
        /// 广告国家码上次成功缓存的持久化条目名。
        /// </summary>
        private const string c_CountryCodePersistItem = "LastSuccess";

        /// <summary>
        /// 未注入配置时等待广告国家码的默认超时时间。
        /// </summary>
        private const float c_DefaultCountryCodeWaitTimeoutSeconds = 5f;

        /// <summary>
        /// SDKManager 注入的广告运行时配置。
        /// </summary>
        private AdPluginConfig m_RuntimeConfig;

        /// <summary>
        /// 所有已注册渠道插件；OnInitializeAsync 从全量 ISDKPlugin 中过滤 IAdInternalPlugin 填充。
        /// </summary>
        private List<IAdInternalPlugin> m_ChannelPlugins;

        /// <summary>
        /// 最近一次 RequestAsync(AdFormat.Banner) 成功后记录的活跃 Banner 渠道。
        /// ShowBanner / HideBanner 等 Banner 控制方法委托给此渠道。
        /// </summary>
        private IAdInternalPlugin m_ActiveBannerChannel;

        /// <summary>
        /// 事件管理器引用，用于订阅/退订 SDKEventData.UserLogin。
        /// </summary>
        private IEventManager m_EventManager;

        /// <summary>
        /// AdPlugin 所有可观察事件的容器；字段全部 readonly，禁止外部替换实例。
        /// 在 OnDisposeAsync 中统一调用 Clear() 释放缓冲。
        /// </summary>
        public AdPluginEvents Events { get; } = new AdPluginEvents();
    }
}
