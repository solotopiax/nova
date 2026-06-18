/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MobileRestoreService.cs
 * author:    yingzheng
 * created:   2026/5/25
 * descrip:   IAP 5.x Restore 流程：RestoreTransactions → CheckEntitlement → 分发验单
 ***************************************************************/

using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NovaFramework.SDK.IAP.Runtime;
using NovaFramework.Runtime;
using UnityEngine.Purchasing;

namespace NovaFramework.SDK.IAP.Mobile.Runtime
{
    /// <summary>
    /// Restore 流程协调服务（Unity IAP 5.x）。
    /// 流程：Controller.RestoreTransactions → 为订阅/非消耗品逐个 CheckEntitlement
    /// → OnCheckEntitlement 收集全部结果后，批量触发订阅验单和非消耗品验单。
    /// </summary>
    internal sealed partial class MobileRestoreService
    {
        /// <summary>
        /// 服务容器，持有共享外部依赖与其他服务引用。
        /// </summary>
        private readonly MobileServiceHub m_Hub;

        /// <summary>
        /// 构造 MobileRestoreService。
        /// </summary>
        /// <param name="hub">服务容器，持有共享外部依赖与其他服务引用。</param>
        internal MobileRestoreService(MobileServiceHub hub)
        {
            m_Hub = hub;
        }

        /// <summary>
        /// 异步执行 Restore 流程，返回本次恢复到的历史订单结果列表。
        /// </summary>
        /// <param name="ct">取消令牌。</param>
        /// <returns>历史订单结果列表。</returns>
        internal async UniTask<IReadOnlyList<IAPResult>> RestoreAsync(CancellationToken ct)
        {
            if (!m_Hub.InitService.IsReady)
            {
                return new List<IAPResult>();
            }

            if (string.IsNullOrEmpty(m_Hub.Store?.GameUID))
            {
                Log.Debug(LogTag.IAPMobile, "账号未登录，跳过恢复流程。");
                return new List<IAPResult>();
            }

            if (m_IsInRestore)
            {
                Log.Warning(LogTag.IAPMobile, "恢复流程正在进行中，拒绝重复发起。");
                return new List<IAPResult>();
            }

            m_IsInRestore = true;
            m_RestoreCoordinator.Reset();
            m_SubscriptionResults = new List<IAPResult>();
            m_NonConsumeResults = new List<IAPResult>();
            var restoreTcs = new UniTaskCompletionSource<IReadOnlyList<IAPResult>>();
            m_RestoreTcs = restoreTcs;

            using var reg = ct.Register(() =>
            {
                if (m_RestoreTcs == restoreTcs)
                {
                    FinishRestore();
                }
            });

            m_Hub.ExtendedService.RestoreTransactions((success, errorInfo) =>
            {
                // RestoreTransactions 仅触发平台侧补全逻辑，success=false 也继续走 CheckEntitlement
                // 避免网络抖动导致整个 Restore 流程中断
                if (!success)
                {
                    Log.Warning(LogTag.IAPMobile, $"平台恢复交易返回失败，详情={errorInfo}");
                }

                StartCheckEntitlements();
            });

            return await restoreTcs.Task;
        }

        /// <summary>
        /// 商品拉取成功后尽早触发平台恢复交易，只唤起平台侧订单补全，不在此处做权益验单。
        /// </summary>
        internal void RequestPlatformRestoreAfterProductsFetched()
        {
            if (m_HasRequestedProductFetchedRestore)
            {
                return;
            }

            if (!m_Hub.InitService.IsReady || m_Hub.ExtendedService?.IsAttached != true)
            {
                return;
            }

            m_HasRequestedProductFetchedRestore = true;
            Log.Debug(LogTag.IAPMobile, "商品拉取成功后触发平台恢复交易。");
            m_Hub.ExtendedService.RestoreTransactions((success, errorInfo) =>
            {
                if (!success)
                {
                    Log.Warning(LogTag.IAPMobile, $"商品拉取后的平台恢复交易返回失败，详情={errorInfo}");
                }
            });
        }

        /// <summary>
        /// 登录后补单扫描末尾刷新订阅和非消耗品权益，不重复触发平台 RestoreTransactions。
        /// </summary>
        /// <param name="ct">取消令牌。</param>
        /// <returns>权益刷新产生的恢复结果列表。</returns>
        internal async UniTask<IReadOnlyList<IAPResult>> RefreshEntitlementsAsync(CancellationToken ct)
        {
            if (!m_Hub.InitService.IsReady)
            {
                return new List<IAPResult>();
            }

            if (string.IsNullOrEmpty(m_Hub.Store?.GameUID))
            {
                Log.Debug(LogTag.IAPMobile, "账号未登录，跳过权益刷新。");
                return new List<IAPResult>();
            }

            if (m_IsInRestore)
            {
                Log.Warning(LogTag.IAPMobile, "恢复流程正在进行中，拒绝重复刷新权益。");
                return new List<IAPResult>();
            }

            m_IsInRestore = true;
            m_RestoreCoordinator.Reset();
            m_SubscriptionResults = new List<IAPResult>();
            m_NonConsumeResults = new List<IAPResult>();
            var restoreTcs = new UniTaskCompletionSource<IReadOnlyList<IAPResult>>();
            m_RestoreTcs = restoreTcs;

            using var reg = ct.Register(() =>
            {
                if (m_RestoreTcs == restoreTcs)
                {
                    FinishRestore();
                }
            });

            StartCheckEntitlements();
            return await restoreTcs.Task;
        }

        /// <summary>
        /// OnCheckEntitlement 回调入口：更新缓存状态，全部完成后触发分发。
        /// </summary>
        /// <param name="entitlement">权益检查结果。</param>
        internal void OnCheckEntitlement(Entitlement entitlement)
        {
            string productId = entitlement.Product?.definition.id;
            if (string.IsNullOrEmpty(productId))
            {
                return;
            }

            Log.Debug(LogTag.IAPMobile, $"权益查询回调：商品ID={productId}，状态={entitlement.Status}");

            if (m_Hub.ProductService.m_CheckEntitlements.TryGetValue(productId, out MobileCheckEntitlementInfo info))
            {
                info.Status = (int)entitlement.Status;
            }

            if (!m_Hub.ProductService.HasPendingCheckEntitlement())
            {
                ProcessAllEntitlementsCompleted();
            }
        }

        /// <summary>
        /// ValidationService 验单完成时通知 Restore 结果收集。
        /// </summary>
        /// <param name="result">验单结果。</param>
        /// <param name="productType">商品类型。</param>
        /// <param name="collectResult">是否把本次结果加入 Restore 事件结果列表；即使为 false 也会推进完成计数。</param>
        internal void NotifyValidationComplete(IAPResult result, ProductType productType, bool collectResult = true)
        {
            if (!m_IsInRestore)
            {
                return;
            }

            if (productType == ProductType.Subscription)
            {
                if (collectResult)
                {
                    // 收集订阅验单结果
                    m_SubscriptionResults?.Add(result);
                }
                // 全部订阅完成时返回 true
                if (m_RestoreCoordinator.MarkSubscriptionItemProcessed())
                {
                    TryFinishRestore();
                }
            }
            else
            {
                if (collectResult)
                {
                    // 收集非消耗品验单结果
                    m_NonConsumeResults?.Add(result);
                }
                // 全部非消耗品完成时返回 true
                if (m_RestoreCoordinator.MarkNonConsumableItemProcessed())
                {
                    TryFinishRestore();
                }
            }
        }

        /// <summary>
        /// 处理 OnExistingPurchasesFetched：缓存已确认订单票据，PendingOrder 交由购买服务先验单后确认。
        /// </summary>
        /// <param name="existingOrders">历史订单集合。</param>
        internal void OnExistingPurchasesFetched(Orders existingOrders)
        {
            foreach (ConfirmedOrder order in existingOrders.ConfirmedOrders)
            {
                foreach (var cartItem in order.CartOrdered.Items())
                {
                    // 缓存已确认订单票据，后续恢复和验单可复用平台回传凭据。
                    m_Hub.ProductService.CacheReceipt(cartItem.Product.definition.id, order.Info.Receipt);
                }
            }

            foreach (PendingOrder order in existingOrders.PendingOrders)
            {
                Product product = m_Hub.ProductService.GetFirstProductInOrder(order);
                if (product == null)
                {
                    Log.Warning(LogTag.IAPMobile, $"平台已有购买拉取完成：待确认订单中未找到商品，订单号={order.Info.TransactionID}");
                    continue;
                }

                Log.Debug(LogTag.IAPMobile, $"平台已有购买拉取完成：待确认商品={product.definition.id}，等待服务端验单后确认。");
                // 待确认订单必须走购买服务，先解析票据并完成服务端验单，再确认平台订单。
                m_Hub.PurchaseService.OnPurchasePending(order);
            }
        }

        /// <summary>
        /// 处理平台已有购买拉取失败：只记录日志，保留本地/服务端补单兜底。
        /// </summary>
        /// <param name="failure">失败描述。</param>
        internal void OnExistingPurchasesFetchFailed(PurchasesFetchFailureDescription failure)
        {
            Log.Warning(LogTag.IAPMobile, $"平台已有购买拉取失败，原因={failure.FailureReason}，详情={failure.Message}");
        }

        /// <summary>
        /// 释放服务资源，若有进行中的 Restore 则强制结束。
        /// </summary>
        internal void Dispose()
        {
            if (m_IsInRestore)
            {
                FinishRestore();
            }
        }
    }
}
