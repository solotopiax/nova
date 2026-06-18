/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  IIAPProductTable.cs
 * author:    yingzheng
 * created:   2026/6/3
 * descrip:   IAP 商品表读取接口，解耦 store 层对 IAPPluginConfig 的直接依赖
 ***************************************************************/

using System.Collections.Generic;

namespace NovaFramework.SDK.IAP.Runtime
{
    /// <summary>
    /// IAP 商品表读取接口。
    /// 定义查询 IAP 商品条目的标准约定，由运行期商品表服务实现。
    /// 隔离 store 层对具体表实现的依赖，允许动态配置来源。
    /// </summary>
    public interface IIAPProductTable
    {
        /// <summary>
        /// 获取所有商品条目的只读列表。返回所有 IAPProductEntry 条目的只读列表；若列表为空或未初始化，返回空列表（不返回 null）。
        /// </summary>
        IReadOnlyList<IAPProductEntry> Products { get; }

        /// <summary>
        /// 按表行 id 查找商品条目。
        /// 实现侧应提供查询缓存优化，避免每次遍历全表。
        /// </summary>
        /// <param name="tableId">
        /// 要查找的表行 id（主键）。
        /// </param>
        /// <returns>
        /// 匹配的 IAPProductEntry；列表为空、tableId 不存在或列表为 null 时返回 null。
        /// </returns>
        IAPProductEntry FindByTableId(long tableId);

        /// <summary>
        /// 按平台商品 id 查找商品条目。
        /// 实现侧应提供查询缓存优化；通常对应 Google Play 或 iOS App Store 的商品 ID。
        /// </summary>
        /// <param name="productId">
        /// 要查找的 Google/iOS 平台商品 id。
        /// </param>
        /// <returns>
        /// 匹配的 IAPProductEntry；入参为 null / 空串、列表为 null 或未命中时返回 null。
        /// </returns>
        IAPProductEntry FindByProductId(string productId);

        /// <summary>
        /// 按商品类型获取对应条目的只读列表。
        /// 实现侧应提供查询缓存优化，避免每次遍历全表。
        /// </summary>
        /// <param name="type">
        /// 商品类型枚举，如 Consumable / NonConsumable / Subscription。
        /// </param>
        /// <returns>
        /// 该类型下的所有条目；无匹配时返回空列表（不返回 null）。
        /// </returns>
        IReadOnlyList<IAPProductEntry> GetByType(IAPProductType type);

        /// <summary>
        /// 获取所有消耗品（Consumable）条目。
        /// 语法糖，等价于 GetByType(IAPProductType.Consumable)。
        /// </summary>
        /// <returns>
        /// 消耗品条目的只读列表；无匹配时返回空列表。
        /// </returns>
        IReadOnlyList<IAPProductEntry> GetConsumables();

        /// <summary>
        /// 获取所有非消耗品（NonConsumable）条目。
        /// 语法糖，等价于 GetByType(IAPProductType.NonConsumable)。
        /// </summary>
        /// <returns>
        /// 非消耗品条目的只读列表；无匹配时返回空列表。
        /// </returns>
        IReadOnlyList<IAPProductEntry> GetNonConsumables();

        /// <summary>
        /// 获取所有订阅商品（Subscription）条目。
        /// 语法糖，等价于 GetByType(IAPProductType.Subscription)。
        /// </summary>
        /// <returns>
        /// 订阅商品条目的只读列表；无匹配时返回空列表。
        /// </returns>
        IReadOnlyList<IAPProductEntry> GetSubscriptions();

        /// <summary>
        /// 获取指定订阅群组下的全部订阅商品。
        /// groupId == 0 视为"不属于任何群组"的独立订阅，直接返回空列表。
        /// 实现侧应提供缓存优化，避免每次遍历全表。
        /// </summary>
        /// <param name="groupId">
        /// 订阅群组 id；仅收录 SubGroupID 非 0 的订阅商品。
        /// </param>
        /// <returns>
        /// 同组的订阅商品条目只读列表；groupId 为 0 或无匹配时返回空列表。
        /// </returns>
        IReadOnlyList<IAPProductEntry> GetSubscriptionGroup(int groupId);

        /// <summary>
        /// 给定一个订阅商品的表行 id，返回其所在订阅群组的全部商品（包含自身）。
        /// tableId 不存在 / 非订阅类型 / 其 SubGroupID 为 0 时均返回空列表。
        /// </summary>
        /// <param name="tableId">
        /// 订阅商品的表行 id。
        /// </param>
        /// <returns>
        /// 同组的订阅商品条目只读列表；tableId 不存在、非订阅类型或 SubGroupID 为 0 时返回空列表。
        /// </returns>
        IReadOnlyList<IAPProductEntry> GetSubscriptionGroupOf(long tableId);

        /// <summary>
        /// 判断两个订阅商品是否属于同一订阅群组。
        /// 任一入参为 null、任一 SubGroupID 为 0（独立订阅）均返回 false。
        /// 仅在两者 SubGroupID 相同且非 0 时返回 true；不校验 ProductType。
        /// </summary>
        /// <param name="a">
        /// 第一个商品条目。
        /// </param>
        /// <param name="b">
        /// 第二个商品条目。
        /// </param>
        /// <returns>
        /// 两者属于同一订阅群组时返回 true；任一为 null、任一 SubGroupID 为 0 或群组 id 不同时返回 false。
        /// </returns>
        bool AreInSameSubGroup(IAPProductEntry a, IAPProductEntry b);
    }
}
