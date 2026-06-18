/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ReplayEvent.cs
 * author:    yingzheng
 * created:   2026/5/14
 * descrip:   Replay Buffer 模式事件容器，保留固定容量的历史队列
 ***************************************************************/

using System;
using System.Collections.Generic;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// Replay Buffer 模式事件容器。
    /// 内部维护固定容量的历史队列；队满时淘汰最老一条。
    /// 新订阅者注册时将队列现有条目按顺序全部补发，之后持续接收新事件。
    /// 适用于"每条记录都有独立意义"的场景（如收益、播放结果、广告关闭）。
    /// </summary>
    /// <typeparam name="T">事件载荷类型。</typeparam>
    public sealed class ReplayEvent<T> : ObservableEvent<T>
    {
        /// <summary>
        /// 缓冲区默认容量。
        /// </summary>
        private const int c_DefaultCapacity = 4;
        /// <summary>
        /// 缓冲区最大容量，构造时固定，运行时不可修改。
        /// </summary>
        private readonly int m_Capacity;
        /// <summary>
        /// 历史事件缓冲队列，容量上限为 m_Capacity。
        /// </summary>
        private readonly Queue<T> m_Buffer;
        /// <summary>
        /// 当前所有订阅者列表。
        /// </summary>
        private readonly List<Action<T>> m_Handlers = new List<Action<T>>();

        /// <summary>
        /// 构造 Replay Buffer 事件容器。
        /// </summary>
        /// <param name="capacity">缓冲区最大容量，超出后淘汰最老条目。</param>
        /// <exception cref="ArgumentOutOfRangeException">capacity 小于等于 0 时抛出。</exception>
        public ReplayEvent(int capacity = c_DefaultCapacity)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity), "容量必须大于 0。");
            m_Capacity = capacity;
            m_Buffer = new Queue<T>(capacity);
        }

        /// <summary>
        /// 订阅事件。注册时将缓冲队列现有条目按顺序全部补发，之后持续接收新事件。
        /// </summary>
        /// <param name="handler">事件处理委托。</param>
        /// <returns>取消订阅句柄，调用 Dispose() 即取消。</returns>
        /// <exception cref="ArgumentNullException">handler 为 null 时抛出。</exception>
        public override IDisposable Subscribe(Action<T> handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            foreach (var item in m_Buffer)
            {
                try { handler(item); }
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
        /// 触发事件，写入缓冲队列（满则淘汰最老条目）并通知所有订阅者。
        /// </summary>
        /// <param name="value">事件载荷。</param>
        public override void Invoke(T value)
        {
            if (m_Buffer.Count >= m_Capacity)
                m_Buffer.Dequeue();
            m_Buffer.Enqueue(value);
            var snapshot = m_Handlers.ToArray();
            for (int i = 0; i < snapshot.Length; i++)
            {
                try { snapshot[i](value); }
                catch (Exception e) { Log.Error(LogTag.SDK, $"[ObservableEvent] handler 异常: {e}"); }
            }
        }

        /// <summary>
        /// 清空缓冲队列并移除所有订阅者。
        /// </summary>
        public override void Clear()
        {
            m_Buffer.Clear();
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
            private readonly ReplayEvent<T> m_Event;
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
            public Subscription(ReplayEvent<T> evt, Action<T> handler)
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
