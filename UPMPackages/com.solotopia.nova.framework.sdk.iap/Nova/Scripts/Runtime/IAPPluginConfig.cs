/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  IAPPluginConfig.cs
 * author:    yingzheng
 * created:   2026/5/20
 * descrip:   IAPPlugin 配置类，仅保存 store 配置项及内联商品表数据
 ***************************************************************/

using System;
using System.Collections.Generic;
using NovaFramework.Runtime;
using UnityEngine;

namespace NovaFramework.SDK.IAP.Runtime
{
    /// <summary>
    /// IAP store 配置列表包装类。
    /// 以 [SerializeReference] List 多态持有所有 IIAPStoreConfig 实现，
    /// 配合 IAPStoreConfigListDrawer 在 Inspector 中提供增删与展开绘制能力。
    /// IAPPluginConfig 以 [SerializeField] 持有本类实例，避免直接暴露裸列表。
    /// </summary>
    [Serializable]
    public sealed class IAPStoreConfigList
    {
        /// <summary>
        /// 多态 store 配置条目列表；每个元素对应一个 IIAPStoreConfig 具体实现。
        /// 由 Inspector Drawer 管理增删，运行期只读。
        /// </summary>
        [SerializeReference, Tooltip("各支付渠道 store 的专属配置；通过 Inspector + 按钮选择具体实现类型添加。")]
        private List<IIAPStoreConfig> m_Items = new List<IIAPStoreConfig>();

        /// <summary>
        /// store 配置条目只读视图，供 IAPPlugin 启动期遍历构建路由字典。
        /// </summary>
        public IReadOnlyList<IIAPStoreConfig> Items => m_Items;
    }

    /// <summary>
    /// IAPPlugin 的配置类，只负责保存序列化配置数据。
    /// 商品查询、缓存和订阅组计算由运行期 IAPProductTableService 负责。
    /// </summary>
    [Serializable]
    public sealed class IAPPluginConfig : ISDKPluginConfig
    {
        /// <summary>
        /// 配置的显示名称，用于 Inspector 和诊断日志中标识此配置。
        /// </summary>
        public string DisplayName => "IAP 支付";

        /// <summary>
        /// 是否开启「始终支付成功」调试开关。
        /// 为 true 时各 store 跳过真实平台调用直接返回成功结果，仅用于开发测试。
        /// </summary>
        [SerializeField, Tooltip("开发调试开关：开启后各 store 跳过真实平台调用直接返回支付成功，上线前必须关闭。")]
        private bool m_EnableAlwaysPaySucceed;

        /// <summary>
        /// 是否开启「始终支付成功」调试开关的当前值。
        /// </summary>
        public bool EnableAlwaysPaySucceed => m_EnableAlwaysPaySucceed;

        /// <summary>
        /// 是否开启 IAP 日志输出。
        /// 开启后将输出 IAP 模块中的详细日志信息，上线前请关闭。
        /// </summary>
        [SerializeField, Tooltip("是否开启 IAP 模块详细日志，上线前请关闭。")]
        private bool m_EnableIAPLog;

        /// <summary>
        /// 是否开启 IAP 日志输出的当前值。
        /// </summary>
        public bool EnableIAPLog => m_EnableIAPLog;

        /// <summary>
        /// 首次支付订单校验失败后的最大重试次数。
        /// 达到最大次数后订单将写入补单队列等待下次重试。
        /// </summary>
        [SerializeField, Tooltip("首次验单失败后的最大重试次数，超出后订单写入补单队列，默认 3 次。")]
        private int m_RetryValidateMaxNum = 3;

        /// <summary>
        /// 最大重试次数的当前值。
        /// </summary>
        public int RetryValidateMaxNum => m_RetryValidateMaxNum;

        /// <summary>
        /// 游戏启动补单时是否跳过 Loading 页面。
        /// 为 true 时补单在后台静默执行，不阻塞 Loading 页面显示；为 false 时补单期间保持 Loading 页面展示。
        /// </summary>
        [SerializeField, Tooltip("游戏启动补单时是否跳过 Loading 页面：开启后补单在后台静默执行，不阻塞加载流程。")]
        private bool m_SkipLoadingForReplenish;

        /// <summary>
        /// 游戏启动补单时是否跳过 Loading 页面的当前值。
        /// </summary>
        public bool SkipLoadingForReplenish => m_SkipLoadingForReplenish;

        /// <summary>
        /// 支付期 Loading 进度面板 Prefab 路径（相对于 Resources/）。
        /// 与 Procedure 闪屏面板相同的配置方式：配置好后各 store 在支付期间据此懒加载并显示该面板。
        /// 为空时不显示默认 Loading 面板（保持回调未绑定，Loading 行为为空操作）。
        /// </summary>
        [SerializeField, Tooltip("支付期 Loading 进度面板 Prefab 路径（相对于 Resources/），为空时不显示默认 Loading 面板。")]
        private string m_LoadingPanelPrefab = "IAP/IAPLoadingPanel";

        /// <summary>
        /// 支付期 Loading 进度面板 Prefab 路径的当前值。
        /// </summary>
        public string LoadingPanelPrefab => m_LoadingPanelPrefab;

        /// <summary>
        /// 各支付渠道 store 的专属配置列表包装；Inspector Drawer 提供 + 按钮多态选择与删除能力。
        /// 子包装上才出现对应实现选项，卸载子包后历史条目变 Missing 引用，运行期跳过、由 StructureGuard 巡检清理。
        /// </summary>
        [SerializeField, Tooltip("各支付渠道 store 的专属配置；通过 Inspector + 按钮选择具体实现类型添加。")]
        private IAPStoreConfigList m_StoreConfigList = new IAPStoreConfigList();

        /// <summary>
        /// 各支付渠道 store 的专属配置只读视图，由 IAPPlugin 启动期遍历构建路由字典。
        /// </summary>
        public IReadOnlyList<IIAPStoreConfig> StoreConfigs => m_StoreConfigList.Items;

        /// <summary>
        /// 所有 store 共用的 IAP 商品条目列表，以 IAPProductList 包装类序列化在当前配置中。
        /// 为空时 IAP 功能整体不可用。
        /// </summary>
        [SerializeField, Tooltip("所有 store 共用的 IAP 商品条目列表，为空时 IAP 功能整体不可用。")]
        private IAPProductList m_Products = new IAPProductList();

        /// <summary>
        /// IAP 商品配置条目只读视图。
        /// </summary>
        public IReadOnlyList<IAPProductEntry> Products => m_Products.Items;

        /// <summary>
        /// 无参构造器，供反射与序列化系统使用。
        /// </summary>
        public IAPPluginConfig()
        {
        }
    }
}
