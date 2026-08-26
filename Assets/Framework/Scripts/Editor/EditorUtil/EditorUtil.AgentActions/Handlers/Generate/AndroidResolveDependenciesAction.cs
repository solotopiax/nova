/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AndroidResolveDependenciesAction.cs
 * author:    taoye
 * created:   2026/8/20
 * descrip:   EDM4U Android 依赖图解析与生成结果验证 Action
 ***************************************************************/

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using NovaFramework.Runtime;
using UnityEditor;
using Path = System.IO.Path;

namespace NovaFramework.Editor
{
    [AgentAction(
        "nova.project.android.resolve-dependencies",
        "解析 Android 原生依赖",
        "android",
        AgentActionOperationType.Generate,
        Description = "冻结 EDM4U Android 依赖输入后执行受控 Resolve，并核验生成的原生依赖结果。",
        Effects = AgentActionEffect.WorkspaceRead | AgentActionEffect.WorkspaceWrite |
                  AgentActionEffect.UnityRead | AgentActionEffect.UnityWrite |
                  AgentActionEffect.Destructive,
        RequiredEvidence = AgentActionEvidence.PackageResolution | AgentActionEvidence.Artifact,
        Idempotency = AgentActionIdempotency.ReplaceGeneratedOutput,
        RequiresConfirmation = true,
        RequiresEditMode = true,
        Locks = new[]
        {
            "unity-editor", "asset-database", "android-dependency-graph",
            "android-gradle-templates", "generated-local-repo",
        })]
    internal sealed class AndroidResolveDependenciesAction :
        AgentActionHandler<AndroidResolveDependenciesAction.Request>
    {
        [Serializable]
        public sealed class Request
        {
            [AgentActionRequired] public string activeBuildTarget;
        }

        private sealed class State
        {
            public AndroidResolveActionCommon.DependencyEntry[] Graph;
            public string GraphSha256;
        }

        [Serializable]
        private sealed class Receipt
        {
            public string activeBuildTarget;
            public AndroidResolveActionCommon.DependencyEntry[] expectedGraph;
            public string expectedGraphSha256;
            public bool resolveSucceeded;
            public AndroidResolveActionCommon.ResolutionState resolvedState;
            public GenerateActionCommon.Artifact[] artifacts;
        }

        /// <summary>
        /// 只接受显式 Android Target；Action 不负责切换 BuildTarget。
        /// </summary>
        protected override bool TryValidateRequest(Request request, out string error)
        {
            error = null;
            if (request?.activeBuildTarget != BuildTarget.Android.ToString())
            {
                error = "activeBuildTarget 必须精确为 Android；Action 不切换 BuildTarget。";
                return false;
            }
            return true;
        }

        /// <summary>
        /// 只读冻结 EDM4U 当前公开依赖图与受限写入根，不调用 ResolveSync。
        /// </summary>
        public override Task<AgentActionHandlerPlan> PlanAsync(
            Request request,
            AgentActionExecutionContext context)
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                return Task.FromResult(new AgentActionHandlerPlan
                {
                    Status = "blocked",
                    Summary = $"当前 activeBuildTarget={EditorUserBuildSettings.activeBuildTarget}，Action 不自动切换到 Android。",
                });
            }
            if (!AndroidResolveActionCommon.TryCaptureDependencyGraph(
                    out AndroidResolveActionCommon.DependencyEntry[] graph,
                    out string graphError))
            {
                return Task.FromResult(new AgentActionHandlerPlan
                {
                    Status = "not_applicable",
                    Summary = graphError,
                });
            }
            if (graph.Length == 0)
            {
                return Task.FromResult(new AgentActionHandlerPlan
                {
                    Status = "not_applicable",
                    Summary = "EDM4U 当前公开依赖图为空；为避免把尚未初始化误判为无依赖，Action 不执行清理型 Resolve。",
                });
            }

            string graphSha256 = AndroidResolveActionCommon.ComputeGraphSha256(graph);
            var receipt = new Receipt
            {
                activeBuildTarget = BuildTarget.Android.ToString(),
                expectedGraph = graph,
                expectedGraphSha256 = graphSha256,
                resolveSucceeded = false,
                resolvedState = null,
                artifacts = Array.Empty<GenerateActionCommon.Artifact>(),
            };
            return Task.FromResult(new AgentActionHandlerPlan
            {
                Status = "ready",
                Summary = $"将通过 EDM4U Force Resolve 重建 {graph.Length} 项 Android 依赖的受管输出。",
                DataJson = Util.Json.Serialize(receipt),
                State = new State { Graph = graph, GraphSha256 = graphSha256 },
                WriteSet = AndroidResolveActionCommon.WriteSet,
                Evidence = new[]
                {
                    $"已冻结 EDM4U 当前公开依赖图，共 {graph.Length} 项，SHA-256={graphSha256}。",
                    "写入限制为 EDM4U 状态文件、固定 GeneratedLocalRepo 根与 Android Gradle 受管模板；Action 不修改 UPM manifest 或 registry。",
                    "若 ResolveSync 引发 domain reload，只能使用 recovery token 进入 Verify，不会恢复或重放 Execute。",
                },
                RecoveryPayloadJson = Util.Json.Serialize(receipt),
            });
        }

        /// <summary>
        /// 复核依赖图未漂移后调用一次既有 AndroidResolver.Resolve，并冻结 EDM4U 输出证据。
        /// </summary>
        public override Task<AgentActionResult> ExecuteAsync(
            object state,
            AgentActionExecutionContext context)
        {
            if (!(state is State frozen))
            {
                return Task.FromResult(AgentActionResult.Create(null, "blocked", "Android 依赖解析冻结状态无效。"));
            }
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                return Task.FromResult(AgentActionResult.Create(null, "blocked", "activeBuildTarget 已离开 Android，请重新 Plan。"));
            }
            if (!AndroidResolveActionCommon.TryCaptureDependencyGraph(
                    out AndroidResolveActionCommon.DependencyEntry[] currentGraph,
                    out string graphError) ||
                !string.Equals(
                    frozen.GraphSha256,
                    AndroidResolveActionCommon.ComputeGraphSha256(currentGraph),
                    StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(AgentActionResult.Create(
                    null,
                    "blocked",
                    "EDM4U 依赖图已漂移，请重新 Plan：" + graphError));
            }

            context.CancellationToken.ThrowIfCancellationRequested();
            EditorUtil.AndroidResolver.Resolve();
            context.CancellationToken.ThrowIfCancellationRequested();

            string packageError = null;
            if (!AndroidResolveActionCommon.TryLoadResolutionState(
                    out AndroidResolveActionCommon.ResolutionState resolvedState,
                    out string stateError) ||
                !AndroidResolveActionCommon.PackagesEqual(frozen.Graph, resolvedState.packages, out packageError))
            {
                return Task.FromResult(AgentActionResult.Create(
                    null,
                    "partial",
                    "EDM4U 已返回，但解析状态无法精确对应冻结依赖图；不会自动重放：" + (stateError ?? packageError)));
            }
            if (!AndroidResolveActionCommon.TryCaptureArtifacts(
                    resolvedState,
                    context,
                    out GenerateActionCommon.Artifact[] artifacts,
                    out string artifactError))
            {
                return Task.FromResult(AgentActionResult.Create(
                    null,
                    "partial",
                    "EDM4U 已返回，但受管输出证据不完整；不会自动重放：" + artifactError));
            }

            var receipt = new Receipt
            {
                activeBuildTarget = BuildTarget.Android.ToString(),
                expectedGraph = frozen.Graph,
                expectedGraphSha256 = frozen.GraphSha256,
                resolveSucceeded = true,
                resolvedState = resolvedState,
                artifacts = artifacts,
            };
            AgentActionResult result = AgentActionResult.Create(
                null,
                "partial",
                "EDM4U ResolveSync 已明确成功，等待只读 Verify 核对依赖图、受管文件集合与 SHA-256。");
            result.ReceiptJson = Util.Json.Serialize(receipt);
            result.DataJson = result.ReceiptJson;
            result.Artifacts.AddRange(artifacts.Select(artifact => artifact.path));
            result.Evidence.Add("Execute 只调用现有 EditorUtil.AndroidResolver.Resolve 一次；失败或 domain reload 后不会自动重放。" );
            return Task.FromResult(result);
        }

        /// <summary>
        /// 只读核对明确成功标记、当前依赖图、EDM4U 状态文件、受管文件集合和全部产物摘要。
        /// </summary>
        public override Task<AgentActionResult> VerifyAsync(
            string receiptJson,
            AgentActionExecutionContext context)
        {
            Receipt receipt;
            try
            {
                receipt = Util.Json.Deserialize<Receipt>(receiptJson);
            }
            catch (Exception exception)
            {
                return Task.FromResult(AgentActionResult.Create(null, "blocked", "Android Resolve Receipt 无法解析：" + exception.Message));
            }
            if (receipt == null || receipt.activeBuildTarget != BuildTarget.Android.ToString() ||
                receipt.expectedGraph == null || string.IsNullOrWhiteSpace(receipt.expectedGraphSha256) ||
                receipt.artifacts == null)
            {
                return Task.FromResult(AgentActionResult.Create(null, "blocked", "Android Resolve Receipt 不完整。"));
            }
            if (!string.Equals(
                    receipt.expectedGraphSha256,
                    AndroidResolveActionCommon.ComputeGraphSha256(receipt.expectedGraph),
                    StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(AgentActionResult.Create(null, "blocked", "Receipt 内冻结依赖图与 SHA-256 不一致。"));
            }
            if (!receipt.resolveSucceeded)
            {
                return Task.FromResult(AgentActionResult.Create(
                    null,
                    "partial",
                    "Recovery Receipt 未包含 ResolveSync 明确成功标记；不会仅凭目录存在推断执行成功，也不会重放。"));
            }
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                return Task.FromResult(AgentActionResult.Create(null, "partial", "当前 activeBuildTarget 已不是 Android，暂不能完成验证。"));
            }
            if (!AndroidResolveActionCommon.TryCaptureDependencyGraph(
                    out AndroidResolveActionCommon.DependencyEntry[] currentGraph,
                    out string graphError) ||
                !string.Equals(
                    receipt.expectedGraphSha256,
                    AndroidResolveActionCommon.ComputeGraphSha256(currentGraph),
                    StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(AgentActionResult.Create(null, "partial", "当前 EDM4U 依赖图已漂移：" + graphError));
            }
            string packageError = null;
            if (!AndroidResolveActionCommon.TryLoadResolutionState(
                    out AndroidResolveActionCommon.ResolutionState currentState,
                    out string stateError) ||
                !AndroidResolveActionCommon.PackagesEqual(receipt.expectedGraph, currentState.packages, out packageError) ||
                !AndroidResolveActionCommon.StatesEqual(receipt.resolvedState, currentState))
            {
                return Task.FromResult(AgentActionResult.Create(
                    null,
                    "partial",
                    "EDM4U 当前状态与 Execute Receipt 不一致：" + (stateError ?? packageError ?? "受管文件或设置已漂移。")));
            }
            if (!GenerateActionCommon.TryVerifyArtifacts(
                    receipt.artifacts,
                    context.CancellationToken,
                    out GenerateActionCommon.Artifact[] actual,
                    out string artifactError))
            {
                AgentActionResult partial = AgentActionResult.Create(null, "partial", "Android 依赖产物验证未通过：" + artifactError);
                partial.DataJson = Util.Json.Serialize(actual);
                return Task.FromResult(partial);
            }
            if (!AndroidResolveActionCommon.ArtifactsCoverState(currentState, actual, out string coverageError))
            {
                return Task.FromResult(AgentActionResult.Create(null, "partial", coverageError));
            }

            AgentActionResult success = AgentActionResult.Create(
                null,
                "success",
                "EDM4U 依赖图、解析状态、GeneratedLocalRepo/受管文件集合与 SHA-256 已只读核对。" );
            success.EvidenceKinds = AgentActionEvidence.PackageResolution | AgentActionEvidence.Artifact;
            success.DataJson = Util.Json.Serialize(currentState);
            success.Artifacts.AddRange(actual.Select(artifact => artifact.path));
            success.Evidence.Add("Verify 未调用 ResolveSync、AssetDatabase.Refresh 或任何恢复写入。" );
            success.Warnings.Add("该证据不代表 Gradle 构建、Player、设备安装或 SDK 运行时初始化成功。" );
            return Task.FromResult(success);
        }
    }

    /// <summary>
    /// Android Resolve Action 的只读 EDM4U 图快照、状态解析与产物覆盖辅助。
    /// </summary>
    internal static class AndroidResolveActionCommon
    {
        internal const string ResolutionStatePath = "ProjectSettings/AndroidResolverDependencies.xml";
        internal const string LocalRepositoryPath = "Assets/GeneratedLocalRepo";
        internal const string AndroidPluginPath = "Assets/Plugins/Android";

        internal static readonly string[] WriteSet =
        {
            ResolutionStatePath,
            LocalRepositoryPath + "/**",
            AndroidPluginPath + "/mainTemplate.gradle",
            AndroidPluginPath + "/settingsTemplate.gradle",
            AndroidPluginPath + "/gradleTemplate.properties",
            AndroidPluginPath + "/AndroidManifest.xml（仅 EDM4U 管理的变量替换）",
            AndroidPluginPath + "/**（仅 EDM4U 标签管理的 AAR/JAR/SRCAAR）",
        };

        [Serializable]
        internal sealed class DependencyEntry
        {
            public string spec;
            public string[] repositorySha256;
            public string[] packageIds;
        }

        [Serializable]
        internal sealed class SettingEntry
        {
            public string name;
            public string value;
        }

        [Serializable]
        internal sealed class ResolutionState
        {
            public string[] packages;
            public string[] files;
            public SettingEntry[] settings;
        }

        /// <summary>
        /// 通过 EDM4U 的公开 PlayServicesSupport.GetAllDependencies 冻结当前完整依赖图。
        /// </summary>
        internal static bool TryCaptureDependencyGraph(out DependencyEntry[] graph, out string error)
        {
            graph = Array.Empty<DependencyEntry>();
            error = null;
            Type supportType = FindType("Google.JarResolver.PlayServicesSupport");
            MethodInfo getAll = supportType?.GetMethod(
                "GetAllDependencies",
                BindingFlags.Public | BindingFlags.Static,
                null,
                Type.EmptyTypes,
                null);
            if (getAll == null)
            {
                error = "当前项目未提供可用的 EDM4U PlayServicesSupport.GetAllDependencies。";
                return false;
            }

            object value;
            try
            {
                value = getAll.Invoke(null, null);
            }
            catch (Exception exception)
            {
                error = "读取 EDM4U 公开依赖图失败：" + (exception.InnerException?.Message ?? exception.Message);
                return false;
            }
            if (!(value is IEnumerable entries))
            {
                error = "EDM4U 公开依赖图返回了未知结构。";
                return false;
            }

            var result = new List<DependencyEntry>();
            foreach (object pair in entries)
            {
                object dependency = pair?.GetType().GetProperty("Value")?.GetValue(pair);
                string spec = dependency?.GetType().GetProperty("Key")?.GetValue(dependency) as string;
                if (string.IsNullOrWhiteSpace(spec))
                {
                    error = "EDM4U 依赖图包含无法识别的依赖项。";
                    return false;
                }
                result.Add(new DependencyEntry
                {
                    spec = spec,
                    repositorySha256 = ReadStringArray(dependency, "Repositories")
                        .Select(GenerateActionCommon.ComputeTextHash)
                        .OrderBy(hash => hash, StringComparer.Ordinal)
                        .ToArray(),
                    packageIds = ReadStringArray(dependency, "PackageIds"),
                });
            }
            graph = result.OrderBy(item => item.spec, StringComparer.Ordinal).ToArray();
            return true;
        }

        /// <summary>
        /// 对排序后的依赖规格、仓库和 Android SDK 包标识生成稳定摘要。
        /// </summary>
        internal static string ComputeGraphSha256(IEnumerable<DependencyEntry> graph)
        {
            DependencyEntry[] canonical = (graph ?? Array.Empty<DependencyEntry>())
                .Select(item => new DependencyEntry
                {
                    spec = item?.spec,
                    repositorySha256 = (item?.repositorySha256 ?? Array.Empty<string>())
                        .OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                    packageIds = (item?.packageIds ?? Array.Empty<string>()).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                })
                .OrderBy(item => item.spec, StringComparer.Ordinal)
                .ToArray();
            return GenerateActionCommon.ComputeTextHash(Util.Json.Serialize(canonical));
        }

        /// <summary>
        /// 从项目中的 EDM4U 状态文件读取本次解析后的包、受管文件和设置快照。
        /// </summary>
        internal static bool TryLoadResolutionState(out ResolutionState state, out string error)
        {
            state = null;
            error = null;
            string absolute = Path.Combine(GenerateActionCommon.ProjectRoot, ResolutionStatePath);
            if (!File.Exists(absolute))
            {
                error = ResolutionStatePath + " 不存在。";
                return false;
            }
            try
            {
                return TryParseResolutionStateXml(File.ReadAllText(absolute), out state, out error);
            }
            catch (Exception exception) when (!(exception is OperationCanceledException))
            {
                error = "读取 EDM4U 状态文件失败：" + exception.Message;
                return false;
            }
        }

        /// <summary>
        /// 严格解析 EDM4U 状态 XML，拒绝重复项、重复设置与项目外受管路径。
        /// </summary>
        internal static bool TryParseResolutionStateXml(string xml, out ResolutionState state, out string error)
        {
            state = null;
            error = null;
            XDocument document;
            try
            {
                using (var reader = XmlReader.Create(new StringReader(xml ?? string.Empty), new XmlReaderSettings
                {
                    DtdProcessing = DtdProcessing.Prohibit,
                    XmlResolver = null,
                }))
                {
                    document = XDocument.Load(reader, LoadOptions.None);
                }
            }
            catch (Exception exception) when (exception is XmlException || exception is InvalidOperationException)
            {
                error = "EDM4U 状态 XML 无法解析：" + exception.Message;
                return false;
            }
            XElement root = document.Root;
            if (root?.Name.LocalName != "dependencies")
            {
                error = "EDM4U 状态 XML 缺少 dependencies 根节点。";
                return false;
            }

            string[] packages = root.Element("packages")?.Elements("package")
                .Select(element => element.Value?.Trim()).ToArray() ?? Array.Empty<string>();
            string[] files = root.Element("files")?.Elements("file")
                .Select(element => (element.Value ?? string.Empty).Trim().Replace('\\', '/')).ToArray() ?? Array.Empty<string>();
            SettingEntry[] settings = root.Element("settings")?.Elements("setting")
                .Select(element => new SettingEntry
                {
                    name = (string)element.Attribute("name"),
                    value = (string)element.Attribute("value"),
                }).ToArray() ?? Array.Empty<SettingEntry>();
            if (packages.Any(string.IsNullOrWhiteSpace) || files.Any(string.IsNullOrWhiteSpace) ||
                settings.Any(item => string.IsNullOrWhiteSpace(item.name) || item.value == null) ||
                packages.Distinct(StringComparer.Ordinal).Count() != packages.Length ||
                files.Distinct(StringComparer.Ordinal).Count() != files.Length ||
                settings.Select(item => item.name).Distinct(StringComparer.Ordinal).Count() != settings.Length)
            {
                error = "EDM4U 状态 XML 包含空值或重复项。";
                return false;
            }
            foreach (string file in files)
            {
                if (!GenerateActionCommon.TryResolveProjectPath(
                        file,
                        "EDM4U 受管文件",
                        out string absoluteFile,
                        out error)) return false;
                string localRepositoryRoot = Path.Combine(GenerateActionCommon.ProjectRoot, LocalRepositoryPath);
                string androidPluginRoot = Path.Combine(GenerateActionCommon.ProjectRoot, AndroidPluginPath);
                if (!IsStrictDescendant(absoluteFile, localRepositoryRoot) &&
                    !IsStrictDescendant(absoluteFile, androidPluginRoot))
                {
                    error = "EDM4U 受管文件必须严格位于 Assets/GeneratedLocalRepo/** 或 Assets/Plugins/Android/**：" + file;
                    return false;
                }
            }

            state = new ResolutionState
            {
                packages = packages.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                files = files.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                settings = settings.OrderBy(item => item.name, StringComparer.Ordinal).ToArray(),
            };
            if (!HasRequiredSetting(state, "localMavenRepoDir", LocalRepositoryPath) ||
                !HasRequiredSetting(state, "packageDir", AndroidPluginPath))
            {
                error = "EDM4U 的 localMavenRepoDir/packageDir 不符合 Nova 固定写入边界。";
                state = null;
                return false;
            }
            return true;
        }

        /// <summary>
        /// 核对状态包集合与冻结图的 Maven 规格完全一致。
        /// </summary>
        internal static bool PackagesEqual(DependencyEntry[] graph, string[] packages, out string error)
        {
            string[] expected = (graph ?? Array.Empty<DependencyEntry>()).Select(item => item.spec)
                .OrderBy(value => value, StringComparer.Ordinal).ToArray();
            string[] actual = (packages ?? Array.Empty<string>()).OrderBy(value => value, StringComparer.Ordinal).ToArray();
            bool equal = expected.SequenceEqual(actual, StringComparer.Ordinal);
            error = equal ? null : "AndroidResolverDependencies.xml 的 package 集合与冻结依赖图不一致。";
            return equal;
        }

        /// <summary>
        /// 捕获 EDM4U 状态、全部受管文件、固定本地仓库和存在的 Android 模板摘要。
        /// </summary>
        internal static bool TryCaptureArtifacts(
            ResolutionState state,
            AgentActionExecutionContext context,
            out GenerateActionCommon.Artifact[] artifacts,
            out string error)
        {
            var result = new List<GenerateActionCommon.Artifact>();
            error = null;
            try
            {
                result.Add(GenerateActionCommon.CaptureFile(
                    Path.Combine(GenerateActionCommon.ProjectRoot, ResolutionStatePath),
                    context.CancellationToken));
                foreach (string file in state?.files ?? Array.Empty<string>())
                {
                    string absolute = Path.Combine(GenerateActionCommon.ProjectRoot, file);
                    result.Add(GenerateActionCommon.CaptureFile(absolute, context.CancellationToken));
                    if (File.Exists(absolute + ".meta"))
                    {
                        result.Add(GenerateActionCommon.CaptureFile(absolute + ".meta", context.CancellationToken));
                    }
                }

                string repository = Path.Combine(GenerateActionCommon.ProjectRoot, LocalRepositoryPath);
                bool repositoryHasManagedFiles = (state?.files ?? Array.Empty<string>()).Any(file =>
                    file.StartsWith(LocalRepositoryPath + "/", StringComparison.Ordinal));
                if (repositoryHasManagedFiles)
                {
                    result.Add(GenerateActionCommon.CaptureDirectory(repository, "*", context.CancellationToken));
                }
                foreach (string relative in new[]
                         {
                             AndroidPluginPath + "/mainTemplate.gradle",
                             AndroidPluginPath + "/settingsTemplate.gradle",
                             AndroidPluginPath + "/gradleTemplate.properties",
                             AndroidPluginPath + "/AndroidManifest.xml",
                         })
                {
                    string absolute = Path.Combine(GenerateActionCommon.ProjectRoot, relative);
                    if (File.Exists(absolute)) result.Add(GenerateActionCommon.CaptureFile(absolute, context.CancellationToken));
                }
                artifacts = result.GroupBy(item => item.path, StringComparer.Ordinal)
                    .Select(group => group.First()).OrderBy(item => item.path, StringComparer.Ordinal).ToArray();
                return true;
            }
            catch (Exception exception) when (!(exception is OperationCanceledException))
            {
                artifacts = result.ToArray();
                error = exception.Message;
                return false;
            }
        }

        /// <summary>
        /// 核对 Execute 与 Verify 读取的包、文件和设置快照完全一致。
        /// </summary>
        internal static bool StatesEqual(ResolutionState expected, ResolutionState actual)
        {
            if (expected == null || actual == null) return false;
            return (expected.packages ?? Array.Empty<string>()).SequenceEqual(actual.packages ?? Array.Empty<string>(), StringComparer.Ordinal) &&
                   (expected.files ?? Array.Empty<string>()).SequenceEqual(actual.files ?? Array.Empty<string>(), StringComparer.Ordinal) &&
                   (expected.settings ?? Array.Empty<SettingEntry>()).Select(SettingKey)
                   .SequenceEqual((actual.settings ?? Array.Empty<SettingEntry>()).Select(SettingKey), StringComparer.Ordinal);
        }

        /// <summary>
        /// 确保状态文件自身、每个 EDM4U 受管文件以及有受管本地文件时的仓库目录均进入证据集合。
        /// </summary>
        internal static bool ArtifactsCoverState(
            ResolutionState state,
            IEnumerable<GenerateActionCommon.Artifact> artifacts,
            out string error)
        {
            error = null;
            var paths = new HashSet<string>((artifacts ?? Array.Empty<GenerateActionCommon.Artifact>())
                .Select(item => item.path), StringComparer.Ordinal);
            string statePath = Path.Combine(GenerateActionCommon.ProjectRoot, ResolutionStatePath);
            if (!paths.Contains(statePath))
            {
                error = "产物证据未覆盖 EDM4U 状态文件。";
                return false;
            }
            foreach (string file in state?.files ?? Array.Empty<string>())
            {
                string absolute = Path.GetFullPath(Path.Combine(GenerateActionCommon.ProjectRoot, file));
                if (!paths.Contains(absolute))
                {
                    error = "产物证据未覆盖 EDM4U 受管文件：" + file;
                    return false;
                }
            }
            if ((state?.files ?? Array.Empty<string>()).Any(file =>
                    file.StartsWith(LocalRepositoryPath + "/", StringComparison.Ordinal)))
            {
                string repository = Path.Combine(GenerateActionCommon.ProjectRoot, LocalRepositoryPath);
                if (!paths.Contains(repository))
                {
                    error = "产物证据未覆盖 GeneratedLocalRepo 完整目录。";
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// 在已加载程序集里查找指定 EDM4U 类型，避免 Framework asmdef 硬依赖其裸 DLL。
        /// </summary>
        private static Type FindType(string fullName)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(fullName, false);
                if (type != null) return type;
            }
            return null;
        }

        /// <summary>
        /// 读取 EDM4U Dependency 的公开字符串数组属性并排序，空属性统一为空数组。
        /// </summary>
        private static string[] ReadStringArray(object target, string propertyName)
        {
            return (target?.GetType().GetProperty(propertyName)?.GetValue(target) as IEnumerable<string> ??
                    Array.Empty<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
        }

        /// <summary>
        /// 核对 EDM4U 状态中的固定路径设置。
        /// </summary>
        private static bool HasRequiredSetting(ResolutionState state, string name, string expected)
        {
            return state.settings.Any(item => item.name == name &&
                                               string.Equals(item.value?.Replace('\\', '/'), expected, StringComparison.Ordinal));
        }

        /// <summary>
        /// 判断文件是否为指定受管根的真实后代；根目录自身不属于合法文件目标。
        /// </summary>
        private static bool IsStrictDescendant(string absoluteFile, string absoluteRoot)
        {
            string file = Path.GetFullPath(absoluteFile);
            string root = Path.GetFullPath(absoluteRoot)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            StringComparison comparison = Path.DirectorySeparatorChar == '\\'
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
            return file.StartsWith(root + Path.DirectorySeparatorChar, comparison);
        }

        /// <summary>
        /// 将设置项转换为稳定比较键。
        /// </summary>
        private static string SettingKey(SettingEntry item)
        {
            return (item?.name ?? string.Empty) + "\n" + (item?.value ?? string.Empty);
        }
    }
}
