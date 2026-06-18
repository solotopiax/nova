/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  FirebasePlugin.Visitors.cs
 * author:    yingzheng
 * created:   2026/4/21
 * descrip:   FirebasePlugin字段、属性、常量定义
 ***************************************************************/

#if !UNITY_WEBGL
using System;
using System.Collections.Generic;
using NovaFramework.Runtime;

namespace NovaFramework.SDK.FirebasePlugin.Runtime
{
    public sealed partial class FirebasePlugin
    {
        /// <summary>
        /// 获取 SDK 友好名称。
        /// </summary>
        public override string Name => "Firebase";

        /// <summary>
        /// 获取插件初始化优先级。
        /// </summary>
        public override int Priority => 110;

        /// <summary>
        /// 声明本插件所需的配置类型；SDKManager 据此从 IConfigManager 拉取 FirebasePluginConfig 注入 OnInitializeAsync。
        /// </summary>
        protected override Type ConfigType => typeof(FirebasePluginConfig);

        /// <summary>
        /// SDK 初始化是否已完成标志。
        /// </summary>
        private bool m_InitOver;
        /// <summary>
        /// 当前 SDK 是否已完成初始化。
        /// </summary>
        public bool IsInitialized => m_InitOver;

        /// <summary>
        /// Firebase Cloud Messaging 收到的推送 Token 缓存。
        /// </summary>
        private string m_TokenReceived = string.Empty;

        /// <summary>
        /// Firebase Analytics 分析实例 ID 缓存。
        /// </summary>
        private string m_AnalyticsInstanceId = string.Empty;

        /// <summary>
        /// 本次启动是否由推送点击触发（冷启动）标志。
        /// </summary>
        private bool m_IsNotificationLaunch;
        /// <summary>
        /// 获取本次启动是否由推送点击触发（冷启动）。
        /// </summary>
        public bool IsNotificationLaunch => m_IsNotificationLaunch;

        /// <summary>
        /// 本次运行时已处理的推送消息 ID 集合，用于去重。
        /// </summary>
        private List<string> m_RuntimeReceivedMessageIDs = new List<string>();

        /// <summary>
        /// IPushPlugin.OnTokenRefreshed 事件委托链，Token 刷新时在主线程触发。
        /// </summary>
        private Action<PushToken> m_OnTokenRefreshed;

        /// <summary>
        /// 事件管理器引用，用于订阅/退订 SDKEventData.UserLogin。
        /// </summary>
        private IEventManager m_EventManager;

        /// <summary>
        /// Firebase 标识上报 NetService 实例；OnInitializeAsync 入口处由 Plugin 自行 new 出。
        /// </summary>
        private FirebaseReportNetService m_ReportNetService;

        /// <summary>
        /// 由 SDKManager 注入并在初始化期缓存的运行时配置；事件回调（如 OnUserLogin）需读取协议名等字段时使用。
        /// </summary>
        private FirebasePluginConfig m_RuntimeConfig;
    }
}
#endif
