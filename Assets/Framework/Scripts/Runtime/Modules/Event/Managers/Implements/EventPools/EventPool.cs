/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EventPool.cs
 * author:    taoye
 * created:   2025/12/5
 * descrip:   事件池
 ***************************************************************/

using System;
using System.Collections.Generic;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 事件池。
    /// </summary>
    /// <typeparam name="T">事件类型。</typeparam>
    internal sealed partial class EventPool<T> where T : EventData
    {
        /// <summary>
        /// 事件处理器。
        /// </summary>
        private readonly NovaMultiDictionary<int, EventHandler<T>> m_EventHandlers;
        
        /// <summary>
        /// 事件队列。
        /// </summary>
        private readonly Queue<Event> m_Events;

        /// <summary>
        /// 处理中的事件队列（用于队列交换模式，减少持锁时间）。
        /// </summary>
        private readonly Queue<Event> m_ProcessingEvents;

        /// <summary>
        /// 当前正在派发事件对应的节点集合。
        /// 格式：【事件参数，处理器集合】。
        /// 目的：为确保在“事件正在派发时取消订阅”不会破坏链表遍历而设计。
        /// </summary>
        private readonly Dictionary<object, LinkedListNode<EventHandler<T>>> m_CurrentFiringNodes;
        
        /// <summary>
        /// 临时节点集合（避免频繁 GC 开销）。
        /// </summary>
        private readonly Dictionary<object, LinkedListNode<EventHandler<T>>> m_TempNodes;
        
        /// <summary>
        /// 事件池模式。
        /// </summary>
        private readonly EventPoolMode m_EventPoolMode;
        
        /// <summary>
        /// 默认事件处理器。
        /// </summary>
        private EventHandler<T> m_DefaultHandler;

        /// <summary>
        /// 每帧最大事件分发数量（0 表示无限制）。
        /// </summary>
        private int m_MaxDispatchPerFrame;

        /// <summary>
        /// 事件积压告警阈值。
        /// </summary>
        private const int c_BacklogWarningThreshold = 100;

        /// <summary>
        /// 获取事件处理函数的数量。
        /// </summary>
        public int EventHandlerCount => m_EventHandlers.Count;

        /// <summary>
        /// 获取事件数量。
        /// </summary>
        public int EventCount => m_Events.Count;

        /// <summary>
        /// 初始化事件池的新实例。
        /// </summary>
        /// <param name="mode">事件池模式。</param>
        public EventPool(EventPoolMode mode)
        {
            m_EventHandlers = new NovaMultiDictionary<int, EventHandler<T>>();
            m_Events = new Queue<Event>();
            m_ProcessingEvents = new Queue<Event>();
            m_CurrentFiringNodes = new Dictionary<object, LinkedListNode<EventHandler<T>>>();
            m_TempNodes = new Dictionary<object, LinkedListNode<EventHandler<T>>>();
            m_EventPoolMode = mode;
            m_DefaultHandler = null;
        }

        /// <summary>
        /// 设置每帧最大事件分发数量。
        /// </summary>
        /// <param name="maxDispatchPerFrame">每帧最大事件分发数量（0 表示无限制）。</param>
        public void SetMaxDispatchPerFrame(int maxDispatchPerFrame)
        {
            m_MaxDispatchPerFrame = maxDispatchPerFrame;
        }

        /// <summary>
        /// 事件池轮询。
        /// </summary>
        public void Update()
        {
            lock (m_Events)
            {
                while (m_Events.Count > 0)
                {
                    m_ProcessingEvents.Enqueue(m_Events.Dequeue());
                }
            }

            if (m_ProcessingEvents.Count <= 0)
            {
                return;
            }

            int dispatchCount = 0;
            while (m_ProcessingEvents.Count > 0)
            {
                if (m_MaxDispatchPerFrame > 0 && dispatchCount >= m_MaxDispatchPerFrame)
                {
                    break;
                }

                Event eventNode = m_ProcessingEvents.Dequeue();
                try
                {
                    HandleEvent(eventNode.Sender, eventNode.EventArgs);
                }
                catch (Exception ex)
                {
                    Log.Error(LogTag.Event, Txt.Format("事件分发异常，事件 ID：{0}，异常：{1}", eventNode.EventArgs.ID, ex));
                }
                finally
                {
                    ReferencePool.Put(eventNode);
                }
                dispatchCount++;
            }

            if (m_MaxDispatchPerFrame > 0 && m_ProcessingEvents.Count > c_BacklogWarningThreshold)
            {
                Log.Warning(LogTag.Event, "事件积压告警：当前未消费事件 {0} 条，超过阈值 {1}。", m_ProcessingEvents.Count, c_BacklogWarningThreshold);
            }
        }

        /// <summary>
        /// 关闭并清理事件池。
        /// </summary>
        public void Shutdown()
        {
            Clear();
            m_EventHandlers.Clear();
            m_ProcessingEvents.Clear();
            m_CurrentFiringNodes.Clear();
            m_TempNodes.Clear();
            m_DefaultHandler = null;
        }

        /// <summary>
        /// 清理事件。
        /// </summary>
        public void Clear()
        {
            lock (m_Events)
            {
                m_Events.Clear();
            }
        }

        /// <summary>
        /// 获取事件处理函数的数量。
        /// </summary>
        /// <param name="id">事件类型编号。</param>
        /// <returns>事件处理函数的数量。</returns>
        public int Count(int id)
        {
            NovaLinkedListRange<EventHandler<T>> range = default(NovaLinkedListRange<EventHandler<T>>);
            if (m_EventHandlers.TryGetValue(id, out range))
            {
                return range.Count;
            }

            return 0;
        }

        /// <summary>
        /// 检查是否存在事件处理函数。
        /// </summary>
        /// <param name="id">事件类型编号。</param>
        /// <param name="handler">要检查的事件处理函数。</param>
        /// <returns>是否存在事件处理函数。</returns>
        public bool Check(int id, EventHandler<T> handler)
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler), "事件处理函数无效。");
            }

            return m_EventHandlers.Contains(id, handler);
        }

        /// <summary>
        /// 订阅事件处理函数。
        /// 此方法不是线程安全的，必须在主线程调用。
        /// </summary>
        /// <param name="id">事件类型编号。</param>
        /// <param name="handler">要订阅的事件处理函数。</param>
        public void Subscribe(int id, EventHandler<T> handler)
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler), "事件处理函数无效。");
            }

            if (!m_EventHandlers.Contains(id))
            {
                m_EventHandlers.Add(id, handler);
            }
            else if ((m_EventPoolMode & EventPoolMode.AllowMultiHandler) != EventPoolMode.AllowMultiHandler)
            {
                throw new InvalidOperationException(Txt.Format("事件 '{0}' 不允许存在多个处理函数。", id));
            }
            else if ((m_EventPoolMode & EventPoolMode.AllowDuplicateHandler) != EventPoolMode.AllowDuplicateHandler && Check(id, handler))
            {
                throw new InvalidOperationException(Txt.Format("事件 '{0}' 不允许存在重复处理函数。", id));
            }
            else
            {
                m_EventHandlers.Add(id, handler);
            }
        }

        /// <summary>
        /// 取消订阅事件处理函数。
        /// 此方法不是线程安全的，必须在主线程调用。
        /// </summary>
        /// <param name="id">事件类型编号。</param>
        /// <param name="handler">要取消订阅的事件处理函数。</param>
        public void Unsubscribe(int id, EventHandler<T> handler)
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler), "事件处理函数无效。");
            }
            
            if (m_CurrentFiringNodes.Count > 0)
            {
                foreach (KeyValuePair<object, LinkedListNode<EventHandler<T>>> cachedNode in m_CurrentFiringNodes)
                {
                    if (cachedNode.Value != null && cachedNode.Value.Value == handler)
                    {
                        m_TempNodes.Add(cachedNode.Key, cachedNode.Value.Next);
                    }
                }

                if (m_TempNodes.Count > 0)
                {
                    foreach (KeyValuePair<object, LinkedListNode<EventHandler<T>>> cachedNode in m_TempNodes)
                    {
                        m_CurrentFiringNodes[cachedNode.Key] = cachedNode.Value;
                    }

                    m_TempNodes.Clear();
                }
            }

            if (!m_EventHandlers.Remove(id, handler))
            {
                throw new InvalidOperationException(Txt.Format("事件 '{0}' 不存在指定的处理函数。", id));
            }
        }

        /// <summary>
        /// 设置默认事件处理函数。
        /// </summary>
        /// <param name="handler">要设置的默认事件处理函数。</param>
        public void SetDefaultHandler(EventHandler<T> handler)
        {
            m_DefaultHandler = handler;
        }

        /// <summary>
        /// 抛出事件，这个操作是线程安全的，即使不在主线程中抛出，也可保证在主线程中回调事件处理函数，但事件会在抛出后的下一帧分发。
        /// </summary>
        /// <param name="sender">事件源。</param>
        /// <param name="e">事件参数。</param>
        public void Fire(object sender, T e)
        {
            if (e == null)
            {
                throw new ArgumentNullException(nameof(e), "事件参数无效。");
            }

            Event eventNode = Event.Create(sender, e);
            lock (m_Events)
            {
                m_Events.Enqueue(eventNode);
            }
        }

        /// <summary>
        /// 抛出事件立即模式，这个操作不是线程安全的，事件会立刻分发。
        /// </summary>
        /// <param name="sender">事件源。</param>
        /// <param name="e">事件参数。</param>
        public void FireNow(object sender, T e)
        {
            if (e == null)
            {
                throw new ArgumentNullException(nameof(e), "事件参数无效。");
            }

            HandleEvent(sender, e);
        }

        /// <summary>
        /// 处理事件结点。
        /// </summary>
        /// <param name="sender">事件源。</param>
        /// <param name="e">事件参数。</param>
        private void HandleEvent(object sender, T e)
        {
            bool noHandlerException = false;
            NovaLinkedListRange<EventHandler<T>> range = default(NovaLinkedListRange<EventHandler<T>>);
            if (m_EventHandlers.TryGetValue(e.ID, out range))
            {
                LinkedListNode<EventHandler<T>> current = range.First;
                while (current != null && current != range.Terminal)
                {
                    m_CurrentFiringNodes[e] = current.Next != range.Terminal ? current.Next : null;
                    try
                    {
                        current.Value(sender, e);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(LogTag.Event, Txt.Format("事件回调异常，事件 ID：{0}，异常：{1}", e.ID, ex));
                    }

                    current = m_CurrentFiringNodes[e];
                }

                m_CurrentFiringNodes.Remove(e);
            }
            else if (m_DefaultHandler != null)
            {
                try
                {
                    m_DefaultHandler(sender, e);
                }
                catch (Exception ex)
                {
                    Log.Error(LogTag.Event, Txt.Format("默认事件回调异常，事件 ID：{0}，异常：{1}", e.ID, ex));
                }
            }
            else if ((m_EventPoolMode & EventPoolMode.AllowNoHandler) == 0)
            {
                noHandlerException = true;
            }

            if (noHandlerException)
            {
                int eventId = e.ID;
                ReferencePool.Put(e);
                throw new InvalidOperationException(Txt.Format("事件 '{0}' 不允许无事件处理函数。", eventId));
            }

            ReferencePool.Put(e);
        }

        /// <summary>
        /// 获取事件处理函数的数量。
        /// </summary>
        /// <typeparam name="TEvent">事件数据类型。</typeparam>
        /// <returns>事件处理函数的数量。</returns>
        public int Count<TEvent>() where TEvent : T
        {
            return Count(EventTypeID.Get<TEvent>());
        }

        /// <summary>
        /// 检查是否存在事件处理函数。
        /// </summary>
        /// <typeparam name="TEvent">事件数据类型。</typeparam>
        /// <param name="handler">要检查的事件处理函数。</param>
        /// <returns>是否存在事件处理函数。</returns>
        public bool Check<TEvent>(EventHandler<T> handler) where TEvent : T
        {
            return Check(EventTypeID.Get<TEvent>(), handler);
        }

        /// <summary>
        /// 订阅事件处理函数。
        /// 此方法不是线程安全的，必须在主线程调用。
        /// </summary>
        /// <typeparam name="TEvent">事件数据类型。</typeparam>
        /// <param name="handler">要订阅的事件处理函数。</param>
        public void Subscribe<TEvent>(EventHandler<T> handler) where TEvent : T
        {
            Subscribe(EventTypeID.Get<TEvent>(), handler);
        }

        /// <summary>
        /// 取消订阅事件处理函数。
        /// 此方法不是线程安全的，必须在主线程调用。
        /// </summary>
        /// <typeparam name="TEvent">事件数据类型。</typeparam>
        /// <param name="handler">要取消订阅的事件处理函数。</param>
        public void Unsubscribe<TEvent>(EventHandler<T> handler) where TEvent : T
        {
            Unsubscribe(EventTypeID.Get<TEvent>(), handler);
        }

        /// <summary>
        /// 获取指定事件类型的所有处理函数委托列表。
        /// </summary>
        /// <typeparam name="TEvent">事件数据类型。</typeparam>
        /// <returns>处理函数委托列表，未注册则返回 null。</returns>
        public List<EventHandler<T>> GetHandlers<TEvent>() where TEvent : T
        {
            return GetHandlers(EventTypeID.Get<TEvent>());
        }

        /// <summary>
        /// 获取所有已注册事件 ID 的只读集合。
        /// </summary>
        /// <returns>已注册的事件 ID 只读集合。</returns>
        public IReadOnlyCollection<int> GetRegisteredEventIDs()
        {
            return m_EventHandlers.Keys;
        }

        /// <summary>
        /// 获取指定事件 ID 的所有处理函数委托列表。
        /// </summary>
        /// <param name="id">事件类型编号。</param>
        /// <returns>处理函数委托列表，未注册则返回 null。</returns>
        public List<EventHandler<T>> GetHandlers(int id)
        {
            NovaLinkedListRange<EventHandler<T>> range = default(NovaLinkedListRange<EventHandler<T>>);
            if (!m_EventHandlers.TryGetValue(id, out range))
            {
                return null;
            }

            List<EventHandler<T>> handlers = new List<EventHandler<T>>();
            for (LinkedListNode<EventHandler<T>> current = range.First; current != null && current != range.Terminal; current = current.Next)
            {
                handlers.Add(current.Value);
            }
            return handlers;
        }
    }
}
