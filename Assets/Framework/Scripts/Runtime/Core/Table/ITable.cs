/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ITable.cs
 * author:    taoye
 * created:   2026/4/15
 * descrip:   表格容器接口
 ***************************************************************/

using System.Collections.Generic;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 表格容器接口，由 Luban 生成的 TbXxx 类实现。
    /// </summary>
    public interface ITable
    {
        /// <summary>
        /// 表格模式。
        /// </summary>
        DataTableMode Mode { get; }
    }

    /// <summary>
    /// 带强类型数据行的表格接口（List / Map 模式通用基接口）。
    /// TData 声明为协变（out），允许 ITable<DerivedRow> 安全转型为 ITable<BaseRow>，
    /// 从而在 FlattenTableToLanguageTexts 等场景中通过接口获取 DataList，消除反射。
    /// </summary>
    /// <typeparam name="TData">数据行类型（Luban 生成的 DtXxx）。</typeparam>
    public interface ITable<out TData> : ITable
    {
        /// <summary>
        /// 全部数据行列表。
        /// </summary>
        IReadOnlyList<TData> DataList { get; }

        /// <summary>
        /// 按下标获取数据行。
        /// </summary>
        /// <param name="index">行下标。</param>
        TData this[int index] { get; }
    }

    /// <summary>
    /// 映射模式表格接口，支持按键查询。
    /// </summary>
    /// <typeparam name="TKey">索引键类型。</typeparam>
    /// <typeparam name="TData">数据行类型（Luban 生成的 DtXxx）。</typeparam>
    public interface ITableMap<TKey, TData> : ITable<TData>
    {
        /// <summary>
        /// 全部键值对映射。
        /// </summary>
        IReadOnlyDictionary<TKey, TData> DataMap { get; }

        /// <summary>
        /// 按键获取数据行，键不存在时抛异常。
        /// </summary>
        /// <param name="key">索引键。</param>
        /// <returns>数据行。</returns>
        TData Get(TKey key);

        /// <summary>
        /// 按键获取数据行，键不存在时返回 default。
        /// </summary>
        /// <param name="key">索引键。</param>
        /// <returns>数据行或 default。</returns>
        TData GetOrDefault(TKey key);

        /// <summary>
        /// 按键获取数据行。
        /// </summary>
        /// <param name="key">索引键。</param>
        TData this[TKey key] { get; }
    }
}
