/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AndroidResolveRefreshContract.cs
 * author:    taoye
 * created:   2026/10/16
 * descrip:   Android 正规解析的保守资源刷新合同与只读回执校验
 ***************************************************************/
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Path = System.IO.Path;
using NovaFramework.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NovaFramework.Editor
{
    /// <summary>
    /// 绑定完整实际派发目标、模板精确输入与已证明的自然支路/派生缓存效应。
    /// 未审类型、订阅、队列或身份漂移具体阻断；不改 vendor、偏好、事件或队列来满足门禁。
    /// </summary>
    internal static class AndroidResolveRefreshContract
    {
        /// <summary>冻结域、回调、精确允许路径和非目标状态；不保存 vendor 实例或私有配置正文。</summary>
        [Serializable]
        internal sealed class Snapshot
        {
            public string domain;
            public string callbackSha256;
            public string schedulerSha256;
            public string naturalBranchesSha256;
            public string protectedFilesSha256;
            public string editorObjectsSha256;
            public string[] editorObjectRows;
            public AndroidResolveUirObjectContract.Snapshot uir;
            public AndroidResolveVendorRefreshContracts.Inputs vendorInputs;
            public string vendorSha256;
            public string speedTreeSha256;
            public AndroidResolveSearchCacheContract.Snapshot search;
            public AndroidVersionHandlerReadOnlyContract.Snapshot version;
            public AndroidResolvePeripheralRefreshContracts.StableSnapshot peripheral;
            public string[] graphUiHandlerKeys;
            public AndroidResolveCodeEditorTailContract.Snapshot codeEditor;
            public string[] allowedPaths;
            public string sha256;
        }

        /// <summary>正常解析窗口的观察结果；域变化、失败或不完整记录均不能证明执行完成。</summary>
        [Serializable]
        internal sealed class Receipt
        {
            public string domain;
            public string snapshotSha256;
            public bool completed;
            public int dispatchCount;
            public string dispatchSha256;
            public string failure;
            public long startedUtcTicks;
            public long closedUtcTicks;
            public string[] expectedTransactions;
            public AndroidResolveSearchCacheContract.Snapshot searchAfter;
            public AndroidVersionHandlerReadOnlyContract.Snapshot versionAfter;
            public string[] graphUiHandlerKeysAfter;
            public AndroidResolveUirObjectContract.Snapshot uirAfter;
            public string sha256;
        }

        /// <summary>精确派生资源及有界临时文件只属于缓存维护；未声明数据库 Task 退出或全 Library 通配写入。</summary>
        internal static readonly string[] DeclaredWriteSet =
        {
            "Unity AssetDatabase：仅本轮精确资产路径对应的 Library 导入产物与源资产登记（不授予其他 Assets 写权限）",
            "Library/Search/propertyDatabase.db", "Library/Search/propertyDatabase.db.st",
            "Library/Search/propertyAliases.db", "Library/Search/propertyAliases.db.st", "Library/Search/transactions.db（仅实际四数组对应的28字节有序事务追加）",
            "Library/Search/propertyDatabase.db_<32位N格式GUID>_temp（引擎空缓存merge临时文件，随后删除）",
            "Library/Search/propertyAliases.db_<32位N格式GUID>_temp（引擎空缓存merge临时文件，随后删除）",
            "Unity 派生依赖登记前缀 SpeedTree9Importer_MaterialSettings（当前无属性方法/输入或现有artifact依赖）",
            "Unity Editor Analytics:assetPostProcessorsUsage v2（既有enabled状态下的importActionId、methodName、duration_sec、invocationCount；不证明外部送达）",
            "已审固定内存/UI派生缓存：Version Handler空可写集通知、Firebase assetsChanged、Facebook currentAsset、Sprite工厂、Grid空单例、Graph固定Console处理器、AssetEvents breadcrumb、UPM timestamp[64]",
            "Unity UI Toolkit：仅冻结面板实际持有的默认材质、默认浮点Shader信息纹理、Shader信息存储纹理及动态Atlas页（含该页直接持有的复制材质）的正常重建；不允许项目资产、输入纹理或其它对象变化",
        };

        /// <summary>将已捕获的精确同值偏好效果加入确认写集；cache-only 项不扩展为持久偏好写权限。</summary>
        /// <param name="snapshot">完整已核刷新计划快照。</param>
        /// <returns>固定引擎派生资源加本轮实际可能同值写入的精确键效果。</returns>
        internal static string[] GetDeclaredWriteSet(Snapshot snapshot)
        {
            if (snapshot?.peripheral?.ClipboardPreferenceEffects == null) throw new InvalidOperationException("缺完整偏好效果快照。");
            return DeclaredWriteSet.Concat(snapshot.peripheral.ClipboardPreferenceEffects
                .Where(effect => effect.Contains("|idempotent-SetString|"))
                .Select(effect => "Unity EditorPrefs:" + effect))
                .Concat((snapshot.peripheral.LightingExpectedEffects ?? Array.Empty<string>()).Select(effect => "Unity Editor UI派生状态:" + effect))
                .Concat(new[] { "Unity Handles.SetSceneViewModeGIContributorsReceiversColors 固定三色 setter（仅已审 Lighting 消费者自然通知分支；无回读 API，不宣称原生值回读）" }).ToArray();
        }

        private static string s_Domain;
        private static int s_MainThread;
        private static ExecutionScope s_ActiveScope;

        /// <summary>由 Editor 初始化阶段确定主线程与域，调用请求不能自行把 worker 认作主线程。</summary>
        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            s_MainThread = Thread.CurrentThread.ManagedThreadId;
            s_Domain = Guid.NewGuid().ToString("N");
        }

        /// <summary>在字段读取前核正常初始化阶段和主线程；不创建 update 消费者、不触发目标 cctor。</summary>
        /// <param name="owner">同一已核 Unity 编译模块中的精确迁移 owner。</param>
        /// <param name="provenHostDomain">仅已冻结且回调摘要相等的同域重核可以复用原见证。</param>
        /// <returns>当前宿主上下文可接纳时为空，否则具体阻断。</returns>
        internal static string CheckLegacyMigrationHost(Type owner, string provenHostDomain)
        {
            if (Thread.CurrentThread.ManagedThreadId != s_MainThread || string.IsNullOrEmpty(s_Domain) ||
                EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
                return "旧配置迁移字段只可在当前域正常主线程稳定编辑态读取。";
            var frames = new System.Diagnostics.StackTrace(false).GetFrames();
            var methods = frames?.Select(frame => frame.GetMethod()).ToArray();
            bool hostProof = !string.IsNullOrEmpty(provenHostDomain) && provenHostDomain == s_Domain;
            if (methods != null)
                foreach (var method in methods)
                    if ((method?.Name == "<HandlePlanStart>b__0" || method?.Name == "<HandleVerifyStart>b__0") &&
                        method.DeclaringType?.DeclaringType?.FullName == "NovaFramework.Mcp.Editor.NovaProjectActionGateway")
                    {
                        string dispatchError = CheckLegacyActionUpdateFrame(method);
                        if (dispatchError != null) return dispatchError;
                        hostProof = true;
                    }
            return CheckLegacyMigrationCallStack(owner, methods, hostProof);
        }

        /// <summary>只认可现有 PlanJobs/VerifyJobs 在真实 update 上解绑再分发的精确闭包；不把通用同步上下文或任意同名方法当见证。</summary>
        /// <param name="method">当前真实管理栈中的候选闭包方法，不调用该方法。</param>
        /// <returns>固定源码、模块和 Unity 编译归属均吻合时为空；否则具体阻断。</returns>
        private static string CheckLegacyActionUpdateFrame(System.Reflection.MethodBase method)
        {
            const string name = "NovaFramework.Mcp.Editor";
            var assembly = method.DeclaringType.Assembly;
            if (assembly.GetName().Name != name || method.IsStatic || method.GetParameters().Length != 0 ||
                !(method is MethodInfo info) || info.ReturnType != typeof(void) ||
                !CustomAttributeData.GetCustomAttributes(method.DeclaringType).Any(item =>
                    item.AttributeType == typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute)) ||
                assembly.ManifestModule.ModuleVersionId.ToString() != "0c769c7e-f120-4b03-8207-2f4fa9e20d11" ||
                GenerateActionCommon.ComputeFileHash(assembly.Location, CancellationToken.None) != "8e04ff323a0e9cb1eac26d3203646e2f0f6a40f7bab3edc46505a51009e4f2a1")
                return "当前 Action update 闭包不属于已审完整编译模块。";
            var package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(assembly);
            if (package?.name != "com.solotopia.nova.framework.mcp") return "当前 Action update 闭包缺真实 MCP 包归属。";
            string root = GenerateActionCommon.ProjectRoot;
            Func<string, string> physical = value => Path.GetFullPath(value.StartsWith(package.assetPath + "/", StringComparison.Ordinal)
                ? Path.Combine(package.resolvedPath, value.Substring(package.assetPath.Length + 1))
                : Path.IsPathRooted(value) ? value : Path.Combine(root, value));
            bool plan = method.Name == "<HandlePlanStart>b__0";
            if (!plan && method.Name != "<HandleVerifyStart>b__0") return "未知 Action update 闭包。";
            string source = Path.GetFullPath(Path.Combine(package.resolvedPath, plan
                ? "Nova/Editor/NovaProjectActionGateway.PlanJobs.cs" : "Nova/Editor/NovaProjectActionGateway.VerifyJobs.cs"));
            string sourceHash = plan ? "f53f30162cf17f87537a530bf9a72eb5298e289a81dd5c0a12c09ee6cdb690da"
                : "cf4ff300864d45933ab645e8b9a9dbfc519fd747fbfab3d6bd15f701ea58f42a";
            var owners = UnityEditor.Compilation.CompilationPipeline.GetAssemblies(UnityEditor.Compilation.AssembliesType.Editor)
                .Where(item => item.name == name).ToArray();
            if (owners.Length != 1 || owners[0].sourceFiles == null ||
                physical(owners[0].outputPath) != Path.GetFullPath(assembly.Location) ||
                !owners[0].sourceFiles.Any(value => physical(value) == source) ||
                GenerateActionCommon.ComputeFileHash(source, CancellationToken.None) != sourceHash)
                return "当前 Action update 闭包的实际源码或 Unity 编译所有者不匹配。";
            return null;
        }

        /// <summary>纯元数据判断：拒绝初始化/迁移重入；新 Plan 需真实 Unity update 或已审 Action update 闭包栈，重核只继承同域原见证。</summary>
        /// <param name="owner">精确迁移 owner；不读取其静态字段。</param>
        /// <param name="methods">当前主线程真实管理栈的方法身份；不得以预造数组作为生产输入。</param>
        /// <param name="provenDomain">调用方已核对同域/相同回调摘要，或当前精确 Action update 闭包已经完整验证时为真。</param>
        /// <returns>可接纳时为空；缺见证、未知帧或嵌套目标均具体阻断。</returns>
        internal static string CheckLegacyMigrationCallStack(Type owner, System.Reflection.MethodBase[] methods, bool provenDomain)
        {
            if (owner == null || methods == null || methods.Length == 0 || methods.Any(method => method?.DeclaringType == null))
                return "旧配置迁移缺完整可判的当前调用栈。";
            bool update = false;
            foreach (var method in methods)
            {
                Type declaring = method.DeclaringType;
                if (declaring == owner || declaring.Assembly == typeof(EditorApplication).Assembly &&
                    declaring.FullName == "UnityEditor.EditorAssemblies" && method.Name.StartsWith("ProcessInitializeOnLoad", StringComparison.Ordinal))
                    return "当前仍嵌套 Unity 初始化或旧配置迁移；不读取 queued、不推进该任务。";
                if (declaring == typeof(EditorApplication) && method.Name == "Internal_CallUpdateFunctions" &&
                    method.IsStatic && method.GetParameters().Length == 0 && method is System.Reflection.MethodInfo info && info.ReturnType == typeof(void))
                    update = true;
            }
            return update || provenDomain ? null : "缺少真实正常 Editor.update 见证；不以字段读取触发初始化。";
        }

        /// <summary>稳定编辑态仅捕获真实派发目标身份和现有 EDM 调度基线；完整自然支路待精确模板域确定后捕获。</summary>
        /// <param name="token">本次只读捕获或遍历的取消令牌；不取消厂商或引擎工作。</param>
        /// <param name="callbackHash">成功时实际派发身份的完整稳定摘要。</param>
        /// <param name="schedulerHash">成功时已审 EDM 自然调度基线摘要。</param>
        /// <param name="error">失败时的精确原因；成功时为 null。</param>
        /// <returns>满足上述精确条件时为 true，否则为 false；不会为通过检查而改变环境。</returns>
        internal static bool TryCapturePreflight(CancellationToken token, out string callbackHash,
            out string schedulerHash, out string error)
        {
            callbackHash = null;
            schedulerHash = null;
            error = null;
            if (Thread.CurrentThread.ManagedThreadId != s_MainThread || string.IsNullOrEmpty(s_Domain) ||
                EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
            { error = "资源导入/刷新回调合同要求 Editor 主线程和稳定编辑态，当前忙碌、未初始化或来自 worker。"; return false; }
            try
            {
                if (!AndroidResolveRefreshCallbacks.TryCaptureIdentities(token, out callbackHash, out error) ||
                    !AndroidResolveActionCommon.TryCaptureSceneScheduler(token,
                        out AndroidResolveActionCommon.SettingEntry[] scheduler, out error)) return false;
                schedulerHash = GenerateActionCommon.ComputeTextHash(Util.Json.Serialize(scheduler));
                return true;
            }
            catch (Exception exception) when (!(exception is OperationCanceledException))
            { error = "资源导入/刷新回调身份只读捕获失败：" + exception.GetType().Name; return false; }
        }

        /// <summary>正规模板输入冻结后建立精确路径与非目标摘要；不初始化 vendor、不创建备份或目录。</summary>
        /// <param name="plan">已冻结的正规本地模板解析计划。</param>
        /// <param name="token">本次只读捕获或遍历的取消令牌；不取消厂商或引擎工作。</param>
        /// <param name="snapshot">同域完整刷新合同快照。</param>
        /// <param name="error">失败时的精确原因；成功时为 null。</param>
        /// <returns>满足上述精确条件时为 true，否则为 false；不会为通过检查而改变环境。</returns>
        internal static bool TryCapturePlan(AndroidResolveActionCommon.TemplateResolutionPlan plan, CancellationToken token,
            out Snapshot snapshot, out string error)
        {
            snapshot = null;
            error = null;
            if (plan?.copies == null || plan.deletionSet == null || plan.generatedDirectoryInputs == null)
            { error = "资源导入/刷新合同缺完整正规模板计划。"; return false; }
            if (!TryCapturePreflight(token, out string callbacks, out string scheduler, out error) ||
                !TryGetAllowedPaths(plan, out string[] paths, out error)) return false;
            AndroidResolveVendorRefreshContracts.Inputs inputs;
            try { inputs = AndroidResolveVendorRefreshContracts.CreateInputs(plan, paths); }
            catch (Exception exception) when (!(exception is OperationCanceledException))
            { error = "资源导入/刷新非目标保护捕获失败：" + exception.GetType().Name; return false; }
            return TryCaptureBoundPaths(paths, inputs, callbacks, scheduler, token, out snapshot, out error);
        }

        /// <summary>只为原AppsFlyer既存properties默认导入读取真实阶段基线，不伪造EDM模板计划或授予其它路径。</summary>
        /// <param name="token">当前取消。</param><param name="snapshot">原callback到达时的完整真实快照。</param><param name="error">未闭合的准确分支。</param><returns>所有既有有限子合同均满足时为true，不表示尾部已经完成。</returns>
        internal static bool TryCaptureAppsFlyerPropertiesStage(CancellationToken token, out Snapshot snapshot, out string error)
        {
            snapshot = null;
            if (!TryCapturePreflight(token, out string callbacks, out string scheduler, out error)) return false;
            string[] paths = { "Assets/Plugins/Android/gradleTemplate.properties" };
            var inputs = new AndroidResolveVendorRefreshContracts.Inputs { allPaths = paths, deletedPaths = Array.Empty<string>(), sourcePaths = Array.Empty<string>() };
            return TryCaptureBoundPaths(paths, inputs, callbacks, scheduler, token, out snapshot, out error);
        }

        /// <summary>复用完整有限导入合同，路径与角色只能由上方两个准确领域入口提供；不对外接受任意路径许可。</summary>
        private static bool TryCaptureBoundPaths(string[] paths, AndroidResolveVendorRefreshContracts.Inputs vendorInputs, string callbacks, string scheduler, CancellationToken token, out Snapshot snapshot, out string error)
        {
            snapshot = null;
            if (!AndroidResolveRefreshCallbacks.TryCaptureNaturalBranches(paths, token, out string naturalBranches, out error)) return false;
            try
            {
                if (!AndroidResolveVendorRefreshContracts.TryCapture(vendorInputs, token, out string vendor, out error) ||
                    !AndroidResolveSpeedTreeDependencyContract.TryCapture(paths, token, out string speedTree, out error) ||
                    !AndroidResolveSearchCacheContract.TryCapture(token, true, out var search, out error) ||
                    !AndroidVersionHandlerReadOnlyContract.TryCapture(token, out var version, out error) ||
                    !AndroidResolveCodeEditorTailContract.TryCapture(paths, token, out var codeEditor, out error)) return false;
                if (!version.naturallyComplete) { error = "Version Handler 尚有正常待处理工作，等待自然收尾后重新 Plan。"; return false; }
                if (!AndroidResolveUirObjectContract.TryCapture(token, out var uir, out error)) return false;
                string[] objects = CaptureEditorObjectRows(paths);
                snapshot = new Snapshot { domain = s_Domain, callbackSha256 = callbacks,
                    schedulerSha256 = scheduler, naturalBranchesSha256 = naturalBranches, allowedPaths = paths,
                    vendorInputs = vendorInputs, vendorSha256 = vendor, speedTreeSha256 = speedTree, search = search, version = version, codeEditor = codeEditor,
                    peripheral = AndroidResolveRefreshCallbacks.CapturePeripheralSnapshot(token),
                    graphUiHandlerKeys = AndroidResolvePeripheralRefreshContracts.CaptureGraphUiHandlerKeys(token),
                    protectedFilesSha256 = CaptureProtectedFiles(paths, token), editorObjectRows = objects, uir = uir,
                    editorObjectsSha256 = GenerateActionCommon.ComputeTextHash(string.Join("\n", objects)) };
                snapshot.sha256 = ComputeSnapshotHash(snapshot);
                // 只改变完整观察材料的有界表示；逻辑字段、原始行和摘要仍保持原值。
                if (!AndroidResolveReceiptCodec.TryEncode(Util.Json.Serialize(snapshot), out _, out string capacityError))
                { snapshot = null; error = "刷新冻结快照无法完整无损封装：" + capacityError; return false; }
                return true;
            }
            catch (Exception exception) when (!(exception is OperationCanceledException))
            { snapshot = null; error = "资源导入/刷新非目标保护捕获失败：" + exception.GetType().Name; return false; }
        }

        /// <summary>条件目录只转成单一路径，绝不把父目录变成递归通配写权限；外部进程文字不作路径。</summary>
        /// <param name="plan">已冻结的正规本地模板解析计划。</param>
        /// <param name="paths">本次冻结的精确路径集合，不授予递归写权限。</param>
        /// <param name="error">失败时的精确原因；成功时为 null。</param>
        /// <returns>满足上述精确条件时为 true，否则为 false；不会为通过检查而改变环境。</returns>
        internal static bool TryGetAllowedPaths(AndroidResolveActionCommon.TemplateResolutionPlan plan,
            out string[] paths, out string error)
        {
            paths = null;
            error = null;
            if (plan?.copies == null || plan.deletionSet == null || plan.generatedDirectoryInputs == null)
            { error = "刷新路径缺完整模板输入。"; return false; }
            var allowed = new SortedSet<string>(StringComparer.Ordinal)
            {
                AndroidResolveActionCommon.ResolutionStatePath,
                "Assets/Plugins/Android/mainTemplate.gradle", "Assets/Plugins/Android/settingsTemplate.gradle",
                "Assets/Plugins/Android/gradleTemplate.properties",
            };
            foreach (var copy in plan.copies)
            {
                if (copy == null || string.IsNullOrWhiteSpace(copy.target))
                { error = "刷新路径含空复制目标。"; return false; }
                foreach (string path in new[] { copy.target, Path.ChangeExtension(copy.target, ".pom") })
                { allowed.Add(path); allowed.Add(path + ".meta"); }
            }
            foreach (string raw in plan.deletionSet.Concat(plan.generatedDirectoryInputs.Select(item => item.name)))
            {
                if (raw == null) { error = "刷新路径含空项。"; return false; }
                string path = raw;
                if (path.EndsWith("/（仅删除后为空时移除目录）", StringComparison.Ordinal))
                    path = path.Substring(0, path.Length - "/（仅删除后为空时移除目录）".Length);
                else if (path.EndsWith(".meta（仅移除空目录时删除）", StringComparison.Ordinal))
                    path = path.Substring(0, path.Length - "（仅移除空目录时删除）".Length);
                allowed.Add(path);
            }
            foreach (string path in allowed)
            {
                if (path != AndroidResolveActionCommon.ResolutionStatePath &&
                    !path.StartsWith("Assets/GeneratedLocalRepo/", StringComparison.Ordinal) &&
                    !path.StartsWith("Assets/Plugins/Android/", StringComparison.Ordinal) &&
                    path != "Assets/GeneratedLocalRepo" && path != "Assets/GeneratedLocalRepo.meta" &&
                    path != "Assets/Plugins" && path != "Assets/Plugins.meta" &&
                    path != "Assets/Plugins/Android" && path != "Assets/Plugins/Android.meta")
                { error = "刷新路径越出固定模板边界：" + path; return false; }
                if (path.Contains("..") || path.Contains("\\") || path.Contains("*") || path.Contains("（") ||
                    !GenerateActionCommon.TryResolveProjectPath(path, "刷新精确路径", out _, out error))
                { error = "刷新路径不是已冻结精确路径：" + path; return false; }
            }
            paths = allowed.ToArray();
            return true;
        }

        /// <summary>完整 DTO 摘要不含自身；参数顺序保留已排序精确路径，不接收目录递归授权。</summary>
        /// <param name="snapshot">同域完整刷新合同快照。</param>
        /// <returns>绑定完整快照各项证据的 SHA。</returns>
        internal static string ComputeSnapshotHash(Snapshot snapshot)
        {
            if (snapshot == null) return null;
            string legacyHash = GenerateActionCommon.ComputeTextHash(Util.Json.Serialize(new
            { snapshot.domain, snapshot.callbackSha256, snapshot.schedulerSha256, snapshot.protectedFilesSha256,
                snapshot.editorObjectsSha256, snapshot.editorObjectRows, snapshot.naturalBranchesSha256, snapshot.allowedPaths,
                snapshot.vendorInputs, snapshot.vendorSha256, snapshot.speedTreeSha256, snapshot.search, snapshot.version,
                snapshot.peripheral, snapshot.graphUiHandlerKeys, snapshot.codeEditor }));
            // 无新字段的历史快照保持原摘要与原严格对象规则，不能事后补造引擎对象许可。
            return snapshot.uir == null ? legacyHash : GenerateActionCommon.ComputeTextHash(
                legacyHash + "\nUIR-v1\n" + Util.Json.Serialize(snapshot.uir));
        }

        /// <summary>写前逐项重核同域完整冻结状态；写后事务增加不能继承为一次新的执行授权。</summary>
        /// <param name="snapshot">同域完整刷新合同快照。</param>
        /// <param name="token">本次只读捕获或遍历的取消令牌；不取消厂商或引擎工作。</param>
        /// <param name="error">失败时的精确原因；成功时为 null。</param>
        /// <returns>满足上述精确条件时为 true，否则为 false；不会为通过检查而改变环境。</returns>
        internal static bool TryVerifySnapshot(Snapshot snapshot, CancellationToken token, out string error)
        {
            error = null;
            if (!IsSnapshotForDomain(snapshot, s_Domain, out error) ||
                !TryCaptureStableAfter(snapshot, token, false, true, out _, out _, out error)) return false;
            if (!AndroidResolveSearchCacheContract.TryCapture(token, true, out var search, out error) ||
                !AndroidResolveSearchCacheContract.SameBefore(snapshot.search, search))
            { error = error ?? "Search 事务输入在 Plan 后变化，须重新 Plan。"; return false; }
            return true;
        }

        /// <summary>复核稳定输入和非目标保护；只在执行后接纳明确声明的缓存/单例及 Graph 处理器物化。</summary>
        /// <param name="snapshot">同域完整刷新合同快照。</param>
        /// <param name="token">本次只读捕获或遍历的取消令牌；不取消厂商或引擎工作。</param>
        /// <param name="after">是否属于执行后阶段；写前不接受声明的自然物化。</param>
        /// <param name="inheritHostWitness">仅原 Plan State/执行观察器为真；Verify 的反序列化回执不继承宿主见证。</param>
        /// <param name="version">本次完整校验实际捕获的版本处理器快照；供回执复用，不重复遍历全部资产。</param>
        /// <param name="uirAfter">实际捕获的有限引擎角色后态；历史无角色合同的快照始终为空。</param>
        /// <param name="error">失败时的精确原因；成功时为 null。</param>
        /// <returns>满足上述精确条件时为 true，否则为 false；不会为通过检查而改变环境。</returns>
        private static bool TryCaptureStableAfter(Snapshot snapshot, CancellationToken token, bool after, bool inheritHostWitness,
            out AndroidVersionHandlerReadOnlyContract.Snapshot version, out AndroidResolveUirObjectContract.Snapshot uirAfter, out string error)
        {
            version = null;
            uirAfter = null;
            error = null;
            if (!TryCapturePreflight(token, out string callbacks, out string scheduler, out error)) return false;
            // 只有原 Plan State/执行观察器持有的快照可继承；反序列化回执的自一致 SHA 不能证明其来源。
            if (!IsSnapshotForDomain(snapshot, s_Domain, out error)) return false;
            if (snapshot.callbackSha256 != callbacks || snapshot.schedulerSha256 != scheduler)
            { error = "实际回调或调度身份已变化，不能继承原正常 update 见证。"; return false; }
            if (!AndroidResolveRefreshCallbacks.TryCaptureNaturalBranches(snapshot.allowedPaths, token, out string natural, out error, inheritHostWitness ? snapshot.domain : null) ||
                !AndroidResolveVendorRefreshContracts.TryCapture(snapshot.vendorInputs, token, out string vendor, out error) ||
                !AndroidResolveSpeedTreeDependencyContract.TryCapture(snapshot.allowedPaths, token, out string speedTree, out error) ||
                !AndroidVersionHandlerReadOnlyContract.TryCapture(token, out version, out error) ||
                !AndroidResolveCodeEditorTailContract.TryCapture(snapshot.allowedPaths, token, out var codeEditor, out error)) return false;
            if (snapshot.callbackSha256 != callbacks || snapshot.schedulerSha256 != scheduler || snapshot.naturalBranchesSha256 != natural ||
                snapshot.vendorSha256 != vendor || snapshot.speedTreeSha256 != speedTree ||
                !AndroidVersionHandlerReadOnlyContract.Matches(snapshot.version, version) || !version.naturallyComplete ||
                !AndroidResolveCodeEditorTailContract.Matches(snapshot.codeEditor, codeEditor))
            { error = "实际回调、自然状态、固定输入或自然完成状态变化，不能证明当前合同。"; return false; }
            var peripheral = AndroidResolveRefreshCallbacks.CapturePeripheralSnapshot(token);
            string peripheralError = after ? AndroidResolvePeripheralRefreshContracts.VerifyStableSnapshot(snapshot.peripheral, peripheral) :
                AndroidResolvePeripheralRefreshContracts.VerifyStableSnapshotInputs(snapshot.peripheral, peripheral);
            string graphError = after ? AndroidResolvePeripheralRefreshContracts.VerifyGraphUiHandlerKeys(snapshot.graphUiHandlerKeys, token) :
                (snapshot.graphUiHandlerKeys.SequenceEqual(AndroidResolvePeripheralRefreshContracts.CaptureGraphUiHandlerKeys(token)) ? null : "Graph UI 处理器在执行前已变化。");
            if (peripheralError != null || graphError != null) { error = peripheralError ?? graphError; return false; }
            if (snapshot.protectedFilesSha256 != CaptureProtectedFiles(snapshot.allowedPaths, token))
            { error = "非目标文件或目录摘要变化；不会自动恢复或覆盖用户状态。"; return false; }
            string[] objects = CaptureEditorObjectRows(snapshot.allowedPaths);
            string[] beforeComparable = snapshot.editorObjectRows;
            string[] afterComparable = objects;
            if (snapshot.uir != null &&
                (!AndroidResolveUirObjectContract.TryCapture(token, out uirAfter, out error) ||
                 !AndroidResolveUirObjectContract.TryGetComparableRows(snapshot.uir, uirAfter,
                     snapshot.editorObjectRows, objects, after, out beforeComparable, out afterComparable, out error))) return false;
            if (!EditorObjectsMatch(beforeComparable, afterComparable, after))
            {
                // 只展示身份/脏状态行的有界差异，不序列化用户对象，也不把诊断当作允许变化的白名单。
                string removed = string.Join(", ", beforeComparable.Except(afterComparable, StringComparer.Ordinal)
                    .Take(4).Select(row => row.Length > 240 ? row.Substring(0, 240) + "…" : row));
                string added = string.Join(", ", afterComparable.Except(beforeComparable, StringComparer.Ordinal)
                    .Take(4).Select(row => row.Length > 240 ? row.Substring(0, 240) + "…" : row));
                error = "非目标对象、场景脏状态或选择变化（移除/新增各至多四项）：移除[" + removed + "]；新增[" + added + "]；不会自动恢复或覆盖用户状态。";
                return false;
            }
            return true;
        }

        /// <summary>供回执与单元测试复核域/结构；反序列化快照不能跨域继承，也不能省略任一保护摘要。</summary>
        /// <param name="snapshot">同域完整刷新合同快照。</param>
        /// <param name="domain">当前 Editor 域的自有不透明标记。</param>
        /// <param name="error">失败时的精确原因；成功时为 null。</param>
        /// <returns>满足上述精确条件时为 true，否则为 false；不会为通过检查而改变环境。</returns>
        internal static bool IsSnapshotForDomain(Snapshot snapshot, string domain, out string error)
        {
            error = null;
            if (snapshot == null || snapshot.domain != domain || string.IsNullOrEmpty(snapshot.domain) ||
                string.IsNullOrEmpty(snapshot.callbackSha256) || string.IsNullOrEmpty(snapshot.schedulerSha256) ||
                string.IsNullOrEmpty(snapshot.protectedFilesSha256) || string.IsNullOrEmpty(snapshot.editorObjectsSha256) ||
                string.IsNullOrEmpty(snapshot.naturalBranchesSha256) ||
                snapshot.allowedPaths == null || snapshot.allowedPaths.Length == 0 || snapshot.editorObjectRows == null ||
                snapshot.graphUiHandlerKeys == null || snapshot.vendorInputs?.allPaths == null ||
                !snapshot.allowedPaths.SequenceEqual(snapshot.vendorInputs.allPaths) || string.IsNullOrEmpty(snapshot.vendorSha256) ||
                string.IsNullOrEmpty(snapshot.speedTreeSha256) || snapshot.search == null || string.IsNullOrEmpty(snapshot.search.sha256) ||
                snapshot.search.sha256 != AndroidResolveSearchCacheContract.ComputeHash(snapshot.search) ||
                !AndroidVersionHandlerReadOnlyContract.IsSelfConsistent(snapshot.version) ||
                !AndroidResolveCodeEditorTailContract.IsSelfConsistent(snapshot.codeEditor) || snapshot.peripheral == null ||
                snapshot.allowedPaths.Distinct(StringComparer.Ordinal).Count() != snapshot.allowedPaths.Length ||
                snapshot.sha256 != ComputeSnapshotHash(snapshot))
            { error = "资源导入/刷新回执缺完整同域合同、域已重载或摘要被改动；不会重放解析。"; return false; }
            return true;
        }

        /// <summary>只读枚举四个项目根的文件与目录；不跟随重解析点，不读取正文到回执，不忽略新生非目标文件。</summary>
        /// <param name="allowed">本轮精确允许路径集合；不是目录递归通配。</param>
        /// <param name="token">本次只读捕获或遍历的取消令牌；不取消厂商或引擎工作。</param>
        /// <returns>四个受保护项目根除精确目标外的完整文件/目录摘要。</returns>
        private static string CaptureProtectedFiles(string[] allowed, CancellationToken token)
        {
            var rows = new List<string>();
            var paths = new HashSet<string>(allowed, StringComparer.Ordinal);
            string root = GenerateActionCommon.ProjectRoot;
            foreach (string folder in new[] { "Assets", "ProjectSettings", "Packages", "UPMPackages" })
            {
                if (!Directory.Exists(Path.Combine(root, folder))) continue;
                var pending = new Stack<string>();
                pending.Push(Path.Combine(root, folder));
                while (pending.Count != 0)
                {
                    string directory = pending.Pop();
                    foreach (string entry in Directory.EnumerateFileSystemEntries(directory).OrderBy(item => item, StringComparer.Ordinal))
                    {
                        token.ThrowIfCancellationRequested();
                        FileAttributes attributes = File.GetAttributes(entry);
                        if ((attributes & FileAttributes.ReparsePoint) != 0)
                            throw new InvalidOperationException("保护根含符号链接，不能推断真实写边界。");
                        string relative = entry.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar).Replace('\\', '/');
                        if ((attributes & FileAttributes.Directory) != 0)
                        { if (!paths.Contains(relative)) rows.Add(relative + "|directory"); pending.Push(entry); }
                        else if (!paths.Contains(relative))
                            rows.Add(relative + "|" + GenerateActionCommon.ComputeFileHash(entry, token));
                    }
                }
            }
            return GenerateActionCommon.ComputeTextHash(string.Join("\n", rows.OrderBy(item => item, StringComparer.Ordinal)));
        }

        /// <summary>只读已加载对象身份/脏标志、已开场景脏标志和选择；不序列化用户对象、不加载资产或调用 getter 初始化单例。</summary>
        /// <param name="allowed">本轮精确允许路径集合；不是目录递归通配。</param>
        /// <returns>当前非目标对象、场景和选择的有序身份与脏状态行。</returns>
        private static string[] CaptureEditorObjectRows(string[] allowed)
        {
            var controlled = new HashSet<string>(allowed, StringComparer.Ordinal);
            var rows = Resources.FindObjectsOfTypeAll<UnityEngine.Object>()
                .Where(item => item != null && !controlled.Contains(AssetDatabase.GetAssetPath(item)))
                .Select(item => item.GetEntityId() + "|" + item.GetType().FullName + "|dirty=" + EditorUtility.IsDirty(item))
                .OrderBy(item => item, StringComparer.Ordinal).ToList();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            { Scene scene = SceneManager.GetSceneAt(i); rows.Add("scene|" + scene.handle + "|" + scene.path + "|" + scene.isDirty); }
            rows.Add("selection|" + string.Join(",", Selection.entityIds));
            return rows.ToArray();
        }

        /// <summary>以线性集合比较保留所有原对象、选择和场景行；只允许已审无 FilePath 单例新增，不忽略重复行或脏标志变化。</summary>
        /// <param name="before">同域执行前冻结的完整状态。</param>
        /// <param name="after">当前重新捕获的执行后状态。</param>
        /// <param name="allowDeclaredInitialization">只有执行后为 true，允许新增已审无 FilePath 的空派生单例。</param>
        /// <returns>满足上述精确条件时为 true，否则为 false；不会为通过检查而改变环境。</returns>
        internal static bool EditorObjectsMatch(string[] before, string[] after, bool allowDeclaredInitialization)
        {
            if (before == null || after == null) return false;
            var original = new HashSet<string>(before, StringComparer.Ordinal);
            var current = new HashSet<string>(after, StringComparer.Ordinal);
            // 完整旧行必须仍在，且两侧重复行仍拒绝；避免对约两万对象逐行扫描整个数组。
            if (original.Count != before.Length || current.Count != after.Length || !original.IsSubsetOf(current)) return false;
            return after.Where(row => !original.Contains(row)).All(row => allowDeclaredInitialization &&
                new[] { "UnityEditor.Tilemaps.GridPaletteBrushes", "UnityEditor.Tilemaps.GridPaintingState", "UnityEditor.Tilemaps.GridPalettes" }
                    .Any(type => row.IndexOf("|" + type + "|dirty=", StringComparison.Ordinal) >= 0));
        }

        /// <summary>绑定一次正常 Resolve 窗口；调用方必须先重核快照，嵌套或旧域执行被拒绝。</summary>
        /// <param name="snapshot">同域完整刷新合同快照。</param>
        /// <returns>仅绑定本次正规执行的自有观察作用域。</returns>
        internal static ExecutionScope Begin(Snapshot snapshot)
        {
            if (!IsSnapshotForDomain(snapshot, s_Domain, out string error) || s_ActiveScope != null)
                throw new InvalidOperationException(error ?? "已有资源刷新观察窗口，不能嵌套解析。");
            s_ActiveScope = new ExecutionScope(snapshot);
            return s_ActiveScope;
        }

        /// <summary>仅由 Nova 自有 AssetPostprocessor 观察实际四数组；观察不调用、取消或修改其他回调。</summary>
        /// <param name="imported">正常 Unity 派发的实际导入数组，保持输入顺序。</param>
        /// <param name="deleted">正常 Unity 派发的实际删除数组，保持输入顺序。</param>
        /// <param name="moved">正常 Unity 派发的实际移动目标数组，与 movedFrom 成对。</param>
        /// <param name="movedFrom">正常 Unity 派发的实际移动来源数组，与 moved 成对。</param>
        internal static void Observe(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            s_ActiveScope?.Record(imported, deleted, moved, movedFrom);
        }

        /// <summary>单次执行的有界观察器；Dispose 只解绑自己的窗口，不恢复用户文件或清除任何队列。</summary>
        internal sealed class ExecutionScope : IDisposable
        {
            private readonly Snapshot m_Snapshot;
            private readonly List<string> m_Dispatches = new List<string>();
            private readonly List<string> m_ExpectedTransactions = new List<string>();
            private readonly long m_StartedUtcTicks = DateTime.UtcNow.Ticks;
            private string m_Failure;
            private bool m_Closed;

            /// <summary>保存已冻结 DTO 和自然窗口开始时间；不执行解析或引擎写操作。</summary>
            /// <param name="snapshot">同域完整刷新合同快照。</param>
            internal ExecutionScope(Snapshot snapshot) { m_Snapshot = snapshot; }

            /// <summary>保留实际四数组顺序和同算法事务 GUID；越界时有界列出实际路径，任何越界、异常或容量溢出仍保持不完整。</summary>
            /// <param name="imported">正常 Unity 派发的实际导入数组，保持输入顺序。</param>
            /// <param name="deleted">正常 Unity 派发的实际删除数组，保持输入顺序。</param>
            /// <param name="moved">正常 Unity 派发的实际移动目标数组，与 movedFrom 成对。</param>
            /// <param name="movedFrom">正常 Unity 派发的实际移动来源数组，与 moved 成对。</param>
            internal void Record(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
            {
                if (m_Closed || m_Failure != null) { m_Failure = m_Failure ?? "观察窗口已关闭。"; return; }
                try
                {
                    if (imported == null || deleted == null || moved == null || movedFrom == null || moved.Length != movedFrom.Length ||
                        m_Dispatches.Count >= 256 || imported.Length + deleted.Length + moved.Length + movedFrom.Length > 8192)
                        throw new InvalidOperationException("实际刷新四数组无效或超限。");
                    var paths = new HashSet<string>(m_Snapshot.allowedPaths, StringComparer.Ordinal);
                    string[] outside = imported.Concat(deleted).Concat(moved).Concat(movedFrom)
                        .Where(path => !paths.Contains(path)).Distinct(StringComparer.Ordinal).Take(9).ToArray();
                    if (outside.Length > 0)
                        throw new InvalidOperationException("实际刷新含冻结精确域以外路径：" + string.Join("、", outside.Take(8)
                            .Select(path => path == null ? "<null>" : path.Length <= 240 ? path : path.Substring(0, 240) + "…")) +
                            (outside.Length > 8 ? "（仅列前八项）" : string.Empty) + "；不扩大授权范围。");
                    string[] expected = AndroidResolveSearchCacheContract.CaptureExpectedTransactions(imported, deleted, moved, movedFrom);
                    if (m_ExpectedTransactions.Count + expected.Length > AndroidResolveSearchCacheContract.MaximumObservedTransactions)
                        throw new InvalidOperationException("实际累计事务超出观察容量。");
                    m_ExpectedTransactions.AddRange(expected);
                    m_Dispatches.Add(GenerateActionCommon.ComputeTextHash(Util.Json.Serialize(new { imported, deleted, moved, movedFrom })));
                }
                catch (Exception exception) { m_Failure = exception is InvalidOperationException ? exception.Message : "刷新观察失败：" + exception.GetType().Name; }
            }

            /// <summary>用同域轻量探针等待正规 update，最多十秒；就绪后仅捕获一次完整稳定状态，不把全量遍历放进轮询，不调用或清理厂商队列。</summary>
            /// <param name="token">本次只读捕获或遍历的取消令牌；不取消厂商或引擎工作。</param>
            /// <returns>自然推进后的完整或未完成回执；不证明所有后台任务退出。</returns>
            internal async Task<Receipt> CompleteAsync(CancellationToken token)
            {
                if (m_Closed) throw new InvalidOperationException("资源刷新窗口不能重复完成。");
                double deadline = EditorApplication.timeSinceStartup + 10;
                AndroidResolveSearchCacheContract.Snapshot search = null;
                AndroidVersionHandlerReadOnlyContract.Snapshot version = null;
                AndroidResolveUirObjectContract.Snapshot uirAfter = null;
                bool completed = false;
                string pending = null;
                try
                {
                    if (!AndroidVersionHandlerReadOnlyContract.TryCreateSchedulerProbe(m_Snapshot.version, token,
                        out var probe, out pending)) m_Failure = pending;
                    else
                    {
                        bool ready = false;
                        // 至少让出一次真实 update；轻量观察不遍历资源、标签或设置，也不能替代最终完整校验。
                        do
                        {
                            await NextEditorUpdate();
                            token.ThrowIfCancellationRequested();
                            if (m_Failure != null || Thread.CurrentThread.ManagedThreadId != s_MainThread || s_Domain != m_Snapshot.domain) break;
                            if (!probe.TryCapture(token, out bool naturallyComplete, out pending)) { m_Failure = pending; break; }
                            if (!naturallyComplete || !AndroidResolveSearchCacheContract.TryCapture(token, false, out search, out pending)) continue;
                            long closed = DateTime.UtcNow.Ticks;
                            if (!AndroidResolveSearchCacheContract.TryVerifyAfter(m_Snapshot.search, search, m_ExpectedTransactions.ToArray(),
                                m_StartedUtcTicks, closed, token, out pending)) continue;
                            if (EditorApplication.isCompiling || EditorApplication.isUpdating) continue;
                            ready = true;
                            break;
                        } while (EditorApplication.timeSinceStartup < deadline);
                        if (ready)
                        {
                            // 完整快照仍核对全部稳定输入、自然完成和非目标保护，直接复用其版本证据写入回执。
                            completed = TryCaptureStableAfter(m_Snapshot, token, true, true, out version, out uirAfter, out pending);
                            if (!completed) m_Failure = pending;
                        }
                        else if (m_Failure == null && Thread.CurrentThread.ManagedThreadId == s_MainThread && s_Domain == m_Snapshot.domain)
                        {
                            // 超时只补一次真实诊断快照；即使稍后队列完成，也不能把本次超时追溯升级为成功。
                            AndroidVersionHandlerReadOnlyContract.TryCapture(token, out version, out pending);
                        }
                    }
                }
                catch (OperationCanceledException) { m_Failure = "正规解析已调用，取消后保留 partial；不会重放。"; }
                catch (Exception exception) { m_Failure = "正规刷新后只读收尾失败：" + exception.GetType().Name; }
                if (!completed) m_Failure = m_Failure ?? pending ?? "正规刷新尚未在有界窗口内完成；保留 partial，不重放。";
                string[] graphAfter = Array.Empty<string>();
                if (completed)
                {
                    try { graphAfter = AndroidResolvePeripheralRefreshContracts.CaptureGraphUiHandlerKeys(CancellationToken.None); }
                    catch (Exception exception) { completed = false; m_Failure = "刷新最终处理器证据失败：" + exception.GetType().Name; }
                }
                m_Closed = true;
                var receipt = new Receipt { domain = s_Domain, snapshotSha256 = m_Snapshot.sha256,
                    completed = completed, dispatchCount = m_Dispatches.Count,
                    dispatchSha256 = GenerateActionCommon.ComputeTextHash(string.Join("\n", m_Dispatches)), failure = m_Failure,
                    startedUtcTicks = m_StartedUtcTicks, closedUtcTicks = DateTime.UtcNow.Ticks,
                    expectedTransactions = m_ExpectedTransactions.ToArray(), searchAfter = search, versionAfter = version,
                    graphUiHandlerKeysAfter = graphAfter, uirAfter = uirAfter };
                receipt.sha256 = ComputeReceiptHash(receipt);
                return receipt;
            }

            /// <summary>仅订阅一个自有 update 回调，先解绑再异步续接 Unity 上下文，避免在回调栈内阻塞后续厂商 update。</summary>
            /// <returns>仅由下一次正常 Editor update 完成的自有等待任务。</returns>
            private static Task NextEditorUpdate()
            {
                var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                EditorApplication.CallbackFunction callback = null;
                callback = () => { EditorApplication.update -= callback; completion.TrySetResult(true); };
                EditorApplication.update += callback;
                return completion.Task;
            }

            /// <summary>只解绑当前自有观察窗；不清理、取消、重放或恢复厂商/用户状态。</summary>
            public void Dispose()
            {
                m_Closed = true;
                if (ReferenceEquals(s_ActiveScope, this)) s_ActiveScope = null;
            }
        }

        /// <summary>Verify 先验完成标记及域，再只读核对冻结保护；历史成功标记不能补造缺失的刷新证据。</summary>
        /// <param name="snapshot">同域完整刷新合同快照。</param>
        /// <param name="receipt">本次正规执行产生的完整观察回执。</param>
        /// <param name="token">本次只读捕获或遍历的取消令牌；不取消厂商或引擎工作。</param>
        /// <param name="error">失败时的精确原因；成功时为 null。</param>
        /// <returns>满足上述精确条件时为 true，否则为 false；不会为通过检查而改变环境。</returns>
        internal static bool TryVerifyReceipt(Snapshot snapshot, Receipt receipt, CancellationToken token, out string error)
        {
            error = null;
            if (!IsSnapshotForDomain(snapshot, s_Domain, out error) || receipt == null || !receipt.completed || receipt.domain != s_Domain ||
                receipt.snapshotSha256 != snapshot.sha256 || receipt.dispatchCount < 0 || receipt.dispatchCount > 256 ||
                string.IsNullOrEmpty(receipt.dispatchSha256) || !string.IsNullOrEmpty(receipt.failure) ||
                receipt.sha256 != ComputeReceiptHash(receipt) || receipt.graphUiHandlerKeysAfter == null ||
                !AndroidVersionHandlerReadOnlyContract.Matches(snapshot.version, receipt.versionAfter) || !receipt.versionAfter.naturallyComplete)
            { error = error ?? "刷新回执不完整、域已失效或摘要变化；不会重放。"; return false; }
            if (snapshot.uir != null &&
                !AndroidResolveUirObjectContract.TryValidateTransition(snapshot.uir, receipt.uirAfter, true, out error)) return false;
            if (!TryCaptureStableAfter(snapshot, token, true, false, out _, out _, out error) ||
                !receipt.graphUiHandlerKeysAfter.SequenceEqual(AndroidResolvePeripheralRefreshContracts.CaptureGraphUiHandlerKeys(token)) ||
                !AndroidResolveSearchCacheContract.TryCapture(token, true, out var search, out error) ||
                !AndroidResolveSearchCacheContract.SameBefore(receipt.searchAfter, search))
            { error = error ?? "执行后证据发生新变化；不能继承旧验收。"; return false; }
            return AndroidResolveSearchCacheContract.TryVerifyAfter(snapshot.search, search, receipt.expectedTransactions,
                receipt.startedUtcTicks, receipt.closedUtcTicks, token, out error);
        }

        /// <summary>冻结观察回执全部字段，避免完整标记、事务数量和时间窗被拆开继承。</summary>
        /// <param name="receipt">本次正规执行产生的完整观察回执。</param>
        /// <returns>绑定完整观察回执各项证据的 SHA。</returns>
        internal static string ComputeReceiptHash(Receipt receipt)
        {
            if (receipt == null) return null;
            string legacyHash = GenerateActionCommon.ComputeTextHash(Util.Json.Serialize(new
            { receipt.domain, receipt.snapshotSha256, receipt.completed, receipt.dispatchCount, receipt.dispatchSha256,
                receipt.failure, receipt.startedUtcTicks, receipt.closedUtcTicks, receipt.expectedTransactions, receipt.searchAfter,
                receipt.versionAfter, receipt.graphUiHandlerKeysAfter }));
            // 旧回执沿用原摘要；新增的有限真实后态单独绑定，不改写任何历史partial。
            return receipt.uirAfter == null ? legacyHash : GenerateActionCommon.ComputeTextHash(
                legacyHash + "\nUIR-v1\n" + Util.Json.Serialize(receipt.uirAfter));
        }

    }

    /// <summary>只记录正常 AssetDatabase 派发的四数组；不发起刷新、不改变其他回调与输入。</summary>
    internal sealed class AndroidResolveRefreshObserver : AssetPostprocessor
    {
        /// <summary>无观察窗口时无操作；有窗口时转入 Nova 自有的有界记录，不持有原数组。</summary>
        /// <param name="imported">正常 Unity 派发的实际导入数组，保持输入顺序。</param>
        /// <param name="deleted">正常 Unity 派发的实际删除数组，保持输入顺序。</param>
        /// <param name="moved">正常 Unity 派发的实际移动目标数组，与 movedFrom 成对。</param>
        /// <param name="movedFrom">正常 Unity 派发的实际移动来源数组，与 moved 成对。</param>
        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            AndroidResolveRefreshContract.Observe(imported, deleted, moved, movedFrom);
            AndroidGenerateAppsFlyerScope.ObserveImports(imported, deleted, moved, movedFrom);
        }
    }
}
