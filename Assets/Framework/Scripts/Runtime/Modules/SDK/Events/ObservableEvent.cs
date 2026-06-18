/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ObservableEvent.cs
 * author:    yingzheng
 * created:   2026/5/14
 * descrip:   带缓冲补发能力的事件容器抽象基类
 ***************************************************************/

using System;
using System.Collections.Generic;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 带缓冲补发能力的事件容器抽象基类。
    /// 新订阅者注册时自动补发缓冲内容，解决事件先于订阅触发导致的丢失问题。
    /// 两种具体策略：StickyEvent（保留最新值）和 ReplayEvent（保留固定容量队列）。
    /// 线程约束：Subscribe / Invoke / Clear 均须在主线程调用，内部不加锁。
    /// </summary>
    /// <typeparam name="T">事件载荷类型。</typeparam>
    public abstract class ObservableEvent<T>
    {
        /// <summary>
        /// 订阅事件。注册时自动补发缓冲内容，之后持续接收新事件。
        /// </summary>
        /// <param name="handler">事件处理委托。</param>
        /// <returns>取消订阅句柄，调用 Dispose() 即取消。</returns>
        public abstract IDisposable Subscribe(Action<T> handler);

        /// <summary>
        /// 取消指定 handler 的事件订阅。
        /// </summary>
        /// <param name="handler">订阅时传入的事件处理委托。</param>
        public abstract void Unsubscribe(Action<T> handler);

        /// <summary>
        /// 触发事件，写入缓冲并通知所有已注册订阅者。Plugin 内部调用。
        /// </summary>
        /// <param name="value">事件载荷。</param>
        public abstract void Invoke(T value);

        /// <summary>
        /// 清空缓冲区并移除所有订阅者。Plugin Dispose 时调用。
        /// </summary>
        public abstract void Clear();
    }

    /// <summary>
    /// ObservableEvent 扩展方法，提供绑定生命周期容器的订阅重载。
    /// </summary>
    public static class ObservableEventExtensions
    {
        /// <summary>
        /// 订阅事件并将取消句柄加入生命周期容器，容器清空时自动批量取消订阅。
        /// </summary>
        /// <typeparam name="T">事件载荷类型。</typeparam>
        /// <param name="observableEvent">目标事件容器。</param>
        /// <param name="handler">事件处理委托。</param>
        /// <param name="bag">生命周期容器，取消句柄将加入此集合。</param>
        /// <exception cref="ArgumentNullException">observableEvent、handler 或 bag 为 null 时抛出。</exception>
        public static void Subscribe<T>(this ObservableEvent<T> observableEvent, Action<T> handler, ICollection<IDisposable> bag)
        {
            if (observableEvent == null) throw new ArgumentNullException(nameof(observableEvent));
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            if (bag == null) throw new ArgumentNullException(nameof(bag));
            bag.Add(observableEvent.Subscribe(handler));
        }
    }
}
