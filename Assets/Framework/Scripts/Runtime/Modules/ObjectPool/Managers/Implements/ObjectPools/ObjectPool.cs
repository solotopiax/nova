/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ObjectPool.cs
 * author:    taoye
 * created:   2025/12/10
 * descrip:   对象池
 ***************************************************************/

/*

两张索引表，解决两种操作场景: 按 Name 索引 和 按 Target 索引

ObjectPool<T> : ObjectPoolBase, IObjectPool<T>
  |-- NovaMultiDictionary<string, Object<T>>  m_Objects      按 Name 索引
  |-- Dictionary<object, Object<T>>           m_ObjectMap    按 Target 索引
  |-- bool     m_AllowMultiGet
  |-- float    m_AutoReleaseInterval / m_AutoReleaseTimeCounter
  |-- int      m_Capacity
  |-- float    m_ExpireTime
  |-- int      m_Priority
  +-- ReleaseObjectsFilter<T> m_DefaultReleaseObjectsFilter

GetCanReleaseObjects 过滤条件（进入候选的门槛）：
    if (internalObject.IsInUse) continue;  // 1. 正在被使用，不能释放
    if (internalObject.Locked) continue;  // 2. 业务锁定，不能释放
    if (!internalObject.CustomCanReleaseFlag) continue;  // 3. 业务自定义否决
    通过以上三关 -> 进入候选列表

DefaultReleaseObjectsFilter（从候选里选谁被释放）：
    第一轮：先淘汰已过期的（LastUseTime <= expireTime）
    第二轮：按优先级(大) 和 LastUseTime(新) 排序，优先淘汰"低优先级且最久未用"

Release() 的三种调用路径：
    定时自动：Update() -> AutoReleaseTimeCounter 到期 -> Release()
    容量溢出：Put() 后 Count > Capacity -> Release()
    容量变更：Capacity.set -> Release()
    ExpireTime 变更：ExpireTime.set -> Release()
    手动：业务代码直接调用 Release()/Release(count)/Release(filter)

最终全部汇聚到 Release(int toReleaseCount, ReleaseObjectsFilter<T> filter) 这一入口，参数化控制释放数量和策略，代码路径统一，不会出现多处实现不一致的问题。

为什么 IObjectPool<T> 不提供实现？
接口（interface）在 C# 里原则上不持有状态（没有字段），而 Name 的值来自 ObjectPoolBase 的私有字段 m_Name
接口无法声明 private readonly string m_Name，也无法在构造函数里给它赋值，所以接口那里的 string Name { get; } 只是一个要求：「实现我的类必须有个能读的 Name 属性」，至于值从哪来，接口不管。
ObjectPool<T> 继承了 ObjectPoolBase，自然就继承了 ObjectPoolBase.Name 的实现，这个继承来的属性自动满足了 IObjectPool<T> 的要求。
这在 C# 里叫做通过继承隐式实现接口。

*/

using System;
using System.Collections.Generic;
using UnityEngine;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 对象池。
    /// </summary>
    /// <typeparam name="T">对象类型。</typeparam>
    internal sealed partial class ObjectPool<T> : ObjectPoolBase, IObjectPool<T> where T : ObjectBase
    {
        /// <summary>
        /// 初始化对象池的新实例。
        /// </summary>
        /// <param name="allowMultiGet">是否允许对象被多次获取。</param>
        /// <param name="config">对象池创建配置。</param>
        public ObjectPool(bool allowMultiGet, ObjectPoolConfig config)
            : base(config.Name)
        {
            m_Objects = new NovaMultiDictionary<string, Object<T>>();
            m_ObjectMap = new Dictionary<object, Object<T>>();
            m_DefaultReleaseObjectsFilter = DefaultReleaseObjectsFilter;
            m_CachedCanReleaseObjects = new List<T>();
            m_CachedToReleaseObjects = new List<T>();
            m_CachedNonExpiredObjects = new List<T>();
            m_CachedObjectInfos = new List<ObjectInfo>();
            m_AllowMultiGet = allowMultiGet;
            m_AutoReleaseInterval = config.AutoReleaseInterval;
            m_Capacity = config.Capacity;
            m_ExpireTime = config.ExpireTime;
            m_Priority = config.Priority;
            m_AutoReleaseTimeCounter = 0f;
        }

        /// <summary>
        /// 创建对象。
        /// </summary>
        /// <param name="obj">对象。</param>
        /// <param name="got">对象是否已被获取。</param>
        public void Register(T obj, bool got)
        {
            if (obj == null)
            {
                throw new ArgumentNullException(nameof(obj), "obj 无效。");
            }

            Object<T> internalObject = Object<T>.Create(obj, got);
            m_Objects.Add(obj.Name, internalObject);
            m_ObjectMap.Add(obj.Target, internalObject);

            if (Count > m_Capacity)
            {
                Release();
            }
        }

        /// <summary>
        /// 检查对象。
        /// </summary>
        /// <returns>要检查的对象是否存在。</returns>
        public bool CanGet()
        {
            return CanGet(string.Empty);
        }

        /// <summary>
        /// 检查对象。
        /// </summary>
        /// <param name="name">对象名称。</param>
        /// <returns>要检查的对象是否存在。</returns>
        public bool CanGet(string name)
        {
            if (name == null)
            {
                throw new ArgumentException("name 无效。", nameof(name));
            }

            NovaLinkedListRange<Object<T>> objectRange = default(NovaLinkedListRange<Object<T>>);
            if (m_Objects.TryGetValue(name, out objectRange))
            {
                foreach (Object<T> internalObject in objectRange)
                {
                    if (m_AllowMultiGet || !internalObject.IsInUse)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// 获取对象。
        /// </summary>
        /// <returns>要获取的对象。</returns>
        public T Get()
        {
            return Get(string.Empty);
        }

        /// <summary>
        /// 获取对象。
        /// </summary>
        /// <param name="name">对象名称。</param>
        /// <returns>要获取的对象。</returns>
        public T Get(string name)
        {
            if (name == null)
            {
                throw new ArgumentException("name 无效。", nameof(name));
            }

            NovaLinkedListRange<Object<T>> objectRange = default(NovaLinkedListRange<Object<T>>);
            if (m_Objects.TryGetValue(name, out objectRange))
            {
                foreach (Object<T> internalObject in objectRange)
                {
                    if (m_AllowMultiGet || !internalObject.IsInUse)
                    {
                        return internalObject.Get();
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// 回收对象。
        /// </summary>
        /// <param name="obj">要回收的对象。</param>
        public void Put(T obj)
        {
            if (obj == null)
            {
                throw new ArgumentNullException(nameof(obj), "obj 无效。");
            }

            Put(obj.Target);
        }

        /// <summary>
        /// 回收对象。
        /// </summary>
        /// <param name="target">要回收的对象。</param>
        public void Put(object target)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target), "target 无效。");
            }

            Object<T> internalObject = GetInternalObject(target);
            if (internalObject != null)
            {
                internalObject.Put();
                if (Count > m_Capacity && internalObject.RefCount <= 0)
                {
                    Release();
                }
            }
            else
            {
                throw new InvalidOperationException(Txt.Format("无法在对象池 '{0}' 中找到目标，目标类型：'{1}', 目标值：'{2}'。", new TypeNamePair(typeof(T), Name), target.GetType().FullName, target));
            }
        }

        /// <summary>
        /// 设置对象是否被加锁。
        /// </summary>
        /// <param name="obj">要设置被加锁的对象。</param>
        /// <param name="locked">是否被加锁。</param>
        public void SetLocked(T obj, bool locked)
        {
            if (obj == null)
            {
                throw new ArgumentNullException(nameof(obj), "obj 无效。");
            }

            SetLocked(obj.Target, locked);
        }

        /// <summary>
        /// 设置对象是否被加锁。
        /// </summary>
        /// <param name="target">要设置被加锁的对象。</param>
        /// <param name="locked">是否被加锁。</param>
        public void SetLocked(object target, bool locked)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target), "target 无效。");
            }

            Object<T> internalObject = GetInternalObject(target);
            if (internalObject != null)
            {
                internalObject.Locked = locked;
            }
            else
            {
                throw new InvalidOperationException(Txt.Format("无法在对象池 '{0}' 中找到目标，目标类型：'{1}', 目标值：'{2}'。", new TypeNamePair(typeof(T), Name), target.GetType().FullName, target));
            }
        }

        /// <summary>
        /// 设置对象的优先级。
        /// </summary>
        /// <param name="obj">要设置优先级的对象。</param>
        /// <param name="priority">优先级。</param>
        public void SetPriority(T obj, int priority)
        {
            if (obj == null)
            {
                throw new ArgumentNullException(nameof(obj), "obj 无效。");
            }

            SetPriority(obj.Target, priority);
        }

        /// <summary>
        /// 设置对象的优先级。
        /// </summary>
        /// <param name="target">要设置优先级的对象。</param>
        /// <param name="priority">优先级。</param>
        public void SetPriority(object target, int priority)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target), "target 无效。");
            }

            Object<T> internalObject = GetInternalObject(target);
            if (internalObject != null)
            {
                internalObject.Priority = priority;
            }
            else
            {
                throw new InvalidOperationException(Txt.Format("无法在对象池 '{0}' 中找到目标，目标类型：'{1}', 目标值：'{2}'。", new TypeNamePair(typeof(T), Name), target.GetType().FullName, target));
            }
        }

        /// <summary>
        /// 释放对象。
        /// </summary>
        /// <param name="obj">要释放的对象。</param>
        /// <returns>释放对象是否成功。</returns>
        public bool ReleaseObject(T obj)
        {
            if (obj == null)
            {
                throw new ArgumentNullException(nameof(obj), "obj 无效。");
            }

            return ReleaseObject(obj.Target);
        }

        /// <summary>
        /// 释放对象。
        /// </summary>
        /// <param name="target">要释放的对象。</param>
        /// <returns>释放对象是否成功。</returns>
        public bool ReleaseObject(object target)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target), "target 无效。");
            }

            Object<T> internalObject = GetInternalObject(target);
            if (internalObject == null)
            {
                return false;
            }

            if (internalObject.IsInUse || internalObject.Locked || !internalObject.CustomCanReleaseFlag)
            {
                return false;
            }

            m_Objects.Remove(internalObject.Name, internalObject);
            m_ObjectMap.Remove(internalObject.Peek().Target);

            internalObject.Release(false);
            ReferencePool.Put(internalObject);
            return true;
        }

        /// <summary>
        /// 释放对象池中的可释放对象。
        /// </summary>
        public override void Release()
        {
            Release(Count - m_Capacity, m_DefaultReleaseObjectsFilter);
        }

        /// <summary>
        /// 释放对象池中的可释放对象。
        /// </summary>
        /// <param name="toReleaseCount">尝试释放对象数量。</param>
        public override void Release(int toReleaseCount)
        {
            Release(toReleaseCount, m_DefaultReleaseObjectsFilter);
        }

        /// <summary>
        /// 释放对象池中的可释放对象。
        /// </summary>
        /// <param name="releaseObjectsFilter">释放对象筛选器。</param>
        public void Release(ReleaseObjectsFilter<T> releaseObjectsFilter)
        {
            Release(Count - m_Capacity, releaseObjectsFilter);
        }

        /// <summary>
        /// 释放对象池中的可释放对象。
        /// </summary>
        /// <param name="toReleaseCount">尝试释放对象数量。</param>
        /// <param name="releaseObjectsFilter">释放对象筛选器。</param>
        public void Release(int toReleaseCount, ReleaseObjectsFilter<T> releaseObjectsFilter)
        {
            if (releaseObjectsFilter == null)
            {
                throw new ArgumentNullException(nameof(releaseObjectsFilter), "releaseObjectsFilter 无效。");
            }

            if (toReleaseCount < 0)
            {
                toReleaseCount = 0;
            }

            DateTime expireTime = DateTime.MinValue;
            if (m_ExpireTime < float.MaxValue)
            {
                expireTime = DateTime.UtcNow.AddSeconds(-m_ExpireTime);
            }

            m_AutoReleaseTimeCounter = 0f;
            GetCanReleaseObjects(m_CachedCanReleaseObjects);
            List<T> toReleaseObjects = releaseObjectsFilter(m_CachedCanReleaseObjects, toReleaseCount, expireTime);
            if (toReleaseObjects == null || toReleaseObjects.Count <= 0)
            {
                return;
            }

            foreach (T toReleaseObject in toReleaseObjects)
            {
                ReleaseObject(toReleaseObject);
            }
        }

        /// <summary>
        /// 释放对象池中的所有未使用对象。
        /// </summary>
        public override void ReleaseAllUnused()
        {
            m_AutoReleaseTimeCounter = 0f;
            GetCanReleaseObjects(m_CachedCanReleaseObjects);
            foreach (T toReleaseObject in m_CachedCanReleaseObjects)
            {
                ReleaseObject(toReleaseObject);
            }
        }

        /// <summary>
        /// 获取所有对象信息。
        /// </summary>
        /// <returns>所有对象信息。</returns>
        public override ObjectInfo[] GetAllObjectInfos()
        {
            m_CachedObjectInfos.Clear();
            foreach (KeyValuePair<string, NovaLinkedListRange<Object<T>>> objectRanges in m_Objects)
            {
                foreach (Object<T> internalObject in objectRanges.Value)
                {
                    m_CachedObjectInfos.Add(new ObjectInfo(internalObject.Name, internalObject.Locked, internalObject.CustomCanReleaseFlag, internalObject.Priority, internalObject.LastUseTime, internalObject.RefCount));
                }
            }

            return m_CachedObjectInfos.ToArray();
        }

        /// <summary>
        /// 对象池轮询。
        /// </summary>
        public override void Update()
        {
            m_AutoReleaseTimeCounter += Time.unscaledDeltaTime;
            if (m_AutoReleaseTimeCounter < m_AutoReleaseInterval)
            {
                return;
            }

            Release();
        }

        /// <summary>
        /// 关闭并清理对象池。
        /// </summary>
        public override void Shutdown()
        {
            foreach (KeyValuePair<object, Object<T>> objectInMap in m_ObjectMap)
            {
                objectInMap.Value.Release(true);
                ReferencePool.Put(objectInMap.Value);
            }

            m_Objects.Clear();
            m_ObjectMap.Clear();
            m_CachedCanReleaseObjects.Clear();
            m_CachedToReleaseObjects.Clear();
            m_CachedNonExpiredObjects.Clear();
            m_CachedObjectInfos.Clear();
        }
    }
}
