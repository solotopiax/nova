/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MobileCheckEntitlementInfo.cs
 * author:    yingzheng
 * created:   2026/5/26
 * descrip:   CheckEntitlement 回调缓存项，追踪 Restore 流程中每个商品的权益检查状态
 ***************************************************************/

using NovaFramework.SDK.IAP.Runtime;
using UnityEngine.Purchasing;

namespace NovaFramework.SDK.IAP.Mobile.Runtime
{
    /// <summary>
    /// 单个商品的 CheckEntitlement 状态缓存项。
    /// Status = -1 表示 Pending（尚未收到回调），其他值为 EntitlementStatus 枚举整数。
    /// </summary>
    internal sealed class MobileCheckEntitlementInfo
    {
        /// <summary>
        /// 商品配置表行 ID。
        /// </summary>
        internal long TableId { get; }

        /// <summary>
        /// 商品类型（Subscription / NonConsumable）。
        /// </summary>
        internal IAPProductType ProductType { get; }

        /// <summary>
        /// Unity IAP Product 对象。
        /// </summary>
        internal Product Product { get; }

        /// <summary>
        /// 当前权益状态（-1 = Pending，其他值对应 EntitlementStatus）。
        /// </summary>
        internal int Status { get; set; }

        /// <summary>
        /// 是否还未收到平台回调（Status == -1）。
        /// </summary>
        internal bool IsPending => Status == -1;

        /// <summary>
        /// 平台商品 ID。
        /// </summary>
        internal string ProductId => Product?.definition.id ?? string.Empty;

        /// <summary>
        /// 构造 MobileCheckEntitlementInfo。
        /// </summary>
        internal MobileCheckEntitlementInfo(long tableId, IAPProductType productType, Product product, int status = -1)
        {
            TableId = tableId;
            ProductType = productType;
            Product = product;
            Status = status;
        }
    }
}
