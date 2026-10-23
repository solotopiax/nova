/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  NativeBuildPreviewObservers.cs
 * author:    taoye
 * created:   2026/10/20
 * descrip:   Native原预览查询的默认休眠内部观察桥
 ***************************************************************/
using System;
using System.Threading;
using NovaFramework.Runtime;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine.SceneManagement;

namespace NovaFramework.Editor
{
    /// <summary>只接收原查询真正发生的同步阶段；不打开、关闭、替换或重放场景及组件行为。</summary>
    internal interface INativeBuildPreviewObserver
    {
        /// <summary>原Native处理器即将查询完整Build Settings数组。</summary>
        void QueryStarted(NativeBuildProcessor processor, BuildReport report, NovaBuildContext context, EditorBuildSettingsScene[] scenes);
        /// <summary>紧邻原OpenPreviewScene之前的真实调用点。</summary>
        void BeforeOpen(int entryIndex, string path);
        /// <summary>原OpenPreviewScene成功返回，且已经进入原try/finally。</summary>
        void Opened(int entryIndex, string path, Scene scene);
        /// <summary>原短路表达式实际求值的结果；未求值字段以evaluated区分，观察者不得再读配置。</summary>
        void CandidateRead(int entryIndex, NativeComponent component, bool enabled, bool activeEvaluated, bool active,
            bool configEvaluated, bool configNonNull, bool installEvaluated, bool installEnabled);
        /// <summary>本场景原扫描实际结束；matched=true表示原方法即将短路返回。</summary>
        void SceneScanCompleted(int entryIndex, Scene scene, bool matched);
        /// <summary>原finally即将关闭同一场景；本观察抛错也不得跳过原关闭。</summary>
        void BeforeClose(int entryIndex, Scene scene);
        /// <summary>原ClosePreviewScene真正返回后记录其返回值。</summary>
        void Closed(int entryIndex, Scene scene, bool result);
        /// <summary>原查询真正返回，不以当前配置补造结果。</summary>
        void QueryCompleted(bool result);
        /// <summary>记录原open、scan或close异常；不补齐尚未发生的阶段。</summary>
        void Failed(string stage, int entryIndex, Exception error);
    }

    /// <summary>同程序集内部唯一租约；默认休眠，不依赖Handler或SDK程序集，不注册Editor事件。</summary>
    internal static class NativeBuildPreviewObservers
    {
        private static Lease active;
        /// <summary>只读当前租约，不创建业务或Unity对象。</summary>
        internal static bool IsInactive => Volatile.Read(ref active) == null;

        /// <summary>登记当前线程的唯一内部观察者；不能覆盖或嵌套原租约。</summary>
        /// <param name="observer">正式typed作用域。</param><returns>必须在原线程finally释放的租约。</returns>
        internal static Lease Register(INativeBuildPreviewObserver observer)
        {
            if (observer == null) throw new ArgumentNullException(nameof(observer));
            var value = new Lease(observer);
            if (Interlocked.CompareExchange(ref active, value, null) != null) throw new InvalidOperationException("Native预览已有观察租约。");
            return value;
        }

        /// <summary>取得原线程观察者；无租约时只返回null，不读取组件或设置。</summary>
        /// <returns>当前原线程观察者，或null。</returns>
        internal static INativeBuildPreviewObserver Current()
        {
            Lease value = Volatile.Read(ref active);
            return value == null ? null : value.GetObserver();
        }

        /// <summary>保留原异常；仅将失败观察本身的异常与原异常合并，避免丢失真实失败。</summary>
        /// <param name="observer">本次原租约观察者。</param><param name="stage">准确失败阶段。</param><param name="entryIndex">原数组索引，查询层为-1。</param><param name="error">真实原异常。</param><returns>原异常或包含双方的聚合。</returns>
        internal static Exception RecordFailure(INativeBuildPreviewObserver observer, string stage, int entryIndex, Exception error)
        {
            if (observer == null) return error;
            try { observer.Failed(stage, entryIndex, error); return error; }
            catch (Exception observationError) { return new AggregateException("Native原查询和失败观察均失败。", error, observationError); }
        }

        /// <summary>只拥有自身登记，不拥有Native、Context、report、scene或其生命周期。</summary>
        internal sealed class Lease : IDisposable
        {
            private readonly int ownerThread = Thread.CurrentThread.ManagedThreadId;
            private INativeBuildPreviewObserver observer;
            private bool disposed;
            /// <summary>只保存观察引用。</summary>
            /// <param name="value">准确观察者。</param>
            internal Lease(INativeBuildPreviewObserver value) { observer = value; }
            /// <summary>核原线程和原登记，不在其它线程转发或清除。</summary>
            private void RequireOwner()
            { if (Thread.CurrentThread.ManagedThreadId != ownerThread) throw new InvalidOperationException("Native预览租约只能在原线程使用和释放。"); }
            /// <summary>仅向同一原租约返回观察者。</summary>
            /// <returns>原观察者。</returns>
            internal INativeBuildPreviewObserver GetObserver()
            {
                RequireOwner();
                if (disposed || !ReferenceEquals(Volatile.Read(ref active), this)) throw new InvalidOperationException("Native预览租约已失效。");
                return observer;
            }
            /// <summary>仅解除自身登记；不代原方法关闭场景或修改事件。</summary>
            public void Dispose()
            {
                RequireOwner(); if (disposed) return;
                if (!ReferenceEquals(Interlocked.CompareExchange(ref active, null, this), this)) throw new InvalidOperationException("Native预览原租约不匹配。");
                disposed = true; observer = null;
            }
        }
    }
}
