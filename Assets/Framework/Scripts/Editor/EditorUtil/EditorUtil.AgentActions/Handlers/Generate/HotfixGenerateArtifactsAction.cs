/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  HotfixGenerateArtifactsAction.cs
 * author:    taoye
 * created:   2026/8/20
 * descrip:   当前 Target 的 HybridCLR GenerateAll + link.xml 校验 Action
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;
using HybridCLR.Editor;
using HybridCLR.Editor.Installer;
using NovaFramework.Runtime;
using UnityEditor;
using UnityEngine;
using Path = System.IO.Path;

namespace NovaFramework.Editor
{
    [AgentAction(
        "nova.project.hotfix.generate-artifacts",
        "生成 HybridCLR 预构建产物",
        "hotfix",
        AgentActionOperationType.Generate,
        Effects = AgentActionEffect.WorkspaceRead | AgentActionEffect.WorkspaceWrite |
                  AgentActionEffect.UnityRead | AgentActionEffect.UnityWrite |
                  AgentActionEffect.BuildArtifact | AgentActionEffect.Destructive,
        RequiredEvidence = AgentActionEvidence.Compile | AgentActionEvidence.Artifact,
        Idempotency = AgentActionIdempotency.ReplaceGeneratedOutput,
        RequiresConfirmation = true,
        RequiresEditMode = true,
        Locks = new[]
        {
            "unity-editor", "asset-database", "build-settings", "active-config-master",
            "hybridclr-generated-output", "hybridclr-strip-output", "link-xml-target",
        })]
    internal sealed class HotfixGenerateArtifactsAction : AgentActionHandler<HotfixGenerateArtifactsAction.Request>
    {
        [Serializable]
        public sealed class Request
        {
            [AgentActionRequired] public string masterGuid;
            [AgentActionRequired] public string platform;
            [AgentActionRequired] public string channel;
            [AgentActionRequired] public string developMode;
            [AgentActionRequired] public string activeBuildTarget;
            public bool developmentBuild;
        }

        private sealed class State
        {
            public Request Request;
            public BuildTarget Target;
            public PlatformType Platform;
            public ChannelType Channel;
            public DevelopMode Mode;
            public string MasterPath;
            public string MasterHash;
            public string SettingsHash;
            public string[] Scenes;
            public string[] AotAssemblies;
            public OutputSet Outputs;
        }

        [Serializable]
        private sealed class OutputSet
        {
            public string hotUpdateDirectory;
            public string strippedAotDirectory;
            public string generatedCppDirectory;
            public string methodBridgeFile;
            public string unityVersionFile;
            public string assemblyManifestFile;
            public string aotGenericReferenceFile;
            public string linkXmlFile;
            public string strippedTempProject;
            public string il2CppBuildCache;
        }

        [Serializable]
        private sealed class Receipt
        {
            public string masterGuid;
            public string masterPath;
            public string platform;
            public string channel;
            public string developMode;
            public string activeBuildTarget;
            public bool developmentBuild;
            public string[] enabledScenes;
            public string[] aotAssemblies;
            public OutputSet outputs;
            public GenerateActionCommon.Artifact[] artifacts;
        }

        protected override bool TryValidateRequest(Request request, out string error)
        {
            if (!GenerateActionCommon.TryValidateGuid(request?.masterGuid, out error)) return false;
            if (!GenerateActionCommon.TryParseCoordinate(
                    request.platform, request.channel, request.developMode,
                    out _, out _, out _, out error)) return false;
            return GenerateActionCommon.TryParseActiveBuildTarget(request.activeBuildTarget, out _, out error);
        }

        /// <summary>
        /// 只读冻结 GenerateAll 上下文、可能写集与后续可核验的精确产物。
        /// </summary>
        public override Task<AgentActionHandlerPlan> PlanAsync(Request request, AgentActionExecutionContext context)
        {
            if (!TryBuildFrozenState(request, context.CancellationToken, out State state, out string status, out string error))
            {
                return Task.FromResult(new AgentActionHandlerPlan { Status = status, Summary = error });
            }

            Receipt receipt = CreateReceipt(state, Array.Empty<GenerateActionCommon.Artifact>());
            return Task.FromResult(new AgentActionHandlerPlan
            {
                Status = "ready",
                Summary = $"将在 {state.Target} / DevelopmentBuild={request.developmentBuild} 生成 HybridCLR 全量预构建产物，再校验 {GenerateActionCommon.ToProjectRelative(state.Outputs.linkXmlFile)}。",
                DataJson = Util.Json.Serialize(receipt),
                State = state,
                WriteSet = GetWriteSet(state.Outputs),
                Evidence = new[]
                {
                    $"已只读冻结 Master={state.MasterPath}，坐标={state.Platform}/{state.Channel}/{state.Mode}。",
                    $"已冻结 {state.Scenes.Length} 个启用场景、activeBuildTarget={state.Target}、DevelopmentBuild={request.developmentBuild}。",
                    "Action 不切换 BuildTarget，也不复制 AOT/Game DLL、构建最终 Player 或 Bundle。",
                    "WriteSet 覆盖 GenerateAll 可能写入或失效的路径；Verify 只对 Receipt 中明列的目录与文件出具产物证据。",
                },
                RecoveryPayloadJson = Util.Json.Serialize(receipt),
            });
        }

        public override Task<AgentActionResult> ExecuteAsync(object state, AgentActionExecutionContext context)
        {
            if (!(state is State frozen)) return Task.FromResult(AgentActionResult.Create(null, "blocked", "HybridCLR Generate 冻结状态无效。"));
            if (!TryBuildFrozenState(frozen.Request, context.CancellationToken, out State current, out _, out string error) ||
                !FrozenStatesEqual(frozen, current))
            {
                return Task.FromResult(AgentActionResult.Create(null, "blocked", "HybridCLR Generate 上下文已漂移，请重新 Plan：" + error));
            }

            context.CancellationToken.ThrowIfCancellationRequested();
            EditorUtil.HybridCLR.GenerateAll();
            context.CancellationToken.ThrowIfCancellationRequested();
            EditorUtil.HybridCLR.ValidateLinkXml();

            if (EditorUserBuildSettings.activeBuildTarget != frozen.Target ||
                EditorUserBuildSettings.development != frozen.Request.developmentBuild)
            {
                return Task.FromResult(AgentActionResult.Create(null, "partial", "GenerateAll 已写入产物，但 Target 或 DevelopmentBuild 未保持冻结值；不会自动重放。"));
            }

            GenerateActionCommon.Artifact[] artifacts = CaptureArtifacts(frozen.Outputs, context);
            ValidateMethodBridge(frozen.Outputs.methodBridgeFile, frozen.Request.developmentBuild);
            ValidateLinkXml(frozen.Outputs.linkXmlFile, frozen.AotAssemblies);
            Receipt receipt = CreateReceipt(frozen, artifacts);
            AgentActionResult result = AgentActionResult.Create(null, "partial", "HybridCLR generate -> validate 已执行，等待 Unity 稳定后只读 Verify。" );
            result.ReceiptJson = Util.Json.Serialize(receipt);
            result.DataJson = result.ReceiptJson;
            result.Artifacts.AddRange(artifacts.Select(artifact => artifact.path));
            result.Evidence.Add("Execute 仅一次调用 GenerateAll 后调用 ValidateLinkXml；异常后不会自动重放。" );
            return Task.FromResult(result);
        }

        /// <summary>
        /// 只读核对 Receipt 明列产物；不把未捕获的整个生成目录或缓存声称为已完整验证。
        /// </summary>
        public override Task<AgentActionResult> VerifyAsync(string receiptJson, AgentActionExecutionContext context)
        {
            Receipt receipt;
            try { receipt = Util.Json.Deserialize<Receipt>(receiptJson); }
            catch (Exception exception)
            {
                return Task.FromResult(AgentActionResult.Create(null, "blocked", "HybridCLR Generate Receipt 无法解析：" + exception.Message));
            }
            if (receipt == null || receipt.outputs == null || receipt.artifacts == null)
            {
                return Task.FromResult(AgentActionResult.Create(null, "blocked", "HybridCLR Generate Receipt 不完整。"));
            }
            if (!GenerateActionCommon.TryVerifyArtifacts(receipt.artifacts, context.CancellationToken, out GenerateActionCommon.Artifact[] actual, out string artifactError))
            {
                AgentActionResult partial = AgentActionResult.Create(null, "partial", "HybridCLR 生成产物验证未通过：" + artifactError);
                partial.DataJson = Util.Json.Serialize(actual);
                return Task.FromResult(partial);
            }
            string[] exactOutputs =
            {
                receipt.outputs.hotUpdateDirectory, receipt.outputs.strippedAotDirectory,
                receipt.outputs.methodBridgeFile, receipt.outputs.unityVersionFile,
                receipt.outputs.assemblyManifestFile, receipt.outputs.aotGenericReferenceFile,
                receipt.outputs.linkXmlFile, receipt.outputs.strippedTempProject,
            };
            foreach (string output in exactOutputs)
            {
                if (!GenerateActionCommon.TryResolveProjectPath(output, "Receipt output", out string fullOutput, out string outputError) ||
                    !actual.Any(artifact => string.Equals(artifact.path, fullOutput, StringComparison.Ordinal)))
                {
                    return Task.FromResult(AgentActionResult.Create(null, "partial", outputError ?? "Receipt 产物集合未覆盖精确输出：" + output));
                }
            }

            try
            {
                ValidateMethodBridge(receipt.outputs.methodBridgeFile, receipt.developmentBuild);
                ValidateLinkXml(receipt.outputs.linkXmlFile, receipt.aotAssemblies ?? Array.Empty<string>());
            }
            catch (Exception exception)
            {
                return Task.FromResult(AgentActionResult.Create(null, "partial", "HybridCLR 生成语义验证未通过：" + exception.Message));
            }

            AgentActionResult success = AgentActionResult.Create(null, "success", "HybridCLR Receipt 明列的 3 个目录、5 个文件及其 SHA-256、MethodBridge DEVELOPMENT 与 link.xml AOT 条目已只读核对。" );
            success.EvidenceKinds = AgentActionEvidence.Compile | AgentActionEvidence.Artifact;
            success.DataJson = Util.Json.Serialize(actual);
            success.Artifacts.AddRange(actual.Select(artifact => artifact.path));
            success.Evidence.Add("Verify 只读；不会重新调用 GenerateAll、ValidateLinkXml 或切换 BuildTarget。" );
            success.Warnings.Add("generatedCppDirectory 仅核验 MethodBridge.cpp、UnityVersion.h 与 AssemblyManifest.cpp；未宣称整个目录完整。" );
            success.Warnings.Add("il2CppBuildCache 仅因 GenerateAll 可删除/失效而纳入 WriteSet，不做存在性或完整性核验。" );
            success.Warnings.Add("该证据不代表最终 Player、Bundle、运行时或设备验证成功。" );
            return Task.FromResult(success);
        }

        private static bool TryBuildFrozenState(
            Request request,
            System.Threading.CancellationToken cancellationToken,
            out State state,
            out string status,
            out string error)
        {
            state = null;
            status = "blocked";
            error = null;
            GenerateActionCommon.TryParseCoordinate(request.platform, request.channel, request.developMode,
                out PlatformType platform, out ChannelType channel, out DevelopMode mode, out _);
            GenerateActionCommon.TryParseActiveBuildTarget(request.activeBuildTarget, out BuildTarget target, out _);
            if (EditorUserBuildSettings.activeBuildTarget != target)
            {
                error = $"请求 Target={target} 与 activeBuildTarget={EditorUserBuildSettings.activeBuildTarget} 不一致；Action 不切换 Target。";
                return false;
            }
            if (EditorUserBuildSettings.development != request.developmentBuild)
            {
                error = $"请求 DevelopmentBuild={request.developmentBuild} 与 EditorUserBuildSettings.development={EditorUserBuildSettings.development} 不一致。";
                return false;
            }
            if (!SettingsUtil.Enable || !new InstallerController().HasInstalledHybridCLR())
            {
                status = "not_applicable";
                error = "当前项目未启用或未初始化 HybridCLR。";
                return false;
            }

            ConfigMasterSO master = GenerateActionCommon.ResolveMaster(request.masterGuid, out string masterPath);
            if (master == null)
            {
                error = "masterGuid 未解析到 ConfigMasterSO。";
                return false;
            }
            if (!GenerateActionCommon.TryValidateActiveMasterBinding(request.masterGuid, masterPath, out error)) return false;
            if (!GenerateActionCommon.CoordinateMatches(master, platform, channel, mode))
            {
                error = $"请求坐标与激活 ConfigMaster 当前坐标 {master.CurrentPlatform}/{master.CurrentChannel}/{master.CurrentDevelopMode} 不一致。";
                return false;
            }

            string[] scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray();
            if (scenes.Length == 0 || scenes.Any(string.IsNullOrWhiteSpace))
            {
                error = "Build Settings 没有完整的启用场景闭包，GenerateAll 会无法诚实验证。";
                return false;
            }

            EditorUtil.Config.DimensionalResolver.HybridCLRResult hybrid =
                EditorUtil.Config.DimensionalResolver.ResolveHybridCLR(master, platform, channel, mode);
            string configLink = string.IsNullOrEmpty(hybrid.LinkXmlTargetPath) ? "Assets/link.xml" : hybrid.LinkXmlTargetPath;
            if (!GenerateActionCommon.TryResolveProjectPath(configLink, "LinkXmlTargetPath", out string configLinkFull, out error)) return false;
            string settingsLinkRaw = Path.Combine(Application.dataPath, SettingsUtil.HybridCLRSettings.outputLinkFile ?? string.Empty);
            if (!GenerateActionCommon.TryResolveProjectPath(settingsLinkRaw, "HybridCLR outputLinkFile", out string settingsLinkFull, out error)) return false;
            if (!string.Equals(configLinkFull, settingsLinkFull, StringComparison.Ordinal))
            {
                error = $"HybridCLR outputLinkFile 与当前 ConfigMaster LinkXmlTargetPath 不一致：{GenerateActionCommon.ToProjectRelative(settingsLinkFull)} != {GenerateActionCommon.ToProjectRelative(configLinkFull)}。";
                return false;
            }

            if (!TryResolveOutputs(target, settingsLinkFull, out OutputSet outputs, out error)) return false;
            string[] aotAssemblies = (hybrid.AotMetadataDlls ?? new List<DllMasterAssetEntry>())
                .Select(entry => StripDllSuffix(entry.AssetLocation)).Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
            string masterHash = GenerateActionCommon.ComputeFileHash(Path.Combine(GenerateActionCommon.ProjectRoot, masterPath), cancellationToken);
            state = new State
            {
                Request = request,
                Target = target,
                Platform = platform,
                Channel = channel,
                Mode = mode,
                MasterPath = masterPath,
                MasterHash = masterHash,
                SettingsHash = ComputeSettingsHash(target, outputs),
                Scenes = scenes,
                AotAssemblies = aotAssemblies,
                Outputs = outputs,
            };
            return true;
        }

        private static bool TryResolveOutputs(BuildTarget target, string linkXml, out OutputSet outputs, out string error)
        {
            outputs = null;
            error = null;
            string genericRaw = Path.Combine(Application.dataPath, SettingsUtil.HybridCLRSettings.outputAOTGenericReferenceFile ?? string.Empty);
            string[] values =
            {
                SettingsUtil.GetHotUpdateDllsOutputDirByTarget(target), SettingsUtil.GetAssembliesPostIl2CppStripDir(target),
                SettingsUtil.GeneratedCppDir, genericRaw, linkXml,
                Path.Combine(SettingsUtil.HybridCLRDataDir, "StrippedAOTDllsTempProj", target.ToString()), SettingsUtil.Il2CppBuildCacheDir,
            };
            var resolved = new string[values.Length];
            for (int index = 0; index < values.Length; index++)
            {
                if (!GenerateActionCommon.TryResolveProjectPath(values[index], "HybridCLR output", out resolved[index], out error)) return false;
            }
            outputs = new OutputSet
            {
                hotUpdateDirectory = resolved[0],
                strippedAotDirectory = resolved[1],
                generatedCppDirectory = resolved[2],
                methodBridgeFile = Path.Combine(resolved[2], "MethodBridge.cpp"),
                unityVersionFile = Path.Combine(resolved[2], "UnityVersion.h"),
                assemblyManifestFile = Path.Combine(resolved[2], "AssemblyManifest.cpp"),
                aotGenericReferenceFile = resolved[3],
                linkXmlFile = resolved[4],
                strippedTempProject = resolved[5],
                il2CppBuildCache = resolved[6],
            };
            return true;
        }

        private static string ComputeSettingsHash(BuildTarget target, OutputSet outputs)
        {
            string value = target + "\n" + EditorUserBuildSettings.development + "\n" +
                           string.Join("\n", SettingsUtil.HotUpdateAssemblyFilesIncludePreserved ?? new List<string>()) + "\n" +
                           string.Join("\n", SettingsUtil.AOTAssemblyNames ?? new List<string>()) + "\n" +
                           Util.Json.Serialize(outputs);
            return GenerateActionCommon.ComputeTextHash(value);
        }

        private static bool FrozenStatesEqual(State expected, State actual)
        {
            return actual != null && expected.Target == actual.Target && expected.Platform == actual.Platform &&
                   expected.Channel == actual.Channel && expected.Mode == actual.Mode &&
                   expected.MasterPath == actual.MasterPath && expected.MasterHash == actual.MasterHash &&
                   expected.SettingsHash == actual.SettingsHash &&
                   expected.Scenes.SequenceEqual(actual.Scenes, StringComparer.Ordinal) &&
                   expected.AotAssemblies.SequenceEqual(actual.AotAssemblies, StringComparer.Ordinal) &&
                   Util.Json.Serialize(expected.Outputs) == Util.Json.Serialize(actual.Outputs);
        }

        private static GenerateActionCommon.Artifact[] CaptureArtifacts(OutputSet outputs, AgentActionExecutionContext context)
        {
            return new[]
            {
                GenerateActionCommon.CaptureDirectory(outputs.hotUpdateDirectory, "*", context.CancellationToken),
                GenerateActionCommon.CaptureDirectory(outputs.strippedAotDirectory, "*", context.CancellationToken),
                GenerateActionCommon.CaptureFile(outputs.methodBridgeFile, context.CancellationToken),
                GenerateActionCommon.CaptureFile(outputs.unityVersionFile, context.CancellationToken),
                GenerateActionCommon.CaptureFile(outputs.assemblyManifestFile, context.CancellationToken),
                GenerateActionCommon.CaptureFile(outputs.aotGenericReferenceFile, context.CancellationToken),
                GenerateActionCommon.CaptureFile(outputs.linkXmlFile, context.CancellationToken),
                GenerateActionCommon.CaptureDirectory(outputs.strippedTempProject, "*", context.CancellationToken),
            };
        }

        private static void ValidateMethodBridge(string path, bool developmentBuild)
        {
            if (!File.Exists(path) || !EditorUtil.HybridCLR.TryReadMethodBridgeDevelopmentBuild(File.ReadAllText(path), out bool actual) || actual != developmentBuild)
            {
                throw new InvalidOperationException("MethodBridge.cpp 的规范 DEVELOPMENT 标记与冻结 DevelopmentBuild 不一致。");
            }
        }

        private static void ValidateLinkXml(string path, IEnumerable<string> assemblies)
        {
            var document = new XmlDocument();
            document.Load(path);
            XmlElement root = document.DocumentElement;
            if (root == null || root.Name != "linker") throw new InvalidOperationException("link.xml 根节点不是 <linker>。");
            var existing = new HashSet<string>(StringComparer.Ordinal);
            foreach (XmlNode node in root.ChildNodes)
            {
                if (node is XmlElement element && element.Name == "assembly") existing.Add(element.GetAttribute("fullname"));
            }
            string missing = (assemblies ?? Array.Empty<string>()).FirstOrDefault(assembly => !existing.Contains(assembly));
            if (missing != null) throw new InvalidOperationException("link.xml 缺少 AOT metadata assembly：" + missing);
        }

        private static string StripDllSuffix(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            string trimmed = value.Trim();
            return trimmed.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
                ? trimmed.Substring(0, trimmed.Length - 4)
                : trimmed;
        }

        private static Receipt CreateReceipt(State state, GenerateActionCommon.Artifact[] artifacts)
        {
            return new Receipt
            {
                masterGuid = state.Request.masterGuid,
                masterPath = state.MasterPath,
                platform = state.Platform.ToString(),
                channel = state.Channel.ToString(),
                developMode = state.Mode.ToString(),
                activeBuildTarget = state.Target.ToString(),
                developmentBuild = state.Request.developmentBuild,
                enabledScenes = state.Scenes,
                aotAssemblies = state.AotAssemblies,
                outputs = state.Outputs,
                artifacts = artifacts,
            };
        }

        /// <summary>
        /// 返回 GenerateAll 的可能写入与失效范围。WriteSet 是边界声明，不等同于 Verify 已对每个路径出具完整性证据。
        /// </summary>
        private static string[] GetWriteSet(OutputSet outputs)
        {
            return new[]
            {
                GenerateActionCommon.ToProjectRelative(outputs.hotUpdateDirectory),
                GenerateActionCommon.ToProjectRelative(outputs.strippedAotDirectory),
                GenerateActionCommon.ToProjectRelative(outputs.generatedCppDirectory),
                GenerateActionCommon.ToProjectRelative(outputs.aotGenericReferenceFile),
                GenerateActionCommon.ToProjectRelative(outputs.linkXmlFile),
                GenerateActionCommon.ToProjectRelative(outputs.strippedTempProject),
                GenerateActionCommon.ToProjectRelative(outputs.il2CppBuildCache),
            };
        }
    }
}
