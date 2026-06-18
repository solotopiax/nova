/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  IObjectPoolManager.cs
 * author:    taoye
 * created:   2025/12/10
 * descrip:   对象池管理器接口
 ***************************************************************/

using System;
using System.Collections.Generic;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 对象池管理器接口。
    /// </summary>
    public interface IObjectPoolManager
    {
        /// <summary>
        /// 获取对象池数量。
        /// </summary>
        int Count { get; }

        /// <summary>
        /// 初始化。
        /// </summary>
        /// <param name="config">配置信息。</param>
        void Initialize(ObjectPoolManagerConfig config);

        /// <summary>
        /// 检查是否存在对象池。
        /// </summary>
        /// <typeparam name="T">对象类型。</typeparam>
        /// <returns>是否存在对象池。</returns>
        bool HasObjectPool<T>() where T : ObjectBase;

        /// <summary>
        /// 检查是否存在对象池。
        /// </summary>
        /// <param name="objectType">对象类型。</param>
        /// <returns>是否存在对象池。</returns>
        bool HasObjectPool(Type objectType);

        /// <summary>
        /// 检查是否存在对象池。
        /// </summary>
        /// <typeparam name="T">对象类型。</typeparam>
        /// <param name="name">对象池名称。</param>
        /// <returns>是否存在对象池。</returns>
        bool HasObjectPool<T>(string name) where T : ObjectBase;

        /// <summary>
        /// 检查是否存在对象池。
        /// </summary>
        /// <param name="objectType">对象类型。</param>
        /// <param name="name">对象池名称。</param>
        /// <returns>是否存在对象池。</returns>
        bool HasObjectPool(Type objectType, string name);

        /// <summary>
        /// 检查是否存在对象池。
        /// </summary>
        /// <param name="condition">要检查的条件。</param>
        /// <returns>是否存在对象池。</returns>
        bool HasObjectPool(Predicate<ObjectPoolBase> condition);

        /// <summary>
        /// 获取对象池。
        /// </summary>
        /// <typeparam name="T">对象类型。</typeparam>
        /// <returns>要获取的对象池。</returns>
        IObjectPool<T> GetObjectPool<T>() where T : ObjectBase;

        /// <summary>
        /// 获取对象池。
        /// </summary>
        /// <param name="objectType">对象类型。</param>
        /// <returns>要获取的对象池。</returns>
        ObjectPoolBase GetObjectPool(Type objectType);

        /// <summary>
        /// 获取对象池。
        /// </summary>
        /// <typeparam name="T">对象类型。</typeparam>
        /// <param name="name">对象池名称。</param>
        /// <returns>要获取的对象池。</returns>
        IObjectPool<T> GetObjectPool<T>(string name) where T : ObjectBase;

        /// <summary>
        /// 获取对象池。
        /// </summary>
        /// <param name="objectType">对象类型。</param>
        /// <param name="name">对象池名称。</param>
        /// <returns>要获取的对象池。</returns>
        ObjectPoolBase GetObjectPool(Type objectType, string name);

        /// <summary>
        /// 获取对象池。
        /// </summary>
        /// <param name="condition">要检查的条件。</param>
        /// <returns>要获取的对象池。</returns>
        ObjectPoolBase GetObjectPool(Predicate<ObjectPoolBase> condition);

        /// <summary>
        /// 获取对象池。
        /// </summary>
        /// <param name="condition">要检查的条件。</param>
        /// <returns>要获取的对象池。高频场景推荐使用 List 重载版本以避免数组分配。</returns>
        ObjectPoolBase[] GetObjectPools(Predicate<ObjectPoolBase> condition);

        /// <summary>
        /// 获取对象池。
        /// </summary>
        /// <param name="condition">要检查的条件。</param>
        /// <param name="results">要获取的对象池。</param>
        void GetObjectPools(Predicate<ObjectPoolBase> condition, List<ObjectPoolBase> results);

        /// <summary>
        /// 获取所有对象池。
        /// </summary>
        /// <returns>所有对象池。高频场景推荐使用 List 重载版本以避免数组分配。</returns>
        ObjectPoolBase[] GetAllObjectPools();

        /// <summary>
        /// 获取所有对象池。
        /// </summary>
        /// <param name="results">所有对象池。</param>
        void GetAllObjectPools(List<ObjectPoolBase> results);

        /// <summary>
        /// 获取所有对象池。
        /// </summary>
        /// <param name="sort">是否根据对象池的优先级排序。</param>
        /// <returns>所有对象池。高频场景推荐使用 List 重载版本以避免数组分配。</returns>
        ObjectPoolBase[] GetAllObjectPools(bool sort);

        /// <summary>
        /// 获取所有对象池。
        /// </summary>
        /// <param name="sort">是否根据对象池的优先级排序。</param>
        /// <param name="results">所有对象池。</param>
        void GetAllObjectPools(bool sort, List<ObjectPoolBase> results);

        /// <summary>
        /// 创建独占模式的对象池。对象被获取后标记为使用中，必须归还后才能再次被获取，适用于大多数常规场景。
        /// </summary>
        /// <typeparam name="T">对象类型。</typeparam>
        /// <param name="config">对象池创建配置，为 null 时使用默认值。</param>
        /// <returns>要创建的独占模式对象池。</returns>
        IObjectPool<T> CreateSingleGettingObjectPool<T>(ObjectPoolConfig config = null) where T : ObjectBase;

        /// <summary>
        /// 创建独占模式的对象池。对象被获取后标记为使用中，必须归还后才能再次被获取，适用于大多数常规场景。
        /// </summary>
        /// <param name="objectType">对象类型。</param>
        /// <param name="config">对象池创建配置，为 null 时使用默认值。</param>
        /// <returns>要创建的独占模式对象池。</returns>
        ObjectPoolBase CreateSingleGettingObjectPool(Type objectType, ObjectPoolConfig config = null);

        /// <summary>
        /// 创建共享模式的对象池。对象在使用中仍可被再次获取，同一实例可被多个持有者共享，适用于资源加载等需要引用计数的场景。
        /// </summary>
        /// <typeparam name="T">对象类型。</typeparam>
        /// <param name="config">对象池创建配置，为 null 时使用默认值。</param>
        /// <returns>要创建的共享模式对象池。</returns>
        IObjectPool<T> CreateMultiGettingObjectPool<T>(ObjectPoolConfig config = null) where T : ObjectBase;

        /// <summary>
        /// 创建共享模式的对象池。对象在使用中仍可被再次获取，同一实例可被多个持有者共享，适用于资源加载等需要引用计数的场景。
        /// </summary>
        /// <param name="objectType">对象类型。</param>
        /// <param name="config">对象池创建配置，为 null 时使用默认值。</param>
        /// <returns>要创建的共享模式对象池。</returns>
        ObjectPoolBase CreateMultiGettingObjectPool(Type objectType, ObjectPoolConfig config = null);

        /// <summary>
        /// 销毁对象池。
        /// </summary>
        /// <typeparam name="T">对象类型。</typeparam>
        /// <returns>是否销毁对象池成功。</returns>
        bool DestroyObjectPool<T>() where T : ObjectBase;

        /// <summary>
        /// 销毁对象池。
        /// </summary>
        /// <param name="objectType">对象类型。</param>
        /// <returns>是否销毁对象池成功。</returns>
        bool DestroyObjectPool(Type objectType);

        /// <summary>
        /// 销毁对象池。
        /// </summary>
        /// <typeparam name="T">对象类型。</typeparam>
        /// <param name="name">要销毁的对象池名称。</param>
        /// <returns>是否销毁对象池成功。</returns>
        bool DestroyObjectPool<T>(string name) where T : ObjectBase;

        /// <summary>
        /// 销毁对象池。
        /// </summary>
        /// <param name="objectType">对象类型。</param>
        /// <param name="name">要销毁的对象池名称。</param>
        /// <returns>是否销毁对象池成功。</returns>
        bool DestroyObjectPool(Type objectType, string name);

        /// <summary>
        /// 销毁对象池。
        /// </summary>
        /// <typeparam name="T">对象类型。</typeparam>
        /// <param name="objectPool">要销毁的对象池。</param>
        /// <returns>是否销毁对象池成功。</returns>
        bool DestroyObjectPool<T>(IObjectPool<T> objectPool) where T : ObjectBase;

        /// <summary>
        /// 销毁对象池。
        /// </summary>
        /// <param name="objectPool">要销毁的对象池。</param>
        /// <returns>是否销毁对象池成功。</returns>
        bool DestroyObjectPool(ObjectPoolBase objectPool);

        /// <summary>
        /// 释放对象池中的可释放对象。
        /// </summary>
        void Release();

        /// <summary>
        /// 释放对象池中的所有未使用对象。
        /// </summary>
        void ReleaseAllUnused();
    }
}
