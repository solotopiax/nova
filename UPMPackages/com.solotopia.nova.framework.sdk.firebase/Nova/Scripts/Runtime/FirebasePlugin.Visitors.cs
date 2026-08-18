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
using System.Threading;
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
        /// 获取插件初始化优先级；Firebase 优先级为 30。
        /// </summary>
        public override int Priority => 30;

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
        /// Firebase push task 调度器，集中管理缓存、计时器和 flush 状态。
        /// </summary>
        private FirebasePushTaskDispatcher m_PushTaskDispatcher;

        /// <summary>
        /// 由 SDKManager 注入并在初始化期缓存的运行时配置；事件回调（如 OnUserLogin）需读取协议名等字段时使用。
        /// </summary>
        private FirebasePluginConfig m_RuntimeConfig;

        /// <summary>
        /// 等待同步到 Firebase 的用户 ID。
        /// SetUserId 可能早于 Firebase 真正初始化，需缓存非空 UID，初始化完成后再补同步。
        /// </summary>
        private string m_PendingUserId;

        /// <summary>
        /// 默认 Topic 同步后台任务的取消令牌源。
        /// Firebase 释放时取消基础 Topic 和国家 Topic 的异步订阅流程，避免插件销毁后继续访问状态。
        /// </summary>
        private CancellationTokenSource m_DefaultTopicSyncCts;

        /// <summary>
        /// 基础默认 Topic 同步锁。
        /// 启动同步和 Localization 刷新同步都可能写 BaseState，需串行化读写存档和 Firebase 订阅差异。
        /// </summary>
        private readonly SemaphoreSlim m_DefaultBaseTopicSyncLock = new SemaphoreSlim(1, 1);

        /// <summary>
        /// 是否已订阅 Localization 刷新事件。
        /// </summary>
        private bool m_DefaultTopicLocalizationSubscribed;

        /// <summary>
        /// 应用是否已经进入过后台暂停状态。
        /// 用于区分真正的后台恢复与 Unity 启动期可能派发的 OnApplicationPause(false)。
        /// </summary>
        private bool m_WasApplicationPaused;
    }
}
#endif
