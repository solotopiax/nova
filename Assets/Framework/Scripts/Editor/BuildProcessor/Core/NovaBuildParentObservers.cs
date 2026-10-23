/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  NovaBuildParentObservers.cs
 * author:    taoye
 * created:   2026/10/19
 * descrip:   Nova普通构建链内部阶段的默认休眠观察桥
 ***************************************************************/
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEditor.Build.Reporting;

namespace NovaFramework.Editor
{
    /// <summary>只接收原普通链内部信号，不创建Context/SDK或调用构建；非公开Action或SDK准入接口。</summary>
    internal interface INovaBuildParentObserver
    {
        /// <summary>原CollectProcessors返回后、Android任何pre或文件写入之前的真实信号。</summary>
        /// <param name="report">原回调report。</param><param name="context">原Shared Context。</param><param name="processors">原普通构造列表。</param>
        void ObserveConstructed(BuildReport report, NovaBuildContext context, List<NovaSDKBuildProcessor> processors);
        /// <summary>原父merge/Manifest.Save/Proguard.Rebuild后且AfterNovaPre叶子前的真实信号。</summary>
        /// <param name="report">原回调report。</param><param name="context">原Shared Context。</param><param name="processors">原pre排序列表。</param>
        void ObservePre(BuildReport report, NovaBuildContext context, List<NovaSDKBuildProcessor> processors);
        /// <summary>原普通Android post列表后且AfterNovaPost叶子前的真实信号。</summary>
        /// <param name="report">原回调report。</param><param name="context">原Shared Context。</param><param name="processors">同批实例的原post排序列表。</param>
        void ObservePost(BuildReport report, NovaBuildContext context, List<NovaSDKBuildProcessor> processors);
    }

    /// <summary>原Android foreach中单个SDK调用的同步边界；不赋予执行、替换或重放SDK的能力。</summary>
    internal interface INovaBuildSdkPreObserver
    {
        /// <summary>原SDK调用前的真实参数；列表已经按原PreprocessPriority排序。</summary>
        void BeforeSdkPre(BuildReport report, NovaBuildContext context, List<NovaSDKBuildProcessor> processors, NovaSDKBuildProcessor processor);
        /// <summary>仅原SDK正常返回后通知；异常时没有after，不能据较晚状态补造。</summary>
        void AfterSdkPre(BuildReport report, NovaBuildContext context, List<NovaSDKBuildProcessor> processors, NovaSDKBuildProcessor processor);
    }

    /// <summary>默认无observer的中立同步桥；不注册Editor事件、不持久化、不执行或替换SDK回调。</summary>
    internal static class NovaBuildParentObservers
    {
        private static Lease active;
        private static SdkPreLease sdkPreActive;
        /// <summary>只读观察桥是否休眠，不初始化任何Unity或SDK对象。</summary>
        internal static bool IsInactive { get { return Volatile.Read(ref active) == null; } }

        /// <summary>登记本线程唯一观察者；调用方仍负责确认实际Unity主线程和原业务授权。</summary>
        /// <param name="observer">当前内部受控作用域，不得为null。</param><returns>必须在原线程finally释放的准确租约。</returns>
        internal static Lease Register(INovaBuildParentObserver observer)
        {
            if (observer == null) throw new ArgumentNullException(nameof(observer));
            var value = new Lease(observer);
            if (Interlocked.CompareExchange(ref active, value, null) != null) throw new InvalidOperationException("Nova parent已有观察租约，不覆盖或嵌套。");
            return value;
        }

        /// <summary>登记独立的原SDK前处理观察租约，可与原父阶段租约并存；不触发SDK或Editor事件。</summary>
        /// <param name="observer">内部准确观察者。</param><returns>须在原线程finally释放的唯一租约。</returns>
        internal static SdkPreLease RegisterSdkPre(INovaBuildSdkPreObserver observer)
        {
            if (observer == null) throw new ArgumentNullException(nameof(observer));
            var value = new SdkPreLease(observer);
            if (Interlocked.CompareExchange(ref sdkPreActive, value, null) != null) throw new InvalidOperationException("Nova SDK pre已有观察租约，不覆盖或嵌套。");
            return value;
        }

        /// <summary>只转发原Android SDK调用前真实参数，默认无租约时直接返回。</summary>
        internal static void BeforeSdkPre(BuildReport report, NovaBuildContext context, List<NovaSDKBuildProcessor> processors, NovaSDKBuildProcessor processor)
        { SdkPreLease current = Volatile.Read(ref sdkPreActive); if (current != null) current.Invoke(false, report, context, processors, processor); }
        /// <summary>只转发原Android SDK正常返回的真实参数；不捕获原SDK异常或制造after。</summary>
        internal static void AfterSdkPre(BuildReport report, NovaBuildContext context, List<NovaSDKBuildProcessor> processors, NovaSDKBuildProcessor processor)
        { SdkPreLease current = Volatile.Read(ref sdkPreActive); if (current != null) current.Invoke(true, report, context, processors, processor); }

        /// <summary>只拥有SDK观察登记与线程身份，不拥有原SDK、父租约、Context或构建。</summary>
        internal sealed class SdkPreLease : IDisposable
        {
            private INovaBuildSdkPreObserver observer;
            private readonly int ownerThread;
            private bool disposed;
            /// <summary>只保存原观察引用与线程，不触发业务初始化。</summary>
            internal SdkPreLease(INovaBuildSdkPreObserver value) { observer = value; ownerThread = Thread.CurrentThread.ManagedThreadId; }
            /// <summary>同线程只读当前准确登记，供原导入旁路信号也核对同一租约；不调用SDK。</summary>
            internal bool IsCurrent { get { RequireOwner(); return !disposed && ReferenceEquals(Volatile.Read(ref sdkPreActive), this); } }
            /// <summary>同步验证原线程及当前准确登记后转发，异常和取消不重试。</summary>
            internal void Invoke(bool after, BuildReport report, NovaBuildContext context, List<NovaSDKBuildProcessor> processors, NovaSDKBuildProcessor processor)
            {
                if (!IsCurrent) throw new InvalidOperationException("Nova SDK pre观察租约已失效。");
                if (after) observer.AfterSdkPre(report, context, processors, processor); else observer.BeforeSdkPre(report, context, processors, processor);
            }
            /// <summary>观察及释放只能来自登记线程。</summary>
            private void RequireOwner() { if (Thread.CurrentThread.ManagedThreadId != ownerThread) throw new InvalidOperationException("Nova SDK pre租约只能在原线程使用和释放。"); }
            /// <summary>只移除自身登记，重复释放幂等；不清理原SDK或Shared状态。</summary>
            public void Dispose()
            {
                RequireOwner(); if (disposed) return;
                if (!ReferenceEquals(Interlocked.CompareExchange(ref sdkPreActive, null, this), this)) throw new InvalidOperationException("Nova SDK pre原租约已替换。");
                disposed = true; observer = null;
            }
        }

        /// <summary>仅由原Android分发内部点同步转发真实构造信号；默认休眠时直接返回。</summary>
        /// <param name="report">原report。</param><param name="context">原Context。</param><param name="processors">原列表。</param>
        internal static void ObserveConstructed(BuildReport report, NovaBuildContext context, List<NovaSDKBuildProcessor> processors)
        { Lease current = Volatile.Read(ref active); if (current != null) current.Invoke(0, report, context, processors); }
        /// <summary>仅由原merge保存内部点同步转发；不把整个pre返回后态当父历史。</summary>
        /// <param name="report">原report。</param><param name="context">原Context。</param><param name="processors">原列表。</param>
        internal static void ObservePre(BuildReport report, NovaBuildContext context, List<NovaSDKBuildProcessor> processors)
        { Lease current = Volatile.Read(ref active); if (current != null) current.Invoke(1, report, context, processors); }
        /// <summary>仅由原普通post列表后的内部点转发，AfterNovaPost叶子仍在其后正常执行。</summary>
        /// <param name="report">原report。</param><param name="context">原Context。</param><param name="processors">原列表。</param>
        internal static void ObservePost(BuildReport report, NovaBuildContext context, List<NovaSDKBuildProcessor> processors)
        { Lease current = Volatile.Read(ref active); if (current != null) current.Invoke(2, report, context, processors); }

        /// <summary>只拥有自身观察登记；不拥有原SDK/report/Context生命周期或任何文件恢复。</summary>
        internal sealed class Lease : IDisposable
        {
            private INovaBuildParentObserver observer;
            private readonly int ownerThread;
            private bool disposed;
            /// <summary>登记前只保存观察者与当前线程，不触发任何业务。</summary>
            /// <param name="value">准确观察者。</param>
            internal Lease(INovaBuildParentObserver value) { observer = value; ownerThread = Thread.CurrentThread.ManagedThreadId; }
            /// <summary>核登记线程，避免工作线程代替原普通同步链或清除主线程租约。</summary>
            private void RequireOwnerThread()
            { if (Thread.CurrentThread.ManagedThreadId != ownerThread) throw new InvalidOperationException("Nova parent观察租约只能在原线程使用和释放。"); }
            /// <summary>同步转发同一批原参数；观察异常和取消原样传播，不重试、不调用下一阶段。</summary>
            /// <param name="phase">桥内固定0/1/2阶段。</param><param name="report">原report。</param><param name="context">原Context。</param><param name="processors">原列表。</param>
            internal void Invoke(int phase, BuildReport report, NovaBuildContext context, List<NovaSDKBuildProcessor> processors)
            {
                RequireOwnerThread();
                if (disposed || !ReferenceEquals(Volatile.Read(ref active), this)) throw new InvalidOperationException("Nova parent观察租约已失效。");
                if (phase == 0) observer.ObserveConstructed(report, context, processors);
                else if (phase == 1) observer.ObservePre(report, context, processors);
                else if (phase == 2) observer.ObservePost(report, context, processors);
                else throw new InvalidOperationException("Nova parent观察阶段未知。");
            }
            /// <summary>只解除自身登记并释放观察引用；原线程finally须调用，重复释放幂等。</summary>
            public void Dispose()
            {
                RequireOwnerThread(); if (disposed) return;
                if (!ReferenceEquals(Interlocked.CompareExchange(ref active, null, this), this)) throw new InvalidOperationException("Nova parent原登记不匹配，不清除其他租约。");
                disposed = true; observer = null;
            }
        }
    }
}
