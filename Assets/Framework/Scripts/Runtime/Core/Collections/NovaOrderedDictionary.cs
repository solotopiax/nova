/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  NovaOrderedDictionary.cs
 * author:    taoye
 * created:   2025/12/2
 * descrip:   序列字典类
 ***************************************************************/
using System;
using System.Collections;
using System.Collections.Generic;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 有序字典，底层由 List+Dictionary 泛型组合实现，保留插入顺序并提供零分配键访问。
    /// </summary>
    /// <typeparam name="TKey">键类型。</typeparam>
    /// <typeparam name="TValue">值类型。</typeparam>
    public sealed class NovaOrderedDictionary<TKey, TValue> : IEnumerable<KeyValuePair<TKey, TValue>>
    {
        /// <summary>
        /// 按插入顺序存储键。
        /// </summary>
        private readonly List<TKey> m_Keys = new List<TKey>();
        /// <summary>
        /// 提供 O(1) 键值查找。
        /// </summary>
        private readonly Dictionary<TKey, TValue> m_Dict = new Dictionary<TKey, TValue>();
        /// <summary>
        /// 缓存的只读包装器，AsReadOnly 首次调用后复用。
        /// </summary>
        private ReadOnlyWrapper m_ReadOnlyWrapper;

        /// <summary>
        /// 获取或设置指定键对应的值。键不存在时自动按插入顺序追加。
        /// </summary>
        /// <param name="key">指定的键。</param>
        public TValue this[TKey key]
        {
            get => m_Dict[key];
            set
            {
                if (!m_Dict.ContainsKey(key))
                {
                    m_Keys.Add(key);
                }
                m_Dict[key] = value;
            }
        }

        /// <summary>
        /// 获取字典中实际包含的键值对数量。
        /// </summary>
        public int Count => m_Dict.Count;

        /// <summary>
        /// 获取字典中所有键的有序只读列表，零分配。
        /// </summary>
        public IReadOnlyList<TKey> Keys => m_Keys;

        /// <summary>
        /// 获取字典中所有值的集合视图。
        /// </summary>
        public Dictionary<TKey, TValue>.ValueCollection Values => m_Dict.Values;

        /// <summary>
        /// 向字典末尾添加指定键值对。键已存在时抛出异常。
        /// </summary>
        /// <param name="key">要添加的键。</param>
        /// <param name="value">要添加的值。</param>
        public void Add(TKey key, TValue value)
        {
            m_Dict.Add(key, value);
            m_Keys.Add(key);
        }

        /// <summary>
        /// 移除指定键的键值对。
        /// </summary>
        /// <param name="key">要移除的键。</param>
        /// <returns>是否移除成功。</returns>
        public bool Remove(TKey key)
        {
            if (m_Dict.Remove(key))
            {
                m_Keys.Remove(key);
                return true;
            }
            return false;
        }

        /// <summary>
        /// 检查是否包含指定键。
        /// </summary>
        /// <param name="key">要检查的键。</param>
        /// <returns>是否包含指定键。</returns>
        public bool ContainsKey(TKey key) => m_Dict.ContainsKey(key);

        /// <summary>
        /// 清空字典。
        /// </summary>
        public void Clear()
        {
            m_Keys.Clear();
            m_Dict.Clear();
        }

        /// <summary>
        /// 尝试获取指定键对应的值。
        /// </summary>
        /// <param name="key">要获取的键。</param>
        /// <param name="value">返回指定键对应的值。</param>
        /// <returns>是否获取成功。</returns>
        public bool TryGetValue(TKey key, out TValue value) => m_Dict.TryGetValue(key, out value);

        /// <summary>
        /// 将所有键按插入顺序复制到指定列表，会先清空目标列表。
        /// </summary>
        /// <param name="result">目标列表。</param>
        public void CopyKeysTo(List<TKey> result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            result.Clear();
            result.AddRange(m_Keys);
        }

        /// <summary>
        /// 将所有值按插入顺序复制到指定列表，会先清空目标列表。
        /// </summary>
        /// <param name="result">目标列表。</param>
        public void CopyValuesTo(List<TValue> result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            result.Clear();
            for (int i = 0; i < m_Keys.Count; i++)
            {
                result.Add(m_Dict[m_Keys[i]]);
            }
        }

        /// <summary>
        /// 获取有序遍历的 struct 枚举器，避免接口装箱。
        /// </summary>
        /// <returns>枚举器。</returns>
        public Enumerator GetEnumerator() => new Enumerator(this);

        IEnumerator<KeyValuePair<TKey, TValue>> IEnumerable<KeyValuePair<TKey, TValue>>.GetEnumerator() => GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>
        /// 获取只读包装器，首次调用后缓存复用，不重复分配。
        /// </summary>
        /// <returns>只读有序字典接口。</returns>
        public IReadOnlyOrderedDictionary<TKey, TValue> AsReadOnly()
        {
            return m_ReadOnlyWrapper ??= new ReadOnlyWrapper(this);
        }

        /// <summary>
        /// 有序字典 struct 枚举器，按插入顺序遍历键值对。
        /// </summary>
        public struct Enumerator : IEnumerator<KeyValuePair<TKey, TValue>>
        {
            /// <summary>
            /// 来源字典引用。
            /// </summary>
            private readonly NovaOrderedDictionary<TKey, TValue> m_Source;
            /// <summary>
            /// 当前遍历索引，初始为 -1。
            /// </summary>
            private int m_Index;

            internal Enumerator(NovaOrderedDictionary<TKey, TValue> source)
            {
                m_Source = source ?? throw new ArgumentNullException(nameof(source));
                m_Index = -1;
            }

            /// <summary>
            /// 获取当前键值对。
            /// </summary>
            public KeyValuePair<TKey, TValue> Current
            {
                get
                {
                    TKey key = m_Source.m_Keys[m_Index];
                    return new KeyValuePair<TKey, TValue>(key, m_Source.m_Dict[key]);
                }
            }

            object IEnumerator.Current => Current;

            /// <summary>
            /// 移动到下一个元素。
            /// </summary>
            /// <returns>是否移动成功。</returns>
            public bool MoveNext() => ++m_Index < m_Source.m_Keys.Count;

            /// <summary>
            /// 释放枚举器资源，当前实现无托管资源。
            /// </summary>
            public void Dispose() { }

            void IEnumerator.Reset() => m_Index = -1;
        }

        /// <summary>
        /// 只读视图包装器，持有外部字典引用，不复制数据。
        /// </summary>
        private sealed class ReadOnlyWrapper : IReadOnlyOrderedDictionary<TKey, TValue>
        {
            /// <summary>
            /// 被包装的原始有序字典。
            /// </summary>
            private readonly NovaOrderedDictionary<TKey, TValue> m_Dict;

            internal ReadOnlyWrapper(NovaOrderedDictionary<TKey, TValue> dict) { m_Dict = dict; }

            /// <summary>
            /// 获取指定键对应的值。
            /// </summary>
            /// <param name="key">指定的键。</param>
            public TValue this[TKey key] => m_Dict[key];
            /// <summary>
            /// 获取所有键的有序只读列表。
            /// </summary>
            public IEnumerable<TKey> Keys => m_Dict.Keys;
            /// <summary>
            /// 获取字典值集合视图。
            /// </summary>
            public IEnumerable<TValue> Values => m_Dict.m_Dict.Values;
            /// <summary>
            /// 获取键值对数量。
            /// </summary>
            public int Count => m_Dict.Count;
            /// <summary>
            /// 检查是否包含指定键。
            /// </summary>
            /// <param name="key">要检查的键。</param>
            /// <returns>是否包含。</returns>
            public bool ContainsKey(TKey key) => m_Dict.ContainsKey(key);
            /// <summary>
            /// 尝试获取指定键对应的值。
            /// </summary>
            /// <param name="key">要获取的键。</param>
            /// <param name="value">返回对应值。</param>
            /// <returns>是否获取成功。</returns>
            public bool TryGetValue(TKey key, out TValue value) => m_Dict.TryGetValue(key, out value);
            /// <summary>
            /// 获取按插入顺序遍历的枚举器。
            /// </summary>
            /// <returns>枚举器。</returns>
            public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => m_Dict.GetEnumerator();
            IEnumerator IEnumerable.GetEnumerator() => m_Dict.GetEnumerator();
        }
    }
}
