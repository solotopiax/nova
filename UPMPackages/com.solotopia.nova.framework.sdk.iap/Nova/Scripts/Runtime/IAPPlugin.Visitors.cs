/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  IAPPlugin.Visitors.cs
 * author:    yingzheng
 * created:   2026/5/20
 * descrip:   IAPPlugin 字段、属性、常量、静态变量
 ***************************************************************/

using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using NovaFramework.Runtime;

namespace NovaFramework.SDK.IAP.Runtime
{
    /// <summary>
    /// IAPPlugin 字段与属性。
    /// </summary>
    public sealed partial class IAPPlugin
    {

        /// <summary>
        /// 插件友好名，用于诊断日志与 Inspector 显示。
        /// </summary>
        public override string Name => "IAPPlugin";

        /// <summary>
        /// 初始化优先级，值越小越先初始化；IAP 优先级设为 20。
        /// </summary>
        public override int Priority => 20;

        /// <summary>
        /// 声明所需配置类型，SDKManager 按此类型从 IConfigManager 拉取并注入。
        /// </summary>
        protected override Type ConfigType => typeof(IAPPluginConfig);

        /// <summary>
        /// 当前已初始化的所有渠道 store 列表，PayAsync 时按 CanHandle 路由。
        /// </summary>
        private List<IIAPInternalStore> m_Stores;

        /// <summary>
        /// IAP store 运行时上下文，封装持久化、埋点、UI、网络四个跨模块依赖。
        /// </summary>
        private IIAPStoreContext m_StoreContext;

        /// <summary>
        /// store 类型到专属配置的路由表，由 BuildStoreConfigMap 构建，TryInitializeStoreAsync 消费。
        /// </summary>
        private Dictionary<IAPStoreType, IIAPStoreConfig> m_StoreConfigMap;

        /// <summary>
        /// 商品表运行期查询服务，由 OnInitializeAsync 阶段基于 IAPPluginConfig.Products 构建；
        /// 通过 ProductTable 属性对外暴露，OnDisposeAsync 置 null。
        /// </summary>
        private IIAPProductTable m_PurchasesTable;

        /// <summary>
        /// 商品表查询服务只读视图，暴露完整 IIAPProductTable 接口。
        /// 插件初始化前或商品表为空时返回 null。
        /// </summary>
        public IIAPProductTable ProductTable => m_PurchasesTable;
        
        /// <summary>
        /// IAP 事件容器，业务层通过此属性订阅支付相关通知。
        /// </summary>
        public IAPPluginEvents Events { get; } = new IAPPluginEvents();

        /// <summary>
        /// 框架事件管理器引用，用于订阅 SDKEventData.UserLogin，OnDisposeAsync 时注销。
        /// </summary>
        private IEventManager m_EventManager;

        /// <summary>
        /// 当前已同步到 IAP 插件的账号 UID。
        /// </summary>
        private string m_CurrentUserId;

        /// <summary>
        /// 条件未满足时缓存的延后执行事件。
        /// </summary>
        private readonly List<Func<UniTask>> m_EventCaches = new List<Func<UniTask>>();

        /// <summary>
        /// 是否正在回放缓存事件。
        /// </summary>
        private bool m_IsReplayingEventCaches;

    }
}
