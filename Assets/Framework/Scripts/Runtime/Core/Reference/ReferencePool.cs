/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ReferencePool.cs
 * author:    taoye
 * created:   2026/1/19
 * descrip:   引用池
 ***************************************************************/

using System;
using System.Collections.Generic;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 引用池。
    /// </summary>
    public static class ReferencePool
    {
        /// <summary>
        /// 引用池助手对象。
        /// </summary>
        private static IReferenceHelper s_ReferenceHelper;

        /// <summary>
        /// 设置引用辅助器。
        /// </summary>
        /// <param name="helper">要设置的引用辅助器。</param>
        public static void SetHelper(IReferenceHelper helper)
        {
            s_ReferenceHelper = helper;
        }

        /// <summary>
        /// 获取引用。
        /// </summary>
        /// <typeparam name="T">引用类型。</typeparam>
        /// <returns>引用实例。</returns>
        public static T Get<T>() where T : class, IReference, new()
        {
            return EnsureHelper().Get<T>();
        }

        /// <summary>
        /// 获取引用。
        /// </summary>
        /// <param name="referenceType">引用类型。</param>
        /// <returns>引用实例。</returns>
        public static IReference Get(Type referenceType)
        {
            return EnsureHelper().Get(referenceType);
        }

        /// <summary>
        /// 归还引用。
        /// </summary>
        /// <param name="reference">引用实例。</param>
        public static void Put(IReference reference)
        {
            EnsureHelper().Put(reference);
        }

        /// <summary>
        /// 追加引用。
        /// </summary>
        /// <typeparam name="T">引用类型。</typeparam>
        /// <param name="count">追加数量。</param>
        public static void Add<T>(int count) where T : class, IReference, new()
        {
            EnsureHelper().Add<T>(count);
        }

        /// <summary>
        /// 追加引用。
        /// </summary>
        /// <param name="referenceType">引用类型。</param>
        /// <param name="count">追加数量。</param>
        public static void Add(Type referenceType, int count)
        {
            EnsureHelper().Add(referenceType, count);
        }

        /// <summary>
        /// 移除引用。
        /// </summary>
        /// <typeparam name="T">引用类型。</typeparam>
        /// <param name="count">移除数量。</param>
        public static void Remove<T>(int count) where T : class, IReference
        {
            EnsureHelper().Remove<T>(count);
        }

        /// <summary>
        /// 移除引用。
        /// </summary>
        /// <param name="referenceType">引用类型。</param>
        /// <param name="count">移除数量。</param>
        public static void Remove(Type referenceType, int count)
        {
            EnsureHelper().Remove(referenceType, count);
        }

        /// <summary>
        /// 移除所有引用。
        /// </summary>
        /// <typeparam name="T">引用类型。</typeparam>
        public static void RemoveAll<T>() where T : class, IReference
        {
            EnsureHelper().RemoveAll<T>();
        }

        /// <summary>
        /// 移除所有引用。
        /// </summary>
        /// <param name="referenceType">引用类型。</param>
        public static void RemoveAll(Type referenceType)
        {
            EnsureHelper().RemoveAll(referenceType);
        }

        /// <summary>
        /// 清空所有引用池。
        /// </summary>
        public static void ClearAll()
        {
            EnsureHelper().ClearAll();
        }

        /// <summary>
        /// 当前引用池数量。
        /// </summary>
        /// <returns>引用池数量。</returns>
        public static int GetPoolCount()
        {
            return EnsureHelper().GetPoolCount();
        }

        /// <summary>
        /// 获取所有引用池信息。
        /// </summary>
        /// <returns>所有引用池信息列表。</returns>
        public static IReadOnlyList<ReferencePoolInfo> GetAllReferencePoolInfos()
        {
            return EnsureHelper().GetAllReferencePoolInfos();
        }

        /// <summary>
        /// 确保引用辅助器已初始化。
        /// </summary>
        /// <returns>引用辅助器实例。</returns>
        private static IReferenceHelper EnsureHelper()
        {
            if (s_ReferenceHelper == null)
            {
                throw new InvalidOperationException("ReferencePool 尚未初始化，请先调用 ReferencePool.SetHelper。");
            }
            return s_ReferenceHelper;
        }

    }
}
