/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  UIViewScope.cs
 * author:    taoye
 * created:   2026/11/12
 * descrip:   页面主动接入的单次打开作用域
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace NovaFramework.Runtime
{
    /// <summary>仅拥有调用方主动登记的本次页面工作；不接管旧页面或全局任务。</summary>
    public sealed class UIViewScope : IDisposable
    {
        private readonly CancellationTokenSource m_Cancellation;
        private readonly CancellationToken m_Token;
        private readonly List<Action> m_Cleanups = new List<Action>();
        private readonly List<IDisposable> m_Owners = new List<IDisposable>();
        private readonly List<KeyValuePair<Type, EventHandler<EventData>>> m_Subscriptions = new List<KeyValuePair<Type, EventHandler<EventData>>>();
        private bool m_Disposed;
        private bool m_Disposing;

        /// <summary>在 Unity 主线程创建独立作用域；通常由 UIView.OpenScope 按需提供。</summary>
        public UIViewScope()
        {
            EnsureMainThread();
            m_Cancellation = new CancellationTokenSource();
            m_Token = m_Cancellation.Token;
        }

        /// <summary>获取本次打开的取消令牌；作用域结束后仍可读取并检查取消状态。</summary>
        public CancellationToken Token => m_Token;

        /// <summary>获取作用域是否已经结束；结束不会回滚已经到达外部服务的操作。</summary>
        public bool IsDisposed => m_Disposed;

        /// <summary>转移一个对象的释放责任；同一活动作用域重复登记同一实例只释放一次。</summary>
        /// <param name="owner">调用方不再另行释放的对象；不能为 null。</param>
        public void Own(IDisposable owner)
        {
            EnsureMainThread();
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            for (int i = 0; i < m_Owners.Count; i++)
                if (ReferenceEquals(m_Owners[i], owner)) return;
            if (m_Disposed)
            {
                // 保留当前释放身份，阻止 owner.Dispose 重入 Own(自身) 造成重复释放或递归。
                m_Owners.Add(owner);
                try { owner.Dispose(); }
                finally { if (!m_Disposing) m_Owners.Remove(owner); }
                return;
            }
            m_Owners.Add(owner);
            m_Cleanups.Add(owner.Dispose);
        }

        /// <summary>登记一项精确收尾操作；已结束时立即执行，不会重新开启作用域。</summary>
        /// <param name="cleanup">只清理本次登记内容的操作；不能为 null。</param>
        public void AddCleanup(Action cleanup)
        {
            EnsureMainThread();
            if (cleanup == null) throw new ArgumentNullException(nameof(cleanup));
            if (m_Disposed) cleanup();
            else m_Cleanups.Add(cleanup);
        }

        /// <summary>沿原事件管理器订阅，并在本作用域结束时精确退订相同处理器。</summary>
        /// <typeparam name="TEvent">原事件数据类型；分发、线程及回池规则不变。</typeparam>
        /// <param name="handler">同步事件处理器，不得跨回池生命周期保存事件对象。</param>
        public void Subscribe<TEvent>(EventHandler<EventData> handler) where TEvent : EventData
        {
            EnsureMainThread();
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            if (m_Disposed) throw new ObjectDisposedException(nameof(UIViewScope));
            foreach (KeyValuePair<Type, EventHandler<EventData>> entry in m_Subscriptions)
                if (entry.Key == typeof(TEvent) && entry.Value == handler)
                    throw new InvalidOperationException("同一作用域不能重复登记相同事件处理器。");
            IEventManager events = FrameworkManagersGroup.GetManager<IEventManager>();
            if (events == null) throw new InvalidOperationException("事件管理器尚未就绪。");
            // 独立委托身份保证允许重复 handler 的旧事件池中，只退订本作用域新增的一次订阅。
            EventHandler<EventData> scopedHandler = (sender, data) => handler(sender, data);
            events.Subscribe<TEvent>(scopedHandler);
            m_Subscriptions.Add(new KeyValuePair<Type, EventHandler<EventData>>(typeof(TEvent), handler));
            // 捕获接口拥有者，不在页面退出时重新解引用可能已销毁的 EventComponent。
            AddCleanup(() => events.Unsubscribe<TEvent>(scopedHandler));
        }

        /// <summary>先取消再逆序执行所有收尾；单项失败不阻止后续，完成后聚合抛出异常。</summary>
        public void Dispose()
        {
            EnsureMainThread();
            if (m_Disposed) return;
            m_Disposed = true;
            m_Disposing = true;
            Action[] cleanups = m_Cleanups.ToArray();
            m_Cleanups.Clear();
            List<Exception> failures = null;
            try { m_Cancellation.Cancel(); }
            catch (Exception exception) { (failures ??= new List<Exception>()).Add(exception); }
            for (int i = cleanups.Length - 1; i >= 0; i--)
            {
                try { cleanups[i](); }
                catch (Exception exception) { (failures ??= new List<Exception>()).Add(exception); }
            }
            m_Cancellation.Dispose();
            m_Owners.Clear();
            m_Subscriptions.Clear();
            m_Disposing = false;
            if (failures != null) throw new AggregateException("页面作用域收尾出现异常，已继续处理其余登记项。", failures);
        }

        /// <summary>框架生命周期收尾记录聚合异常，保证页面仍可继续关闭或回收。</summary>
        internal void DisposeFromOwner()
        {
            try { Dispose(); }
            catch (Exception exception) { Log.Error(LogTag.UI, "页面作用域收尾失败：{0}", exception); }
        }

        /// <summary>拒绝在后台线程登记、退订或释放 Unity 所拥有的工作。</summary>
        private static void EnsureMainThread()
        {
            if (!PlayerLoopHelper.IsMainThread)
                throw new InvalidOperationException("页面作用域只能在 Unity 主线程操作。");
        }
    }
}
