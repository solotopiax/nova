/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  NovaAndroidParentBuildObserverScope.cs
 * author:    taoye
 * created:   2026/10/19
 * descrip:   Android Nova父内部三阶段的本域精确观察作用域
 ***************************************************************/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Newtonsoft.Json;
using UnityEditor.Build.Reporting;
using C = NovaFramework.Editor.NovaAndroidParentBuildContract;

namespace NovaFramework.Editor
{
    /// <summary>只观察普通构造和父内部阶段，不调用SDK、BuildPipeline或私有回调；三阶段完整也不代表AfterNova/SDK整体构建成功。</summary>
    internal sealed class NovaAndroidParentBuildObserverScope : IDisposable, INovaBuildParentObserver
    {
        /// <summary>不含Unity/SDK引用的真实阶段材料；外层异常不能用较晚状态补造缺帧。</summary>
        [Serializable] internal sealed class Evidence
        {
            public C.ContextFrame constructed;
            public C.ContextFrame parentMerged;
            public C.ContextFrame post;
            public string observerFailure;
            public bool parentPhasesObserved;
            public bool disposed;
        }

        /// <summary>独立纯身份/阶段状态机，仅保护引用与顺序，不替SDK/Context领域合同准入。</summary>
        internal sealed class PhaseSequence
        {
            private object context;
            private object processors;
            private object report;
            private object[] instances;
            private string previous;
            /// <summary>先验证下一阶段和原引用；不会因错误输入推进状态。</summary>
            /// <param name="phase">constructed/pre/post固定名称。</param><param name="actualContext">实际原Context引用。</param><param name="actualList">实际原列表引用。</param><param name="actualReport">实际原report引用。</param><param name="actualInstances">本时点列表的完整实例引用数组。</param>
            internal void ValidateNext(string phase, object actualContext, object actualList, object actualReport, object[] actualInstances)
            {
                if (!C.IsNextPhase(previous, phase) || actualContext == null || actualList == null || actualReport == null || actualInstances == null || actualInstances.Length == 0 || actualInstances.Any(value => value == null) || actualInstances.Any(value => actualInstances.Count(item => ReferenceEquals(item, value)) != 1)) throw new InvalidOperationException("Nova parent阶段或完整实例集合缺失/重复。");
                if (previous != null && (!ReferenceEquals(context, actualContext) || !ReferenceEquals(processors, actualList) || !ReferenceEquals(report, actualReport) || actualInstances.Length != instances.Length || instances.Any(value => actualInstances.Count(item => ReferenceEquals(item, value)) != 1))) throw new InvalidOperationException("Nova parent原Context/report/列表或同批实例已替换。");
            }
            /// <summary>仅真实领域阶段完整验证通过后提交；构造首次保留原引用，之后不替换业务对象。</summary>
            /// <param name="phase">已验证准确阶段。</param><param name="actualContext">原Context。</param><param name="actualList">原列表。</param><param name="actualReport">原report。</param><param name="actualInstances">完整原实例数组。</param>
            internal void Commit(string phase, object actualContext, object actualList, object actualReport, object[] actualInstances)
            {
                ValidateNext(phase, actualContext, actualList, actualReport, actualInstances);
                if (previous == null) { context = actualContext; processors = actualList; report = actualReport; instances = (object[])actualInstances.Clone(); }
                previous = phase;
            }
            /// <summary>只读是否按原顺序完整提交三个阶段，不证明普通调用链或SDK成功。</summary>
            internal bool IsComplete { get { return previous == "post"; } }
            /// <summary>只清自有引用和阶段，不改原对象；释放后不能作为旧历史继续使用。</summary>
            internal void Clear() { context = null; processors = null; report = null; instances = null; previous = null; }
        }

        private C.FactorySnapshot factory;
        private C.Prepared prepared;
        private C.ContextBinding binding;
        private NovaBuildParentObservers.Lease lease;
        private readonly CancellationToken token;
        private readonly int ownerThread;
        private readonly PhaseSequence sequence = new PhaseSequence();
        private readonly Evidence evidence = new Evidence();
        private bool disposed;
        /// <summary>登记前仅保存已验证借用材料，不取得Prepared所有权，不构造SDK。</summary>
        /// <param name="expected">本域真实metadata。</param><param name="outputs">借用的独立父预测，外层另负责Dispose。</param><param name="cancellation">原Action取消令牌。</param>
        private NovaAndroidParentBuildObserverScope(C.FactorySnapshot expected, C.Prepared outputs, CancellationToken cancellation)
        { factory = expected; prepared = outputs; token = cancellation; ownerThread = Thread.CurrentThread.ManagedThreadId; }

        /// <summary>在正常Build入口前取得唯一主线程观察租约，写前再次完整验证；不调用构造、回调或构建。</summary>
        /// <param name="expected">原同域真实工厂计划，禁止JSON恢复见证。</param><param name="outputs">原父预测材料。</param><param name="cancellation">原取消令牌。</param><returns>必须在原主线程finally释放的scope。</returns>
        internal static NovaAndroidParentBuildObserverScope Begin(C.FactorySnapshot expected, C.Prepared outputs, CancellationToken cancellation)
        {
            if (!AgentActionRuntime.IsMainThread) throw new InvalidOperationException("Nova parent观察只能由原Unity主线程取得。");
            if (outputs == null) throw new ArgumentNullException(nameof(outputs));
            cancellation.ThrowIfCancellationRequested(); outputs.RequireProjectRoot(BuildActionCommon.ProjectRoot);
            C.VerifyFactoryBefore(expected, cancellation); outputs.VerifyBefore(cancellation);
            var value = new NovaAndroidParentBuildObserverScope(expected, outputs, cancellation);
            try { cancellation.ThrowIfCancellationRequested(); value.lease = NovaBuildParentObservers.Register(value); return value; }
            catch { value.Dispose(); throw; }
        }
        /// <summary>只由中立桥的原constructed内部点进入，不允许外层方法用较晚current补帧。</summary>
        /// <param name="report">原普通pre报告。</param><param name="context">原Shared Context。</param><param name="processors">原CollectProcessors实际创建列表。</param>
        void INovaBuildParentObserver.ObserveConstructed(BuildReport report, NovaBuildContext context, List<NovaSDKBuildProcessor> processors) { Observe("constructed", report, context, processors); }
        /// <summary>原父merge/保存内部点进入；AfterNovaPre的物理变换仍未执行，另需独立合同。</summary>
        /// <param name="report">原报告。</param><param name="context">原Context。</param><param name="processors">同批pre排序列表。</param>
        void INovaBuildParentObserver.ObservePre(BuildReport report, NovaBuildContext context, List<NovaSDKBuildProcessor> processors) { Observe("pre", report, context, processors); }
        /// <summary>原普通post列表后进入；AfterNovaPost叶子仍由原链继续正常执行。</summary>
        /// <param name="report">原报告。</param><param name="context">原Context。</param><param name="processors">同批post排序列表。</param>
        void INovaBuildParentObserver.ObservePost(BuildReport report, NovaBuildContext context, List<NovaSDKBuildProcessor> processors) { Observe("post", report, context, processors); }

        /// <summary>同步核同对象、取消、准确阶段和父输出；异常保留已实际取得材料及固定类型诊断并原样传播。</summary>
        /// <param name="phase">固定真实阶段。</param><param name="report">原报告。</param><param name="context">原Context。</param><param name="processors">原列表。</param>
        private void Observe(string phase, BuildReport report, NovaBuildContext context, List<NovaSDKBuildProcessor> processors)
        {
            RequireOwnerThread();
            if (disposed || evidence.observerFailure != null) throw new InvalidOperationException("Nova parent观察已结束/失败，不补帧或重试。");
            try
            {
                token.ThrowIfCancellationRequested(); object[] instances = processors == null ? null : processors.Cast<object>().ToArray();
                sequence.ValidateNext(phase, context, processors, report, instances);
                if (phase == "constructed")
                {
                    prepared.VerifyBefore(token);
                    binding = new C.ContextBinding(factory, context, processors, report);
                    evidence.constructed = Copy(binding.constructed);
                }
                else if (phase == "pre")
                {
                    if (binding == null) throw new InvalidOperationException("Nova parent缺原构造观察。");
                    evidence.parentMerged = Copy(binding.Observe("pre"));
                    if (!C.MatchesExpectedPre(evidence.parentMerged, prepared.summary) || !prepared.VerifyAfter(token)) throw new InvalidOperationException("Nova parent真实合并阶段与冻结父预测不符。");
                }
                else
                {
                    if (binding == null) throw new InvalidOperationException("Nova parent缺原构造观察。");
                    evidence.post = Copy(binding.Observe("post"));
                }
                sequence.Commit(phase, context, processors, report, instances);
            }
            catch (Exception error) { evidence.observerFailure = phase + ":" + error.GetType().Name; throw; }
        }
        /// <summary>只读复制当前真实材料，无较晚重采集；外层SDK异常由原Action另行保留，不被此DTO改成成功。</summary>
        /// <returns>独立DTO，不持有Unity/SDK对象。</returns>
        internal Evidence CaptureEvidence() { RequireOwnerThread(); return Copy(evidence); }
        /// <summary>只确认三个真实父阶段均提交；必须在正常调用返回后使用，之后仍需叶子及最终文件独立验证。</summary>
        /// <returns>父阶段完整DTO，不是SDK/Player成功收据。</returns>
        internal Evidence CompleteObservation()
        {
            RequireOwnerThread();
            if (disposed || evidence.observerFailure != null || !sequence.IsComplete || evidence.constructed == null || evidence.parentMerged == null || evidence.post == null) throw new InvalidOperationException("Nova parent真实三阶段不完整，不能用外层后态补造。");
            token.ThrowIfCancellationRequested(); evidence.parentPhasesObserved = true; return Copy(evidence);
        }
        /// <summary>保持原Unity主线程作用域，不允许工作线程转发或释放。</summary>
        private void RequireOwnerThread()
        { if (Thread.CurrentThread.ManagedThreadId != ownerThread) throw new InvalidOperationException("Nova parent scope只能在原主线程使用和释放。"); }
        /// <summary>仅复制固定小DTO，不持有Unity对象，不从当前状态重新构造历史。</summary>
        /// <typeparam name="T">本类或父合同明确DTO。</typeparam><param name="value">已真实取得材料。</param><returns>完整独立副本。</returns>
        private static T Copy<T>(T value) => JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(value));
        /// <summary>原主线程finally解除自身登记并释放观察引用；不Dispose借用Prepared，不改Shared、SDK、文件或事件。</summary>
        public void Dispose()
        {
            RequireOwnerThread(); if (disposed) return;
            if (lease != null) { lease.Dispose(); lease = null; }
            if (!evidence.parentPhasesObserved && evidence.observerFailure == null) evidence.observerFailure = "scope-disposed-without-complete-observation";
            disposed = true; evidence.disposed = true; binding?.Dispose(); binding = null; sequence.Clear(); factory = null; prepared = null;
        }
    }
}
