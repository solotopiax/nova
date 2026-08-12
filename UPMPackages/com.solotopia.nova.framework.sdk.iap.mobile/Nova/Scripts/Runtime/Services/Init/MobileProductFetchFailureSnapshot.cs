/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MobileProductFetchFailureSnapshot.cs
 * author:    yingzheng
 * created:   2026/8/11
 * descrip:   Unity IAP 商品拉取失败回调快照
 ***************************************************************/

using System.Collections.Generic;
using UnityEngine.Purchasing;

namespace NovaFramework.SDK.IAP.Mobile.Runtime
{
    /// <summary>
    /// 商品拉取失败回调快照。
    /// Unity IAP 回调对象进入状态机前先物化，避免后续逻辑依赖第三方对象生命周期。
    /// </summary>
    internal readonly struct MobileProductFetchFailureSnapshot
    {
        /// <summary>
        /// 本次商品拉取失败回调中报告失败的商品定义列表。
        /// </summary>
        internal IReadOnlyList<ProductDefinition> FailedProducts { get; }

        /// <summary>
        /// 本次商品拉取失败回调中的失败原因描述。
        /// </summary>
        internal string FailureReason { get; }

        /// <summary>
        /// 构造商品拉取失败快照。
        /// </summary>
        /// <param name="failedProducts">已物化的失败商品定义列表。</param>
        /// <param name="failureReason">失败原因描述。</param>
        private MobileProductFetchFailureSnapshot(IReadOnlyList<ProductDefinition> failedProducts, string failureReason)
        {
            FailedProducts = failedProducts;
            FailureReason = failureReason;
        }

        /// <summary>
        /// 从 Unity IAP 失败回调对象创建商品拉取失败快照。
        /// </summary>
        /// <param name="failure">Unity IAP 商品拉取失败回调对象。</param>
        /// <returns>已脱离第三方回调对象生命周期的失败快照。</returns>
        internal static MobileProductFetchFailureSnapshot From(ProductFetchFailed failure)
        {
            var failedProducts = new List<ProductDefinition>();
            if (failure?.FailedFetchProducts != null)
            {
                foreach (ProductDefinition definition in failure.FailedFetchProducts)
                {
                    if (definition != null)
                    {
                        failedProducts.Add(definition);
                    }
                }
            }

            return new MobileProductFetchFailureSnapshot(failedProducts, failure?.FailureReason.ToString());
        }
    }
}
