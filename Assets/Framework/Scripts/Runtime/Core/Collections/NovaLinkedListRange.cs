/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  NovaLinkedListRange.cs
 * author:    taoye
 * created:   2025/12/2
 * descrip:   链表范围
 ***************************************************************/
using System;
using System.Collections;
using System.Collections.Generic;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 链表范围。
    /// </summary>
    /// <typeparam name="T">指定链表范围的元素类型。</typeparam>
    public sealed class NovaLinkedListRange<T> : IEnumerable<T>, IEnumerable
    {
        /// <summary>
        /// 链表范围的开始结点。
        /// </summary>
        private readonly LinkedListNode<T> m_First;
        
        /// <summary>
        /// 链表范围的终结标记结点。
        /// </summary>
        private readonly LinkedListNode<T> m_Terminal;

        /// <summary>
        /// 初始化链表范围的新实例。
        /// </summary>
        /// <param name="first">链表范围的开始结点。</param>
        /// <param name="terminal">链表范围的终结标记结点。</param>
        public NovaLinkedListRange(LinkedListNode<T> first, LinkedListNode<T> terminal)
        {
            if (first == null || terminal == null || first == terminal)
            {
                throw new ArgumentException("范围无效。");
            }

            m_First = first;
            m_Terminal = terminal;
        }

        /// <summary>
        /// 获取链表范围是否有效。
        /// </summary>
        public bool IsValid => m_First != null && m_Terminal != null && m_First != m_Terminal;

        /// <summary>
        /// 获取链表范围的开始结点。
        /// </summary>
        public LinkedListNode<T> First => m_First;

        /// <summary>
        /// 获取链表范围的终结标记结点。
        /// </summary>
        public LinkedListNode<T> Terminal => m_Terminal;

        /// <summary>
        /// 获取链表范围的结点数量。
        /// </summary>
        public int Count
        {
            get
            {
                if (!IsValid)
                    return 0;

                int count = 0;
                for (LinkedListNode<T> current = m_First; current != null && current != m_Terminal; current = current.Next)
                {
                    count++;
                }

                return count;
            }
        }

        /// <summary>
        /// 检查是否包含指定值。
        /// </summary>
        /// <param name="value">要检查的值。</param>
        /// <returns>是否包含指定值。</returns>
        public bool Contains(T value)
        {
            for (LinkedListNode<T> current = m_First; current != null && current != m_Terminal; current = current.Next)
            {
                if (EqualityComparer<T>.Default.Equals(current.Value, value))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 返回循环访问集合的枚举数。
        /// </summary>
        /// <returns>循环访问集合的枚举数。</returns>
        public Enumerator GetEnumerator() => new Enumerator(this);

        IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>
        /// 循环访问集合的枚举数。
        /// </summary>
        public struct Enumerator : IEnumerator<T>, IEnumerator
        {
            private readonly NovaLinkedListRange<T> m_Range;
            private LinkedListNode<T> m_Current;
            private T m_CurrentValue;

            internal Enumerator(NovaLinkedListRange<T> range)
            {
                if (!range.IsValid)
                {
                    throw new ArgumentException("范围无效。");
                }

                m_Range = range;
                m_Current = range.m_First;
                m_CurrentValue = default(T);
            }

            /// <summary>
            /// 获取当前结点。
            /// </summary>
            public T Current => m_CurrentValue;

            /// <summary>
            /// 获取当前的枚举数。
            /// </summary>
            object IEnumerator.Current => m_CurrentValue;

            /// <summary>
            /// 清理枚举数。
            /// </summary>
            public void Dispose()
            {
            }

            /// <summary>
            /// 获取下一个结点。
            /// </summary>
            /// <returns>返回下一个结点。</returns>
            public bool MoveNext()
            {
                if (m_Current == null || m_Current == m_Range.m_Terminal)
                {
                    return false;
                }

                m_CurrentValue = m_Current.Value;
                m_Current = m_Current.Next;
                return true;
            }

            /// <summary>
            /// 重置枚举数。
            /// </summary>
            void IEnumerator.Reset()
            {
                m_Current = m_Range.m_First;
                m_CurrentValue = default(T);
            }
        }
    }
}
