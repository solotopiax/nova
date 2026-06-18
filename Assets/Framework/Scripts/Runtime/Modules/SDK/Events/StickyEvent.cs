/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  StickyEvent.cs
 * author:    yingzheng
 * created:   2026/5/14
 * descrip:   Sticky 模式事件容器，保留最新一次载荷
 ***************************************************************/

using System;
using System.Collections.Generic;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// Sticky 模式事件容器。
    /// 内部保留最新一次触发的载荷；新订阅者注册时若已有值则立即同步补发一次，
    /// 之后持续接收后续事件。适用于"只关心最新状态"的场景（如加载成功/失败、初始化结果）。
    /// </summary>
    /// <typeparam name="T">事件载荷类型。</typeparam>
    public sealed class StickyEvent<T> : ObservableEvent<T>
    {
        /// <summary>
        /// 是否已触发过至少一次。
        /// </summary>
        private bool m_HasValue;
        /// <summary>
        /// 最近一次触发的载荷值。
        /// </summary>
        private T m_LastValue;
        /// <summary>
        /// 当前所有订阅者列表。
        /// </summary>
        private readonly List<Action<T>> m_Handlers = new List<Action<T>>();

        /// <summary>
        /// 订阅事件。若已有缓存值则立即同步补发，之后持续接收新事件。
        /// </summary>
        /// <param name="handler">事件处理委托。</param>
        /// <returns>取消订阅句柄，调用 Dispose() 即取消。</returns>
        /// <exception cref="ArgumentNullException">handler 为 null 时抛出。</exception>
        public override IDisposable Subscribe(Action<T> handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            if (m_HasValue)
            {
                try { handler(m_LastValue); }
                catch (Exception e) { Log.Error(LogTag.SDK, $"[ObservableEvent] handler 异常: {e}"); }
            }
            m_Handlers.Add(handler);
            return new Subscription(this, handler);
        }

        /// <summary>
        /// 取消指定 handler 的事件订阅。
        /// </summary>
        /// <param name="handler">订阅时传入的事件处理委托。</param>
        /// <exception cref="ArgumentNullException">handler 为 null 时抛出。</exception>
        public override void Unsubscribe(Action<T> handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            m_Handlers.Remove(handler);
        }

        /// <summary>
        /// 触发事件，更新缓存值并通知所有订阅者。
        /// </summary>
        /// <param name="value">事件载荷。</param>
        public override void Invoke(T value)
        {
            m_LastValue = value;
            m_HasValue = true;
            var snapshot = m_Handlers.ToArray();
            for (int i = 0; i < snapshot.Length; i++)
            {
                try { snapshot[i](value); }
                catch (Exception e) { Log.Error(LogTag.SDK, $"[ObservableEvent] handler 异常: {e}"); }
            }
        }

        /// <summary>
        /// 清空缓存值并移除所有订阅者。
        /// </summary>
        public override void Clear()
        {
            m_HasValue = false;
            m_LastValue = default;
            m_Handlers.Clear();
        }

        /// <summary>
        /// 取消订阅句柄，Dispose 时将 handler 从订阅者列表移除。
        /// </summary>
        private sealed class Subscription : IDisposable
        {
            /// <summary>
            /// 所属事件容器。
            /// </summary>
            private readonly StickyEvent<T> m_Event;
            /// <summary>
            /// 对应的订阅委托。
            /// </summary>
            private readonly Action<T> m_Handler;
            /// <summary>
            /// 是否已取消订阅。
            /// </summary>
            private bool m_Disposed;

            /// <summary>
            /// 构造取消订阅句柄。
            /// </summary>
            /// <param name="evt">所属事件容器。</param>
            /// <param name="handler">对应的订阅委托。</param>
            public Subscription(StickyEvent<T> evt, Action<T> handler)
            {
                m_Event = evt;
                m_Handler = handler;
            }

            /// <summary>
            /// 取消订阅，从事件容器的订阅者列表中移除 handler。
            /// </summary>
            public void Dispose()
            {
                if (m_Disposed) return;
                m_Disposed = true;
                m_Event.Unsubscribe(m_Handler);
            }
        }
    }
}
