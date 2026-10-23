/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AndroidGenerateNativePreviewScope.cs
 * author:    taoye
 * created:   2026/10/20
 * descrip:   Native原预览查询的同线程真实阶段与局部副作用观察
 ***************************************************************/
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using Newtonsoft.Json;
using NovaFramework.Runtime;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using C = NovaFramework.Editor.AndroidGenerateNativePreviewContract;
using Object = UnityEngine.Object;

namespace NovaFramework.Editor
{
    /// <summary>仅接收原调用点；不打开/关闭场景、不运行生命周期、不复写UI/Input/运行时集合。</summary>
    internal sealed class AndroidGenerateNativePreviewScope : IDisposable, INativeBuildPreviewObserver
    {
        /// <summary>每个真实源码位置的一份小材料，严格ordinal不依赖渲染帧数。</summary>
        [Serializable] internal sealed class Stage
        {
            public int ordinal, entryIndex, previewCount;
            public string phase, path, sceneHandle;
            public bool preview, closeReturned, result;
            public string[] objects, canvas, environment;
        }
        /// <summary>值仅来自原短路表达式；未求值字段不能用false冒充已读。</summary>
        [Serializable] internal sealed class Candidate
        {
            public int ordinal, entryIndex;
            public ulong nativeId;
            public bool enabled, activeEvaluated, active, configEvaluated, configNonNull, installEvaluated, installEnabled;
        }
        /// <summary>原阶段/候选/失败完整历史；缺失或失败不从较晚current补帧。</summary>
        [Serializable] internal sealed class Evidence
        {
            public Stage[] stages;
            public Candidate[] candidates;
            public string[] failures, sceneEvents;
            public bool queryStarted, queryReturned, result, complete, disposed;
        }
        /// <summary>即时快照与真正对象引用分开；字符串身份仅供记录，准入同时用ReferenceEquals。</summary>
        private sealed class LiveFrame
        {
            internal string[] rows;
            internal object[] references;
            internal AssetFrame[] inputs;
            internal bool? needResolve;
        }
        /// <summary>只读已加载InputActionAsset及原map backing，不调用任何lazy action/control getter。</summary>
        private sealed class AssetFrame
        {
            internal Object asset;
            internal string path, json;
            internal bool dirty, freshMaps;
            internal string[] rows;
            internal object[] references;
        }

        private readonly C.Plan plan;
        private readonly NovaAndroidParentBuildContract.FactorySnapshot factory;
        private readonly CancellationToken token;
        private readonly int owner = Thread.CurrentThread.ManagedThreadId;
        private readonly List<Stage> stages = new List<Stage>();
        private readonly List<Candidate> candidates = new List<Candidate>();
        private readonly List<string> failures = new List<string>(), sceneEvents = new List<string>();
        private NativeBuildPreviewObservers.Lease lease;
        private NativeBuildProcessor processor;
        private NovaBuildContext context;
        private BuildReport report;
        private LiveFrame before;
        private GameObject[] roots;
        private MonoBehaviour scaler;
        private Scene currentScene;
        private int ordinal, entryIndex = -1;
        private string phase;
        private bool queryStarted, queryReturned, result, complete, disposed, eventRegistered;

        /// <summary>只保存已冻结材料，不创建业务对象或场景。</summary>
        private AndroidGenerateNativePreviewScope(C.Plan expected, NovaAndroidParentBuildContract.FactorySnapshot expectedFactory, CancellationToken cancellation)
        { plan = expected; factory = expectedFactory; token = cancellation; }

        /// <summary>写前复核完整计划后取得唯一主线程租约；事件监听只记录自然事件，不调用原监听。</summary>
        internal static AndroidGenerateNativePreviewScope Begin(C.Plan expected, NovaAndroidParentBuildContract.FactorySnapshot expectedFactory, CancellationToken cancellation)
        {
            if (!AgentActionRuntime.IsMainThread || expectedFactory?.types == null || expectedFactory.preprocessOrder?.FirstOrDefault() != NovaAndroidSdkLeafBuildContract.Native) throw new InvalidOperationException("Native观察缺真实主线程/首个原Native实例工厂。");
            C.VerifyPlan(expected, cancellation); var value = new AndroidGenerateNativePreviewScope(expected, expectedFactory, cancellation);
            try
            {
                value.lease = NativeBuildPreviewObservers.Register(value);
                EditorSceneManager.sceneOpened += value.ObserveSceneOpened; value.eventRegistered = true; return value;
            }
            catch { value.Dispose(); throw; }
        }

        /// <summary>每个原callback都核原线程、租约与同一原Context/report/processor，不仅依赖首次Current。</summary>
        private void RequireCurrent(bool allowFailure = false)
        {
            if (Thread.CurrentThread.ManagedThreadId != owner || !AgentActionRuntime.IsMainThread || disposed || !ReferenceEquals(NativeBuildPreviewObservers.Current(), this)) throw new InvalidOperationException("Native观察原主线程/租约已失效。");
            if (!allowFailure) token.ThrowIfCancellationRequested();
            if (queryStarted && (!ReferenceEquals(context, NovaBuildShared.Context) || !ReferenceEquals(report, context?.Report) || context.Target != BuildTarget.Android || NovaBuildShared.Processors == null || !NovaBuildShared.Processors.Select(value => value.GetType().FullName).SequenceEqual(factory.preprocessOrder, StringComparer.Ordinal) || NovaBuildShared.Processors.Count(value => ReferenceEquals(value, processor)) != 1)) throw new InvalidOperationException("Native原processor/Context/report/完整排序实例已替换。");
        }

        /// <summary>绑定原Native实例和其真正report/context；原POST_NOTIFICATIONS已经加入该Context。</summary>
        public void QueryStarted(NativeBuildProcessor actual, BuildReport actualReport, NovaBuildContext actualContext, EditorBuildSettingsScene[] scenes)
        {
            RequireCurrent();
            if (queryStarted || actual == null || actual.GetType() != typeof(NativeBuildProcessor) || actualContext == null || actualReport == null || !ReferenceEquals(actualContext, NovaBuildShared.Context) || !ReferenceEquals(actualContext.Report, actualReport) || !C.SceneRows(scenes).SequenceEqual(plan.scenes, StringComparer.Ordinal)) throw new InvalidOperationException("Native原查询身份/场景数组不符。");
            processor = actual; context = actualContext; report = actualReport; queryStarted = true; RequireCurrent();
            var expected = new[] { new ManifestRuleSet { Permissions = new[] { new PermissionRule("android.permission.POST_NOTIFICATIONS") } } };
            if (JsonConvert.SerializeObject(context.ManifestRuleSets) != JsonConvert.SerializeObject(expected) || context.ProguardRules.Count != 0) throw new InvalidOperationException("Native原查询前Context不是准确POST_NOTIFICATIONS阶段。");
            phase = "query"; Add("query-started", -1, default);
        }

        /// <summary>紧邻原Open前核源并取得本同步窗口before，不用Parent-pre后态代替。</summary>
        public void BeforeOpen(int index, string path)
        {
            RequireCurrent();
            if (!queryStarted || phase != "query" || index != Array.FindIndex(plan.scenes, value => value == "True|" + C.ScenePath) || path != C.ScenePath || stages.Count(value => value.phase == "before-open") != 0 || !C.CaptureSources(token).SequenceEqual(plan.sourcePins, StringComparer.Ordinal)) throw new InvalidOperationException("Native原open顺序/准确场景/源闭包不符。");
            C.ValidateSceneOpenedSubscribers(C.SceneOpenedDelegates(this));
            entryIndex = index; before = CaptureEnvironment(); phase = "before-open";
            Stage stage = Add(phase, index, default); stage.path = path; stage.environment = Describe(before);
        }

        /// <summary>只观察原Open返回的真实scene/root/22型；仅CanvasScaler允许已审编辑态行为。</summary>
        public void Opened(int index, string path, Scene scene)
        {
            RequireCurrent();
            currentScene = scene; roots = scene.GetRootGameObjects();
            Stage stage = Add("opened", index, scene);
            if (phase != "before-open" || index != entryIndex || path != C.ScenePath || !scene.IsValid() || !scene.isLoaded || !EditorSceneManager.IsPreviewScene(scene) || scene.path != path || EditorSceneManager.previewSceneCount != PreviewCount(before) + 1) throw new InvalidOperationException("Native原Opened场景/preview计数不符。");
            MonoBehaviour[] components = roots.SelectMany(root => root.GetComponentsInChildren<MonoBehaviour>(true)).ToArray();
            if (components.Any(value => value == null) || !components.Select(value => value.GetType().FullName).OrderBy(value => value, StringComparer.Ordinal).SequenceEqual(C.Components.OrderBy(value => value, StringComparer.Ordinal))) throw new InvalidOperationException("Native原preview实际MonoBehaviour集合不是已审22型。");
            if (components.Any(value => value.GetType().FullName != "UnityEngine.UI.CanvasScaler" && value.runInEditMode)) throw new InvalidOperationException("Native非CanvasScaler组件出现runInEditMode。");
            scaler = components.Single(value => value.GetType().FullName == "UnityEngine.UI.CanvasScaler");
            Object inputModule = components.Single(value => value.GetType().FullName == "UnityEngine.InputSystem.UI.InputSystemUIInputModule");
            if ((bool)Field(inputModule.GetType(), "m_ActionsHooked").GetValue(inputModule) || Field(inputModule.GetType(), "m_OnControlsChangedDelegate").GetValue(inputModule) != null || Field(typeof(NativeComponent), "m_NativeManager").GetValue(components.Single(value => value is NativeComponent)) != null) throw new InvalidOperationException("Native preview进入了未准入的InputModule/Native业务初始化。");
            LiveFrame during = CaptureEnvironment(); RequireEnvironment(before, during, true); RequireCanvasSubscribers(true);
            phase = "opened"; stage.environment = Describe(during);
            stage.objects = components.Select(value => UnityEngine.EntityId.ToULong(value.GetEntityId()) + "|" + value.GetType().FullName + "|enabled=" + value.enabled + "|active=" + value.gameObject.activeInHierarchy + "|runInEditMode=" + value.runInEditMode).ToArray();
            stage.canvas = CaptureCanvas();
        }

        /// <summary>只存原表达式给出的值，严格保留enabled→active→config→install的求值标志。</summary>
        public void CandidateRead(int index, NativeComponent component, bool enabled, bool activeEvaluated, bool active, bool configEvaluated, bool configNonNull, bool installEvaluated, bool installEnabled)
        {
            RequireCurrent();
            candidates.Add(new Candidate { ordinal = ++ordinal, entryIndex = index, nativeId = component == null ? 0 : UnityEngine.EntityId.ToULong(component.GetEntityId()), enabled = enabled, activeEvaluated = activeEvaluated, active = active, configEvaluated = configEvaluated, configNonNull = configNonNull, installEvaluated = installEvaluated, installEnabled = installEnabled });
            if (phase != "opened" || index != entryIndex || component == null || component.gameObject.scene != currentScene || activeEvaluated != enabled || configEvaluated != (activeEvaluated && active) || installEvaluated != (configEvaluated && configNonNull) || !activeEvaluated && active || !configEvaluated && configNonNull || !installEvaluated && installEnabled || candidates.Count != 1 || !enabled || !active || !configNonNull) throw new InvalidOperationException("Native候选不是原短路求值或当前唯一激活Native实例。");
        }

        /// <summary>原扫描实际结束；当前源预测为false，实际不同则保留真实候选后失败。</summary>
        public void SceneScanCompleted(int index, Scene scene, bool matched)
        {
            RequireCurrent();
            if (phase != "opened" || index != entryIndex || scene != currentScene || candidates.Count != 1 || candidates[0].installEnabled != matched) throw new InvalidOperationException("Native原扫描结束缺准确候选/结果。");
            phase = "scanned"; Stage stage = Add(phase, index, scene); stage.result = matched;
            if (matched != plan.predictedAppInstaller) throw new InvalidOperationException("Native实际AppInstaller结果不同于已审源预测。");
        }

        /// <summary>即使原try或本观察失败也不关闭场景；关闭仍由原finally恰一次执行。</summary>
        public void BeforeClose(int index, Scene scene)
        {
            RequireCurrent(true);
            if (before == null || index != entryIndex || scene != currentScene || roots == null) throw new InvalidOperationException("Native BeforeClose缺原成功Opened身份。");
            Stage stage = Add("before-close", index, scene); LiveFrame during = CaptureEnvironment(); stage.environment = Describe(during); stage.canvas = CaptureCanvas(); phase = "before-close";
            token.ThrowIfCancellationRequested(); RequireEnvironment(before, during, true); RequireCanvasSubscribers(true);
        }

        /// <summary>只有原Close真正返回才收录；返回false、对象残留或外部状态漂移均保留partial。</summary>
        public void Closed(int index, Scene scene, bool closeResult)
        {
            RequireCurrent(true); Stage stage = Add("closed", index, scene); stage.closeReturned = true; stage.result = closeResult;
            if (index != entryIndex || scene != currentScene || !closeResult || before == null || roots == null || roots.Any(root => root != null) || scene.isLoaded) throw new InvalidOperationException("Native原Close未成功或preview对象残留。");
            LiveFrame after = CaptureEnvironment(); stage.environment = Describe(after); RequireEnvironment(before, after, false); RequireCanvasSubscribers(false);
            if (!C.CaptureSources(CancellationToken.None).SequenceEqual(plan.sourcePins, StringComparer.Ordinal)) throw new InvalidOperationException("Native preview改写了源闭包。");
            phase = "closed"; roots = null; scaler = null;
        }

        /// <summary>原方法真正返回；只有完整一对open/close与实际false才能提交本合同。</summary>
        public void QueryCompleted(bool actual)
        {
            RequireCurrent(); result = actual; queryReturned = true; Add("query-returned", -1, default).result = actual;
            if (phase != "closed" || actual != plan.predictedAppInstaller || failures.Count != 0 || sceneEvents.Any(value => !value.EndsWith("|Additive", StringComparison.Ordinal)) || candidates.Count != 1 || candidates[0].installEnabled != actual) throw new InvalidOperationException("Native原查询历史/自然事件模式/结果不完整。");
            phase = "returned"; complete = true;
        }

        /// <summary>允许同一真实错误自然从inner传播到outer query；只加失败材料，绝不补成功阶段。</summary>
        public void Failed(string stage, int index, Exception error)
        { RequireCurrent(true); complete = false; failures.Add((++ordinal) + "|" + stage + "|" + index + "|" + (error?.GetType().FullName ?? "null")); }

        /// <summary>观察原API自然触发的事件模式；不调用原监听、排delayCall或伪造Opened。</summary>
        private void ObserveSceneOpened(Scene scene, OpenSceneMode mode)
        {
            RequireCurrent(true); sceneEvents.Add(Handle(scene) + "|" + scene.path + "|" + mode);
            if (mode != OpenSceneMode.Additive) throw new InvalidOperationException("Native preview自然sceneOpened不是已审Additive早退。");
        }

        /// <summary>只复制已经实际取得的材料，不重新open或补捕旧阶段。</summary>
        internal Evidence CaptureEvidence()
        {
            if (Thread.CurrentThread.ManagedThreadId != owner) throw new InvalidOperationException("Native历史只能在原线程读取。");
            return new Evidence { stages = stages.ToArray(), candidates = candidates.ToArray(), failures = failures.ToArray(), sceneEvents = sceneEvents.ToArray(), queryStarted = queryStarted, queryReturned = queryReturned, result = result, complete = complete && failures.Count == 0, disposed = disposed };
        }

        /// <summary>正常原链返回后要求完整历史；并不代表其它SDK或Build成功。</summary>
        internal Evidence CompleteObservation() { RequireCurrent(); if (!complete || failures.Count != 0) throw new InvalidOperationException("Native原预览真实观察未完成。"); return CaptureEvidence(); }

        /// <summary>只解除自身事件与租约，不另关场景、恢复全局集合或清除Input缓存。</summary>
        public void Dispose()
        {
            if (Thread.CurrentThread.ManagedThreadId != owner) throw new InvalidOperationException("Native观察只能在原线程释放。");
            if (disposed) return;
            if (eventRegistered) { EditorSceneManager.sceneOpened -= ObserveSceneOpened; eventRegistered = false; }
            if (lease != null) { lease.Dispose(); lease = null; }
            disposed = true; processor = null; context = null; report = null; roots = null; scaler = null; before = null;
        }

        /// <summary>保存原源码位置的真实Scene标量；失败时不推断尚未取得的scene。</summary>
        private Stage Add(string name, int index, Scene scene)
        {
            bool hasScene = name == "opened" || name == "scanned" || name == "before-close" || name == "closed";
            var value = new Stage { ordinal = ++ordinal, phase = name, entryIndex = index, path = scene.IsValid() ? scene.path : null, sceneHandle = hasScene ? Handle(scene) : null, preview = scene.IsValid() && EditorSceneManager.IsPreviewScene(scene), previewCount = EditorSceneManager.previewSceneCount };
            stages.Add(value); return value;
        }
        /// <summary>当前引擎64位Scene句柄使用十进制字符串，不假定为int。</summary>
        private static string Handle(Scene scene) => scene.handle.GetRawData().ToString(CultureInfo.InvariantCulture);
        /// <summary>读取原before记录的preview计数，不用较晚current代替。</summary>
        private static int PreviewCount(LiveFrame frame) => int.Parse(frame.rows.Single(value => value.StartsWith("previewCount=", StringComparison.Ordinal)).Substring("previewCount=".Length), CultureInfo.InvariantCulture);

        /// <summary>完整保存当时已取得的InputAsset/map摘要和raw标量，不用较晚current补旧环境。</summary>
        private static string[] Describe(LiveFrame frame) => frame.rows.Concat(new[] { "input.needResolve=" + (frame.needResolve.HasValue ? frame.needResolve.Value.ToString() : "not-read-no-loaded-assets") }).Concat(frame.inputs.SelectMany(value => new[] { "input.asset=" + UnityEngine.EntityId.ToULong(value.asset.GetEntityId()) + "|" + value.path + "|json=" + value.json + "|dirty=" + value.dirty + "|freshMaps=" + value.freshMaps }.Concat(value.rows.Select(row => "input.raw=" + row)))).ToArray();

        /// <summary>只读本preview Canvas局部缩放/PPU和原scaler缓存，不给其它Canvas写许可。</summary>
        private string[] CaptureCanvas()
        {
            if (scaler == null) return Array.Empty<string>(); Canvas canvas = scaler.GetComponent<Canvas>();
            if (canvas == null || canvas.gameObject.scene != currentScene) throw new InvalidOperationException("Native scaler缺本preview Canvas。");
            return new[] { "canvas=" + UnityEngine.EntityId.ToULong(canvas.GetEntityId()), "scale=" + canvas.scaleFactor.ToString("R", CultureInfo.InvariantCulture), "ppu=" + canvas.referencePixelsPerUnit.ToString("R", CultureInfo.InvariantCulture), "prevScale=" + Field(scaler.GetType(), "m_PrevScaleFactor").GetValue(scaler), "prevPpu=" + Field(scaler.GetType(), "m_PrevReferencePixelsPerUnit").GetValue(scaler) };
        }

        /// <summary>原Canvas订阅全保留，仅允许本preview scaler的准确单个方法短暂追加。</summary>
        private void RequireCanvasSubscribers(bool opened)
        {
            Delegate[] actual = Delegates(typeof(Canvas), "preWillRenderCanvases");
            Delegate[] extra = actual.Where(value => !ReferenceEquals(scaler, null) && ReferenceEquals(value.Target, scaler)).ToArray();
            if (opened ? extra.Length != 1 || extra[0].Method.Name != "Canvas_preWillRenderCanvases" || extra[0].Method.DeclaringType != scaler.GetType() : extra.Length != 0) throw new InvalidOperationException("Native preview Canvas订阅未正确注册/移除。");
            string[] preserved = actual.Where(value => ReferenceEquals(scaler, null) || !ReferenceEquals(value.Target, scaler)).Select(C.DelegateIdentity).ToArray();
            string[] expected = before.rows.Where(value => value.StartsWith("canvasDelegate=", StringComparison.Ordinal)).Select(value => value.Substring("canvasDelegate=".Length)).ToArray();
            if (!preserved.SequenceEqual(expected, StringComparer.Ordinal)) throw new InvalidOperationException("Native preview改动了原Canvas订阅顺序/目标。");
        }

        /// <summary>固定字段反射仅读取backing，不调用任意getter；缺字段即阻断。</summary>
        private static FieldInfo Field(Type type, string name) => type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance) ?? throw new InvalidOperationException("Native已审raw字段缺失：" + type.FullName + "." + name);
        /// <summary>只读实际事件/字段的原invocation list，不回放或改写。</summary>
        private static Delegate[] Delegates(Type type, string name) => ((Delegate)Field(type, name).GetValue(null))?.GetInvocationList() ?? Array.Empty<Delegate>();

        /// <summary>读取本同步窗口普通场景、Selection、原delegate及准确UI/Framework/Input backing；不查询Manager属性。</summary>
        private LiveFrame CaptureEnvironment()
        {
            var rows = new List<string>(); var references = new List<object>();
            rows.Add("previewCount=" + EditorSceneManager.previewSceneCount); rows.Add("active=" + Handle(SceneManager.GetActiveScene()));
            for (int index = 0; index < SceneManager.sceneCount; index++)
            {
                Scene scene = SceneManager.GetSceneAt(index); if (!EditorSceneManager.IsPreviewScene(scene)) rows.Add("scene=" + Handle(scene) + "|" + scene.path + "|" + scene.isLoaded + "|" + scene.isDirty);
            }
            rows.Add("selection=" + string.Join(",", Selection.entityIds.Select(UnityEngine.EntityId.ToULong)));
            if (PrefabStageUtility.GetCurrentPrefabStage() != null) throw new InvalidOperationException("Native preview未审Prefab Stage窗口。");
            foreach (Delegate value in Delegates(typeof(Canvas), "preWillRenderCanvases").Where(value => scaler == null || !ReferenceEquals(value.Target, scaler))) { rows.Add("canvasDelegate=" + C.DelegateIdentity(value)); Ref(value.Target, references); }
            foreach (Delegate value in C.SceneOpenedDelegates(this)) { rows.Add("sceneDelegate=" + C.DelegateIdentity(value)); Ref(value.Target, references); }
            foreach (Delegate value in Delegates(typeof(EditorApplication), "delayCall")) { rows.Add("delay=" + C.DelegateIdentity(value)); Ref(value.Target, references); }
            CaptureCollection("components", Field(typeof(FrameworkComponentsGroup), "s_FrameworkComponents").GetValue(null), true, rows, references);
            CaptureCollection("component-cache", Field(typeof(FrameworkComponentsGroup), "s_ComponentCache").GetValue(null), false, rows, references);
            CaptureCollection("managers", Field(typeof(FrameworkManagersGroup), "s_FrameworkManagers").GetValue(null), true, rows, references);
            CaptureCollection("manager-cache", Field(typeof(FrameworkManagersGroup), "s_ManagerCache").GetValue(null), false, rows, references);
            Type eventSystem = C.ResolveType("UnityEngine.EventSystems.EventSystem"), raycasters = C.ResolveType("UnityEngine.EventSystems.RaycasterManager"), module = C.ResolveType("UnityEngine.InputSystem.UI.InputSystemUIInputModule");
            CaptureCollection("event-systems", Field(eventSystem, "m_EventSystems").GetValue(null), false, rows, references);
            CaptureCollection("raycasters", Field(raycasters, "s_Raycasters").GetValue(null), false, rows, references);
            CaptureCollection("input-refcounts", Field(module, "s_InputActionReferenceCounts").GetValue(null), false, rows, references);
            foreach (Object value in Resources.FindObjectsOfTypeAll(module))
            {
                if (value is Component component && EditorSceneManager.IsPreviewScene(component.gameObject.scene) && component.gameObject.scene == currentScene) continue;
                rows.Add("input-module=" + Ref(value, references) + "|hooked=" + Field(module, "m_ActionsHooked").GetValue(value) + "|controls=" + Ref(Field(module, "m_OnControlsChangedDelegate").GetValue(value), references));
            }
            AssetFrame[] assets = CaptureInputAssets();
            bool? resolve = assets.Length == 0 ? (bool?)null : (bool)Field(C.ResolveType("UnityEngine.InputSystem.InputActionMap"), "s_NeedToResolveBindings").GetValue(null);
            return new LiveFrame { rows = rows.ToArray(), references = references.ToArray(), inputs = assets, needResolve = resolve };
        }

        /// <summary>只枚举准确BCL容器；NovaLinkedList仅取已审m_LinkedList，不执行其枚举器或业务虚方法。</summary>
        private static void CaptureCollection(string role, object value, bool linked, List<string> rows, List<object> references)
        {
            rows.Add(role + "-container=" + Ref(value, references)); if (value == null) throw new InvalidOperationException("Native原集合缺失。");
            if (linked) { value = Field(value.GetType(), "m_LinkedList").GetValue(value); rows.Add(role + "-bcl=" + Ref(value, references)); }
            Type definition = value.GetType().IsGenericType ? value.GetType().GetGenericTypeDefinition() : null;
            if (definition != typeof(List<>) && definition != typeof(LinkedList<>) && definition != typeof(Dictionary<,>)) throw new InvalidOperationException("Native raw集合不是已审BCL类型。");
            int count = 0;
            if (value is IDictionary dictionary)
                foreach (DictionaryEntry entry in dictionary)
                {
                    if (++count > 4096) throw new InvalidOperationException("Native raw集合超预算。");
                    string item = entry.Value?.GetType().IsValueType == true ? "refCount=" + Field(entry.Value.GetType(), "refCount").GetValue(entry.Value) + ",enabledByInputModule=" + Field(entry.Value.GetType(), "enabledByInputModule").GetValue(entry.Value) : Ref(entry.Value, references);
                    rows.Add(role + "=" + Ref(entry.Key, references) + "|" + item);
                }
            else foreach (object entry in (IEnumerable)value) { if (++count > 4096) throw new InvalidOperationException("Native raw集合超预算。"); rows.Add(role + "=" + Ref(entry, references)); }
        }

        /// <summary>只读已加载准确InputActionAsset和各map固定raw字段；不Load、Enable、Resolve或读InputActionReference.action。</summary>
        private static AssetFrame[] CaptureInputAssets()
        {
            Type assetType = C.ResolveType("UnityEngine.InputSystem.InputActionAsset");
            return Resources.FindObjectsOfTypeAll(assetType).OrderBy(value => UnityEngine.EntityId.ToULong(value.GetEntityId())).Select(value =>
            {
                if (value.GetType() != assetType) throw new InvalidOperationException("Native InputActionAsset存在未审派生类型。");
                var rows = new List<string>(); var references = new List<object>(); bool fresh = true;
                object shared = Field(assetType, "m_SharedStateForAllMaps").GetValue(value); rows.Add("shared=" + Ref(shared, references)); if (shared != null) fresh = false;
                var maps = (Array)Field(assetType, "m_ActionMaps").GetValue(value); rows.Add("maps=" + Ref(maps, references));
                if (maps?.Length > 256) throw new InvalidOperationException("Native已加载Input maps超预算。");
                if (maps != null) foreach (object map in maps)
                {
                    if (map == null) throw new InvalidOperationException("Native Input map缺失。");
                    Type type = map.GetType(); rows.Add("map=" + Ref(map, references));
                    foreach (string name in new[] { "m_State", "m_MapIndexInState", "m_EnabledActionsCount", "m_Flags", "m_BindingsForEachAction", "m_ActionIndexByNameOrId", "m_ControlsForEachAction" })
                    {
                        object field = Field(type, name).GetValue(map); rows.Add(name + "=" + (field == null || field.GetType().IsValueType ? field?.ToString() ?? "null" : Ref(field, references)));
                        if (name == "m_State" && field != null || name == "m_MapIndexInState" && (int)field != -1 || name == "m_EnabledActionsCount" && (int)field != 0 || (name == "m_BindingsForEachAction" || name == "m_ActionIndexByNameOrId" || name == "m_ControlsForEachAction") && field != null || name == "m_Flags" && (Convert.ToInt32(field, CultureInfo.InvariantCulture) & 12) != 0) fresh = false;
                    }
                    var actions = (Array)Field(type, "m_Actions").GetValue(map); rows.Add("actions=" + Ref(actions, references));
                    if (actions?.Length > 4096) throw new InvalidOperationException("Native已加载Input actions超预算。");
                    if (actions != null) foreach (object action in actions) { rows.Add("action=" + Ref(action, references)); if (!ReferenceEquals(Field(action.GetType(), "m_ActionMap").GetValue(action), map)) fresh = false; }
                }
                bool dirty = EditorUtility.IsDirty(value); string body = EditorJsonUtility.ToJson(value);
                if (System.Text.Encoding.UTF8.GetByteCount(body) > 2 * 1024 * 1024) throw new InvalidOperationException("Native已加载InputAsset序列化超2MiB。");
                string json = GenerateActionCommon.ComputeTextHash(body);
                if (EditorUtility.IsDirty(value) != dirty) throw new InvalidOperationException("Native InputAsset序列化捕获改变dirty。");
                return new AssetFrame { asset = value, path = AssetDatabase.GetAssetPath(value), json = json, dirty = dirty, freshMaps = fresh, rows = rows.ToArray(), references = references.ToArray() };
            }).ToArray();
        }

        /// <summary>序列化只记诊断身份；真正同域校验同时保留原引用，不调用业务GetHashCode。</summary>
        private static string Ref(object value, List<object> references)
        {
            if (ReferenceEquals(value, null)) return "null"; references.Add(value);
            return value.GetType().FullName + "@" + RuntimeHelpers.GetHashCode(value).ToString(CultureInfo.InvariantCulture) + (value is Object native && native != null ? "#" + UnityEngine.EntityId.ToULong(native.GetEntityId()) : "");
        }

        /// <summary>普通环境原引用/行必须相等；只允许该预览多一个preview计数和准确默认InputAsset首次加载。</summary>
        private static void RequireEnvironment(LiveFrame expected, LiveFrame actual, bool opened)
        {
            string[] left = expected.rows.Where(value => !value.StartsWith("previewCount=", StringComparison.Ordinal)).ToArray(), right = actual.rows.Where(value => !value.StartsWith("previewCount=", StringComparison.Ordinal)).ToArray();
            if (!left.SequenceEqual(right, StringComparer.Ordinal) || expected.references.Length != actual.references.Length || expected.references.Where((value, index) => !ReferenceEquals(value, actual.references[index])).Any() || PreviewCount(actual) != PreviewCount(expected) + (opened ? 1 : 0)) throw new InvalidOperationException("Native preview改动普通场景/Selection/原UI/runtime集合/延迟队列。");
            foreach (AssetFrame beforeAsset in expected.inputs)
            {
                AssetFrame after = actual.inputs.SingleOrDefault(value => ReferenceEquals(value.asset, beforeAsset.asset));
                if (after == null || after.path != beforeAsset.path || after.json != beforeAsset.json || after.dirty != beforeAsset.dirty || !after.rows.SequenceEqual(beforeAsset.rows, StringComparer.Ordinal) || after.references.Length != beforeAsset.references.Length || beforeAsset.references.Where((value, index) => !ReferenceEquals(value, after.references[index])).Any()) throw new InvalidOperationException("Native preview改动已加载InputAsset/map原状态。");
            }
            AssetFrame[] added = actual.inputs.Where(value => expected.inputs.All(beforeAsset => !ReferenceEquals(beforeAsset.asset, value.asset))).ToArray();
            string expectedPath = AssetDatabase.GUIDToAssetPath("ca9f5fa95ffab41fb9a615ab714db018");
            if (added.Length > 1 || added.Any(value => value.path != expectedPath || value.dirty || !value.freshMaps) || added.Length == 0 && expected.needResolve != actual.needResolve || added.Length == 1 && actual.needResolve != true) throw new InvalidOperationException("Native preview新增/改变了未审InputAsset或resolve状态。");
        }
    }
}
