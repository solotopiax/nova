/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  IAPProductTableService.cs
 * author:    yingzheng
 * created:   2026/6/4
 * descrip:   IAP 商品表运行期查询服务，负责缓存与查询计算
 ***************************************************************/

using System;
using System.Collections.Generic;

namespace NovaFramework.SDK.IAP.Runtime
{
    /// <summary>
    /// IAP 商品表运行期查询服务。
    /// IAPPluginConfig 只保存序列化数据，本服务基于 Products 构建只读查询缓存。
    /// </summary>
    internal sealed class IAPProductTableService : IIAPProductTable
    {
        /// <summary>
        /// 商品条目只读视图。
        /// </summary>
        public IReadOnlyList<IAPProductEntry> Products { get; }

        /// <summary>
        /// 按商品类型分桶的缓存。
        /// </summary>
        private readonly Dictionary<IAPProductType, IReadOnlyList<IAPProductEntry>> m_TypeCache;

        /// <summary>
        /// 按订阅群组 id 分桶的缓存，仅收录 Subscription 类型且 SubGroupID != 0 的条目。
        /// </summary>
        private readonly Dictionary<int, IReadOnlyList<IAPProductEntry>> m_SubGroupCache;

        /// <summary>
        /// 按表行 id 建立的商品条目缓存。
        /// </summary>
        private readonly Dictionary<long, IAPProductEntry> m_TableIdCache;

        /// <summary>
        /// 按平台商品 id 建立的商品条目缓存。
        /// </summary>
        private readonly Dictionary<string, IAPProductEntry> m_ProductIdCache;

        /// <summary>
        /// 构造商品表查询服务，并一次性构建查询缓存。
        /// </summary>
        /// <param name="products">配置中的商品条目列表；为 null 时按空表处理。</param>
        internal IAPProductTableService(IReadOnlyList<IAPProductEntry> products)
        {
            Products = products ?? Array.Empty<IAPProductEntry>();
            BuildCache(
                Products,
                out m_TableIdCache,
                out m_ProductIdCache,
                out m_TypeCache,
                out m_SubGroupCache);
        }

        /// <summary>
        /// 按表行 id 查找商品条目。
        /// </summary>
        /// <param name="tableId">要查找的表行 id（主键）。</param>
        /// <returns>匹配的 IAPProductEntry；未命中时返回 null。</returns>
        public IAPProductEntry FindByTableId(long tableId)
        {
            return m_TableIdCache.TryGetValue(tableId, out IAPProductEntry entry) ? entry : null;
        }

        /// <summary>
        /// 按平台商品 id 查找商品条目。
        /// </summary>
        /// <param name="productId">要查找的 Google/iOS 平台商品 id。</param>
        /// <returns>匹配的 IAPProductEntry；入参为空或未命中时返回 null。</returns>
        public IAPProductEntry FindByProductId(string productId)
        {
            if (string.IsNullOrEmpty(productId))
            {
                return null;
            }

            return m_ProductIdCache.TryGetValue(productId, out IAPProductEntry entry) ? entry : null;
        }

        /// <summary>
        /// 按商品类型获取对应条目的只读列表。
        /// </summary>
        /// <param name="type">商品类型枚举。</param>
        /// <returns>该类型下的所有条目；无匹配时返回空列表。</returns>
        public IReadOnlyList<IAPProductEntry> GetByType(IAPProductType type)
        {
            return m_TypeCache.TryGetValue(type, out IReadOnlyList<IAPProductEntry> list) ? list : Array.Empty<IAPProductEntry>();
        }

        /// <summary>
        /// 获取所有消耗品（Consumable）条目。
        /// </summary>
        /// <returns>消耗品条目的只读列表；无匹配时返回空列表。</returns>
        public IReadOnlyList<IAPProductEntry> GetConsumables()
        {
            return GetByType(IAPProductType.Consumable);
        }

        /// <summary>
        /// 获取所有非消耗品（NonConsumable）条目。
        /// </summary>
        /// <returns>非消耗品条目的只读列表；无匹配时返回空列表。</returns>
        public IReadOnlyList<IAPProductEntry> GetNonConsumables()
        {
            return GetByType(IAPProductType.NonConsumable);
        }

        /// <summary>
        /// 获取所有订阅商品（Subscription）条目。
        /// </summary>
        /// <returns>订阅商品条目的只读列表；无匹配时返回空列表。</returns>
        public IReadOnlyList<IAPProductEntry> GetSubscriptions()
        {
            return GetByType(IAPProductType.Subscription);
        }

        /// <summary>
        /// 获取指定订阅群组下的全部订阅商品。
        /// </summary>
        /// <param name="groupId">订阅群组 id。</param>
        /// <returns>同组的订阅商品条目只读列表；groupId 为 0 或无匹配时返回空列表。</returns>
        public IReadOnlyList<IAPProductEntry> GetSubscriptionGroup(int groupId)
        {
            if (groupId == 0)
            {
                return Array.Empty<IAPProductEntry>();
            }

            return m_SubGroupCache.TryGetValue(groupId, out IReadOnlyList<IAPProductEntry> list) ? list : Array.Empty<IAPProductEntry>();
        }

        /// <summary>
        /// 给定一个订阅商品的表行 id，返回其所在订阅群组的全部商品（包含自身）。
        /// </summary>
        /// <param name="tableId">订阅商品的表行 id。</param>
        /// <returns>同组的订阅商品条目只读列表；不满足条件时返回空列表。</returns>
        public IReadOnlyList<IAPProductEntry> GetSubscriptionGroupOf(long tableId)
        {
            IAPProductEntry entry = FindByTableId(tableId);
            if (entry == null || entry.ProductType != IAPProductType.Subscription)
            {
                return Array.Empty<IAPProductEntry>();
            }

            return GetSubscriptionGroup(entry.SubGroupID);
        }

        /// <summary>
        /// 判断两个订阅商品是否属于同一订阅群组。
        /// </summary>
        /// <param name="a">第一个商品条目。</param>
        /// <param name="b">第二个商品条目。</param>
        /// <returns>两者属于同一订阅群组时返回 true，否则返回 false。</returns>
        public bool AreInSameSubGroup(IAPProductEntry a, IAPProductEntry b)
        {
            if (a == null || b == null)
            {
                return false;
            }

            if (a.SubGroupID == 0 || b.SubGroupID == 0)
            {
                return false;
            }

            return a.SubGroupID == b.SubGroupID;
        }

        /// <summary>
        /// 一次性遍历商品数据并生成所有查询缓存。
        /// </summary>
        private static void BuildCache(
            IReadOnlyList<IAPProductEntry> products,
            out Dictionary<long, IAPProductEntry> tableIdCache,
            out Dictionary<string, IAPProductEntry> productIdCache,
            out Dictionary<IAPProductType, IReadOnlyList<IAPProductEntry>> typeCache,
            out Dictionary<int, IReadOnlyList<IAPProductEntry>> subGroupCache)
        {
            tableIdCache = new Dictionary<long, IAPProductEntry>();
            productIdCache = new Dictionary<string, IAPProductEntry>();

            Dictionary<IAPProductType, List<IAPProductEntry>> typeBuckets = new Dictionary<IAPProductType, List<IAPProductEntry>>();
            Dictionary<int, List<IAPProductEntry>> subGroupBuckets = new Dictionary<int, List<IAPProductEntry>>();

            for (int i = 0; i < products.Count; i++)
            {
                IAPProductEntry entry = products[i];
                if (entry == null)
                {
                    continue;
                }

                // TableId 与 ProductID 重复时保留首条，避免后续重复配置覆盖已建立的路由。
                if (entry.TableId != 0L && !tableIdCache.ContainsKey(entry.TableId))
                {
                    tableIdCache.Add(entry.TableId, entry);
                }

                if (!string.IsNullOrEmpty(entry.ProductID) && !productIdCache.ContainsKey(entry.ProductID))
                {
                    productIdCache.Add(entry.ProductID, entry);
                }

                if (!typeBuckets.TryGetValue(entry.ProductType, out List<IAPProductEntry> typeList))
                {
                    typeList = new List<IAPProductEntry>();
                    typeBuckets[entry.ProductType] = typeList;
                }

                typeList.Add(entry);

                // 订阅群组只收录显式配置了 SubGroupID 的订阅商品，独立订阅不进入群组缓存。
                if (entry.ProductType == IAPProductType.Subscription && entry.SubGroupID != 0)
                {
                    if (!subGroupBuckets.TryGetValue(entry.SubGroupID, out List<IAPProductEntry> subList))
                    {
                        subList = new List<IAPProductEntry>();
                        subGroupBuckets[entry.SubGroupID] = subList;
                    }

                    subList.Add(entry);
                }
            }

            typeCache = new Dictionary<IAPProductType, IReadOnlyList<IAPProductEntry>>(typeBuckets.Count);
            foreach (KeyValuePair<IAPProductType, List<IAPProductEntry>> kv in typeBuckets)
            {
                typeCache[kv.Key] = kv.Value;
            }

            subGroupCache = new Dictionary<int, IReadOnlyList<IAPProductEntry>>(subGroupBuckets.Count);
            foreach (KeyValuePair<int, List<IAPProductEntry>> kv in subGroupBuckets)
            {
                subGroupCache[kv.Key] = kv.Value;
            }
        }
    }
}
