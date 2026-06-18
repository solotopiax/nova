/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  IIAPStoreContext.cs
 * author:    yingzheng
 * created:   2026/5/20
 * descrip:   IAP store 运行时上下文，提供跨模块依赖注入
 ***************************************************************/

using NovaFramework.Runtime;

namespace NovaFramework.SDK.IAP.Runtime
{
    /// <summary>
    /// IAP store 运行时上下文接口。
    /// 在 IIAPInternalStore.InitializeAsync 阶段由插件层注入，
    /// 供各 store 实现访问持久化、埋点、UI、网络等跨模块能力。
    /// </summary>
    public interface IIAPStoreContext
    {
        /// <summary>
        /// 持久化管理器，用于读写本地订单缓存及金币/代金券余额。
        /// </summary>
        IPersistManager PersistManager { get; }

        /// <summary>
        /// 埋点插件，用于上报支付漏斗事件。
        /// 可为 null（未接入埋点时），store 实现需做 null 检查。
        /// </summary>
        ITrackPlugin TrackPlugin { get; }

        /// <summary>
        /// UI 管理器，用于控制支付中间页、加载遮罩等 UI 元素。
        /// </summary>
        IUIManager UIManager { get; }

        /// <summary>
        /// 网络管理器，用于服务端订单验证等网络请求。
        /// </summary>
        INetworkManager NetworkManager { get; }

        /// <summary>
        /// 是否开启「始终支付成功」调试开关。
        /// 为 true 时各 store 应跳过真实平台调用直接返回成功结果，仅用于开发测试。
        /// </summary>
        bool EnableAlwaysPaySucceed { get; }

        /// <summary>
        /// 当前运行时开发模式，用于支付打点 Debug 字段等运行环境判断。
        /// </summary>
        DevelopMode DevelopMode { get; }

        /// <summary>
        /// 服务端订单验证的最大重试次数，由 IAPPluginConfig.RetryValidateMaxNum 注入。
        /// 各 store 在验单失败重试时统一读此值，避免在每个 StoreConfig 中重复定义。
        /// </summary>
        int RetryValidateMaxNum { get; }

        /// <summary>
        /// 游戏启动补单时是否跳过 Loading 页面，由 IAPPluginConfig.SkipLoadingForReplenish 注入。
        /// 为 true 时各 store 在初始化成功后的补单阶段应让 Loading 静默执行，不阻塞加载流程；
        /// 为 false 时补单期间各 store 自行通过 UIManager 维持 Loading 展示。具体如何控制 Loading 由各 store 自决。
        /// </summary>
        bool SkipLoadingForReplenish { get; }

        /// <summary>
        /// 支付期 Loading 进度面板 Prefab 路径（相对于 Resources/），由 IAPPluginConfig.LoadingPanelPrefab 注入。
        /// IAPStoreBase 在初始化阶段据此创建默认 Loading 呈现器并绑定显隐回调；为空时不绑定默认面板。
        /// </summary>
        string LoadingPanelPrefab { get; }

        /// <summary>
        /// IAP 事件桥接，各 store 服务完成核心操作后通过此接口上报结果。
        /// 由 IAPPlugin 在构造 IAPStoreContext 时注入自身。
        /// </summary>
        IIAPStoreEventBridge EventBridge { get; }
    }
}
