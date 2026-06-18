/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  IReferenceHelper.cs
 * author:    taoye
 * created:   2026/1/19
 * descrip:   引用助手
 ***************************************************************/

using System;
using System.Collections.Generic;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 引用助手接口。
    /// </summary>
    public interface IReferenceHelper
    {
        /// <summary>
        /// 初始化。
        /// </summary>
        void Initialize(bool strictCheck);

        /// <summary>
        /// 清除所有引用池。
        /// </summary>
        void ClearAll();
        
        /// <summary>
        /// 当前引用池的数量。
        /// </summary>
        int GetPoolCount();

        /// <summary>
        /// 从引用池获取引用。
        /// </summary>
        /// <typeparam name="T">引用类型。</typeparam>
        /// <returns>引用实例。</returns>
        T Get<T>() where T : class, IReference, new();

        /// <summary>
        /// 从引用池获取引用。
        /// </summary>
        /// <param name="referenceType">引用类型。</param>
        /// <returns>引用实例。</returns>
        IReference Get(Type referenceType);
        
        /// <summary>
        /// 将引用归还到引用池。
        /// </summary>
        /// <param name="reference">引用实例。</param>
        void Put(IReference reference);

        /// <summary>
        /// 向引用池中追加指定数量的引用。
        /// </summary>
        /// <typeparam name="T">引用类型。</typeparam>
        /// <param name="count">追加数量。</param>
        void Add<T>(int count) where T : class, IReference, new();
        
        /// <summary>
        /// 向引用池中追加指定数量的引用。
        /// </summary>
        /// <param name="referenceType">引用类型。</param>
        /// <param name="count">追加数量。</param>
        void Add(Type referenceType, int count);

        /// <summary>
        /// 从引用池中移除指定数量的引用。
        /// </summary>
        /// <typeparam name="T">引用类型。</typeparam>
        /// <param name="count">移除数量。</param>
        void Remove<T>(int count) where T : class, IReference;
        
        /// <summary>
        /// 从引用池中移除指定数量的引用。
        /// </summary>
        /// <param name="referenceType">引用类型。</param>
        /// <param name="count">移除数量。</param>
        void Remove(Type referenceType, int count);

        /// <summary>
        /// 从引用池中移除所有引用。
        /// </summary>
        /// <typeparam name="T">引用类型。</typeparam>
        void RemoveAll<T>() where T : class, IReference;
        
        /// <summary>
        /// 从引用池中移除所有引用。
        /// </summary>
        /// <param name="referenceType">引用类型。</param>
        void RemoveAll(Type referenceType);
        
        /// <summary>
        /// 获取所有引用池的信息。
        /// </summary>
        /// <returns>引用池信息数组。</returns>
        IReadOnlyList<ReferencePoolInfo> GetAllReferencePoolInfos();
    }
}
