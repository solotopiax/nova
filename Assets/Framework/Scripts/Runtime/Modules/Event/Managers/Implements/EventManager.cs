/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EventManager.cs
 * author:    taoye
 * created:   2025/12/5
 * descrip:   事件管理器
 ***************************************************************/

using System;
using System.Collections.Generic;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 事件管理器。
    /// </summary>
    internal sealed class EventManager : EventManagerBase
    {
        /// <summary>
        /// 事件池。
        /// </summary>
        private EventPool<EventData> m_EventPool;

        /// <summary>
        /// 获取事件处理函数的数量。
        /// </summary>
        public override int HandlerCount => m_EventPool.EventHandlerCount;

        /// <summary>
        /// 获取事件数量。
        /// </summary>
        public override int Count => m_EventPool.EventCount;
        
        /// <summary>
        /// 初始化事件管理器的新实例。
        /// </summary>
        public EventManager()
        {
            m_EventPool = new EventPool<EventData>(EventPoolMode.AllowNoHandler | EventPoolMode.AllowMultiHandler);
        }

        /// <summary>
        /// 初始化。
        /// </summary>
        /// <param name="config">配置信息。</param>
        public override void Initialize(EventManagerConfig config)
        {
            m_EventPool.Shutdown();
            m_EventPool = new EventPool<EventData>(config.PoolMode);
            m_EventPool.SetMaxDispatchPerFrame(config.MaxDispatchPerFrame);
        }
        
        /// <summary>
        /// 管理器轮询。
        /// </summary>
        public override void Update()
        {
            m_EventPool.Update();
        }

        /// <summary>
        /// 关闭并清理管理器。
        /// </summary>
        public override void Shutdown()
        {
            m_EventPool.Shutdown();
        }

        /// <summary>
        /// 获取事件处理函数的数量。
        /// </summary>
        /// <param name="id">事件类型编号。</param>
        /// <returns>事件处理函数的数量。</returns>
        public override int GetCountByID(int id)
        {
            return m_EventPool.Count(id);
        }

        /// <summary>
        /// 检查是否存在事件处理函数。
        /// </summary>
        /// <param name="id">事件类型编号。</param>
        /// <param name="handler">要检查的事件处理函数。</param>
        /// <returns>是否存在事件处理函数。</returns>
        public override bool Check(int id, EventHandler<EventData> handler)
        {
            return m_EventPool.Check(id, handler);
        }

        /// <summary>
        /// 订阅事件处理函数。
        /// </summary>
        /// <param name="id">事件类型编号。</param>
        /// <param name="handler">要订阅的事件处理函数。</param>
        public override void Subscribe(int id, EventHandler<EventData> handler)
        {
            m_EventPool.Subscribe(id, handler);
        }

        /// <summary>
        /// 取消订阅事件处理函数。
        /// </summary>
        /// <param name="id">事件类型编号。</param>
        /// <param name="handler">要取消订阅的事件处理函数。</param>
        public override void Unsubscribe(int id, EventHandler<EventData> handler)
        {
            m_EventPool.Unsubscribe(id, handler);
        }

        /// <summary>
        /// 设置默认事件处理函数。
        /// </summary>
        /// <param name="handler">要设置的默认事件处理函数。</param>
        public override void SetDefaultHandler(EventHandler<EventData> handler)
        {
            m_EventPool.SetDefaultHandler(handler);
        }

        /// <summary>
        /// 抛出事件，这个操作是线程安全的，即使不在主线程中抛出，也可保证在主线程中回调事件处理函数，但事件会在抛出后的下一帧分发。
        /// </summary>
        /// <param name="sender">事件源。</param>
        /// <param name="e">事件参数。</param>
        public override void Fire(object sender, EventData e)
        {
            m_EventPool.Fire(sender, e);
        }

        /// <summary>
        /// 抛出事件立即模式，这个操作不是线程安全的，事件会立刻分发。
        /// </summary>
        /// <param name="sender">事件源。</param>
        /// <param name="e">事件参数。</param>
        public override void FireNow(object sender, EventData e)
        {
            m_EventPool.FireNow(sender, e);
        }

        /// <summary>
        /// 获取所有已注册事件 ID 的只读集合。
        /// </summary>
        /// <returns>已注册的事件 ID 只读集合。</returns>
        public override IReadOnlyCollection<int> GetRegisteredEventIDs()
        {
            return m_EventPool.GetRegisteredEventIDs();
        }

        /// <summary>
        /// 获取指定事件 ID 的所有处理函数委托列表。
        /// </summary>
        /// <param name="id">事件类型编号。</param>
        /// <returns>处理函数委托列表。</returns>
        public override List<EventHandler<EventData>> GetHandlers(int id)
        {
            return m_EventPool.GetHandlers(id);
        }
    }
}
