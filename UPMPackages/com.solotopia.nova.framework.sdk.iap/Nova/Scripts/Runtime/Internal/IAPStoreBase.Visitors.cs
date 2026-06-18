/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  IAPStoreBase.Visitors.cs
 * author:    yingzheng
 * created:   2026/5/20
 * descrip:   IAPStoreBase 字段、属性、常量
 ***************************************************************/

using System.Collections.Generic;

namespace NovaFramework.SDK.IAP.Runtime
{
    /// <summary>
    /// IAPStoreBase 字段与属性。
    /// </summary>
    public abstract partial class IAPStoreBase
    {
        /// <summary>
        /// 当前登录用户的唯一标识，用于存档 UID 分区及订单参数透传。
        /// 通过 SetUserId 写入，子类只读使用。
        /// </summary>
        protected string m_GameUID = string.Empty;

        /// <summary>
        /// 当前正在进行中的支付对应的配置表行 ID；0 = 空闲，同时起防重入作用。
        /// PayAsync 开始时写入 tableId，完成或失败后清零。
        /// </summary>
        protected long m_InPayTableId;
        /// <summary>
        /// 当前是否有支付进行中。
        /// </summary>
        protected bool IsInPay => m_InPayTableId != 0;

        /// <summary>
        /// 当前 store 是否处于启用状态；由 config.Enabled 初始化，可通过 SetEnabled 动态覆盖。
        /// </summary>
        private bool m_IsEnabled = true;
        /// <summary>
        /// 当前 store 是否处于启用状态。
        /// </summary>
        public bool IsEnabled => m_IsEnabled;

        /// <summary>
        /// 当前 store 是否已完成 InitializeAsync；config.Enabled=false 时懒初始化尚未触发则为 false。
        /// </summary>
        private bool m_IsInitialized;
        /// <summary>
        /// 当前 store 是否已完成 InitializeAsync。
        /// </summary>
        protected bool IsInitialized => m_IsInitialized;

        /// <summary>
        /// 平台标记为不可购买的商品 ID 集合。
        /// 在 store 初始化后由平台回调填充，PayAsync 前通过 IsUnavailableSku 过滤。
        /// </summary>
        private HashSet<string> m_UnavailableSkus;

        /// <summary>
        /// 引用计数式 Loading 管理器，控制支付期间 Loading 的显示与隐藏。
        /// 业务层通过 BindLoadingCallbacks 注入具体 UI 回调。
        /// </summary>
        protected readonly IAPLoadingGuard m_LoadingGuard = new IAPLoadingGuard();

        /// <summary>
        /// 默认 Loading 面板呈现器，按 Context.LoadingPanelPrefab 路径加载预制体并作为 m_LoadingGuard 的显隐回调；
        /// 未配置路径时为 null，Loading 行为保持空操作。
        /// </summary>
        private IAPLoadingPanelPresenter m_LoadingPresenter;

        /// <summary>
        /// 当前 store 持久化分类键，规则为 "iap_{StoreType.ToLowerInvariant()}"。
        /// 各 Store 共用此命名约定，避免散落字符串常量。
        /// </summary>
        protected string PersistClassify => "iap_" + StoreType.ToString().ToLowerInvariant();

        /// <summary>
        /// 当前账号下持久化容器使用的 item key，规则为 "data_{m_GameUID}"。
        /// m_GameUID 为空时使用 "data_" 前缀作为匿名占位档。
        /// </summary>
        protected string PersistItemKey => "data_" + (m_GameUID ?? string.Empty);
    }
}
