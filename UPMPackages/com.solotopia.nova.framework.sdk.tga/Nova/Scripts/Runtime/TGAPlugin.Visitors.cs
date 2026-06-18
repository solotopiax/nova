/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  TGAPlugin.Visitors.cs
 * author:    yingzheng
 * created:   2026/4/20
 * descrip:   TGAPlugin 字段、属性、常量定义
 ***************************************************************/

#if !UNITY_WEBGL
using System;
using System.Collections.Generic;
using NovaFramework.Runtime;

namespace NovaFramework.SDK.TGAPlugin.Runtime
{
    public sealed partial class TGAPlugin
    {
        /// <summary>
        /// 获取插件友好名称，用于诊断日志与 Inspector 显示。
        /// </summary>
        public override string Name => "TGA";

        /// <summary>
        /// 获取插件初始化优先级。
        /// </summary>
        public override int Priority => 20;

        /// <summary>
        /// 声明本插件所需的配置类型；SDKManager 据此从 IConfigManager 拉取 TGAPluginConfig 注入 OnInitializeAsync。
        /// </summary>
        protected override Type ConfigType => typeof(TGAPluginConfig);

        /// <summary>
        /// 当前登录的账号 ID，Logout 后置为 null。
        /// </summary>
        private string m_AccountId;

        /// <summary>
        /// 动态公共事件属性缓存，每次 SDK 回调时返回此字典的快照。
        /// 键为属性名，值为属性值；实例构造时即分配，早于 TDAnalytics.Init。
        /// </summary>
        private Dictionary<string, object> m_DynamicSuperProperties = new Dictionary<string, object>();

        /// <summary>
        /// 框架静态公共事件属性，初始化时一次性注入 SDK。
        /// </summary>
        private readonly Dictionary<string, object> m_FrameworkSuperProperties = new Dictionary<string, object>();

        /// <summary>
        /// 框架动态公共事件属性，每次 SDK 回调时取最新值与业务属性合并。
        /// </summary>
        private readonly Dictionary<string, object> m_FrameworkDynamicProperties = new Dictionary<string, object>();

        /// <summary>
        /// 框架 UserSet 属性，每次调用 UserSet 时与业务属性合并。
        /// </summary>
        private readonly Dictionary<string, object> m_FrameworkUserSetProperties = new Dictionary<string, object>();

        /// <summary>
        /// 框架 UserSetOnce 属性，每次调用 UserSetOnce 时与业务属性合并。
        /// </summary>
        private readonly Dictionary<string, object> m_FrameworkUserSetOnceProperties = new Dictionary<string, object>();

        /// <summary>
        /// 动态公共属性回调监听器，OnInitializeAsync 时动态创建并注册给 TDAnalytics；负责将 SDK 回调转发至 TGAPlugin。
        /// </summary>
        private TGADynamicSuperPropertyListener m_DynamicSuperPropertyListener;

        /// <summary>
        /// 事件管理器引用，用于订阅/退订 SDKEventData.UserLogin。
        /// </summary>
        private IEventManager m_EventManager;

        /// <summary>
        /// TGA 标识上报 NetService 实例；OnInitializeAsync 入口处由 Plugin 自行 new 出。
        /// </summary>
        private TGAReportNetService m_ReportNetService;

        /// <summary>
        /// 由 SDKManager 注入并在初始化期缓存的运行时配置；事件回调（如 OnUserLogin）需读取协议名等字段时使用。
        /// </summary>
        private TGAPluginConfig m_RuntimeConfig;
    }
}
#endif
