/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  NovaMultiDictionary.cs
 * author:    taoye
 * created:   2025/12/2
 * descrip:   多值字典类
 ***************************************************************/
using System;
using System.Collections;
using System.Collections.Generic;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 多值字典类。
    /// </summary>
    /// <typeparam name="TKey">指定多值字典的主键类型。</typeparam>
    /// <typeparam name="TValue">指定多值字典的值类型。</typeparam>
    public sealed class NovaMultiDictionary<TKey, TValue> : IEnumerable<KeyValuePair<TKey, NovaLinkedListRange<TValue>>>, IEnumerable
    {
        /// <summary>
        /// 内部链表。
        /// </summary>
        private readonly NovaLinkedList<TValue> m_LinkedList;
        
        /// <summary>
        /// 内部字典。
        /// </summary>
        private readonly Dictionary<TKey, NovaLinkedListRange<TValue>> m_Dictionary;

        /// <summary>
        /// 初始化多值字典类的新实例。
        /// </summary>
        public NovaMultiDictionary()
        {
            m_LinkedList = new NovaLinkedList<TValue>();
            m_Dictionary = new Dictionary<TKey, NovaLinkedListRange<TValue>>();
        }

        /// <summary>
        /// 获取多值字典中实际包含的主键数量。
        /// </summary>
        public int Count => m_Dictionary.Count;

        /// <summary>
        /// 获取多值字典中所有主键的只读集合。
        /// </summary>
        public IReadOnlyCollection<TKey> Keys => m_Dictionary.Keys;

        /// <summary>
        /// 获取多值字典中指定主键的范围。
        /// </summary>
        /// <param name="key">指定的主键。</param>
        /// <returns>指定主键的范围。若主键不存在则返回 default（IsValid 为 false）。</returns>
        public NovaLinkedListRange<TValue> this[TKey key]
        {
            get
            {
                m_Dictionary.TryGetValue(key, out NovaLinkedListRange<TValue> range);
                return range;
            }
        }

        /// <summary>
        /// 清理多值字典。
        /// </summary>
        public void Clear()
        {
            m_Dictionary.Clear();
            m_LinkedList.Clear();
        }

        /// <summary>
        /// 检查多值字典中是否包含指定主键。
        /// </summary>
        /// <param name="key">要检查的主键。</param>
        /// <returns>多值字典中是否包含指定主键。</returns>
        public bool Contains(TKey key) => m_Dictionary.ContainsKey(key);

        /// <summary>
        /// 检查多值字典中是否包含指定值。
        /// </summary>
        /// <param name="key">要检查的主键。</param>
        /// <param name="value">要检查的值。</param>
        /// <returns>多值字典中是否包含指定值。</returns>
        public bool Contains(TKey key, TValue value)
        {
            if (m_Dictionary.TryGetValue(key, out NovaLinkedListRange<TValue> range))
            {
                return range.Contains(value);
            }
            return false;
        }

        /// <summary>
        /// 尝试获取多值字典中指定主键的范围。
        /// </summary>
        /// <param name="key">指定的主键。</param>
        /// <param name="range">指定主键的范围。</param>
        /// <returns>是否获取成功。</returns>
        public bool TryGetValue(TKey key, out NovaLinkedListRange<TValue> range)
        {
            return m_Dictionary.TryGetValue(key, out range);
        }

        /// <summary>
        /// 向指定的主键增加指定的值。
        /// </summary>
        /// <param name="key">指定的主键。</param>
        /// <param name="value">指定的值。</param>
        public void Add(TKey key, TValue value)
        {
            if (m_Dictionary.TryGetValue(key, out NovaLinkedListRange<TValue> range))
            {
                m_LinkedList.AddBefore(range.Terminal, value);
            }
            else
            {
                LinkedListNode<TValue> first = m_LinkedList.AddLast(value);
                LinkedListNode<TValue> terminal = m_LinkedList.AddLast(default(TValue));
                m_Dictionary.Add(key, new NovaLinkedListRange<TValue>(first, terminal));
            }
        }

        /// <summary>
        /// 从指定的主键中移除指定的值。
        /// </summary>
        /// <param name="key">指定的主键。</param>
        /// <param name="value">指定的值。</param>
        /// <returns>是否移除成功。</returns>
        public bool Remove(TKey key, TValue value)
        {
            if (m_Dictionary.TryGetValue(key, out NovaLinkedListRange<TValue> range))
            {
                for (LinkedListNode<TValue> current = range.First; current != null && current != range.Terminal; current = current.Next)
                {
                    if (EqualityComparer<TValue>.Default.Equals(current.Value, value))
                    {
                        if (current == range.First)
                        {
                            LinkedListNode<TValue> next = current.Next;
                            if (next == range.Terminal)
                            {
                                m_LinkedList.Remove(next);
                                m_Dictionary.Remove(key);
                            }
                            else
                            {
                                m_Dictionary[key] = new NovaLinkedListRange<TValue>(next, range.Terminal);
                            }
                        }

                        m_LinkedList.Remove(current);
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// 从指定的主键中移除所有的值。
        /// </summary>
        /// <param name="key">指定的主键。</param>
        /// <returns>是否移除成功。</returns>
        public bool RemoveAll(TKey key)
        {
            if (m_Dictionary.TryGetValue(key, out NovaLinkedListRange<TValue> range))
            {
                m_Dictionary.Remove(key);

                LinkedListNode<TValue> current = range.First;
                while (current != null)
                {
                    LinkedListNode<TValue> next = current != range.Terminal ? current.Next : null;
                    m_LinkedList.Remove(current);
                    current = next;
                }

                return true;
            }

            return false;
        }

        /// <summary>
        /// 返回循环访问集合的枚举数。
        /// </summary>
        /// <returns>循环访问集合的枚举数。</returns>
        public Enumerator GetEnumerator() => new Enumerator(m_Dictionary);

        IEnumerator<KeyValuePair<TKey, NovaLinkedListRange<TValue>>> IEnumerable<KeyValuePair<TKey, NovaLinkedListRange<TValue>>>.GetEnumerator()
        {
            return GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>
        /// 循环访问集合的枚举数。
        /// </summary>
        public struct Enumerator : IEnumerator<KeyValuePair<TKey, NovaLinkedListRange<TValue>>>, IEnumerator
        {
            private Dictionary<TKey, NovaLinkedListRange<TValue>>.Enumerator m_Enumerator;

            internal Enumerator(Dictionary<TKey, NovaLinkedListRange<TValue>> dictionary)
            {
                if (dictionary == null)
                {
                    throw new ArgumentNullException(nameof(dictionary), "Dictionary 无效。");
                }

                m_Enumerator = dictionary.GetEnumerator();
            }

            /// <summary>
            /// 获取当前结点。
            /// </summary>
            public KeyValuePair<TKey, NovaLinkedListRange<TValue>> Current => m_Enumerator.Current;

            /// <summary>
            /// 获取当前的枚举数。
            /// </summary>
            object IEnumerator.Current => m_Enumerator.Current;

            /// <summary>
            /// 清理枚举数。
            /// </summary>
            public void Dispose() => m_Enumerator.Dispose();

            /// <summary>
            /// 获取下一个结点。
            /// </summary>
            /// <returns>返回下一个结点。</returns>
            public bool MoveNext() => m_Enumerator.MoveNext();

            /// <summary>
            /// 重置枚举数。
            /// </summary>
            void IEnumerator.Reset() => ((IEnumerator<KeyValuePair<TKey, NovaLinkedListRange<TValue>>>)m_Enumerator).Reset();
        }
    }
}
