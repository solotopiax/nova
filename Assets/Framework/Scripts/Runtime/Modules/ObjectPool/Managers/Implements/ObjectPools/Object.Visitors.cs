/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  Object.Visitors.cs
 * author:    taoye
 * created:   2025/12/10
 * descrip:   对象-访问器
 ***************************************************************/

using System;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 内部对象。
    /// 管理引用计数
    /// </summary>
    /// <typeparam name="T">对象类型。</typeparam>
    internal sealed partial class Object<T> : IReference where T : ObjectBase
    {
        /// <summary>
        /// 内部对象实例。
        /// </summary>
        private T m_Object;

        /// <summary>
        /// 内部对象的引用计数。
        /// </summary>
        private int m_RefCount;

        /// <summary>
        /// 获取对象名称。
        /// </summary>
        public string Name => m_Object.Name;

        /// <summary>
        /// 获取对象是否被加锁。
        /// 被加锁对象不会被回收。
        /// </summary>
        public bool Locked
        {
            get => m_Object.Locked;
            internal set => m_Object.Locked = value;
        }

        /// <summary>
        /// 获取对象的优先级（值越小，优先级越高）。
        /// </summary>
        public int Priority
        {
            get => m_Object.Priority;
            internal set => m_Object.Priority = value;
        }

        /// <summary>
        /// 获取自定义释放检查标记。
        /// </summary>
        public bool CustomCanReleaseFlag => m_Object.CustomCanReleaseFlag;

        /// <summary>
        /// 获取对象上次使用时间。
        /// </summary>
        public DateTime LastUseTime => m_Object.LastUseTime;

        /// <summary>
        /// 获取对象是否正在使用。
        /// </summary>
        public bool IsInUse => m_RefCount > 0;

        /// <summary>
        /// 获取对象的获取计数。
        /// </summary>
        public int RefCount => m_RefCount;
    }
}
