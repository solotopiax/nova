/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AndroidRefreshInitializationWitness.cs
 * author:    taoye
 * created:   2026/11/16
 * descrip:   正常资源派发完成后的同域初始化见证，不触发厂商静态构造
 ***************************************************************/
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using UnityEditor;
using UnityEngine;

namespace NovaFramework.Editor
{
    /// <summary>纯证据状态机；真实调用栈和引擎派发表仅由下方生产观察器提供，不以测试输入作为运行许可。</summary>
    internal sealed class AndroidRefreshInitializationEvidence
    {
        private readonly string m_Domain;
        private string m_Pending, m_Ready;
        private long m_ErrorEpoch;
        /// <summary>只有观察到的新派发候选需要下一正常update做完整方法集合复核。</summary>
        internal bool HasPending => m_Pending != null;

        /// <summary>每个真实域创建独立身份，跨域不能继承完成状态。</summary>
        internal AndroidRefreshInitializationEvidence(string domain) { m_Domain = domain; }

        /// <summary>四参观察器只在非全空派发中被引擎调用；实际方法多重集合一致且本周期无异常才记录候选。</summary>
        internal bool Begin(string expected, string actual, bool trustedFourParameterDispatch, long quietErrors, long currentErrors)
        {
            m_Ready = m_Pending = null;
            if (!trustedFourParameterDispatch || string.IsNullOrEmpty(expected) || expected != actual || quietErrors != currentErrors) return false;
            m_Pending = expected; m_ErrorEpoch = currentErrors; return true;
        }

        /// <summary>只有派发帧已退出后的正规update、同域同roster且没有异常增量才提交。</summary>
        internal bool Complete(string domain, string roster, long errors, bool normalUpdateOutsideDispatch)
        {
            if (m_Pending == null) return false;
            string pending = m_Pending; m_Pending = null;
            if (!normalUpdateOutsideDispatch || domain != m_Domain || roster != pending || errors != m_ErrorEpoch)
            { m_Ready = null; return false; }
            m_Ready = roster; return true;
        }

        /// <summary>候选未完成、异常、域或方法集合改变均拒绝；不提供初始化或恢复功能。</summary>
        internal bool Matches(string domain, string roster, long errors) => m_Pending == null && m_Ready != null &&
            domain == m_Domain && m_Ready == roster && m_ErrorEpoch == errors;

        /// <summary>编译、重载、未知帧或读取失败时撤销证据，不触发资源刷新来重建。</summary>
        internal void Reset() { m_Pending = m_Ready = null; }
    }

    /// <summary>在正常Unity生命周期中观察，不调用vendor回调、getter、cctor或AssetDatabase.Refresh。</summary>
    [InitializeOnLoad]
    internal static class AndroidRefreshInitializationWitness
    {
        private static readonly string s_Domain = Guid.NewGuid().ToString("N");
        private static readonly AndroidRefreshInitializationEvidence s_Evidence = new AndroidRefreshInitializationEvidence(s_Domain);
        private static readonly AndroidRefreshInitializationEvidence s_TailEvidence = new AndroidRefreshInitializationEvidence(s_Domain);
        private static readonly int s_MainThread = Thread.CurrentThread.ManagedThreadId;
        private static long s_Errors, s_QuietErrors;
        /// <summary>本方观察入口收到的通知数和既有状态机成功完成数；仅供诊断，不参与见证许可。</summary>
        private static long s_ObservedDispatchCount, s_CompletedDispatchCount;
        /// <summary>最近一次已接纳候选的诊断镜像，只用于解释完成拒绝，不替代状态机内的真实证据。</summary>
        private static string s_LastCandidateRoster;
        private static long s_LastCandidateErrors;
        private static bool s_CoreVerified;
        private static string s_TailFailure = "本域尚无真实资源派发尾部完成证据。";
        private static string s_LastFailure = "本次启动还没有正常资源导入的完成记录；首次接入或升级请等导入结束后重试。仅打开没有文件变化的工程时，需等下一次实际导入后再计划。";

        /// <summary>仅供已审URP首次检查的独立完成推导排除异常半程；此值本身不授予任何cctor读取许可。</summary>
        internal static bool HasNoObservedDomainErrors => Interlocked.Read(ref s_Errors) == 0;

        /// <summary>只安装自有日志计数和完成帧观察；不读取任何vendor静态字段。</summary>
        static AndroidRefreshInitializationWitness()
        {
            Application.logMessageReceivedThreaded += OnLog;
            EditorApplication.update += OnUpdate;
            AssemblyReloadEvents.beforeAssemblyReload += Shutdown;
        }

        /// <summary>异常只计数，不记录用户日志正文，也不从日志推断或伪造回调完成。</summary>
        private static void OnLog(string condition, string stack, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) Interlocked.Increment(ref s_Errors); }

        /// <summary>由既有四参观察器通知；核真实六参Core派发帧及已物化排序表，诊断计数不构成完成证据。</summary>
        internal static void ObserveNormalDispatch()
        {
            Interlocked.Increment(ref s_ObservedDispatchCount);
            try
            {
                RequireCore();
                MethodBase[] frames = new System.Diagnostics.StackTrace(false).GetFrames()?.Select(frame => frame.GetMethod()).ToArray();
                bool trusted = Thread.CurrentThread.ManagedThreadId == s_MainThread && frames != null && frames.Any(IsEngineDispatchFrame);
                string expected = CaptureRoster(out _), actual = CaptureActualDispatchRoster();
                long errors = Interlocked.Read(ref s_Errors);
                if (s_Evidence.Begin(expected, actual, trusted, s_QuietErrors, errors))
                {
                    s_LastCandidateRoster = expected;
                    s_LastCandidateErrors = errors;
                    s_LastFailure = "已观察到正常资源派发，等待派发退出后的正常 Editor.update 完成帧。";
                }
                else if (!trusted)
                    s_LastFailure = "本轮派发不在当前主线程的真实引擎派发栈中，未建立初始化候选。";
                else if (string.IsNullOrEmpty(expected))
                    s_LastFailure = "本轮预期派发方法集合为空，未建立初始化候选。";
                else if (expected != actual)
                    s_LastFailure = "本轮实际派发方法集合与预期不一致，未建立初始化候选。";
                else
                    s_LastFailure = $"本轮静默错误基线与当前计数不一致（基线={s_QuietErrors}，当前={errors}），未建立初始化候选。";
            }
            catch (Exception exception)
            { s_Evidence.Reset(); s_LastFailure = "正常派发见证读取失败：" + exception.GetType().Name; }
        }

        /// <summary>独立五参观察器只为无条件执行的CodeEditor尾部记候选；不授予任何四参回调初始化许可。</summary>
        internal static void ObserveCodeEditorTailDispatch()
        {
            try
            {
                RequireCore();
                MethodBase[] frames = new System.Diagnostics.StackTrace(false).GetFrames()?.Select(frame => frame.GetMethod()).ToArray();
                bool trusted = Thread.CurrentThread.ManagedThreadId == s_MainThread && frames != null && frames.Any(IsEngineDispatchFrame);
                string expected = CaptureRoster(out _), actual = CaptureActualDispatchRoster();
                long errors = Interlocked.Read(ref s_Errors);
                s_TailFailure = s_TailEvidence.Begin(expected, actual, trusted, s_QuietErrors, errors)
                    ? "已观察到真实派发，等待CodeEditor尾部退出后的正常update。" : "尾部候选缺真实派发栈、完整方法集合或静默错误基线。";
            }
            catch (Exception exception)
            { s_TailEvidence.Reset(); s_TailFailure = "尾部候选读取失败：" + exception.GetType().Name; }
        }

        /// <summary>沿既有完成条件提交见证；分别记录更新等待、编译撤销和完成拒绝，不以诊断镜像授予许可。</summary>
        private static void OnUpdate()
        {
            if (EditorApplication.isCompiling)
            {
                s_Evidence.Reset();
                s_TailEvidence.Reset();
                s_LastFailure = "脚本正在编译；当前域的初始化候选和完成证据已撤销，等待正常资源派发及后续 update。";
                return;
            }
            if (EditorApplication.isUpdating)
            {
                if (s_Evidence.HasPending) s_LastFailure = "资源更新尚未结束；已观察到的派发仍在等待正常 Editor.update 完成帧。";
                return;
            }
            if (s_TailEvidence.HasPending)
            {
                try
                {
                    RequireCore();
                    MethodBase[] normalFrames = new System.Diagnostics.StackTrace(false).GetFrames()?.Select(frame => frame.GetMethod()).ToArray();
                    bool normal = normalFrames != null && normalFrames.Any(IsNormalUpdateFrame) && !normalFrames.Any(IsEngineDispatchFrame);
                    s_TailFailure = s_TailEvidence.Complete(s_Domain, CaptureRoster(out _), Interlocked.Read(ref s_Errors), normal)
                        ? null : "尾部完成帧的真实update、方法集合或错误计数不一致。";
                }
                catch (Exception exception)
                { s_TailEvidence.Reset(); s_TailFailure = "尾部完成证据读取失败：" + exception.GetType().Name; }
            }
            if (!s_Evidence.HasPending) { s_QuietErrors = Interlocked.Read(ref s_Errors); return; }
            try
            {
                RequireCore();
                MethodBase[] frames = new System.Diagnostics.StackTrace(false).GetFrames()?.Select(frame => frame.GetMethod()).ToArray();
                bool update = frames != null && frames.Any(IsNormalUpdateFrame) && !frames.Any(IsEngineDispatchFrame);
                string roster = CaptureRoster(out _);
                long errors = Interlocked.Read(ref s_Errors);
                if (s_Evidence.Complete(s_Domain, roster, errors, update))
                {
                    Interlocked.Increment(ref s_CompletedDispatchCount);
                    s_LastFailure = null;
                }
                else if (!update)
                    s_LastFailure = "初始化候选完成被拒绝：当前不在派发退出后的正常 Editor.update 栈中。";
                else if (roster != s_LastCandidateRoster)
                    s_LastFailure = "初始化候选完成被拒绝：派发方法集合在观察后发生变化。";
                else if (errors != s_LastCandidateErrors)
                    s_LastFailure = $"初始化候选完成被拒绝：观察后出现错误、异常或断言（候选={s_LastCandidateErrors}，当前={errors}）。";
                else
                    s_LastFailure = "初始化候选完成被拒绝：候选未满足既有同域完成条件。";
            }
            catch (Exception exception)
            { s_Evidence.Reset(); s_LastFailure = "派发完成帧见证失败：" + exception.GetType().Name; }
            s_QuietErrors = Interlocked.Read(ref s_Errors);
        }

        /// <summary>仅供CodeEditor合同核无条件尾部；空五参派发不进入原四参见证状态机。</summary>
        internal static bool TryGetCodeEditorTailProof(out string proof, out string error)
        {
            proof = null;
            error = null;
            try
            {
                RequireCore();
                string roster = CaptureRoster(out _);
                if (Thread.CurrentThread.ManagedThreadId != s_MainThread || EditorApplication.isCompiling || EditorApplication.isUpdating ||
                    !s_TailEvidence.Matches(s_Domain, roster, Interlocked.Read(ref s_Errors)))
                { error = s_TailFailure ?? "CodeEditor尾部证据已因当前域、方法集合或错误计数变化失效。"; return false; }
                proof = GenerateActionCommon.ComputeTextHash(s_Domain + "|code-editor-tail|" + roster);
                return true;
            }
            catch (Exception exception)
            { error = "CodeEditor尾部证据核验失败：" + exception.GetType().Name; return false; }
        }

        /// <summary>读取前只核元数据及已有完成证据；未正常派发的类型不能通过FieldInfo.GetValue首次初始化。</summary>
        internal static bool TryGetProof(Type callback, out string proof, out string error)
        {
            proof = null; error = null;
            try
            {
                RequireCore();
                string roster = CaptureRoster(out MethodInfo[] methods);
                if (Thread.CurrentThread.ManagedThreadId != s_MainThread || EditorApplication.isCompiling || EditorApplication.isUpdating ||
                    callback == null || !methods.Any(method => method.DeclaringType == callback) ||
                    !s_Evidence.Matches(s_Domain, roster, Interlocked.Read(ref s_Errors)))
                { error = s_LastFailure ?? "当前域/派发集合或异常计数已改变；等待正常资源派发及后续update，不主动刷新或初始化。"; return false; }
                proof = GenerateActionCommon.ComputeTextHash(s_Domain + "|" + roster); return true;
            }
            catch (Exception exception)
            { error = "初始化见证核验失败：" + exception.GetType().Name; return false; }
        }

        /// <summary>复现已审GetCallbacks的4参优先/5参后备，方法多重集合保留重复，不执行方法。</summary>
        private static string CaptureRoster(out MethodInfo[] methods)
        {
            methods = TypeCache.GetTypesDerivedFrom<AssetPostprocessor>().SelectMany(AndroidResolveRefreshCallbacks.GetDispatchMethods)
                .Where(method => method.Name == "OnPostprocessAllAssets").ToArray();
            if (methods.Length < 1 || methods.Length > 512) throw new InvalidOperationException("派发方法集合数量超界。");
            return RosterHash(methods);
        }

        /// <summary>只读当前真实派发中的已初始化排序表；表中每个节点必须是Core内准确MethodInfoCallback。</summary>
        private static string CaptureActualDispatchRoster()
        {
            var core = typeof(AssetDatabase).Assembly;
            Type dispatcher = core.GetType("UnityEditor.AssetPostprocessingInternal", true);
            object collection = dispatcher.GetField("s_OnPostprocessAllAssetsCallbacks", BindingFlags.Static | BindingFlags.NonPublic)?.GetValue(null);
            Type ordered = core.GetType("UnityEditor.Callbacks.OrderedCallbackCollection", true);
            Type node = core.GetType("UnityEditor.OnPostprocessAllAssetsCallbackCollection+MethodInfoCallback", true);
            object sorted = ordered.GetField("m_SortedCallbacks", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(collection);
            if (!(sorted is IList list) || !sorted.GetType().IsGenericType || sorted.GetType().GetGenericTypeDefinition() != typeof(List<>) || list.Count < 1 || list.Count > 512)
                throw new InvalidOperationException("真实排序派发表尚未初始化或结构变化。");
            var methods = new List<MethodInfo>();
            foreach (object item in list)
            {
                if (item?.GetType() != node) throw new InvalidOperationException("派发表含未知节点。");
                object value = node.GetField("<Method>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(item);
                object reload = node.GetField("<MethodDomainReload>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(item);
                if (!(value is MethodInfo method) || !(reload is bool domainReload) || domainReload != (method.GetParameters().Length == 5))
                    throw new InvalidOperationException("派发方法或重载标记变化。");
                methods.Add(method);
            }
            return RosterHash(methods);
        }

        /// <summary>只以已加载模块和方法元数据生成稳定多重集合摘要，忽略排序但保留重复调用项。</summary>
        private static string RosterHash(IEnumerable<MethodInfo> methods) => GenerateActionCommon.ComputeTextHash(string.Join("\n",
            methods.Select(method => method.Module.ModuleVersionId.ToString("D") + "|" + method.MetadataToken + "|" + method.DeclaringType.Assembly.FullName + "|" +
                method.DeclaringType.FullName + "|" + method).OrderBy(value => value, StringComparer.Ordinal)));

        /// <summary>准确识别当前已审Core中的六参资源派发入口，拒绝同名业务帧。</summary>
        private static bool IsEngineDispatchFrame(MethodBase method) => method?.DeclaringType?.Assembly == typeof(AssetDatabase).Assembly &&
            method.DeclaringType.FullName == "UnityEditor.AssetPostprocessingInternal" && method.Name == "PostprocessAllAssets" && method.IsStatic &&
            method.GetParameters().Length == 6 && method is MethodInfo info && info.ReturnType == typeof(void);

        /// <summary>只认可实际Core的正规update入口，不把任意同步上下文视为完成帧。</summary>
        private static bool IsNormalUpdateFrame(MethodBase method) => method?.DeclaringType == typeof(EditorApplication) &&
            method.Name == "Internal_CallUpdateFunctions" && method.IsStatic && method.GetParameters().Length == 0;

        /// <summary>绑定本合同已审的实际加载CoreModule，磁盘被替换时也不建立见证。</summary>
        private static void RequireCore()
        {
            var core = typeof(AssetDatabase).Assembly;
            if (core.ManifestModule.ModuleVersionId.ToString("D") != "11f76f7e-1291-4a87-8174-9af3fa5b621a") throw new InvalidOperationException("派发CoreModule未经审查。");
            if (!s_CoreVerified)
            {
                if (GenerateActionCommon.ComputeFileHash(core.Location, CancellationToken.None) != "14a25302095cb3c648b4b83b9936ba2df10454ae254da6440a1a968395f2d581")
                    throw new InvalidOperationException("派发CoreModule磁盘身份不同。");
                s_CoreVerified = true;
            }
        }

        /// <summary>域退出时移除本方订阅；不取消或操作任何vendor队列。</summary>
        private static void Shutdown()
        {
            Application.logMessageReceivedThreaded -= OnLog;
            EditorApplication.update -= OnUpdate;
            AssemblyReloadEvents.beforeAssemblyReload -= Shutdown;
            s_Evidence.Reset();
            s_TailEvidence.Reset();
        }
    }

    /// <summary>真实五参派发只用于观察无条件CodeEditor尾部；不调用或替代任何厂商回调。</summary>
    internal sealed class AndroidResolveCodeEditorTailObserver : AssetPostprocessor
    {
        /// <summary>空派发也由引擎正规进入，完成与否仍由独立后续update及完整派发表核验。</summary>
        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom, bool didDomainReload)
        { AndroidRefreshInitializationWitness.ObserveCodeEditorTailDispatch(); }
    }
}
