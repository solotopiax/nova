/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EventManagerBase.cs
 * author:    taoye
 * created:   2025/12/9
 * descrip:   事件管理器基类
 ***************************************************************/

using System;
using System.Collections.Generic;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 事件管理器基类。
    /// </summary>
    internal abstract class EventManagerBase : FrameworkManager, IEventManager
    {
        /// <summary>
        /// 管理器优先级（值越小越先 Update、越后 Shutdown）。
        /// </summary>
        /// <remarks>值越小优先级越高，越先 Update、越后 Shutdown。</remarks>
        public override int Priority => 1;
        
        /// <summary>
        /// 获取事件处理函数的数量。
        /// </summary>
        public abstract int HandlerCount { get; }

        /// <summary>
        /// 获取事件数量。
        /// </summary>
        public abstract int Count { get; }

        /// <summary>
        /// 初始化。
        /// </summary>
        /// <param name="config">配置信息。</param>
        public abstract void Initialize(EventManagerConfig config);
        
        /// <summary>
        /// 管理器轮询。
        /// </summary>
        public abstract override void Update();

        /// <summary>
        /// 关闭并清理管理器。
        /// </summary>
        public abstract override void Shutdown();

        /// <summary>
        /// 获取事件处理函数的数量。
        /// </summary>
        /// <param name="id">事件类型编号。</param>
        /// <returns>事件处理函数的数量。</returns>
        public abstract int GetCountByID(int id);

        /// <summary>
        /// 检查是否存在事件处理函数。
        /// </summary>
        /// <param name="id">事件类型编号。</param>
        /// <param name="handler">要检查的事件处理函数。</param>
        /// <returns>是否存在事件处理函数。</returns>
        public abstract bool Check(int id, EventHandler<EventData> handler);

        /// <summary>
        /// 订阅事件处理函数。
        /// </summary>
        /// <param name="id">事件类型编号。</param>
        /// <param name="handler">要订阅的事件处理函数。</param>
        public abstract void Subscribe(int id, EventHandler<EventData> handler);

        /// <summary>
        /// 取消订阅事件处理函数。
        /// </summary>
        /// <param name="id">事件类型编号。</param>
        /// <param name="handler">要取消订阅的事件处理函数。</param>
        public abstract void Unsubscribe(int id, EventHandler<EventData> handler);

        /// <summary>
        /// 设置默认事件处理函数。
        /// </summary>
        /// <param name="handler">要设置的默认事件处理函数。</param>
        public abstract void SetDefaultHandler(EventHandler<EventData> handler);

        /// <summary>
        /// 抛出事件，这个操作是线程安全的，即使不在主线程中抛出，也可保证在主线程中回调事件处理函数，但事件会在抛出后的下一帧分发。
        /// </summary>
        /// <param name="sender">事件源。</param>
        /// <param name="e">事件参数。</param>
        public abstract void Fire(object sender, EventData e);

        /// <summary>
        /// 抛出事件立即模式，这个操作不是线程安全的，事件会立刻分发。
        /// </summary>
        /// <param name="sender">事件源。</param>
        /// <param name="e">事件参数。</param>
        public abstract void FireNow(object sender, EventData e);

        /// <summary>
        /// 获取事件处理函数的数量。
        /// </summary>
        /// <typeparam name="T">事件数据类型。</typeparam>
        /// <returns>事件处理函数的数量。</returns>
        public int GetCountByID<T>() where T : EventData
        {
            return GetCountByID(EventTypeID.Get<T>());
        }

        /// <summary>
        /// 检查是否存在事件处理函数。
        /// </summary>
        /// <typeparam name="T">事件数据类型。</typeparam>
        /// <param name="handler">要检查的事件处理函数。</param>
        /// <returns>是否存在事件处理函数。</returns>
        public bool Check<T>(EventHandler<EventData> handler) where T : EventData
        {
            return Check(EventTypeID.Get<T>(), handler);
        }

        /// <summary>
        /// 订阅事件处理函数。
        /// </summary>
        /// <typeparam name="T">事件数据类型。</typeparam>
        /// <param name="handler">要订阅的事件处理函数。</param>
        public void Subscribe<T>(EventHandler<EventData> handler) where T : EventData
        {
            Subscribe(EventTypeID.Get<T>(), handler);
        }

        /// <summary>
        /// 取消订阅事件处理函数。
        /// </summary>
        /// <typeparam name="T">事件数据类型。</typeparam>
        /// <param name="handler">要取消订阅的事件处理函数。</param>
        public void Unsubscribe<T>(EventHandler<EventData> handler) where T : EventData
        {
            Unsubscribe(EventTypeID.Get<T>(), handler);
        }

        /// <summary>
        /// 获取指定事件类型的所有处理函数委托列表。
        /// </summary>
        /// <typeparam name="T">事件数据类型。</typeparam>
        /// <returns>处理函数委托列表。</returns>
        public List<EventHandler<EventData>> GetHandlers<T>() where T : EventData
        {
            return GetHandlers(EventTypeID.Get<T>());
        }

        /// <summary>
        /// 获取所有已注册事件 ID 的只读集合。
        /// </summary>
        /// <returns>已注册的事件 ID 只读集合。</returns>
        public abstract IReadOnlyCollection<int> GetRegisteredEventIDs();

        /// <summary>
        /// 获取指定事件 ID 的所有处理函数委托列表。
        /// </summary>
        /// <param name="id">事件类型编号。</param>
        /// <returns>处理函数委托列表。</returns>
        public abstract List<EventHandler<EventData>> GetHandlers(int id);
    }
}
