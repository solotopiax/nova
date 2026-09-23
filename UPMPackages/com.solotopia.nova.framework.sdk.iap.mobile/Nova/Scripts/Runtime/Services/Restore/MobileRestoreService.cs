/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MobileRestoreService.cs
 * author:    yingzheng
 * created:   2026/5/25
 * descrip:   IAP 5.x 手动 Restore 与权益刷新流程
 ***************************************************************/

using System;
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
    /// 手动 Restore 会先调用 Controller.RestoreTransactions；启动期和补单期只执行 CheckEntitlement 权益刷新。
    /// OnCheckEntitlement 收集全部结果后，批量触发订阅验单和非消耗品验单。
    /// </summary>
    internal sealed partial class MobileRestoreService : MobileLogOwner
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
                LogDebug("账号未登录，跳过恢复流程。");
                return new List<IAPResult>();
            }

            if (m_IsInRestore)
            {
                LogWarning("恢复流程正在进行中，拒绝重复发起。");
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
                    LogWarning($"平台恢复交易返回失败，详情={errorInfo}");
                }

                StartCheckEntitlements();
            });

            return await restoreTcs.Task;
        }

        /// <summary>
        /// 商品拉取完成后发起平台已有购买拉取；其回调会先缓存票据，再决定补跑权益刷新或完整补单。
        /// </summary>
        internal void RequestExistingPurchasesFetch()
        {
            if (m_IsExistingPurchasesFetchInProgress)
            {
                LogDebug("平台已有购买正在拉取，本次请求复用当前回调。");
                return;
            }

            if (m_Hub.ExtendedService?.IsAttached != true)
            {
                LogWarning("平台已有购买拉取跳过，原因=StoreControllerNotAttached。");
                return;
            }

            m_IsExistingPurchasesFetchInProgress = true;
            LogDebug("开始拉取平台已有购买，权益刷新将在票据缓存完成后继续。");
            m_Hub.ExtendedService.FetchPurchases();
        }

        /// <summary>
        /// 平台已有购买拉取结束后尝试补跑延后的权益刷新。
        /// </summary>
        /// <returns>本次已调度权益刷新时返回 true。</returns>
        private bool TryRunPendingEntitlementRefreshAfterPurchasesFetched()
        {
            if (!m_PendingEntitlementRefresh)
            {
                return false;
            }

            if (string.IsNullOrEmpty(m_Hub.Store?.GameUID))
            {
                return false;
            }

            if (m_Hub.InitService?.IsReady != true ||
                m_Hub.InitService.ProductFetchState != MobileProductFetchState.Succeeded ||
                m_IsExistingPurchasesFetchInProgress)
            {
                LogDebug($"延后权益刷新尚不具备补跑条件，商店就绪={m_Hub.InitService?.IsReady == true}，商品状态={m_Hub.InitService?.ProductFetchState}，已有购买拉取中={m_IsExistingPurchasesFetchInProgress}。");
                return false;
            }

            m_PendingEntitlementRefresh = false;
            LogDebug("商店、商品和平台已有购买已就绪，开始补跑延后权益刷新。");
            m_Hub.RunBackgroundTask(async token => { await RefreshEntitlementsAsync(token); }, "平台已有购买就绪后的权益刷新");
            return true;
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
                m_PendingEntitlementRefresh = true;
                LogDebug("商店尚未连接，已延后权益刷新，原因=StoreNotReady。");
                return new List<IAPResult>();
            }

            if (string.IsNullOrEmpty(m_Hub.Store?.GameUID))
            {
                LogDebug("账号未登录，跳过权益刷新。");
                return new List<IAPResult>();
            }

            MobileProductFetchState productFetchState = await m_Hub.InitService.WaitForProductsFetchedAsync(5000, ct);
            if (productFetchState != MobileProductFetchState.Succeeded)
            {
                m_PendingEntitlementRefresh = true;
                LogWarning($"商品信息尚未就绪，延后权益刷新，原因=ProductsNotReady，状态={productFetchState}。");
                return new List<IAPResult>();
            }

            if (m_IsExistingPurchasesFetchInProgress)
            {
                m_PendingEntitlementRefresh = true;
                LogDebug("平台已有购买尚未拉取完成，已延后权益刷新，原因=PurchasesFetchInProgress。");
                return new List<IAPResult>();
            }

            if (m_IsInRestore)
            {
                m_PendingEntitlementRefresh = false;
                LogWarning("恢复流程正在进行中，拒绝重复刷新权益。");
                return new List<IAPResult>();
            }

            m_PendingEntitlementRefresh = false;
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

            EntitlementStatus status = entitlement.Status;
            CacheEntitlementReceipt(productId, entitlement);
            if (status == EntitlementStatus.FullyEntitled &&
                entitlement.Product?.definition.type == ProductType.Subscription &&
                TryGetSubscriptionExpireDate(entitlement.Product, entitlement, out DateTime expireDate))
            {
                bool isExpired = expireDate <= DateTime.UtcNow;
                LogDebug($"权益查询回调订阅信息：商品ID={productId}，到期时间={expireDate:O}，是否已过期={isExpired}");
                if (isExpired)
                {
                    status = EntitlementStatus.NotEntitled;
                }
            }

            LogDebug($"权益查询回调：商品ID={productId}，状态={status}");

            if (m_Hub.ProductService.m_CheckEntitlements.TryGetValue(productId, out MobileCheckEntitlementInfo info))
            {
                info.Status = (int)status;
            }

            if (!m_Hub.ProductService.HasPendingCheckEntitlement())
            {
                ProcessAllEntitlementsCompleted();
            }
        }

        /// <summary>
        /// 缓存权益回调关联订单的票据，使 FullyEntitled 恢复记录可直接取得 Google token 或 Apple order id。
        /// </summary>
        /// <param name="productId">当前权益回调对应的平台商品 ID。</param>
        /// <param name="entitlement">Unity IAP 权益检查结果。</param>
        private void CacheEntitlementReceipt(string productId, Entitlement entitlement)
        {
            string receipt = entitlement?.Order?.Info?.Receipt;
            if (string.IsNullOrEmpty(receipt))
            {
                return;
            }

            m_Hub.ProductService.CacheReceipt(productId, receipt);
        }

        /// <summary>
        /// 从 Unity IAP 5.x Entitlement 关联订单中读取当前商品的订阅到期时间。
        /// </summary>
        /// <param name="product">当前权益回调对应的商品。</param>
        /// <param name="entitlement">权益检查结果。</param>
        /// <param name="expireDate">Unity IAP 解析出的订阅到期 UTC 时间。</param>
        /// <returns>成功读取到有效到期时间时返回 true。</returns>
        private static bool TryGetSubscriptionExpireDate(Product product, Entitlement entitlement, out DateTime expireDate)
        {
            expireDate = default;
            if (product == null)
            {
                return false;
            }

            var purchasedProductInfo = entitlement.Order?.Info?.PurchasedProductInfo;
            if (purchasedProductInfo == null)
            {
                return false;
            }

            bool hasExpireDate = false;
            DateTime bestExpireDate = default;
            foreach (IPurchasedProductInfo productInfo in purchasedProductInfo)
            {
                if (productInfo == null)
                {
                    continue;
                }

                if (!MatchesPurchasedProduct(productInfo.productId, product))
                {
                    continue;
                }

                SubscriptionInfo subscriptionInfo = productInfo?.subscriptionInfo;
                if (subscriptionInfo == null)
                {
                    continue;
                }

                DateTime value = subscriptionInfo.GetExpireDate();
                if (value == default || value == DateTime.MinValue)
                {
                    continue;
                }

                value = value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
                if (!hasExpireDate || value > bestExpireDate)
                {
                    bestExpireDate = value;
                    hasExpireDate = true;
                }
            }

            expireDate = bestExpireDate;
            return hasExpireDate;
        }

        /// <summary>
        /// 判断 PurchasedProductInfo 的商品 ID 是否对应当前权益回调商品。
        /// </summary>
        /// <param name="purchasedProductId">Unity IAP PurchasedProductInfo 中的商店商品 ID。</param>
        /// <param name="product">当前权益回调商品。</param>
        /// <returns>商品 ID 与当前商品的定义 ID 或商店 ID 匹配时返回 true。</returns>
        private static bool MatchesPurchasedProduct(string purchasedProductId, Product product)
        {
            if (string.IsNullOrEmpty(purchasedProductId) || product?.definition == null)
            {
                return false;
            }

            return string.Equals(purchasedProductId, product.definition.id, StringComparison.Ordinal) ||
                   string.Equals(purchasedProductId, product.definition.storeSpecificId, StringComparison.Ordinal);
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
                // 补单 / 平台自动恢复 / 服务端未完成订单查询等非协调 Restore 路径下完成的验单：
                // 订阅与非消耗品的恢复只经 SubscriptionRestored / NonConsumeRestored 通知（PaySuccess 已对二者的补单抑制），
                // 而协调 Restore 又未开启，此处按类型就地补发单条恢复通知，避免恢复成功却无任何全局通知。
                if (collectResult)
                {
                    if (productType == ProductType.Subscription)
                    {
                        RaiseStandaloneSubscriptionRestored(result);
                    }
                    else if (productType == ProductType.NonConsumable)
                    {
                        RaiseStandaloneNonConsumeRestored(result);
                    }
                }
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
            m_IsExistingPurchasesFetchInProgress = false;
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
                    LogWarning($"平台已有购买拉取完成：待确认订单中未找到商品，订单号={order.Info.TransactionID}");
                    continue;
                }

                LogDebug($"平台已有购买拉取完成：待确认商品={product.definition.id}，等待服务端验单后确认。");
                // 待确认订单必须走购买服务，先解析票据并完成服务端验单，再确认平台订单。
                m_Hub.PurchaseService.OnPurchasePending(order);
            }

            if (!string.IsNullOrEmpty(m_Hub.Store?.GameUID) &&
                !TryRunPendingEntitlementRefreshAfterPurchasesFetched())
            {
                m_Hub.RunBackgroundTask(m_Hub.ValidationService.CheckLocalOrdersAsync, "平台已有购买后的补单扫描");
            }
        }

        /// <summary>
        /// 处理平台已有购买拉取失败：记录日志，并在已登录时通过统一补单入口重跑服务端、本地和权益检查。
        /// 缺少平台 PendingOrder 引用的订单继续保留，等待后续 FetchPurchases 或手动 Restore。
        /// </summary>
        /// <param name="failure">失败描述。</param>
        internal void OnExistingPurchasesFetchFailed(PurchasesFetchFailureDescription failure)
        {
            m_IsExistingPurchasesFetchInProgress = false;
            LogWarning($"平台已有购买拉取失败，原因={failure.FailureReason}，详情={failure.Message}");
            if (string.IsNullOrEmpty(m_Hub.Store?.GameUID))
            {
                return;
            }

            if (TryRunPendingEntitlementRefreshAfterPurchasesFetched())
            {
                return;
            }

            LogDebug("平台已有购买拉取失败，开始执行服务端、本地和权益补单兜底。");
            m_Hub.RunBackgroundTask(m_Hub.ValidationService.CheckLocalOrdersAsync, "平台已有购买拉取失败后的补单兜底");
        }

        /// <summary>
        /// 释放服务资源，若有进行中的 Restore 则强制结束。
        /// </summary>
        internal void Dispose()
        {
            m_IsExistingPurchasesFetchInProgress = false;
            m_PendingEntitlementRefresh = false;
            if (m_IsInRestore)
            {
                FinishRestore();
            }
        }
    }
}
