/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ReferenceHelper.cs
 * author:    taoye
 * created:   2026/1/19
 * descrip:   引用助手
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
        /// 引用池集合。
        /// 格式：【类型，引用集合】。
        /// </summary>
        private readonly Dictionary<Type, ReferenceCollection> m_ReferencePools = new Dictionary<Type, ReferenceCollection>();
        
        /// <summary>
        /// 是否开启强制检查。
        /// </summary>
        private bool m_StrictCheck;
        
        /// <summary>
        /// 初始化。
        /// </summary>
        public void Initialize(bool strictCheck)
        {
            m_StrictCheck = strictCheck;
        }

        /// <summary>
        /// 清除所有引用池。
        /// </summary>
        public void ClearAll()
        {
            lock (m_ReferencePools)
            {
                foreach (KeyValuePair<Type, ReferenceCollection> poolItr in m_ReferencePools)
                {
                    poolItr.Value.RemoveAll();
                }

                m_ReferencePools.Clear();
            }
        }

        /// <summary>
        /// 当前引用池的数量。
        /// </summary>
        public int GetPoolCount()
        {
            lock (m_ReferencePools)
            {
                return m_ReferencePools.Count;
            }
        }

        /// <summary>
        /// 从引用池获取引用。
        /// </summary>
        /// <typeparam name="T">引用类型。</typeparam>
        /// <returns>引用实例。</returns>
        public T Get<T>() where T : class, IReference, new()
        {
            return (T)Get(typeof(T));
        }

        /// <summary>
        /// 从引用池获取引用。
        /// </summary>
        /// <param name="referenceType">引用类型。</param>
        /// <returns>引用实例。</returns>
        public IReference Get(Type referenceType)
        {
            InternalCheckReferenceType(referenceType);
            return GetReferencePool(referenceType).Get();
        }

        /// <summary>
        /// 将引用归还到引用池。
        /// </summary>
        /// <param name="reference">引用实例。</param>
        public void Put(IReference reference)
        {
            if (reference == null)
            {
                throw new ArgumentNullException(nameof(reference), "reference 无效。");
            }

            Type referenceType = reference.GetType();
            if (m_StrictCheck)
            {
                InternalCheckReferenceType(referenceType);
            }
            GetReferencePool(referenceType).Put(reference);
        }

        /// <summary>
        /// 向引用池中追加指定数量的引用。
        /// </summary>
        /// <typeparam name="T">引用类型。</typeparam>
        /// <param name="count">追加数量。</param>
        public void Add<T>(int count) where T : class, IReference, new()
        {
            Add(typeof(T), count);
        }

        /// <summary>
        /// 向引用池中追加指定数量的引用。
        /// </summary>
        /// <param name="referenceType">引用类型。</param>
        /// <param name="count">追加数量。</param>
        public void Add(Type referenceType, int count)
        {
            InternalCheckReferenceType(referenceType);
            GetReferencePool(referenceType).Add(count);
        }

        /// <summary>
        /// 从引用池中移除指定数量的引用。
        /// </summary>
        /// <typeparam name="T">引用类型。</typeparam>
        /// <param name="count">移除数量。</param>
        public void Remove<T>(int count) where T : class, IReference
        {
            Remove(typeof(T), count);
        }

        /// <summary>
        /// 从引用池中移除指定数量的引用。
        /// </summary>
        /// <param name="referenceType">引用类型。</param>
        /// <param name="count">移除数量。</param>
        public void Remove(Type referenceType, int count)
        {
            InternalCheckReferenceType(referenceType);
            GetReferencePool(referenceType).Remove(count);
        }

        /// <summary>
        /// 从引用池中移除所有引用。
        /// </summary>
        /// <typeparam name="T">引用类型。</typeparam>
        public void RemoveAll<T>() where T : class, IReference
        {
            RemoveAll(typeof(T));
        }

        /// <summary>
        /// 从引用池中移除所有引用。
        /// </summary>
        /// <param name="referenceType">引用类型。</param>
        public void RemoveAll(Type referenceType)
        {
            InternalCheckReferenceType(referenceType);
            GetReferencePool(referenceType).RemoveAll();
        }

        /// <summary>
        /// 获取所有引用池的信息。
        /// </summary>
        /// <returns>所有引用池的信息。</returns>
        public IReadOnlyList<ReferencePoolInfo> GetAllReferencePoolInfos()
        {
            int index = 0;
            ReferencePoolInfo[] results = null;

            lock (m_ReferencePools)
            {
                results = new ReferencePoolInfo[m_ReferencePools.Count];
                foreach (KeyValuePair<Type, ReferenceCollection> poolItr in m_ReferencePools)
                {
                    results[index++] = new ReferencePoolInfo(
                        poolItr.Key,
                        poolItr.Value.UnusedReferenceCount,
                        poolItr.Value.UsingReferenceCount,
                        poolItr.Value.GetReferenceCount,
                        poolItr.Value.PutReferenceCount,
                        poolItr.Value.AddReferenceCount,
                        poolItr.Value.RemoveReferenceCount
                    );
                }
            }

            return results;
        }
        
        /// <summary>
        /// 内部引用类型检查。
        /// </summary>
        private void InternalCheckReferenceType(Type referenceType)
        {
            if (!m_StrictCheck)
            {
                return;
            }

            if (referenceType == null)
            {
                throw new ArgumentNullException(nameof(referenceType), "referenceType 无效。");
            }

            if (!referenceType.IsClass || referenceType.IsAbstract)
            {
                throw new ArgumentException(Txt.Format("referenceType '{0}' 必须为 class 且必须为非抽象 class。", referenceType.FullName), nameof(referenceType));
            }

            if (!typeof(IReference).IsAssignableFrom(referenceType))
            {
                throw new ArgumentException(Txt.Format("referenceType '{0}' 必须基于 IReference 派生。", referenceType.FullName), nameof(referenceType));
            }
        }

        /// <summary>
        /// 获取指定类型的引用池。
        /// </summary>
        private ReferenceCollection GetReferencePool(Type referenceType)
        {
            if (referenceType == null)
            {
                throw new ArgumentNullException(nameof(referenceType), "referenceType 无效。");
            }

            ReferenceCollection referenceCollection = null;

            lock (m_ReferencePools)
            {
                if (!m_ReferencePools.TryGetValue(referenceType, out referenceCollection))
                {
                    referenceCollection = new ReferenceCollection(referenceType, m_StrictCheck);
                    m_ReferencePools.Add(referenceType, referenceCollection);
                }
            }

            return referenceCollection;
        }
        
    }
}
