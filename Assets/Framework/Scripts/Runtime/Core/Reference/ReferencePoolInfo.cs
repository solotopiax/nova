/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ReferencePoolInfo.cs
 * author:    taoye
 * created:   2025/12/4
 * descrip:   引用池信息
 ***************************************************************/

using System;
using System.Runtime.InteropServices;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 引用池信息。
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    public struct ReferencePoolInfo
    {
        /// <summary>
        /// 引用池管理的引用类型。
        /// </summary>
        private readonly Type m_Type;
        public Type Type => m_Type;

        /// <summary>
        /// 当前未使用的引用数量（对象池中等待复用的实例数量）。
        /// </summary>
        private readonly int m_UnusedReferenceCount;
        public int UnusedReferenceCount => m_UnusedReferenceCount;

        /// <summary>
        /// 当前正在使用的引用数量（已从对象池取出但尚未归还的实例数量）。
        /// </summary>
        private readonly int m_UsingReferenceCount;
        public int UsingReferenceCount => m_UsingReferenceCount;

        /// <summary>
        /// 累积获取引用的次数（包含从对象池取出及因池空而新建的实例总次数）。
        /// </summary>
        private readonly int m_GetReferenceCount;
        public int GetReferenceCount => m_GetReferenceCount;

        /// <summary>
        /// 累积归还引用的次数（对象被回收到池中的总次数）。
        /// </summary>
        private readonly int m_PutReferenceCount;
        public int PutReferenceCount => m_PutReferenceCount;

        /// <summary>
        /// 累积新增引用的数量（对象池内部因池空或预加载而创建的新实例数量）。
        /// </summary>
        private readonly int m_AddReferenceCount;
        public int AddReferenceCount => m_AddReferenceCount;

        /// <summary>
        /// 累积移除引用的数量（被从对象池中永久删除的实例数量）。
        /// </summary>
        private readonly int m_RemoveReferenceCount;
        public int RemoveReferenceCount => m_RemoveReferenceCount;

        /// <summary>
        /// 初始化引用池信息的新实例。
        /// </summary>
        /// <param name="type">引用类型。</param>
        /// <param name="unusedReferenceCount">未使用引用数量。</param>
        /// <param name="usingReferenceCount">正在使用引用数量。</param>
        /// <param name="getReferenceCount">累积获取数量。</param>
        /// <param name="putReferenceCount">累积归还数量。</param>
        /// <param name="addReferenceCount">累积新增数量。</param>
        /// <param name="removeReferenceCount">累积移除数量。</param>
        public ReferencePoolInfo(
            Type type,
            int unusedReferenceCount,
            int usingReferenceCount,
            int getReferenceCount,
            int putReferenceCount,
            int addReferenceCount,
            int removeReferenceCount)
        {
            m_Type = type;
            m_UnusedReferenceCount = unusedReferenceCount;
            m_UsingReferenceCount = usingReferenceCount;
            m_GetReferenceCount = getReferenceCount;
            m_PutReferenceCount = putReferenceCount;
            m_AddReferenceCount = addReferenceCount;
            m_RemoveReferenceCount = removeReferenceCount;
        }
        
    }
}
