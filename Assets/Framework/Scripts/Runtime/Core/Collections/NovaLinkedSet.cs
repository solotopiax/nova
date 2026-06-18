/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  NovaLinkedSet.cs
 * author:    taoye
 * created:   2025/12/2
 * descrip:   Set容器类
 ***************************************************************/
using System.Collections;
using System.Collections.Generic;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// Set 容器类。
    /// </summary>
    /// <typeparam name="T">指定 Set 元素的类型。</typeparam>
    public sealed class NovaLinkedSet<T> : IEnumerable<T>
    {
        /// <summary>
        /// 链表，维护插入顺序。
        /// </summary>
        private readonly LinkedList<T> m_List;
        /// <summary>
        /// 字典，提供 O(1) 成员查找和结点定位。
        /// </summary>
        private readonly Dictionary<T, LinkedListNode<T>> m_Dictionary;

        /// <summary>
        /// 初始化新实例。
        /// </summary>
        public NovaLinkedSet()
        {
            m_List = new LinkedList<T>();
            m_Dictionary = new Dictionary<T, LinkedListNode<T>>();
        }

        /// <summary>
        /// 初始化新实例。
        /// </summary>
        /// <param name="comparer">指定元素比较器。</param>
        public NovaLinkedSet(IEqualityComparer<T> comparer)
        {
            m_List = new LinkedList<T>();
            m_Dictionary = new Dictionary<T, LinkedListNode<T>>(comparer);
        }

        /// <summary>
        /// 追加元素。
        /// </summary>
        /// <param name="t">要添加的元素。</param>
        /// <returns>如果添加成功返回 true；如果已存在返回 false。</returns>
        public bool Add(T t)
        {
            if (m_Dictionary.ContainsKey(t))
            {
                return false;
            }

            LinkedListNode<T> node = m_List.AddLast(t);
            m_Dictionary.Add(t, node);
            return true;
        }

        /// <summary>
        /// 移除元素。
        /// </summary>
        /// <param name="t">要移除的元素。</param>
        /// <returns>如果移除成功返回 true；否则返回 false。</returns>
        public bool Remove(T t)
        {
            if (m_Dictionary.TryGetValue(t, out LinkedListNode<T> node))
            {
                m_Dictionary.Remove(t);
                m_List.Remove(node);
                return true;
            }
            return false;
        }

        /// <summary>
        /// 清空集合。
        /// </summary>
        public void Clear()
        {
            m_List.Clear();
            m_Dictionary.Clear();
        }

        /// <summary>
        /// 是否包含指定元素。
        /// </summary>
        /// <param name="t">要检查的元素。</param>
        /// <returns>如果包含返回 true；否则返回 false。</returns>
        public bool Contains(T t) => m_Dictionary.ContainsKey(t);

        /// <summary>
        /// 集合元素数量。
        /// </summary>
        public int Count => m_List.Count;

        /// <summary>
        /// 获取 struct 枚举器，避免接口装箱。
        /// </summary>
        /// <returns>集合的 struct 枚举器。</returns>
        public Enumerator GetEnumerator() => new Enumerator(m_List);

        IEnumerator<T> IEnumerable<T>.GetEnumerator() => new Enumerator(m_List);

        IEnumerator IEnumerable.GetEnumerator() => new Enumerator(m_List);

        /// <summary>
        /// NovaLinkedSet struct 枚举器，包装 LinkedList 枚举器避免接口装箱。
        /// </summary>
        public struct Enumerator : IEnumerator<T>
        {
            /// <summary>
            /// 被包装的 LinkedList 枚举器。
            /// </summary>
            private LinkedList<T>.Enumerator m_Inner;

            internal Enumerator(LinkedList<T> list)
            {
                m_Inner = list.GetEnumerator();
            }

            /// <summary>
            /// 获取当前元素。
            /// </summary>
            public T Current => m_Inner.Current;

            object IEnumerator.Current => m_Inner.Current;

            /// <summary>
            /// 移动到下一个元素。
            /// </summary>
            /// <returns>是否移动成功。</returns>
            public bool MoveNext() => m_Inner.MoveNext();

            /// <summary>
            /// 释放枚举器资源。
            /// </summary>
            public void Dispose() => m_Inner.Dispose();

            void IEnumerator.Reset() => ((IEnumerator)m_Inner).Reset();
        }
    }
}
