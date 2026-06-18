/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EventTypeID.cs
 * author:    taoye
 * created:   2026/4/11
 * descrip:   事件类型静态注册表
 ***************************************************************/

using System;
using System.Collections.Concurrent;
using System.Threading;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 事件类型 ID 静态注册表。
    /// 为每个 EventData 子类分配全局唯一的自增 int ID，替代 Type.GetHashCode() 以避免哈希碰撞。
    /// </summary>
    public static class EventTypeID
    {
        /// <summary>
        /// 自增计数器（从 1 开始，0 保留为无效值）。
        /// </summary>
        private static int s_Counter = 0;

        /// <summary>
        /// 类型到 ID 的映射缓存。
        /// </summary>
        private static readonly ConcurrentDictionary<Type, int> s_TypeToID = new ConcurrentDictionary<Type, int>();

        /// <summary>
        /// 获取指定类型的唯一事件 ID，首次调用时自动注册。
        /// </summary>
        /// <param name="type">事件数据类型。</param>
        /// <returns>全局唯一的事件 ID。</returns>
        public static int Get(Type type)
        {
            return s_TypeToID.GetOrAdd(type, _ => Interlocked.Increment(ref s_Counter));
        }

        /// <summary>
        /// 获取指定泛型类型的唯一事件 ID，首次调用时自动注册。
        /// </summary>
        /// <typeparam name="T">事件数据类型。</typeparam>
        /// <returns>全局唯一的事件 ID。</returns>
        public static int Get<T>() where T : EventData
        {
            return TypeCache<T>.ID;
        }

        /// <summary>
        /// 泛型类型缓存（利用 CLR 静态泛型特化，零查找开销）。
        /// </summary>
        /// <typeparam name="T">事件数据类型。</typeparam>
        private static class TypeCache<T> where T : EventData
        {
            /// <summary>
            /// 当前泛型类型的唯一事件 ID。
            /// </summary>
            public static readonly int ID = Get(typeof(T));
        }
    }
}
