/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  IEventManager.cs
 * author:    taoye
 * created:   2025/12/5
 * descrip:   事件管理器接口
 ***************************************************************/

using System;
using System.Collections.Generic;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 事件管理器接口。
    /// </summary>
    public interface IEventManager
    {
        /// <summary>
        /// 获取事件处理函数的数量。
        /// </summary>
        int HandlerCount { get; }

        /// <summary>
        /// 获取事件数量。
        /// </summary>
        int Count { get; }
        
        /// <summary>
        /// 初始化。
        /// </summary>
        /// <param name="config">配置信息。</param>
        void Initialize(EventManagerConfig config);

        /// <summary>
        /// 获取事件处理函数的数量。
        /// </summary>
        /// <param name="id">事件类型编号。</param>
        /// <returns>事件处理函数的数量。</returns>
        int GetCountByID(int id);

        /// <summary>
        /// 检查是否存在事件处理函数。
        /// </summary>
        /// <param name="id">事件类型编号。</param>
        /// <param name="handler">要检查的事件处理函数。</param>
        /// <returns>是否存在事件处理函数。</returns>
        bool Check(int id, EventHandler<EventData> handler);

        /// <summary>
        /// 订阅事件处理函数。
        /// </summary>
        /// <param name="id">事件类型编号。</param>
        /// <param name="handler">要订阅的事件处理函数。</param>
        void Subscribe(int id, EventHandler<EventData> handler);

        /// <summary>
        /// 取消订阅事件处理函数。
        /// </summary>
        /// <param name="id">事件类型编号。</param>
        /// <param name="handler">要取消订阅的事件处理函数。</param>
        void Unsubscribe(int id, EventHandler<EventData> handler);

        /// <summary>
        /// 设置默认事件处理函数。
        /// </summary>
        /// <param name="handler">要设置的默认事件处理函数。</param>
        void SetDefaultHandler(EventHandler<EventData> handler);

        /// <summary>
        /// 抛出事件，这个操作是线程安全的，即使不在主线程中抛出，也可保证在主线程中回调事件处理函数，但事件会在抛出后的下一帧分发。
        /// </summary>
        /// <param name="sender">事件源。</param>
        /// <param name="e">事件参数。</param>
        void Fire(object sender, EventData e);

        /// <summary>
        /// 抛出事件立即模式，这个操作不是线程安全的，事件会立刻分发。
        /// </summary>
        /// <param name="sender">事件源。</param>
        /// <param name="e">事件参数。</param>
        void FireNow(object sender, EventData e);

        /// <summary>
        /// 获取事件处理函数的数量。
        /// </summary>
        /// <typeparam name="T">事件数据类型。</typeparam>
        /// <returns>事件处理函数的数量。</returns>
        int GetCountByID<T>() where T : EventData;

        /// <summary>
        /// 检查是否存在事件处理函数。
        /// </summary>
        /// <typeparam name="T">事件数据类型。</typeparam>
        /// <param name="handler">要检查的事件处理函数。</param>
        /// <returns>是否存在事件处理函数。</returns>
        bool Check<T>(EventHandler<EventData> handler) where T : EventData;

        /// <summary>
        /// 订阅事件处理函数。
        /// </summary>
        /// <typeparam name="T">事件数据类型。</typeparam>
        /// <param name="handler">要订阅的事件处理函数。</param>
        void Subscribe<T>(EventHandler<EventData> handler) where T : EventData;

        /// <summary>
        /// 取消订阅事件处理函数。
        /// </summary>
        /// <typeparam name="T">事件数据类型。</typeparam>
        /// <param name="handler">要取消订阅的事件处理函数。</param>
        void Unsubscribe<T>(EventHandler<EventData> handler) where T : EventData;

        /// <summary>
        /// 获取指定事件类型的所有处理函数委托列表。
        /// </summary>
        /// <typeparam name="T">事件数据类型。</typeparam>
        /// <returns>处理函数委托列表。</returns>
        List<EventHandler<EventData>> GetHandlers<T>() where T : EventData;

        /// <summary>
        /// 获取所有已注册事件 ID 的只读集合。
        /// </summary>
        /// <returns>已注册的事件 ID 只读集合。</returns>
        IReadOnlyCollection<int> GetRegisteredEventIDs();

        /// <summary>
        /// 获取指定事件 ID 的所有处理函数委托列表。
        /// </summary>
        /// <param name="id">事件类型编号。</param>
        /// <returns>处理函数委托列表。</returns>
        List<EventHandler<EventData>> GetHandlers(int id);
    }
}
