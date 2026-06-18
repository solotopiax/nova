/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ObjectPoolManager.Methods.cs
 * author:    taoye
 * created:   2025/12/10
 * descrip:   对象池管理器-方法
 ***************************************************************/

using System;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 对象池管理器。
    /// </summary>
    internal sealed partial class ObjectPoolManager : ObjectPoolManagerBase
    {
        /// <summary>
        /// 校验对象类型是否为有效的 ObjectBase 派生类型。
        /// </summary>
        /// <param name="objectType">对象类型。</param>
        private static void ValidateObjectType(Type objectType)
        {
            if (objectType == null)
            {
                throw new ArgumentNullException(nameof(objectType), "objectType 无效。");
            }

            if (!typeof(ObjectBase).IsAssignableFrom(objectType))
            {
                throw new ArgumentException(Txt.Format("objectType '{0}' 无效。", objectType.FullName));
            }
        }

        /// <summary>
        /// 检查是否存在对象池（内部接口）。
        /// </summary>
        /// <param name="typeNamePair">类型名称对。</param>
        /// <returns>是否存在对象池。</returns>
        private bool InternalHasObjectPool(TypeNamePair typeNamePair)
        {
            return m_ObjectPools.ContainsKey(typeNamePair);
        }

        /// <summary>
        /// 获取对象池（内部接口）。
        /// </summary>
        /// <param name="typeNamePair">类型名称对。</param>
        /// <returns>对象池。</returns>
        private ObjectPoolBase InternalGetObjectPool(TypeNamePair typeNamePair)
        {
            ObjectPoolBase objectPool = null;
            if (m_ObjectPools.TryGetValue(typeNamePair, out objectPool))
            {
                return objectPool;
            }

            return null;
        }

        /// <summary>
        /// 创建对象池（内部接口）。
        /// </summary>
        /// <param name="name">对象池名称。</param>
        /// <param name="allowMultiGet">是否允许多次获取。</param>
        /// <param name="autoReleaseInterval">自动释放间隔。</param>
        /// <param name="capacity">容量。</param>
        /// <param name="expireTime">过期时间。</param>
        /// <param name="priority">优先级。</param>
        /// <typeparam name="T">对象类型。</typeparam>
        /// <returns>对象池。</returns>
        /// <exception cref="InvalidOperationException">对象池已存在时抛出。</exception>
        private IObjectPool<T> InternalCreateObjectPool<T>(string name, bool allowMultiGet, float autoReleaseInterval, int capacity, float expireTime, int priority) where T : ObjectBase
        {
            TypeNamePair typeNamePair = new TypeNamePair(typeof(T), name);
            if (HasObjectPool<T>(name))
            {
                throw new InvalidOperationException(Txt.Format("objectPool '{0}' 已经存在。", typeNamePair));
            }

            ObjectPool<T> objectPool = new ObjectPool<T>(allowMultiGet, new ObjectPoolConfig { Name = name, AutoReleaseInterval = autoReleaseInterval, Capacity = capacity, ExpireTime = expireTime, Priority = priority });
            m_ObjectPools.Add(typeNamePair, objectPool);
            return objectPool;
        }

        /// <summary>
        /// 创建对象池（内部接口）。
        /// </summary>
        /// <param name="objectType">对象类型。</param>
        /// <param name="name">对象池名称。</param>
        /// <param name="allowMultiGet">是否允许多次获取。</param>
        /// <param name="autoReleaseInterval">自动释放间隔。</param>
        /// <param name="capacity">容量。</param>
        /// <param name="expireTime">过期时间。</param>
        /// <param name="priority">优先级。</param>
        /// <returns>对象池。</returns>
        /// <exception cref="InvalidOperationException">对象池已存在时抛出。</exception>
        private ObjectPoolBase InternalCreateObjectPool(Type objectType, string name, bool allowMultiGet, float autoReleaseInterval, int capacity, float expireTime, int priority)
        {
            ValidateObjectType(objectType);

            TypeNamePair typeNamePair = new TypeNamePair(objectType, name);
            if (HasObjectPool(objectType, name))
            {
                throw new InvalidOperationException(Txt.Format("objectPool '{0}' 已经存在。", typeNamePair));
            }

            Type objectPoolType = typeof(ObjectPool<>).MakeGenericType(objectType);
            // IL2CPP 环境下需确保泛型类型已通过 AOT 预编译，否则可能抛出 ExecutionEngineException。
            ObjectPoolBase objectPool = (ObjectPoolBase)Activator.CreateInstance(objectPoolType, allowMultiGet, new ObjectPoolConfig { Name = name, AutoReleaseInterval = autoReleaseInterval, Capacity = capacity, ExpireTime = expireTime, Priority = priority });
            m_ObjectPools.Add(typeNamePair, objectPool);
            return objectPool;
        }

        /// <summary>
        /// 销毁对象池（内部接口）。
        /// </summary>
        /// <param name="typeNamePair">类型名称对。</param>
        /// <returns>是否成功。</returns>
        private bool InternalDestroyObjectPool(TypeNamePair typeNamePair)
        {
            ObjectPoolBase objectPool = null;
            if (m_ObjectPools.TryGetValue(typeNamePair, out objectPool))
            {
                objectPool.Shutdown();
                return m_ObjectPools.Remove(typeNamePair);
            }

            return false;
        }

        /// <summary>
        /// 对象池比较器（内部接口，用于排序）。
        /// </summary>
        /// <param name="a">对象池 a。</param>
        /// <param name="b">对象池 b。</param>
        /// <returns>比较结果。</returns>
        private static int ObjectPoolComparer(ObjectPoolBase a, ObjectPoolBase b)
        {
            return a.Priority.CompareTo(b.Priority);
        }
    }
}
