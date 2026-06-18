/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ObjectPool.Visitors.cs
 * author:    taoye
 * created:   2025/12/10
 * descrip:   对象池-访问器
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
        /// 释放对象比较器委托缓存（避免每次 Sort 产生委托分配）。
        /// </summary>
        private static readonly Comparison<T> s_ReleaseObjectComparer = ReleaseObjectComparer;

        /// <summary>
        /// 对象集合（按 Name 索引，支持同名多实例）。
        /// 格式：【对象集合唯一标识：对象集合】。
        /// </summary>
        private readonly NovaMultiDictionary<string, Object<T>> m_Objects;

        /// <summary>
        /// 本体对象与对象的映射表（按 Target 索引）。
        /// </summary>
        private readonly Dictionary<object, Object<T>> m_ObjectMap;

        /// <summary>
        /// 默认释放对象筛选器。
        /// </summary>
        private readonly ReleaseObjectsFilter<T> m_DefaultReleaseObjectsFilter;

        /// <summary>
        /// 缓存的可以被释放的对象集合。
        /// </summary>
        private readonly List<T> m_CachedCanReleaseObjects;

        /// <summary>
        /// 缓存的马上要被释放的对象集合（通过释放对象筛选器筛选后的对象集合）。
        /// </summary>
        private readonly List<T> m_CachedToReleaseObjects;

        /// <summary>
        /// 缓存的非过期候选对象集合（DefaultReleaseObjectsFilter 第二轮排序时使用）。
        /// </summary>
        private readonly List<T> m_CachedNonExpiredObjects;

        /// <summary>
        /// 缓存的对象信息列表（GetAllObjectInfos 复用以避免重复分配）。
        /// </summary>
        private readonly List<ObjectInfo> m_CachedObjectInfos;

        /// <summary>
        /// 是否允许被多次获取。
        /// </summary>
        private readonly bool m_AllowMultiGet;
        public override bool AllowMultiGet => m_AllowMultiGet;

        /// <summary>
        /// 对象池自动释放可释放对象的间隔秒数。
        /// </summary>
        private float m_AutoReleaseInterval;
        public override float AutoReleaseInterval
        {
            get => m_AutoReleaseInterval;
            set => m_AutoReleaseInterval = value;
        }

        /// <summary>
        /// 对象池自动释放可释放对象的时间计数器。
        /// </summary>
        private float m_AutoReleaseTimeCounter;
        public override float AutoReleaseTimeCounter => m_AutoReleaseTimeCounter;

        /// <summary>
        /// 对象池的容量。
        /// </summary>
        private int m_Capacity;
        public override int Capacity
        {
            get => m_Capacity;
            set
            {
                if (value < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), "Capacity 无效。");
                }

                if (m_Capacity == value)
                {
                    return;
                }

                m_Capacity = value;
                Release();
            }
        }

        /// <summary>
        /// 对象池中对象的过期秒数。
        /// </summary>
        private float m_ExpireTime;
        public override float ExpireTime
        {
            get => m_ExpireTime;
            set
            {
                if (value < 0f)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), "ExpireTime 无效。");
                }

                if (ExpireTime == value)
                {
                    return;
                }

                m_ExpireTime = value;
                Release();
            }
        }

        /// <summary>
        /// 对象池的优先级（数值越小，优先级越高）。
        /// </summary>
        private int m_Priority;
        public override int Priority
        {
            get => m_Priority;
            set => m_Priority = value;
        }

        /// <summary>
        /// 获取对象池对象类型。
        /// </summary>
        public override Type ObjectType => typeof(T);

        /// <summary>
        /// 获取对象池中对象的数量。
        /// </summary>
        public override int Count => m_ObjectMap.Count;

        /// <summary>
        /// 获取对象池中能被释放的对象的数量。
        /// </summary>
        public override int CanReleaseCount => GetCanReleaseCount();
    }
}
