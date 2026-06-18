/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MobileProductService.cs
 * author:    yingzheng
 * created:   2026/5/25
 * descrip:   Unity IAP 商品对象缓存、票据解析、CheckEntitlement 状态追踪
 ***************************************************************/

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using NovaFramework.SDK.IAP.Runtime;
using NovaFramework.Runtime;
using UnityEngine.Purchasing;

namespace NovaFramework.SDK.IAP.Mobile.Runtime
{
    /// <summary>
    /// Unity IAP 商品对象缓存服务。
    /// 缓存 order.Info.Receipt（IAP 5.x），通过 MobileReceiptParser 解析 orderId / googleToken；
    /// 提供按 Order 取第一个 Product 的统一入口；
    /// 持有 CheckEntitlement 状态字典，供 Restore 流程追踪每个商品的权益检查结果。
    /// </summary>
    internal sealed partial class MobileProductService
    {
        /// <summary>
        /// 服务容器，持有共享外部依赖与其他服务引用。
        /// </summary>
        private readonly MobileServiceHub m_Hub;

        /// <summary>
        /// 构造 MobileProductService。
        /// </summary>
        /// <param name="hub">服务容器，持有共享外部依赖与其他服务引用。</param>
        internal MobileProductService(MobileServiceHub hub)
        {
            m_Hub = hub;
            m_CheckEntitlements = new Dictionary<string, MobileCheckEntitlementInfo>();
            m_ProductReceipts = new Dictionary<string, string>();
        }

        /// <summary>
        /// 缓存 order.Info.Receipt（IAP 5.x）。
        /// </summary>
        /// <param name="productId">平台商品 ID。</param>
        /// <param name="receipt">order.Info.Receipt 字符串。</param>
        internal void CacheReceipt(string productId, string receipt)
        {
            if (!string.IsNullOrEmpty(productId))
            {
                m_ProductReceipts[productId] = receipt ?? string.Empty;
            }
        }

        /// <summary>
        /// 解析指定商品的票据，输出 orderId / googleToken 用于服务端验单。
        /// </summary>
        /// <param name="productId">平台商品 ID。</param>
        /// <param name="orderId">输出：订单号（Google = PayloadJson.OrderId；Apple = TransactionID）。</param>
        /// <param name="googleToken">输出：Google Play purchase token；Apple 为空。</param>
        internal void GetReceiptInfo(string productId, out string orderId, out string googleToken)
        {
            orderId = string.Empty;
            googleToken = string.Empty;
            if (string.IsNullOrEmpty(productId))
            {
                return;
            }

            if (!m_ProductReceipts.TryGetValue(productId, out string receiptJson) || string.IsNullOrEmpty(receiptJson))
            {
                return;
            }

            MobileReceiptInfo info = MobileReceiptParser.Parse(productId, receiptJson);
            if (info == null)
            {
                return;
            }

            orderId = info.OrderId;
            googleToken = info.GoogleToken;
        }

        /// <summary>
        /// 取 Unity IAP Product 对象（通过 Controller.GetProductById）。
        /// </summary>
        /// <param name="productId">平台商品 ID。</param>
        /// <returns>Product 对象；Controller 未就绪或商品不存在时返回 null。</returns>
        internal Product GetProduct(string productId)
        {
            if (string.IsNullOrEmpty(productId))
            {
                return null;
            }

            return m_Hub.ExtendedService.GetProductById(productId);
        }

        /// <summary>
        /// 获取 Order 中第一个 Product 对象。
        /// </summary>
        /// <param name="order">目标订单。</param>
        /// <returns>第一个 Product；OrderCart 为空时返回 null。</returns>
        internal Product GetFirstProductInOrder(Order order)
        {
            if (order?.CartOrdered == null)
            {
                return null;
            }

            return order.CartOrdered.Items().FirstOrDefault()?.Product;
        }

        /// <summary>
        /// 判断 CheckEntitlement 缓存中是否还有状态为 Pending 的项。
        /// </summary>
        internal bool HasPendingCheckEntitlement()
        {
            return m_CheckEntitlements.Values.Any(item => item.IsPending);
        }

        /// <summary>
        /// 获取所有订阅类型 CheckEntitlement 中状态为 FullyEntitled 的 tableId 列表。
        /// </summary>
        internal List<long> GetFullyEntitledSubscriptionTableIds()
        {
            return GetFullyEntitledTableIds(IAPProductType.Subscription);
        }

        /// <summary>
        /// 获取所有非消耗品类型 CheckEntitlement 中状态为 FullyEntitled 的 tableId 列表。
        /// </summary>
        internal List<long> GetFullyEntitledNonConsumableTableIds()
        {
            return GetFullyEntitledTableIds(IAPProductType.NonConsumable);
        }

        /// <summary>
        /// 批量查询平台商品信息（本地化价格、标题等）。
        /// InitService 未就绪或 productIds 为 null 时返回空列表。
        /// </summary>
        /// <param name="productIds">商品 ID 列表。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>查询到的商品信息列表。</returns>
        internal UniTask<IReadOnlyList<ProductInfo>> QueryProductsAsync(IReadOnlyList<string> productIds, CancellationToken ct)
        {
            var results = new List<ProductInfo>();
            if (m_Hub.InitService.IsReady && m_Hub.ExtendedService.IsAttached && productIds != null)
            {
                foreach (string id in productIds)
                {
                    Product p = m_Hub.ExtendedService.GetProductById(id);
                    if (p != null)
                    {
                        results.Add(new ProductInfo(p.definition.id, p.metadata.localizedPriceString, p.metadata.isoCurrencyCode, p.metadata.localizedTitle));
                    }
                }
            }

            return UniTask.FromResult<IReadOnlyList<ProductInfo>>(results);
        }

        /// <summary>
        /// 释放缓存。
        /// </summary>
        internal void Dispose()
        {
            // 清空票据缓存
            m_ProductReceipts.Clear();
            // 清空权益检查状态
            m_CheckEntitlements.Clear();
            // 清空非消耗品持有 Set
            m_NonConsumablePurchased.Clear();
        }
    }
}
