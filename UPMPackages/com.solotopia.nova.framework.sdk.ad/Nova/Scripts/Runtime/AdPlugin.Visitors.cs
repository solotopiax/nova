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
        /// 初始化优先级；值 50 高于框架默认值 100。
        /// </summary>
        public override int Priority => 50;

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
