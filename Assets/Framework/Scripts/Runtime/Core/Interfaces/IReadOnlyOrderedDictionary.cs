/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  IReadOnlyOrderedDictionary.cs
 * author:    taoye
 * created:   2025/12/26
 * descrip:   只读型序列字典接口类
 ***************************************************************/
using System.Collections.Generic;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 只读型有序字典接口，保留插入顺序的只读视图契约。
    /// </summary>
    /// <typeparam name="TKey">字典键类型。</typeparam>
    /// <typeparam name="TValue">字典值类型。</typeparam>
    public interface IReadOnlyOrderedDictionary<TKey, TValue> : IEnumerable<KeyValuePair<TKey, TValue>>
    {
        /// <summary>
        /// 获取指定键对应的值。
        /// </summary>
        /// <param name="key">指定的键。</param>
        TValue this[TKey key] { get; }

        /// <summary>
        /// 获取按插入顺序排列的所有键。
        /// </summary>
        IEnumerable<TKey> Keys { get; }

        /// <summary>
        /// 获取所有值的集合视图。
        /// </summary>
        IEnumerable<TValue> Values { get; }

        /// <summary>
        /// 获取字典中实际包含的键值对数量。
        /// </summary>
        int Count { get; }

        /// <summary>
        /// 检查是否包含指定键。
        /// </summary>
        /// <param name="key">要检查的键。</param>
        /// <returns>是否包含指定键。</returns>
        bool ContainsKey(TKey key);

        /// <summary>
        /// 尝试获取指定键对应的值。
        /// </summary>
        /// <param name="key">要获取的键。</param>
        /// <param name="value">返回指定键对应的值。</param>
        /// <returns>是否获取成功。</returns>
        bool TryGetValue(TKey key, out TValue value);
    }
}
