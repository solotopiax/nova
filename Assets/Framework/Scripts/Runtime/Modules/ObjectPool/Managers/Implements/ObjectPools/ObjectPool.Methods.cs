/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ObjectPool.Methods.cs
 * author:    taoye
 * created:   2025/12/10
 * descrip:   对象池-方法
 ***************************************************************/

using System;
using System.Collections.Generic;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 对象池。
    /// </summary>
    /// <typeparam name="T">对象类型。</typeparam>
    internal sealed partial class ObjectPool<T> : ObjectPoolBase, IObjectPool<T> where T : ObjectBase
    {
        /// <summary>
        /// 获取可释放对象的数量（仅计数，不填充缓存列表）。
        /// </summary>
        /// <returns>可释放对象数量。</returns>
        private int GetCanReleaseCount()
        {
            int count = 0;
            foreach (KeyValuePair<object, Object<T>> pair in m_ObjectMap)
            {
                Object<T> internalObject = pair.Value;
                if (internalObject.IsInUse || internalObject.Locked || !internalObject.CustomCanReleaseFlag)
                {
                    continue;
                }

                count++;
            }

            return count;
        }

        /// <summary>
        /// 获取对象。
        /// </summary>
        /// <param name="target">本体对象。</param>
        /// <returns>内部对象包装器，未找到时返回 null。</returns>
        /// <exception cref="ArgumentNullException">target 为空时抛出。</exception>
        private Object<T> GetInternalObject(object target)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target), "target 无效。");
            }

            if (m_ObjectMap.TryGetValue(target, out Object<T> internalObject))
            {
                return internalObject;
            }

            return null;
        }

        /// <summary>
        /// 获取可以被释放的对象集合。
        /// </summary>
        /// <param name="results">可释放对象列表。</param>
        /// <exception cref="ArgumentNullException">results 为空时抛出。</exception>
        private void GetCanReleaseObjects(List<T> results)
        {
            if (results == null)
            {
                throw new ArgumentNullException(nameof(results), "results 无效。");
            }

            results.Clear();
            foreach (KeyValuePair<object, Object<T>> objectInMap in m_ObjectMap)
            {
                Object<T> internalObject = objectInMap.Value;
                if (internalObject.IsInUse || internalObject.Locked || !internalObject.CustomCanReleaseFlag)
                {
                    continue;
                }

                results.Add(internalObject.Peek());
            }
        }

        /// <summary>
        /// 默认释放对象过滤器（实现）。
        /// 不修改传入的 candidateObjects 列表，确保调用方数据不受影响。
        /// </summary>
        /// <param name="candidateObjects">待筛查对象集合。</param>
        /// <param name="toReleaseCount">目标数量。</param>
        /// <param name="expireTime">过期时间。</param>
        /// <returns>经筛选需要释放的对象集合。</returns>
        private List<T> DefaultReleaseObjectsFilter(List<T> candidateObjects, int toReleaseCount, DateTime expireTime)
        {
            m_CachedToReleaseObjects.Clear();

            if (expireTime > DateTime.MinValue)
            {
                for (int i = 0; i < candidateObjects.Count; i++)
                {
                    if (candidateObjects[i].LastUseTime <= expireTime)
                    {
                        m_CachedToReleaseObjects.Add(candidateObjects[i]);
                    }
                }

                toReleaseCount -= m_CachedToReleaseObjects.Count;
            }

            if (toReleaseCount > 0)
            {
                m_CachedNonExpiredObjects.Clear();
                for (int i = 0; i < candidateObjects.Count; i++)
                {
                    if (!m_CachedToReleaseObjects.Contains(candidateObjects[i]))
                    {
                        m_CachedNonExpiredObjects.Add(candidateObjects[i]);
                    }
                }

                if (m_CachedNonExpiredObjects.Count > 0)
                {
                    // 使用 O(n log n) 排序替代 O(n^2) 选择排序：按优先级升序，相同优先级按最久未使用排序。
                    m_CachedNonExpiredObjects.Sort(s_ReleaseObjectComparer);
                    int releaseCount = Math.Min(toReleaseCount, m_CachedNonExpiredObjects.Count);
                    for (int i = 0; i < releaseCount; i++)
                    {
                        m_CachedToReleaseObjects.Add(m_CachedNonExpiredObjects[i]);
                    }
                }
            }

            return m_CachedToReleaseObjects;
        }

        /// <summary>
        /// 释放对象比较器（按优先级升序，相同优先级按最久未使用排序）。
        /// </summary>
        /// <param name="a">对象 a。</param>
        /// <param name="b">对象 b。</param>
        /// <returns>比较结果。</returns>
        private static int ReleaseObjectComparer(T a, T b)
        {
            int priorityCompare = a.Priority.CompareTo(b.Priority);
            if (priorityCompare != 0)
            {
                return priorityCompare;
            }

            return a.LastUseTime.CompareTo(b.LastUseTime);
        }
    }
}
