/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ReferenceHelper.ReferenceCollection.cs
 * author:    taoye
 * created:   2026/1/21
 * descrip:   引用助手-容器
 ***************************************************************/

using System;
using System.Collections.Generic;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 引用助手。
    /// </summary>
    internal sealed partial class ReferenceHelper : IReferenceHelper
    {
        /// <summary>
        /// 引用助手-容器。
        /// </summary>
        private sealed class ReferenceCollection
        {
            /// <summary>
            /// 引用对象的缓存队列，存放所有未使用的引用实例。
            /// </summary>
            private readonly Queue<IReference> m_References;

            /// <summary>
            /// 辅助去重的 HashSet，无论是否开启 StrictCheck 均分配，保证重复 Put 检测始终生效。
            /// </summary>
            private readonly HashSet<IReference> m_ReferenceSet;

            /// <summary>
            /// 当前引用池所管理的引用类型。
            /// </summary>
            private readonly Type m_ReferenceType;
            public Type ReferenceType => m_ReferenceType;

            /// <summary>
            /// 获取未使用引用数量（缓存中引用实例的实时数量）。
            /// </summary>
            public int UnusedReferenceCount
            {
                get
                {
                    lock (m_References)
                    {
                        return m_References.Count;
                    }
                }
            }

            /// <summary>
            /// 正在被外部使用的引用数量（正常加减）。
            /// </summary>
            private int m_UsingReferenceCount;
            public int UsingReferenceCount => m_UsingReferenceCount;

            /// <summary>
            /// 累积 Get（获取）引用的次数（包含从池中取出以及新建实例）（只增不减）。
            /// </summary>
            private int m_GetReferenceCount;
            public int GetReferenceCount => m_GetReferenceCount;

            /// <summary>
            /// 累积 Put（归还）引用的次数（归还到池中）（只增不减）。
            /// </summary>
            private int m_PutReferenceCount;
            public int PutReferenceCount => m_PutReferenceCount;

            /// <summary>
            /// 累积向池中 Add（新增）引用实例的次数（只增不减）。
            /// </summary>
            private int m_AddReferenceCount;
            public int AddReferenceCount => m_AddReferenceCount;

            /// <summary>
            /// 累积从池中 Remove（移除）引用实例的次数（只增不减）。
            /// </summary>
            private int m_RemoveReferenceCount;
            public int RemoveReferenceCount => m_RemoveReferenceCount;

            /// <summary>
            /// 是否开启严格检查。
            /// </summary>
            private bool m_StrictCheck;

            /// <summary>
            /// 初始化引用集合。
            /// </summary>
            /// <param name="referenceType">引用类型。</param>
            /// <param name="strictCheck">是否启用强制检查。</param>
            public ReferenceCollection(Type referenceType, bool strictCheck)
            {
                m_References = new Queue<IReference>();
                m_ReferenceSet = new HashSet<IReference>(ReferenceEqualityComparer.Instance);
                m_ReferenceType = referenceType;
                m_UsingReferenceCount = 0;
                m_GetReferenceCount = 0;
                m_PutReferenceCount = 0;
                m_AddReferenceCount = 0;
                m_RemoveReferenceCount = 0;

                m_StrictCheck = strictCheck;
            }

            /// <summary>
            /// 获取指定类型的引用。
            /// </summary>
            /// <typeparam name="T">引用类型。</typeparam>
            public T Get<T>() where T : class, IReference, new()
            {
                if (typeof(T) != m_ReferenceType)
                {
                    throw new InvalidOperationException("Reference type 不匹配。");
                }

                lock (m_References)
                {
                    m_UsingReferenceCount++;
                    m_GetReferenceCount++;

                    if (m_References.Count > 0)
                    {
                        var reference = (T)m_References.Dequeue();
                        m_ReferenceSet.Remove(reference);
                        return reference;
                    }

                    m_AddReferenceCount++;
                }

                return new T();
            }

            /// <summary>
            /// 获取引用。
            /// </summary>
            public IReference Get()
            {
                lock (m_References)
                {
                    m_UsingReferenceCount++;
                    m_GetReferenceCount++;

                    if (m_References.Count > 0)
                    {
                        var reference = m_References.Dequeue();
                        m_ReferenceSet.Remove(reference);
                        return reference;
                    }

                    m_AddReferenceCount++;
                }

                return (IReference)Activator.CreateInstance(m_ReferenceType);
            }

            /// <summary>
            /// 回收引用。
            /// </summary>
            /// <param name="reference">引用对象。</param>
            public void Put(IReference reference)
            {
                lock (m_References)
                {
                    if (!m_ReferenceSet.Add(reference))
                    {
                        throw new InvalidOperationException("reference 已回收过，不可重复回收。");
                    }

                    if (m_StrictCheck && m_ReferenceType != reference.GetType())
                    {
                        throw new InvalidOperationException(Txt.Format("reference 类型 '{0}' 与引用池类型 '{1}' 不匹配。", reference.GetType().FullName, m_ReferenceType.FullName));
                    }

                    reference.Clear();
                    m_References.Enqueue(reference);
                    m_PutReferenceCount++;
                    m_UsingReferenceCount--;
                }
            }

            /// <summary>
            /// 添加引用。
            /// </summary>
            /// <typeparam name="T">引用类型。</typeparam>
            /// <param name="count">数量。</param>
            public void Add<T>(int count) where T : class, IReference, new()
            {
                if (typeof(T) != m_ReferenceType)
                {
                    throw new InvalidOperationException("Reference type 不匹配。");
                }

                lock (m_References)
                {
                    m_AddReferenceCount += count;

                    while (count-- > 0)
                    {
                        var reference = new T();
                        m_References.Enqueue(reference);
                        m_ReferenceSet.Add(reference);
                    }
                }
            }

            /// <summary>
            /// 添加引用。
            /// </summary>
            /// <param name="count">数量。</param>
            public void Add(int count)
            {
                lock (m_References)
                {
                    m_AddReferenceCount += count;

                    while (count-- > 0)
                    {
                        var reference = (IReference)Activator.CreateInstance(m_ReferenceType);
                        m_References.Enqueue(reference);
                        m_ReferenceSet.Add(reference);
                    }
                }
            }

            /// <summary>
            /// 移除引用。
            /// </summary>
            /// <param name="count">数量。</param>
            public void Remove(int count)
            {
                lock (m_References)
                {
                    if (count > m_References.Count)
                    {
                        count = m_References.Count;
                    }

                    m_RemoveReferenceCount += count;

                    while (count-- > 0)
                    {
                        var reference = m_References.Dequeue();
                        m_ReferenceSet.Remove(reference);
                    }
                }
            }

            /// <summary>
            /// 移除所有引用。
            /// </summary>
            public void RemoveAll()
            {
                lock (m_References)
                {
                    m_RemoveReferenceCount += m_References.Count;
                    m_References.Clear();
                    m_ReferenceSet.Clear();
                }
            }

            /// <summary>
            /// 引用相等性比较器（基于引用地址比较，避免值相等干扰）。
            /// </summary>
            private sealed class ReferenceEqualityComparer : IEqualityComparer<IReference>
            {
                public static readonly ReferenceEqualityComparer Instance = new ReferenceEqualityComparer();

                public bool Equals(IReference x, IReference y) => ReferenceEquals(x, y);

                public int GetHashCode(IReference obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
            }
        }
    }
    
}
