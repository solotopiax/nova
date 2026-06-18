/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MobileProductService.Visitors.cs
 * author:    yingzheng
 * created:   2026/5/25
 * descrip:   MobileProductService 字段与属性
 ***************************************************************/

using System.Collections.Generic;

namespace NovaFramework.SDK.IAP.Mobile.Runtime
{
    internal sealed partial class MobileProductService
    {
        /// <summary>
        /// 缓存 productId → order.Info.Receipt（IAP 5.x），供 GetReceiptInfo 解析 orderId / googleToken。
        /// </summary>
        internal readonly Dictionary<string, string> m_ProductReceipts;

        /// <summary>
        /// CheckEntitlement 状态字典，key = productId，Restore 流程中由 RestoreService 写入与查询。
        /// </summary>
        internal readonly Dictionary<string, MobileCheckEntitlementInfo> m_CheckEntitlements;

        /// <summary>
        /// 内存中已持有的非消耗品 tableId 集合，验单成功后由 ValidationService 写入；
        /// HasNonConsumeProduct 优先查此 Set，未命中再回退到 PersistManager（lazy 回填）。
        /// </summary>
        internal readonly HashSet<long> m_NonConsumablePurchased = new HashSet<long>();
    }
}
