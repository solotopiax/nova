/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  IAPStoreContext.cs
 * author:    yingzheng
 * created:   2026/5/20
 * descrip:   IIAPStoreContext 具体实现，由插件层构造并注入各 store
 ***************************************************************/

using NovaFramework.Runtime;

namespace NovaFramework.SDK.IAP.Runtime
{
    /// <summary>
    /// IIAPStoreContext 的具体实现。
    /// 由 IAPPlugin 在初始化阶段构造，传入各 IIAPInternalStore.InitializeAsync。
    /// 封装持久化、埋点、UI、网络四个跨模块依赖，避免 store 层直接访问 Nova 框架入口。
    /// </summary>
    public sealed class IAPStoreContext : IIAPStoreContext
    {
        /// <summary>
        /// 持久化管理器，用于读写本地订单缓存及金币/代金券余额。
        /// </summary>
        public IPersistManager PersistManager { get; }

        /// <summary>
        /// 埋点插件，用于上报支付漏斗事件。可为 null。
        /// </summary>
        public ITrackPlugin TrackPlugin { get; }

        /// <summary>
        /// UI 管理器，用于控制支付相关 UI 元素。
        /// </summary>
        public IUIManager UIManager { get; }

        /// <summary>
        /// 网络管理器，用于服务端订单验证等网络请求。
        /// </summary>
        public INetworkManager NetworkManager { get; }

        /// <summary>
        /// 是否开启「始终支付成功」调试开关。
        /// </summary>
        public bool EnableAlwaysPaySucceed { get; }

        /// <summary>
        /// 当前运行时开发模式，用于支付打点 Debug 字段等运行环境判断。
        /// </summary>
        public DevelopMode DevelopMode { get; }

        /// <summary>
        /// 服务端订单验证的最大重试次数。
        /// </summary>
        public int RetryValidateMaxNum { get; }

        /// <summary>
        /// 游戏启动补单时是否跳过 Loading 页面。
        /// 各 store 在初始化成功后的补单阶段读此值，自行决定是否让 Loading 静默执行。
        /// </summary>
        public bool SkipLoadingForReplenish { get; }

        /// <summary>
        /// 支付期 Loading 进度面板 Prefab 路径（相对于 Resources/），由 IAPPluginConfig.LoadingPanelPrefab 注入。
        /// </summary>
        public string LoadingPanelPrefab { get; }

        /// <summary>
        /// IAP 事件桥接，由 IAPPlugin 在构造 IAPStoreContext 时注入自身。
        /// </summary>
        public IIAPStoreEventBridge EventBridge { get; }

        /// <summary>
        /// 构造 IAP store 运行时上下文。
        /// </summary>
        /// <param name="persistManager">持久化管理器。</param>
        /// <param name="trackPlugin">埋点插件，可为 null。</param>
        /// <param name="uiManager">UI 管理器。</param>
        /// <param name="networkManager">网络管理器。</param>
        /// <param name="enableAlwaysPaySucceed">是否开启始终支付成功的调试模式。</param>
        /// <param name="retryValidateMaxNum">验单失败时的最大重试次数。</param>
        /// <param name="skipLoadingForReplenish">游戏启动补单时是否跳过 Loading 页面。</param>
        /// <param name="loadingPanelPrefab">支付期 Loading 进度面板 Prefab 路径（相对于 Resources/）。</param>
        /// <param name="eventBridge">IAP 事件桥接，由 IAPPlugin 注入自身。</param>
        /// <param name="developMode">当前运行时开发模式。</param>
        public IAPStoreContext(IPersistManager persistManager, ITrackPlugin trackPlugin, IUIManager uiManager, INetworkManager networkManager, bool enableAlwaysPaySucceed, int retryValidateMaxNum, bool skipLoadingForReplenish, string loadingPanelPrefab, IIAPStoreEventBridge eventBridge, DevelopMode developMode = DevelopMode.Debug)
        {
            PersistManager = persistManager;
            TrackPlugin = trackPlugin;
            UIManager = uiManager;
            NetworkManager = networkManager;
            EnableAlwaysPaySucceed = enableAlwaysPaySucceed;
            DevelopMode = developMode;
            RetryValidateMaxNum = retryValidateMaxNum;
            SkipLoadingForReplenish = skipLoadingForReplenish;
            LoadingPanelPrefab = loadingPanelPrefab;
            EventBridge = eventBridge;
        }
    }
}
