/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EventComponent.cs
 * author:    taoye
 * created:   2025/12/5
 * descrip:   事件组件
 ***************************************************************/

using System;
using System.Collections.Generic;
using UnityEngine;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 事件组件。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed partial class EventComponent : FrameworkComponent
    {
        /// <summary>
        /// 唤醒。
        /// </summary>
        protected override void Awake()
        {
            base.Awake();
            m_EventManager = Util.TypeCreator.Create<IEventManager>(m_CurManagerTypeName);
            if (m_EventManager == null)
            {
                throw new InvalidOperationException("EventManager 无效。");
            }
        }

        /// <summary>
        /// 开始。
        /// </summary>
        private void Start()
        {
            m_EventManager.Initialize(new EventManagerConfig()
            {
                PoolMode = m_EventPoolMode,
                MaxDispatchPerFrame = m_MaxDispatchPerFrame,
            });
        }

        /// <summary>
        /// 销毁。
        /// </summary>
        private void OnDestroy()
        {
            if (m_EventManager != null)
            {
                m_EventManager = null;
            }
        }

        /// <summary>
        /// 获取事件处理函数的数量。
        /// </summary>
        /// <param name="id">事件类型编号。</param>
        /// <returns>事件处理函数的数量。</returns>
        public int GetCountByID(int id)
        {
            return m_EventManager.GetCountByID(id);
        }

        /// <summary>
        /// 检查是否存在事件处理函数。
        /// </summary>
        /// <param name="id">事件类型编号。</param>
        /// <param name="handler">要检查的事件处理函数。</param>
        /// <returns>是否存在事件处理函数。</returns>
        public bool Check(int id, EventHandler<EventData> handler)
        {
            return m_EventManager.Check(id, handler);
        }

        /// <summary>
        /// 订阅事件处理回调函数。
        /// </summary>
        /// <param name="id">事件类型编号。</param>
        /// <param name="handler">要订阅的事件处理回调函数。</param>
        public void Subscribe(int id, EventHandler<EventData> handler)
        {
            m_EventManager.Subscribe(id, handler);
        }

        /// <summary>
        /// 取消订阅事件处理回调函数。
        /// </summary>
        /// <param name="id">事件类型编号。</param>
        /// <param name="handler">要取消订阅的事件处理回调函数。</param>
        public void Unsubscribe(int id, EventHandler<EventData> handler)
        {
            m_EventManager.Unsubscribe(id, handler);
        }

        /// <summary>
        /// 获取事件处理函数的数量。
        /// </summary>
        /// <typeparam name="T">事件数据类型。</typeparam>
        /// <returns>事件处理函数的数量。</returns>
        public int GetCountByID<T>() where T : EventData
        {
            return m_EventManager.GetCountByID<T>();
        }

        /// <summary>
        /// 检查是否存在事件处理函数。
        /// </summary>
        /// <typeparam name="T">事件数据类型。</typeparam>
        /// <param name="handler">要检查的事件处理函数。</param>
        /// <returns>是否存在事件处理函数。</returns>
        public bool Check<T>(EventHandler<EventData> handler) where T : EventData
        {
            return m_EventManager.Check<T>(handler);
        }

        /// <summary>
        /// 订阅事件处理回调函数。
        /// </summary>
        /// <typeparam name="T">事件数据类型。</typeparam>
        /// <param name="handler">要订阅的事件处理回调函数。</param>
        public void Subscribe<T>(EventHandler<EventData> handler) where T : EventData
        {
            m_EventManager.Subscribe<T>(handler);
        }

        /// <summary>
        /// 取消订阅事件处理回调函数。
        /// </summary>
        /// <typeparam name="T">事件数据类型。</typeparam>
        /// <param name="handler">要取消订阅的事件处理回调函数。</param>
        public void Unsubscribe<T>(EventHandler<EventData> handler) where T : EventData
        {
            m_EventManager.Unsubscribe<T>(handler);
        }

        /// <summary>
        /// 获取指定事件类型的所有处理函数委托列表。
        /// </summary>
        /// <typeparam name="T">事件数据类型。</typeparam>
        /// <returns>处理函数委托列表。</returns>
        public List<EventHandler<EventData>> GetHandlers<T>() where T : EventData
        {
            return m_EventManager.GetHandlers<T>();
        }

        /// <summary>
        /// 获取所有已注册事件 ID 的只读集合。
        /// </summary>
        /// <returns>已注册的事件 ID 只读集合。</returns>
        public IReadOnlyCollection<int> GetRegisteredEventIDs()
        {
            return m_EventManager?.GetRegisteredEventIDs();
        }

        /// <summary>
        /// 获取指定事件 ID 的所有处理函数委托列表。
        /// </summary>
        /// <param name="id">事件类型编号。</param>
        /// <returns>处理函数委托列表。</returns>
        public List<EventHandler<EventData>> GetHandlers(int id)
        {
            return m_EventManager?.GetHandlers(id);
        }

        /// <summary>
        /// 设置默认事件处理函数。
        /// </summary>
        /// <param name="handler">要设置的默认事件处理函数。</param>
        public void SetDefaultHandler(EventHandler<EventData> handler)
        {
            m_EventManager.SetDefaultHandler(handler);
        }

        /// <summary>
        /// 抛出事件，这个操作是线程安全的，即使不在主线程中抛出，也可保证在主线程中回调事件处理函数，但事件会在抛出后的下一帧分发。
        /// </summary>
        /// <param name="sender">事件发送者。</param>
        /// <param name="e">事件内容。</param>
        public void Fire(object sender, EventData e)
        {
            m_EventManager.Fire(sender, e);
        }

        /// <summary>
        /// 抛出事件立即模式，这个操作不是线程安全的，事件会立刻分发。
        /// </summary>
        /// <param name="sender">事件发送者。</param>
        /// <param name="e">事件内容。</param>
        public void FireNow(object sender, EventData e)
        {
            m_EventManager.FireNow(sender, e);
        }
    }
}
