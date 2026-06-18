/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ObjectPoolManager.cs
 * author:    taoye
 * created:   2025/12/10
 * descrip:   对象池管理器
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
        /// 初始化对象池管理器的新实例。
        /// </summary>
        public ObjectPoolManager()
        {
            m_ObjectPools = new Dictionary<TypeNamePair, ObjectPoolBase>();
            m_CachedAllObjectPools = new List<ObjectPoolBase>();
            m_ObjectPoolComparer = ObjectPoolComparer;
        }

        /// <summary>
        /// 初始化。
        /// </summary>
        /// <param name="config">配置信息。</param>
        public override void Initialize(ObjectPoolManagerConfig config)
        {

        }

        /// <summary>
        /// 管理器轮询。
        /// </summary>
        public override void Update()
        {
            foreach (KeyValuePair<TypeNamePair, ObjectPoolBase> objectPool in m_ObjectPools)
            {
                objectPool.Value.Update();
            }
        }

        /// <summary>
        /// 关闭并清理管理器。
        /// </summary>
        public override void Shutdown()
        {
            foreach (KeyValuePair<TypeNamePair, ObjectPoolBase> objectPool in m_ObjectPools)
            {
                objectPool.Value.Shutdown();
            }

            m_ObjectPools.Clear();
            m_CachedAllObjectPools.Clear();
        }

        /// <summary>
        /// 检查是否存在对象池。
        /// </summary>
        /// <typeparam name="T">对象类型。</typeparam>
        /// <returns>是否存在对象池。</returns>
        public override bool HasObjectPool<T>()
        {
            return InternalHasObjectPool(new TypeNamePair(typeof(T)));
        }

        /// <summary>
        /// 检查是否存在对象池。
        /// </summary>
        /// <param name="objectType">对象类型。</param>
        /// <returns>是否存在对象池。</returns>
        public override bool HasObjectPool(Type objectType)
        {
            ValidateObjectType(objectType);
            return InternalHasObjectPool(new TypeNamePair(objectType));
        }

        /// <summary>
        /// 检查是否存在对象池。
        /// </summary>
        /// <typeparam name="T">对象类型。</typeparam>
        /// <param name="name">对象池名称。</param>
        /// <returns>是否存在对象池。</returns>
        public override bool HasObjectPool<T>(string name)
        {
            return InternalHasObjectPool(new TypeNamePair(typeof(T), name));
        }

        /// <summary>
        /// 检查是否存在对象池。
        /// </summary>
        /// <param name="objectType">对象类型。</param>
        /// <param name="name">对象池名称。</param>
        /// <returns>是否存在对象池。</returns>
        public override bool HasObjectPool(Type objectType, string name)
        {
            ValidateObjectType(objectType);
            return InternalHasObjectPool(new TypeNamePair(objectType, name));
        }

        /// <summary>
        /// 检查是否存在对象池。
        /// </summary>
        /// <param name="condition">要检查的条件。</param>
        /// <returns>是否存在对象池。</returns>
        public override bool HasObjectPool(Predicate<ObjectPoolBase> condition)
        {
            if (condition == null)
            {
                throw new ArgumentNullException(nameof(condition), "condition 无效。");
            }

            foreach (KeyValuePair<TypeNamePair, ObjectPoolBase> objectPool in m_ObjectPools)
            {
                if (condition(objectPool.Value))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 获取对象池。
        /// </summary>
        /// <typeparam name="T">对象类型。</typeparam>
        /// <returns>要获取的对象池。</returns>
        public override IObjectPool<T> GetObjectPool<T>()
        {
            return (IObjectPool<T>)InternalGetObjectPool(new TypeNamePair(typeof(T)));
        }

        /// <summary>
        /// 获取对象池。
        /// </summary>
        /// <param name="objectType">对象类型。</param>
        /// <returns>要获取的对象池。</returns>
        public override ObjectPoolBase GetObjectPool(Type objectType)
        {
            ValidateObjectType(objectType);
            return InternalGetObjectPool(new TypeNamePair(objectType));
        }

        /// <summary>
        /// 获取对象池。
        /// </summary>
        /// <typeparam name="T">对象类型。</typeparam>
        /// <param name="name">对象池名称。</param>
        /// <returns>要获取的对象池。</returns>
        public override IObjectPool<T> GetObjectPool<T>(string name)
        {
            return (IObjectPool<T>)InternalGetObjectPool(new TypeNamePair(typeof(T), name));
        }

        /// <summary>
        /// 获取对象池。
        /// </summary>
        /// <param name="objectType">对象类型。</param>
        /// <param name="name">对象池名称。</param>
        /// <returns>要获取的对象池。</returns>
        public override ObjectPoolBase GetObjectPool(Type objectType, string name)
        {
            ValidateObjectType(objectType);
            return InternalGetObjectPool(new TypeNamePair(objectType, name));
        }

        /// <summary>
        /// 获取对象池。
        /// </summary>
        /// <param name="condition">要检查的条件。</param>
        /// <returns>要获取的对象池。</returns>
        public override ObjectPoolBase GetObjectPool(Predicate<ObjectPoolBase> condition)
        {
            if (condition == null)
            {
                throw new ArgumentNullException(nameof(condition), "condition 无效。");
            }

            foreach (KeyValuePair<TypeNamePair, ObjectPoolBase> objectPool in m_ObjectPools)
            {
                if (condition(objectPool.Value))
                {
                    return objectPool.Value;
                }
            }

            return null;
        }

        /// <summary>
        /// 获取对象池。
        /// </summary>
        /// <param name="condition">要检查的条件。</param>
        /// <returns>要获取的对象池。</returns>
        public override ObjectPoolBase[] GetObjectPools(Predicate<ObjectPoolBase> condition)
        {
            if (condition == null)
            {
                throw new ArgumentNullException(nameof(condition), "condition 无效。");
            }

            m_CachedAllObjectPools.Clear();
            foreach (KeyValuePair<TypeNamePair, ObjectPoolBase> objectPool in m_ObjectPools)
            {
                if (condition(objectPool.Value))
                {
                    m_CachedAllObjectPools.Add(objectPool.Value);
                }
            }

            return m_CachedAllObjectPools.ToArray();
        }

        /// <summary>
        /// 获取对象池。
        /// </summary>
        /// <param name="condition">要检查的条件。</param>
        /// <param name="results">要获取的对象池。</param>
        public override void GetObjectPools(Predicate<ObjectPoolBase> condition, List<ObjectPoolBase> results)
        {
            if (condition == null)
            {
                throw new ArgumentNullException(nameof(condition), "condition 无效。");
            }

            if (results == null)
            {
                throw new ArgumentNullException(nameof(results), "results 无效。");
            }

            results.Clear();
            foreach (KeyValuePair<TypeNamePair, ObjectPoolBase> objectPool in m_ObjectPools)
            {
                if (condition(objectPool.Value))
                {
                    results.Add(objectPool.Value);
                }
            }
        }

        /// <summary>
        /// 获取所有对象池。
        /// </summary>
        /// <returns>所有对象池。</returns>
        public override ObjectPoolBase[] GetAllObjectPools()
        {
            return GetAllObjectPools(false);
        }

        /// <summary>
        /// 获取所有对象池。
        /// </summary>
        /// <param name="results">所有对象池。</param>
        public override void GetAllObjectPools(List<ObjectPoolBase> results)
        {
            GetAllObjectPools(false, results);
        }

        /// <summary>
        /// 获取所有对象池。
        /// </summary>
        /// <param name="sort">是否根据对象池的优先级排序。</param>
        /// <returns>所有对象池。</returns>
        public override ObjectPoolBase[] GetAllObjectPools(bool sort)
        {
            GetAllObjectPools(sort, m_CachedAllObjectPools);
            return m_CachedAllObjectPools.ToArray();
        }

        /// <summary>
        /// 获取所有对象池。
        /// </summary>
        /// <param name="sort">是否根据对象池的优先级排序。</param>
        /// <param name="results">所有对象池。</param>
        public override void GetAllObjectPools(bool sort, List<ObjectPoolBase> results)
        {
            if (results == null)
            {
                throw new ArgumentNullException(nameof(results), "results 无效。");
            }

            results.Clear();
            foreach (KeyValuePair<TypeNamePair, ObjectPoolBase> objectPool in m_ObjectPools)
            {
                results.Add(objectPool.Value);
            }

            if (sort)
            {
                results.Sort(m_ObjectPoolComparer);
            }
        }

        /// <summary>
        /// 创建独占模式的对象池。对象被获取后标记为使用中，必须归还后才能再次被获取，适用于大多数常规场景。
        /// </summary>
        /// <typeparam name="T">对象类型。</typeparam>
        /// <param name="config">对象池创建配置，为 null 时使用默认值。</param>
        /// <returns>要创建的独占模式对象池。</returns>
        public override IObjectPool<T> CreateSingleGettingObjectPool<T>(ObjectPoolConfig config = null)
        {
            ObjectPoolConfig c = config ?? new ObjectPoolConfig();
            return InternalCreateObjectPool<T>(c.Name, false, c.AutoReleaseInterval, c.Capacity, c.ExpireTime, c.Priority);
        }

        /// <summary>
        /// 创建独占模式的对象池。对象被获取后标记为使用中，必须归还后才能再次被获取，适用于大多数常规场景。
        /// </summary>
        /// <param name="objectType">对象类型。</param>
        /// <param name="config">对象池创建配置，为 null 时使用默认值。</param>
        /// <returns>要创建的独占模式对象池。</returns>
        public override ObjectPoolBase CreateSingleGettingObjectPool(Type objectType, ObjectPoolConfig config = null)
        {
            ObjectPoolConfig c = config ?? new ObjectPoolConfig();
            return InternalCreateObjectPool(objectType, c.Name, false, c.AutoReleaseInterval, c.Capacity, c.ExpireTime, c.Priority);
        }

        /// <summary>
        /// 创建共享模式的对象池。对象在使用中仍可被再次获取，同一实例可被多个持有者共享，适用于资源加载等需要引用计数的场景。
        /// </summary>
        /// <typeparam name="T">对象类型。</typeparam>
        /// <param name="config">对象池创建配置，为 null 时使用默认值。</param>
        /// <returns>要创建的共享模式对象池。</returns>
        public override IObjectPool<T> CreateMultiGettingObjectPool<T>(ObjectPoolConfig config = null)
        {
            ObjectPoolConfig c = config ?? new ObjectPoolConfig();
            return InternalCreateObjectPool<T>(c.Name, true, c.AutoReleaseInterval, c.Capacity, c.ExpireTime, c.Priority);
        }

        /// <summary>
        /// 创建共享模式的对象池。对象在使用中仍可被再次获取，同一实例可被多个持有者共享，适用于资源加载等需要引用计数的场景。
        /// </summary>
        /// <param name="objectType">对象类型。</param>
        /// <param name="config">对象池创建配置，为 null 时使用默认值。</param>
        /// <returns>要创建的共享模式对象池。</returns>
        public override ObjectPoolBase CreateMultiGettingObjectPool(Type objectType, ObjectPoolConfig config = null)
        {
            ObjectPoolConfig c = config ?? new ObjectPoolConfig();
            return InternalCreateObjectPool(objectType, c.Name, true, c.AutoReleaseInterval, c.Capacity, c.ExpireTime, c.Priority);
        }

        /// <summary>
        /// 销毁对象池。
        /// </summary>
        /// <typeparam name="T">对象类型。</typeparam>
        /// <returns>是否销毁对象池成功。</returns>
        public override bool DestroyObjectPool<T>()
        {
            return InternalDestroyObjectPool(new TypeNamePair(typeof(T)));
        }

        /// <summary>
        /// 销毁对象池。
        /// </summary>
        /// <param name="objectType">对象类型。</param>
        /// <returns>是否销毁对象池成功。</returns>
        public override bool DestroyObjectPool(Type objectType)
        {
            ValidateObjectType(objectType);
            return InternalDestroyObjectPool(new TypeNamePair(objectType));
        }

        /// <summary>
        /// 销毁对象池。
        /// </summary>
        /// <typeparam name="T">对象类型。</typeparam>
        /// <param name="name">要销毁的对象池名称。</param>
        /// <returns>是否销毁对象池成功。</returns>
        public override bool DestroyObjectPool<T>(string name)
        {
            return InternalDestroyObjectPool(new TypeNamePair(typeof(T), name));
        }

        /// <summary>
        /// 销毁对象池。
        /// </summary>
        /// <param name="objectType">对象类型。</param>
        /// <param name="name">要销毁的对象池名称。</param>
        /// <returns>是否销毁对象池成功。</returns>
        public override bool DestroyObjectPool(Type objectType, string name)
        {
            ValidateObjectType(objectType);
            return InternalDestroyObjectPool(new TypeNamePair(objectType, name));
        }

        /// <summary>
        /// 销毁对象池。
        /// </summary>
        /// <typeparam name="T">对象类型。</typeparam>
        /// <param name="objectPool">要销毁的对象池。</param>
        /// <returns>是否销毁对象池成功。</returns>
        public override bool DestroyObjectPool<T>(IObjectPool<T> objectPool)
        {
            if (objectPool == null)
            {
                throw new ArgumentNullException(nameof(objectPool), "objectPool 无效。");
            }

            return InternalDestroyObjectPool(new TypeNamePair(typeof(T), objectPool.Name));
        }

        /// <summary>
        /// 销毁对象池。
        /// </summary>
        /// <param name="objectPool">要销毁的对象池。</param>
        /// <returns>是否销毁对象池成功。</returns>
        public override bool DestroyObjectPool(ObjectPoolBase objectPool)
        {
            if (objectPool == null)
            {
                throw new ArgumentNullException(nameof(objectPool), "objectPool 无效。");
            }

            return InternalDestroyObjectPool(new TypeNamePair(objectPool.ObjectType, objectPool.Name));
        }

        /// <summary>
        /// 释放对象池中的可释放对象。
        /// </summary>
        public override void Release()
        {
            GetAllObjectPools(true, m_CachedAllObjectPools);
            foreach (ObjectPoolBase objectPool in m_CachedAllObjectPools)
            {
                objectPool.Release();
            }
        }

        /// <summary>
        /// 释放对象池中的所有未使用对象。
        /// </summary>
        public override void ReleaseAllUnused()
        {
            GetAllObjectPools(true, m_CachedAllObjectPools);
            foreach (ObjectPoolBase objectPool in m_CachedAllObjectPools)
            {
                objectPool.ReleaseAllUnused();
            }
        }
    }
}
