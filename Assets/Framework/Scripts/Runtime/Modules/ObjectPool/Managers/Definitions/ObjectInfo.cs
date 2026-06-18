/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ObjectInfo.cs
 * author:    taoye
 * created:   2025/12/10
 * descrip:   对象信息
 ***************************************************************/

using System;
using System.Runtime.InteropServices;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 对象信息。
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    public struct ObjectInfo
    {
        /// <summary>
        /// 对象名称。
        /// </summary>
        private readonly string m_Name;
        public string Name => m_Name;
        
        /// <summary>
        /// 对象是否被加锁。
        /// 被加锁对象不会被回收。
        /// </summary>
        private readonly bool m_Locked;
        public bool Locked => m_Locked;
        
        /// <summary>
        /// 对象自定义释放检查标记。
        /// </summary>
        private readonly bool m_CustomCanReleaseFlag;
        public bool CustomCanReleaseFlag => m_CustomCanReleaseFlag;
        
        /// <summary>
        /// 对象的优先级（值越小，优先级越高）。
        /// </summary>
        private readonly int m_Priority;
        public int Priority => m_Priority;
        
        /// <summary>
        /// 对象上次使用时间。
        /// </summary>
        private readonly DateTime m_LastUseTime;
        public DateTime LastUseTime => m_LastUseTime;
        
        /// <summary>
        /// 对象的获取计数。
        /// </summary>
        private readonly int m_RefCount;
        public int RefCount => m_RefCount;
        
        /// <summary>
        /// 获取对象是否正在使用。
        /// </summary>
        public bool IsInUse => m_RefCount > 0;

        /// <summary>
        /// 初始化对象信息的新实例。
        /// </summary>
        /// <param name="name">对象名称。</param>
        /// <param name="locked">对象是否被加锁。</param>
        /// <param name="customCanReleaseFlag">对象自定义释放检查标记。</param>
        /// <param name="priority">对象的优先级。</param>
        /// <param name="lastUseTime">对象上次使用时间。</param>
        /// <param name="refCount">对象的获取计数。</param>
        public ObjectInfo(string name, bool locked, bool customCanReleaseFlag, int priority, DateTime lastUseTime, int refCount)
        {
            m_Name = name;
            m_Locked = locked;
            m_CustomCanReleaseFlag = customCanReleaseFlag;
            m_Priority = priority;
            m_LastUseTime = lastUseTime;
            m_RefCount = refCount;
        }
    }
}
