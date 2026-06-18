/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MobileServiceHub.cs
 * author:    yingzheng
 * created:   2026/5/26
 * descrip:   MobileStore 服务容器，统一持有共享外部依赖与内部服务引用
 ***************************************************************/

using NovaFramework.SDK.IAP.Runtime;

namespace NovaFramework.SDK.IAP.Mobile.Runtime
{
    /// <summary>
    /// MobileStore 服务容器。
    /// 持有共享外部依赖（Context/Config/Table/Store）以及内部服务引用。
    /// 服务在 MobileStore.InitializeAsync 中按序创建并写入对应属性；
    /// 各服务在运行时（非构造期）通过 hub 属性互相访问，天然解决循环依赖。
    /// </summary>
    internal sealed class MobileServiceHub
    {
        /// <summary>
        /// IAP store 运行时上下文，提供持久化、网络、事件桥接等跨模块依赖。
        /// </summary>
        internal IIAPStoreContext Context { get; }

        /// <summary>
        /// MobileStore 专属配置（包名、AppId、AppsFlyer 参数等）。
        /// </summary>
        internal MobileStoreConfig Config { get; }

        /// <summary>
        /// IAP 商品配置表接口，所有服务共用。
        /// </summary>
        internal IIAPProductTable Table { get; }

        /// <summary>
        /// 所属 MobileStore，用于事件回调转发与日志。
        /// </summary>
        internal MobileStore Store { get; }

        /// <summary>
        /// 业务网络 Service，封装验单/查单协议发送。
        /// </summary>
        internal MobileIapNetService PayService { get; set; }

        /// <summary>
        /// Unity IAP 初始化服务。
        /// </summary>
        internal MobileInitService InitService { get; set; }

        /// <summary>
        /// 商品对象缓存与票据解析服务。
        /// </summary>
        internal MobileProductService ProductService { get; set; }

        /// <summary>
        /// 订阅到期时间持久化与倒计时服务。
        /// </summary>
        internal MobileSubscriptionService SubscriptionService { get; set; }

        /// <summary>
        /// 订单状态机与验单队列服务。
        /// </summary>
        internal MobileValidationService ValidationService { get; set; }

        /// <summary>
        /// Restore 流程协调服务。
        /// </summary>
        internal MobileRestoreService RestoreService { get; set; }

        /// <summary>
        /// 购买发起与平台回调处理服务。
        /// </summary>
        internal MobilePurchaseService PurchaseService { get; set; }

        /// <summary>
        /// StoreController 调用收口服务，封装所有平台调用入口。
        /// </summary>
        internal MobileExtendedService ExtendedService { get; set; }

        /// <summary>
        /// 平台生命周期事件路由服务，StoreController 所有 On* 回调的统一入口。
        /// </summary>
        internal MobileStoreService StoreService { get; set; }

        /// <summary>
        /// 构造 MobileServiceHub，写入共享外部依赖；服务引用由 MobileStore.InitializeAsync 填充。
        /// </summary>
        /// <param name="context">IAP store 运行时上下文。</param>
        /// <param name="config">MobileStore 专属配置。</param>
        /// <param name="table">IAP 商品配置表接口。</param>
        /// <param name="store">所属 MobileStore。</param>
        internal MobileServiceHub(IIAPStoreContext context, MobileStoreConfig config, IIAPProductTable table, MobileStore store)
        {
            Context = context;
            Config = config;
            Table = table;
            Store = store;
        }
    }
}
