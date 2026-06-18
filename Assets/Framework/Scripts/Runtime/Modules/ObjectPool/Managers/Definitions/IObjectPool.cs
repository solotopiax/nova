/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  IObjectPool.cs
 * author:    taoye
 * created:   2025/12/10
 * descrip:   对象池接口
 ***************************************************************/

/*

弥补 ObjectPoolBase 没有泛型方法的空缺，IObjectPool<T> 是 ObjectPoolBase 的泛型实现

IObjectPool<T> where T : ObjectBase
  ├── 继承 ObjectPoolBase 的所有属性（通过同名属性声明）
  ├── Register(T obj, bool got)       注册对象进池
  ├── CanGet() / CanGet(name)         检查是否有可用对象
  ├── Get() / Get(name) → T           取对象（强类型返回）
  ├── Put(T obj) / Put(object target) 归还对象
  ├── SetLocked / SetPriority         操作元数据
  ├── ReleaseObject(T) / (object)     主动释放单个对象
  └── Release(ReleaseObjectsFilter<T>) 自定义筛选器释放
*/

using System;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 对象池接口。
    /// </summary>
    /// <typeparam name="T">对象类型。</typeparam>
    public interface IObjectPool<T> where T : ObjectBase
    {
        /// <summary>
        /// 获取对象池名称。
        /// </summary>
        string Name { get; }

        /// <summary>
        /// 获取对象池完整名称。
        /// </summary>
        string FullName { get; }

        /// <summary>
        /// 获取对象池对象类型。
        /// </summary>
        Type ObjectType { get; }

        /// <summary>
        /// 获取对象池中对象的数量。
        /// </summary>
        int Count { get; }

        /// <summary>
        /// 获取对象池中能被释放的对象的数量。
        /// </summary>
        int CanReleaseCount { get; }

        /// <summary>
        /// 获取是否允许对象被多次获取。
        /// </summary>
        bool AllowMultiGet { get; }

        /// <summary>
        /// 获取或设置对象池自动释放可释放对象的间隔秒数。
        /// </summary>
        float AutoReleaseInterval { set; get; }

        /// <summary>
        /// 获取或设置对象池的容量。
        /// </summary>
        int Capacity { set; get; }

        /// <summary>
        /// 获取或设置对象池对象过期秒数。
        /// </summary>
        float ExpireTime { set; get; }

        /// <summary>
        /// 获取或设置对象池的优先级（值越小，优先级越高）。
        /// </summary>
        int Priority { set; get; }

        /// <summary>
        /// 创建对象。
        /// </summary>
        /// <param name="obj">对象。</param>
        /// <param name="got">对象是否已被获取。</param>
        void Register(T obj, bool got);

        /// <summary>
        /// 检查对象是否可以被获取。
        /// </summary>
        /// <returns>要检查的对象是否存在。</returns>
        bool CanGet();

        /// <summary>
        /// 检查对象是否可以被获取。
        /// </summary>
        /// <param name="name">对象名称。</param>
        /// <returns>要检查的对象是否存在。</returns>
        bool CanGet(string name);

        /// <summary>
        /// 获取对象。
        /// </summary>
        /// <returns>要获取的对象。</returns>
        T Get();

        /// <summary>
        /// 获取对象。
        /// </summary>
        /// <param name="name">对象名称。</param>
        /// <returns>要获取的对象。</returns>
        T Get(string name);

        /// <summary>
        /// 回收对象。
        /// </summary>
        /// <param name="obj">要回收的对象。</param>
        void Put(T obj);

        /// <summary>
        /// 回收对象。
        /// </summary>
        /// <param name="target">要回收的对象。</param>
        void Put(object target);

        /// <summary>
        /// 设置对象是否被加锁。
        /// </summary>
        /// <param name="obj">要设置被加锁的对象。</param>
        /// <param name="locked">是否被加锁。</param>
        void SetLocked(T obj, bool locked);

        /// <summary>
        /// 设置对象是否被加锁。
        /// </summary>
        /// <param name="target">要设置被加锁的对象。</param>
        /// <param name="locked">是否被加锁。</param>
        void SetLocked(object target, bool locked);

        /// <summary>
        /// 设置对象的优先级。
        /// </summary>
        /// <param name="obj">要设置优先级的对象。</param>
        /// <param name="priority">优先级。</param>
        void SetPriority(T obj, int priority);

        /// <summary>
        /// 设置对象的优先级。
        /// </summary>
        /// <param name="target">要设置优先级的对象。</param>
        /// <param name="priority">优先级。</param>
        void SetPriority(object target, int priority);

        /// <summary>
        /// 释放对象。
        /// </summary>
        /// <param name="obj">要释放的对象。</param>
        /// <returns>释放对象是否成功。</returns>
        bool ReleaseObject(T obj);

        /// <summary>
        /// 释放对象。
        /// </summary>
        /// <param name="target">要释放的对象。</param>
        /// <returns>释放对象是否成功。</returns>
        bool ReleaseObject(object target);

        /// <summary>
        /// 释放对象池中的可释放对象。
        /// </summary>
        void Release();

        /// <summary>
        /// 释放对象池中的可释放对象。
        /// </summary>
        /// <param name="toReleaseCount">尝试释放对象数量。</param>
        void Release(int toReleaseCount);

        /// <summary>
        /// 释放对象池中的可释放对象。
        /// </summary>
        /// <param name="releaseObjectsFilter">释放对象筛选函数。</param>
        void Release(ReleaseObjectsFilter<T> releaseObjectsFilter);

        /// <summary>
        /// 释放对象池中的可释放对象。
        /// </summary>
        /// <param name="toReleaseCount">尝试释放对象数量。</param>
        /// <param name="releaseObjectsFilter">释放对象筛选函数。</param>
        void Release(int toReleaseCount, ReleaseObjectsFilter<T> releaseObjectsFilter);

        /// <summary>
        /// 释放对象池中的所有未使用对象。
        /// </summary>
        void ReleaseAllUnused();
    }
}
