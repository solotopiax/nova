/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MobileStoreService.cs
 * author:    yingzheng
 * created:   2026/5/28
 * descrip:   平台生命周期事件路由：StoreController 所有 On* 回调的统一入口
 ***************************************************************/

using System.Collections.Generic;
using NovaFramework.SDK.IAP.Runtime;
using UnityEngine.Purchasing;

namespace NovaFramework.SDK.IAP.Mobile.Runtime
{
    /// <summary>
    /// 平台生命周期事件路由服务。
    /// StoreController 所有 On* 回调的统一入口，职责是将每个事件路由到对应的业务 Service，
    /// 不在此处做业务决策。ExtendedService.RegisterStoreCallbacks / RegisterProductCallbacks
    /// 将回调注册到此服务的方法。
    /// </summary>
    internal sealed partial class MobileStoreService
    {
        /// <summary>
        /// 构造 MobileStoreService。
        /// </summary>
        /// <param name="hub">服务容器。</param>
        internal MobileStoreService(MobileServiceHub hub)
        {
            m_Hub = hub;
        }

        /// <summary>
        /// 商店连接成功：注册商品级回调，通知 InitService 标记就绪并触发后台商品拉取。
        /// RegisterProductCallbacks 在此处调用，确保商品事件在连接成功后才开始监听。
        /// </summary>
        internal void OnStoreConnected()
        {
            // 连接成功后才注册商品级回调，避免连接前收到脏事件
            m_Hub.ExtendedService.RegisterProductCallbacks();
            m_Hub.InitService.OnStoreConnected();
        }

        /// <summary>
        /// 商店连接断开：通知 InitService 处理断连，初始化期间断连会触发失败流程。
        /// </summary>
        /// <param name="description">断连描述信息。</param>
        internal void OnStoreDisconnected(StoreConnectionFailureDescription description) => m_Hub.InitService.OnStoreDisconnected(description);

        /// <summary>
        /// 商品拉取成功：通知 InitService 记录平台商品拉取结果。
        /// 初始化就绪状态已在 OnStoreConnected 阶段完成，不再等待商品拉取。
        /// </summary>
        /// <param name="products">成功拉取的商品列表。</param>
        internal void OnProductsFetched(List<Product> products) => m_Hub.InitService.OnProductsFetched(products);

        /// <summary>
        /// 商品拉取失败：通知 InitService 记录不可用 SKU，不影响初始化结果。
        /// </summary>
        /// <param name="failure">包含失败商品列表和失败原因的对象。</param>
        internal void OnProductsFetchFailed(ProductFetchFailed failure) => m_Hub.InitService.OnProductsFetchFailed(failure);

        /// <summary>
        /// 历史购买拉取完成：路由给 RestoreService 处理缓存和自动确认 PendingOrder。
        /// </summary>
        /// <param name="existingOrders">历史订单集合（已确认、待处理、已延期）。</param>
        internal void OnExistingPurchasesFetched(Orders existingOrders) => m_Hub.RestoreService.OnExistingPurchasesFetched(existingOrders);

        /// <summary>
        /// 历史购买拉取失败：路由给 RestoreService 记录失败原因，等待后续补单或手动 Restore。
        /// </summary>
        /// <param name="failure">历史购买拉取失败描述。</param>
        internal void OnExistingPurchasesFetchFailed(PurchasesFetchFailureDescription failure) => m_Hub.RestoreService.OnExistingPurchasesFetchFailed(failure);

        /// <summary>
        /// 购买进入待确认状态：路由给 PurchaseService，验单成功后再确认平台订单。
        /// </summary>
        /// <param name="order">待确认订单。</param>
        internal void OnPurchasePending(PendingOrder order) => m_Hub.PurchaseService.OnPurchasePending(order);

        /// <summary>
        /// 购买被系统延期（家长控制等场景）：路由给 PurchaseService 记录日志。
        /// </summary>
        /// <param name="order">延期订单。</param>
        internal void OnPurchaseDeferred(DeferredOrder order) => m_Hub.PurchaseService.OnPurchaseDeferred(order);

        /// <summary>
        /// 购买确认结果（含 ConfirmedOrder / FailedOrder）：路由给 PurchaseService 处理票据缓存和验单入队。
        /// </summary>
        /// <param name="order">确认结果订单。</param>
        internal void OnPurchaseConfirmed(Order order) => m_Hub.PurchaseService.OnPurchaseConfirmed(order);

        /// <summary>
        /// 平台侧购买失败（非 ConfirmPurchase 失败）：路由给 PurchaseService 映射错误码并返回结果。
        /// </summary>
        /// <param name="order">失败订单。</param>
        internal void OnPurchaseFailed(FailedOrder order) => m_Hub.PurchaseService.OnPurchaseFailed(order);

        /// <summary>
        /// 权益检查结果：路由给 RestoreService 收集状态，全部完成后触发批量验单。
        /// </summary>
        /// <param name="entitlement">权益检查结果（含商品与 EntitlementStatus）。</param>
        internal void OnCheckEntitlement(Entitlement entitlement) => m_Hub.RestoreService.OnCheckEntitlement(entitlement);
    }
}
