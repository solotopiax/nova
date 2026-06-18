/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MobileProductService.Methods.cs
 * author:    yingzheng
 * created:   2026/5/28
 * descrip:   MobileProductService 内部方法
 ***************************************************************/

using System.Collections.Generic;
using NovaFramework.SDK.IAP.Runtime;
using UnityEngine.Purchasing;

namespace NovaFramework.SDK.IAP.Mobile.Runtime
{
    internal sealed partial class MobileProductService
    {
        /// <summary>
        /// 从 CheckEntitlement 字典中筛选指定商品类型且状态为 FullyEntitled 的 tableId 列表。
        /// </summary>
        /// <param name="productType">要筛选的商品类型（Subscription 或 NonConsumable）。</param>
        /// <returns>符合条件的 tableId 列表；无匹配时返回空列表。</returns>
        private List<long> GetFullyEntitledTableIds(IAPProductType productType)
        {
            var result = new List<long>();
            foreach (MobileCheckEntitlementInfo info in m_CheckEntitlements.Values)
            {
                if (info.ProductType == productType && info.Status == (int)EntitlementStatus.FullyEntitled)
                    result.Add(info.TableId);
            }
            return result;
        }
    }
}
