/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ObjectPoolComponent.cs
 * author:    taoye
 * created:   2025/12/10
 * descrip:   对象池组件
 ***************************************************************/

using System;
using System.Collections.Generic;
using UnityEngine;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 对象池组件。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed partial class ObjectPoolComponent : FrameworkComponent
    {
        /// <summary>
        /// 唤醒。
        /// </summary>
        protected override void Awake()
        {
            base.Awake();
            m_ObjectPoolManager = Util.TypeCreator.Create<IObjectPoolManager>(m_CurManagerTypeName);
            if (m_ObjectPoolManager == null)
            {
                throw new InvalidOperationException("ObjectPoolManager 无效。");
            }
        }

        /// <summary>
        /// 开始。
        /// </summary>
        private void Start()
        {
            m_ObjectPoolManager.Initialize(new ObjectPoolManagerConfig(){});
        }

        /// <summary>
        /// 销毁。
        /// </summary>
        private void OnDestroy()
        {
            m_ObjectPoolManager = null;
        }

        /// <summary>
        /// 检查是否存在对象池。
        /// </summary>
        /// <typeparam name="T">对象类型。</typeparam>
        /// <returns>是否存在对象池。</returns>
        public bool HasObjectPool<T>() where T : ObjectBase
        {
            return m_ObjectPoolManager.HasObjectPool<T>();
        }

        /// <summary>
        /// 检查是否存在对象池。
        /// </summary>
        /// <param name="objectType">对象类型。</param>
        /// <returns>是否存在对象池。</returns>
        public bool HasObjectPool(Type objectType)
        {
            return m_ObjectPoolManager.HasObjectPool(objectType);
        }

        /// <summary>
        /// 检查是否存在对象池。
        /// </summary>
        /// <typeparam name="T">对象类型。</typeparam>
        /// <param name="name">对象池名称。</param>
        /// <returns>是否存在对象池。</returns>
        public bool HasObjectPool<T>(string name) where T : ObjectBase
        {
            return m_ObjectPoolManager.HasObjectPool<T>(name);
        }

        /// <summary>
        /// 检查是否存在对象池。
        /// </summary>
        /// <param name="objectType">对象类型。</param>
        /// <param name="name">对象池名称。</param>
        /// <returns>是否存在对象池。</returns>
        public bool HasObjectPool(Type objectType, string name)
        {
            return m_ObjectPoolManager.HasObjectPool(objectType, name);
        }

        /// <summary>
        /// 检查是否存在对象池。
        /// </summary>
        /// <param name="condition">要检查的条件。</param>
        /// <returns>是否存在对象池。</returns>
        public bool HasObjectPool(Predicate<ObjectPoolBase> condition)
        {
            return m_ObjectPoolManager.HasObjectPool(condition);
        }

        /// <summary>
        /// 获取对象池。
        /// </summary>
        /// <typeparam name="T">对象类型。</typeparam>
        /// <returns>要获取的对象池。</returns>
        public IObjectPool<T> GetObjectPool<T>() where T : ObjectBase
        {
            return m_ObjectPoolManager.GetObjectPool<T>();
        }

        /// <summary>
        /// 获取对象池。
        /// </summary>
        /// <param name="objectType">对象类型。</param>
        /// <returns>要获取的对象池。</returns>
        public ObjectPoolBase GetObjectPool(Type objectType)
        {
            return m_ObjectPoolManager.GetObjectPool(objectType);
        }

        /// <summary>
        /// 获取对象池。
        /// </summary>
        /// <typeparam name="T">对象类型。</typeparam>
        /// <param name="name">对象池名称。</param>
        /// <returns>要获取的对象池。</returns>
        public IObjectPool<T> GetObjectPool<T>(string name) where T : ObjectBase
        {
            return m_ObjectPoolManager.GetObjectPool<T>(name);
        }

        /// <summary>
        /// 获取对象池。
        /// </summary>
        /// <param name="objectType">对象类型。</param>
        /// <param name="name">对象池名称。</param>
        /// <returns>要获取的对象池。</returns>
        public ObjectPoolBase GetObjectPool(Type objectType, string name)
        {
            return m_ObjectPoolManager.GetObjectPool(objectType, name);
        }

        /// <summary>
        /// 获取对象池。
        /// </summary>
        /// <param name="condition">要检查的条件。</param>
        /// <returns>要获取的对象池。</returns>
        public ObjectPoolBase GetObjectPool(Predicate<ObjectPoolBase> condition)
        {
            return m_ObjectPoolManager.GetObjectPool(condition);
        }

        /// <summary>
        /// 获取对象池。
        /// </summary>
        /// <param name="condition">要检查的条件。</param>
        /// <returns>要获取的对象池。</returns>
        public ObjectPoolBase[] GetObjectPools(Predicate<ObjectPoolBase> condition)
        {
            return m_ObjectPoolManager.GetObjectPools(condition);
        }

        /// <summary>
        /// 获取对象池。
        /// </summary>
        /// <param name="condition">要检查的条件。</param>
        /// <param name="results">要获取的对象池。</param>
        public void GetObjectPools(Predicate<ObjectPoolBase> condition, List<ObjectPoolBase> results)
        {
            m_ObjectPoolManager.GetObjectPools(condition, results);
        }

        /// <summary>
        /// 获取所有对象池。
        /// </summary>
        /// <returns>所有对象池。</returns>
        public ObjectPoolBase[] GetAllObjectPools()
        {
            return m_ObjectPoolManager.GetAllObjectPools();
        }

        /// <summary>
        /// 获取所有对象池。
        /// </summary>
        /// <param name="results">所有对象池。</param>
        public void GetAllObjectPools(List<ObjectPoolBase> results)
        {
            m_ObjectPoolManager.GetAllObjectPools(results);
        }

        /// <summary>
        /// 获取所有对象池。
        /// </summary>
        /// <param name="sort">是否根据对象池的优先级排序。</param>
        /// <returns>所有对象池。</returns>
        public ObjectPoolBase[] GetAllObjectPools(bool sort)
        {
            return m_ObjectPoolManager.GetAllObjectPools(sort);
        }

        /// <summary>
        /// 获取所有对象池。
        /// </summary>
        /// <param name="sort">是否根据对象池的优先级排序。</param>
        /// <param name="results">所有对象池。</param>
        public void GetAllObjectPools(bool sort, List<ObjectPoolBase> results)
        {
            m_ObjectPoolManager.GetAllObjectPools(sort, results);
        }

        /// <summary>
        /// 创建独占模式的对象池。对象被获取后标记为使用中，必须归还后才能再次被获取，适用于大多数常规场景。
        /// </summary>
        /// <typeparam name="T">对象类型。</typeparam>
        /// <param name="config">对象池创建配置。</param>
        /// <returns>要创建的独占模式对象池。</returns>
        public IObjectPool<T> CreateSingleGettingObjectPool<T>(ObjectPoolConfig config = null) where T : ObjectBase
        {
            return m_ObjectPoolManager.CreateSingleGettingObjectPool<T>(config);
        }

        /// <summary>
        /// 创建独占模式的对象池。对象被获取后标记为使用中，必须归还后才能再次被获取，适用于大多数常规场景。
        /// </summary>
        /// <param name="objectType">对象类型。</param>
        /// <param name="config">对象池创建配置。</param>
        /// <returns>要创建的独占模式对象池。</returns>
        public ObjectPoolBase CreateSingleGettingObjectPool(Type objectType, ObjectPoolConfig config = null)
        {
            return m_ObjectPoolManager.CreateSingleGettingObjectPool(objectType, config);
        }

        /// <summary>
        /// 创建共享模式的对象池。对象在使用中仍可被再次获取，同一实例可被多个持有者共享，适用于资源加载等需要引用计数的场景。
        /// </summary>
        /// <typeparam name="T">对象类型。</typeparam>
        /// <param name="config">对象池创建配置。</param>
        /// <returns>要创建的共享模式对象池。</returns>
        public IObjectPool<T> CreateMultiGettingObjectPool<T>(ObjectPoolConfig config = null) where T : ObjectBase
        {
            return m_ObjectPoolManager.CreateMultiGettingObjectPool<T>(config);
        }

        /// <summary>
        /// 创建共享模式的对象池。对象在使用中仍可被再次获取，同一实例可被多个持有者共享，适用于资源加载等需要引用计数的场景。
        /// </summary>
        /// <param name="objectType">对象类型。</param>
        /// <param name="config">对象池创建配置。</param>
        /// <returns>要创建的共享模式对象池。</returns>
        public ObjectPoolBase CreateMultiGettingObjectPool(Type objectType, ObjectPoolConfig config = null)
        {
            return m_ObjectPoolManager.CreateMultiGettingObjectPool(objectType, config);
        }

        /// <summary>
        /// 销毁对象池。
        /// </summary>
        /// <typeparam name="T">对象类型。</typeparam>
        /// <returns>是否销毁对象池成功。</returns>
        public bool DestroyObjectPool<T>() where T : ObjectBase
        {
            return m_ObjectPoolManager.DestroyObjectPool<T>();
        }

        /// <summary>
        /// 销毁对象池。
        /// </summary>
        /// <param name="objectType">对象类型。</param>
        /// <returns>是否销毁对象池成功。</returns>
        public bool DestroyObjectPool(Type objectType)
        {
            return m_ObjectPoolManager.DestroyObjectPool(objectType);
        }

        /// <summary>
        /// 销毁对象池。
        /// </summary>
        /// <typeparam name="T">对象类型。</typeparam>
        /// <param name="name">要销毁的对象池名称。</param>
        /// <returns>是否销毁对象池成功。</returns>
        public bool DestroyObjectPool<T>(string name) where T : ObjectBase
        {
            return m_ObjectPoolManager.DestroyObjectPool<T>(name);
        }

        /// <summary>
        /// 销毁对象池。
        /// </summary>
        /// <param name="objectType">对象类型。</param>
        /// <param name="name">要销毁的对象池名称。</param>
        /// <returns>是否销毁对象池成功。</returns>
        public bool DestroyObjectPool(Type objectType, string name)
        {
            return m_ObjectPoolManager.DestroyObjectPool(objectType, name);
        }

        /// <summary>
        /// 销毁对象池。
        /// </summary>
        /// <typeparam name="T">对象类型。</typeparam>
        /// <param name="objectPool">要销毁的对象池。</param>
        /// <returns>是否销毁对象池成功。</returns>
        public bool DestroyObjectPool<T>(IObjectPool<T> objectPool) where T : ObjectBase
        {
            return m_ObjectPoolManager.DestroyObjectPool(objectPool);
        }

        /// <summary>
        /// 销毁对象池。
        /// </summary>
        /// <param name="objectPool">要销毁的对象池。</param>
        /// <returns>是否销毁对象池成功。</returns>
        public bool DestroyObjectPool(ObjectPoolBase objectPool)
        {
            return m_ObjectPoolManager.DestroyObjectPool(objectPool);
        }
    }
}
