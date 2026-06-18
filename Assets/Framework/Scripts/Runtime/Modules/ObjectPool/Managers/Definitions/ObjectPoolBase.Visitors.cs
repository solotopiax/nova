/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ObjectPoolBase.Visitors.cs
 * author:    taoye
 * created:   2025/12/10
 * descrip:   对象池基类-访问器
 ***************************************************************/

using System;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 对象池基类。
    /// </summary>
    public abstract partial class ObjectPoolBase
    {
        /// <summary>
        /// 对象池名称。
        /// </summary>
        private readonly string m_Name;

        /// <summary>
        /// 对象池完整名称的缓存（首次访问时计算）。
        /// </summary>
        private string m_FullName;

        /// <summary>
        /// 获取对象池名称。
        /// </summary>
        public string Name => m_Name;

        /// <summary>
        /// 获取对象池完整名称。
        /// </summary>
        public string FullName => m_FullName ?? (m_FullName = new TypeNamePair(ObjectType, m_Name).ToString());

        /// <summary>
        /// 获取对象池对象类型。
        /// </summary>
        public abstract Type ObjectType { get; }

        /// <summary>
        /// 获取对象池中对象的数量。
        /// </summary>
        public abstract int Count { get; }

        /// <summary>
        /// 获取对象池中能被释放的对象的数量。
        /// </summary>
        public abstract int CanReleaseCount { get; }

        /// <summary>
        /// 获取是否允许对象被多次获取。
        /// </summary>
        public abstract bool AllowMultiGet { get; }

        /// <summary>
        /// 获取或设置对象池自动释放可释放对象的间隔秒数。
        /// </summary>
        public abstract float AutoReleaseInterval { set; get; }

        /// <summary>
        /// 获取对象池自动释放时间计数器的当前值。
        /// </summary>
        public abstract float AutoReleaseTimeCounter { get; }

        /// <summary>
        /// 获取或设置对象池的容量。
        /// </summary>
        public abstract int Capacity { set; get; }

        /// <summary>
        /// 获取或设置对象池对象过期秒数。
        /// </summary>
        public abstract float ExpireTime { set; get; }

        /// <summary>
        /// 获取或设置对象池的优先级。
        /// </summary>
        public abstract int Priority { set; get; }
    }
}
