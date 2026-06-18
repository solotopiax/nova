/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ObjectBase.Visitors.cs
 * author:    taoye
 * created:   2025/12/10
 * descrip:   对象基类-访问器
 ***************************************************************/

using System;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 对象基类。
    /// 管理真实资源的清理
    /// </summary>
    public abstract partial class ObjectBase : IReference
    {
        /// <summary>
        /// 对象名称。
        /// </summary>
        private string m_Name;
        public string Name => m_Name;

        /// <summary>
        /// 目标对象。
        /// </summary>
        private object m_Target;
        public object Target => m_Target;

        /// <summary>
        /// 对象是否被加锁。
        /// 被加锁对象不会被回收。
        /// </summary>
        private bool m_Locked;
        public bool Locked
        {
            get => m_Locked;
            internal set => m_Locked = value;
        }

        /// <summary>
        /// 对象优先级（值越小，优先级越高）。
        /// </summary>
        private int m_Priority;
        public int Priority
        {
            get => m_Priority;
            internal set => m_Priority = value;
        }

        /// <summary>
        /// 对象上次使用时间。
        /// </summary>
        private DateTime m_LastUseTime;
        public DateTime LastUseTime
        {
            get => m_LastUseTime;
            internal set => m_LastUseTime = value;
        }

        /// <summary>
        /// 获取自定义释放检查标记。
        /// </summary>
        public virtual bool CustomCanReleaseFlag => true;
    }
}
