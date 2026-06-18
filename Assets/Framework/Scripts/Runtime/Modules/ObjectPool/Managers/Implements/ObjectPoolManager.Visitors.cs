/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ObjectPoolManager.Visitors.cs
 * author:    taoye
 * created:   2025/12/10
 * descrip:   对象池管理器-访问器
 ***************************************************************/

using System;
using System.Collections.Generic;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 对象池管理器。
    /// </summary>
    internal sealed partial class ObjectPoolManager : ObjectPoolManagerBase
    {
        /// <summary>
        /// 对象池集合。
        /// </summary>
        private readonly Dictionary<TypeNamePair, ObjectPoolBase> m_ObjectPools;

        /// <summary>
        /// 缓存的所有对象池（避免反复 GC 开销）。
        /// </summary>
        private readonly List<ObjectPoolBase> m_CachedAllObjectPools;

        /// <summary>
        /// 对象池比较器（用于排序）。
        /// </summary>
        private readonly Comparison<ObjectPoolBase> m_ObjectPoolComparer;

        /// <summary>
        /// 获取对象池数量。
        /// </summary>
        public override int Count
        {
            get
            {
                return m_ObjectPools.Count;
            }
        }
    }
}
