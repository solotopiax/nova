/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ObjectPoolManagerBase.cs
 * author:    taoye
 * created:   2025/12/10
 * descrip:   对象池管理器基类
 ***************************************************************/

/*

ObjectBase：定义"一个可被池化的对象"长什么样，业务继承它。
Object<T>：给 ObjectBase 套一个引用计数壳，池内部使用，业务不可见。
ObjectPoolBase：非泛型抽象，让管理器能统一持有和遍历所有类型的池。
IObjectPool<T>：泛型接口，让业务代码用强类型操作池，不需要强转。
ObjectPool<T>：同时继承 ObjectPoolBase 和实现 IObjectPool<T>，用双索引表和可插拔释放策略把上面四者粘合在一起。

*/

using System;
using System.Collections.Generic;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 对象池管理器基类。
    /// </summary>
    internal abstract class ObjectPoolManagerBase : FrameworkManager, IObjectPoolManager
    {
        /// <summary>
        /// 管理器优先级（值越小越先 Update、越后 Shutdown）。
        /// </summary>
        /// <remarks>值越小优先级越高，越先 Update、越后 Shutdown。</remarks>
        public override int Priority => 2;

        /// <summary>
        /// 获取对象池数量。
        /// </summary>
        public abstract int Count { get; }

        /// <summary>
        /// 初始化。
        /// </summary>
        /// <param name="config">配置信息。</param>
        public abstract void Initialize(ObjectPoolManagerConfig config);

        /// <summary>
        /// 管理器轮询。
        /// </summary>
        public abstract override void Update();

        /// <summary>
        /// 关闭并清理管理器。
        /// </summary>
        public abstract override void Shutdown();

        /// <summary>
        /// 检查是否存在对象池。
        /// </summary>
        /// <typeparam name="T">对象类型。</typeparam>
        /// <returns>是否存在对象池。</returns>
        public abstract bool HasObjectPool<T>() where T : ObjectBase;

        /// <summary>
        /// 检查是否存在对象池。
        /// </summary>
        /// <param name="objectType">对象类型。</param>
        /// <returns>是否存在对象池。</returns>
        public abstract bool HasObjectPool(Type objectType);

        /// <summary>
        /// 检查是否存在对象池。
        /// </summary>
        /// <typeparam name="T">对象类型。</typeparam>
        /// <param name="name">对象池名称。</param>
        /// <returns>是否存在对象池。</returns>
        public abstract bool HasObjectPool<T>(string name) where T : ObjectBase;

        /// <summary>
        /// 检查是否存在对象池。
        /// </summary>
        /// <param name="objectType">对象类型。</param>
        /// <param name="name">对象池名称。</param>
        /// <returns>是否存在对象池。</returns>
        public abstract bool HasObjectPool(Type objectType, string name);

        /// <summary>
        /// 检查是否存在对象池。
        /// </summary>
        /// <param name="condition">要检查的条件。</param>
        /// <returns>是否存在对象池。</returns>
        public abstract bool HasObjectPool(Predicate<ObjectPoolBase> condition);

        /// <summary>
        /// 获取对象池。
        /// </summary>
        /// <typeparam name="T">对象类型。</typeparam>
        /// <returns>要获取的对象池。</returns>
        public abstract IObjectPool<T> GetObjectPool<T>() where T : ObjectBase;

        /// <summary>
        /// 获取对象池。
        /// </summary>
        /// <param name="objectType">对象类型。</param>
        /// <returns>要获取的对象池。</returns>
        public abstract ObjectPoolBase GetObjectPool(Type objectType);

        /// <summary>
        /// 获取对象池。
        /// </summary>
        /// <typeparam name="T">对象类型。</typeparam>
        /// <param name="name">对象池名称。</param>
        /// <returns>要获取的对象池。</returns>
        public abstract IObjectPool<T> GetObjectPool<T>(string name) where T : ObjectBase;

        /// <summary>
        /// 获取对象池。
        /// </summary>
        /// <param name="objectType">对象类型。</param>
        /// <param name="name">对象池名称。</param>
        /// <returns>要获取的对象池。</returns>
        public abstract ObjectPoolBase GetObjectPool(Type objectType, string name);

        /// <summary>
        /// 获取对象池。
        /// </summary>
        /// <param name="condition">要检查的条件。</param>
        /// <returns>要获取的对象池。</returns>
        public abstract ObjectPoolBase GetObjectPool(Predicate<ObjectPoolBase> condition);

        /// <summary>
        /// 获取对象池。
        /// </summary>
        /// <param name="condition">要检查的条件。</param>
        /// <returns>要获取的对象池。</returns>
        public abstract ObjectPoolBase[] GetObjectPools(Predicate<ObjectPoolBase> condition);

        /// <summary>
        /// 获取对象池。
        /// </summary>
        /// <param name="condition">要检查的条件。</param>
        /// <param name="results">要获取的对象池。</param>
        public abstract void GetObjectPools(Predicate<ObjectPoolBase> condition, List<ObjectPoolBase> results);

        /// <summary>
        /// 获取所有对象池。
        /// </summary>
        /// <returns>所有对象池。</returns>
        public abstract ObjectPoolBase[] GetAllObjectPools();

        /// <summary>
        /// 获取所有对象池。
        /// </summary>
        /// <param name="results">所有对象池。</param>
        public abstract void GetAllObjectPools(List<ObjectPoolBase> results);

        /// <summary>
        /// 获取所有对象池。
        /// </summary>
        /// <param name="sort">是否根据对象池的优先级排序。</param>
        /// <returns>所有对象池。</returns>
        public abstract ObjectPoolBase[] GetAllObjectPools(bool sort);

        /// <summary>
        /// 获取所有对象池。
        /// </summary>
        /// <param name="sort">是否根据对象池的优先级排序。</param>
        /// <param name="results">所有对象池。</param>
        public abstract void GetAllObjectPools(bool sort, List<ObjectPoolBase> results);

        /// <summary>
        /// 创建独占模式的对象池。对象被获取后标记为使用中，必须归还后才能再次被获取，适用于大多数常规场景。
        /// </summary>
        /// <typeparam name="T">对象类型。</typeparam>
        /// <param name="config">对象池创建配置，为 null 时使用默认值。</param>
        /// <returns>要创建的独占模式对象池。</returns>
        public abstract IObjectPool<T> CreateSingleGettingObjectPool<T>(ObjectPoolConfig config = null) where T : ObjectBase;

        /// <summary>
        /// 创建独占模式的对象池。对象被获取后标记为使用中，必须归还后才能再次被获取，适用于大多数常规场景。
        /// </summary>
        /// <param name="objectType">对象类型。</param>
        /// <param name="config">对象池创建配置，为 null 时使用默认值。</param>
        /// <returns>要创建的独占模式对象池。</returns>
        public abstract ObjectPoolBase CreateSingleGettingObjectPool(Type objectType, ObjectPoolConfig config = null);

        /// <summary>
        /// 创建共享模式的对象池。对象在使用中仍可被再次获取，同一实例可被多个持有者共享，适用于资源加载等需要引用计数的场景。
        /// </summary>
        /// <typeparam name="T">对象类型。</typeparam>
        /// <param name="config">对象池创建配置，为 null 时使用默认值。</param>
        /// <returns>要创建的共享模式对象池。</returns>
        public abstract IObjectPool<T> CreateMultiGettingObjectPool<T>(ObjectPoolConfig config = null) where T : ObjectBase;

        /// <summary>
        /// 创建共享模式的对象池。对象在使用中仍可被再次获取，同一实例可被多个持有者共享，适用于资源加载等需要引用计数的场景。
        /// </summary>
        /// <param name="objectType">对象类型。</param>
        /// <param name="config">对象池创建配置，为 null 时使用默认值。</param>
        /// <returns>要创建的共享模式对象池。</returns>
        public abstract ObjectPoolBase CreateMultiGettingObjectPool(Type objectType, ObjectPoolConfig config = null);

        /// <summary>
        /// 销毁对象池。
        /// </summary>
        /// <typeparam name="T">对象类型。</typeparam>
        /// <returns>是否销毁对象池成功。</returns>
        public abstract bool DestroyObjectPool<T>() where T : ObjectBase;

        /// <summary>
        /// 销毁对象池。
        /// </summary>
        /// <param name="objectType">对象类型。</param>
        /// <returns>是否销毁对象池成功。</returns>
        public abstract bool DestroyObjectPool(Type objectType);

        /// <summary>
        /// 销毁对象池。
        /// </summary>
        /// <typeparam name="T">对象类型。</typeparam>
        /// <param name="name">要销毁的对象池名称。</param>
        /// <returns>是否销毁对象池成功。</returns>
        public abstract bool DestroyObjectPool<T>(string name) where T : ObjectBase;

        /// <summary>
        /// 销毁对象池。
        /// </summary>
        /// <param name="objectType">对象类型。</param>
        /// <param name="name">要销毁的对象池名称。</param>
        /// <returns>是否销毁对象池成功。</returns>
        public abstract bool DestroyObjectPool(Type objectType, string name);

        /// <summary>
        /// 销毁对象池。
        /// </summary>
        /// <typeparam name="T">对象类型。</typeparam>
        /// <param name="objectPool">要销毁的对象池。</param>
        /// <returns>是否销毁对象池成功。</returns>
        public abstract bool DestroyObjectPool<T>(IObjectPool<T> objectPool) where T : ObjectBase;

        /// <summary>
        /// 销毁对象池。
        /// </summary>
        /// <param name="objectPool">要销毁的对象池。</param>
        /// <returns>是否销毁对象池成功。</returns>
        public abstract bool DestroyObjectPool(ObjectPoolBase objectPool);

        /// <summary>
        /// 释放对象池中的可释放对象。
        /// </summary>
        public abstract void Release();

        /// <summary>
        /// 释放对象池中的所有未使用对象。
        /// </summary>
        public abstract void ReleaseAllUnused();
    }
}
