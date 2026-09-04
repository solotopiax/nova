/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.ProjectGuard.BuildReadiness.cs
 * author:    taoye
 * created:   2026/8/20
 * descrip:   Player 构建前的纯只读就绪探针
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HybridCLR.Editor;
using HybridCLR.Editor.Installer;
using NovaFramework.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using YooAsset;
using YooAsset.Editor;
using IOPath = System.IO.Path;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        public static partial class ProjectGuard
        {
            /// <summary>
            /// 构建就绪规则的判定状态；Error 才会令报告不可构建。
            /// </summary>
            internal enum BuildReadinessRuleStatus
            {
                Pass,
                Warning,
                Error,
                NotApplicable,
            }

            /// <summary>
            /// 单条稳定构建就绪规则及其可复核证据。
            /// </summary>
            [Serializable]
            internal sealed class BuildReadinessRule
            {
                public string id;
                public string area;
                public string status;
                public string message;
                public string evidence;
            }

            /// <summary>
            /// 构建前纯只读探针的结构化快照；不代表实际构建或真机运行成功。
            /// </summary>
            [Serializable]
            internal sealed class BuildReadinessReport
            {
                public string target;
                public string activeBuildTarget;
                public string targetGroup;
                public string[] enabledScenes = Array.Empty<string>();
                public string masterGuid;
                public string masterAssetPath;
                public string platform;
                public string channel;
                public string developMode;
                public string runtimeConfigPath;
                public string yooAssetSettingsPath;
                public string bundleCollectorPath;
                public string packageName;
                public bool hybridClrEnabled;
                public bool hybridClrInstalled;
                public string linkXmlPath;
                public bool ready;
                public int errorCount;
                public int warningCount;
                public BuildReadinessRule[] rules = Array.Empty<BuildReadinessRule>();
            }

            /// <summary>
            /// 读取当前构建输入并执行纯检查。该入口不调用 WorkspaceActive.Get、ValidateBuild、
            /// SessionState、保存 API 或任何构建/生成入口。
            /// </summary>
            internal static BuildReadinessReport InspectBuildReadiness(BuildTarget target, string requestedPackageName)
            {
                var result = new BuildReadinessReport
                {
                    target = target.ToString(),
                    activeBuildTarget = EditorUserBuildSettings.activeBuildTarget.ToString(),
                    targetGroup = BuildPipeline.GetBuildTargetGroup(target).ToString(),
                };
                var rules = new List<BuildReadinessRule>();

                InspectTarget(target, result, rules);
                InspectScenes(result, rules);
                ConfigMasterSO master = InspectActiveConfig(target, result, rules);
                if (master != null)
                {
                    InspectSceneChannels(result.enabledScenes, master.ExportTarget, rules);
                    InspectBundle(master, requestedPackageName, result, rules);
                    InspectHybridClr(master, result, rules);
                }
                else
                {
                    AddRule(rules, "NOVA-BUILD-007", "bundle", BuildReadinessRuleStatus.Error,
                        "无法检查 Bundle Collector 与 Package。", "当前没有可验证的激活 ConfigMaster。");
                    AddRule(rules, "NOVA-BUILD-009", "hybridclr", BuildReadinessRuleStatus.Error,
                        "无法检查 HybridCLR 构建前置。", "当前没有可验证的激活 ConfigMaster。");
                }

                result.rules = rules
                    .OrderBy(rule => rule.id, StringComparer.Ordinal)
                    .ThenBy(rule => rule.status, StringComparer.Ordinal)
                    .ThenBy(rule => rule.evidence, StringComparer.Ordinal)
                    .ToArray();
                result.errorCount = result.rules.Count(rule => rule.status == BuildReadinessRuleStatus.Error.ToString());
                result.warningCount = result.rules.Count(rule => rule.status == BuildReadinessRuleStatus.Warning.ToString());
                result.ready = result.errorCount == 0;
                return result;
            }

            /// <summary>
            /// 检查目标平台已激活且当前 Unity 安装具备对应 Build Support。
            /// </summary>
            private static void InspectTarget(BuildTarget target, BuildReadinessReport result,
                ICollection<BuildReadinessRule> rules)
            {
                bool active = target == EditorUserBuildSettings.activeBuildTarget;
                AddRule(rules, "NOVA-BUILD-001", "target",
                    active ? BuildReadinessRuleStatus.Pass : BuildReadinessRuleStatus.Error,
                    active ? "目标平台与 activeBuildTarget 一致。" : "目标平台尚未切换为当前活动平台。",
                    $"target={target}; activeBuildTarget={EditorUserBuildSettings.activeBuildTarget}");

                BuildTargetGroup group = BuildPipeline.GetBuildTargetGroup(target);
                bool supported = group != BuildTargetGroup.Unknown && BuildPipeline.IsBuildTargetSupported(group, target);
                AddRule(rules, "NOVA-BUILD-002", "target",
                    supported ? BuildReadinessRuleStatus.Pass : BuildReadinessRuleStatus.Error,
                    supported ? "Unity 已安装目标平台 Build Support。" : "Unity 未报告目标平台 Build Support 可用。",
                    $"targetGroup={result.targetGroup}; supported={supported}");
            }

            /// <summary>
            /// 冻结启用场景闭包，并复用 ProjectGuard 的无配置导航场景规则。
            /// </summary>
            private static void InspectScenes(BuildReadinessReport result, ICollection<BuildReadinessRule> rules)
            {
                result.enabledScenes = EditorBuildSettings.scenes
                    .Where(scene => scene.enabled)
                    .Select(scene => NormalizePath(scene.path))
                    .ToArray();
                bool complete = result.enabledScenes.Length > 0 &&
                                result.enabledScenes.All(path => !string.IsNullOrWhiteSpace(path) &&
                                                                 !string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(path)));
                AddRule(rules, "NOVA-BUILD-003", "scenes",
                    complete ? BuildReadinessRuleStatus.Pass : BuildReadinessRuleStatus.Error,
                    complete ? "Build Settings 已声明完整的启用场景闭包。" : "Build Settings 缺少可加载的启用场景。",
                    result.enabledScenes.Length == 0 ? "enabledScenes=0" : string.Join(";", result.enabledScenes));

                if (!complete) return;
                var sceneReport = new NovaGuardReport();
                ValidateScenes(result.enabledScenes, true, true, false, sceneReport);
                string[] managedRoots = result.enabledScenes
                    .Select(path => NormalizePath(IOPath.GetDirectoryName(path)))
                    .Where(path => !string.IsNullOrWhiteSpace(path))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                ValidateResources(managedRoots, sceneReport);
                foreach (NovaGuardIssue issue in sceneReport.Issues)
                {
                    AddRule(rules, issue.RuleId, "scenes", ToBuildStatus(issue.Severity),
                        FirstLine(issue.Message), issue.AssetPath);
                }
            }

            /// <summary>
            /// 通过 WorkspaceActive 的唯一只读解析入口读取当前持久化 Master。
            /// 不执行场景路由或 pathHint 修复。
            /// </summary>
            private static ConfigMasterSO InspectActiveConfig(BuildTarget target, BuildReadinessReport result,
                ICollection<BuildReadinessRule> rules)
            {
                bool bindingValid = EditorUtil.Config.WorkspaceActive.TryGetPersistedConfigMaster(
                    out ConfigMasterSO master, out string masterGuid, out string masterPath, out string bindingError);
                result.masterGuid = masterGuid;
                result.masterAssetPath = masterPath;
                AddRule(rules, "NOVA-BUILD-004", "config",
                    bindingValid ? BuildReadinessRuleStatus.Pass : BuildReadinessRuleStatus.Error,
                    bindingValid ? "Globals.json 已精确绑定可加载的 ConfigMaster。" : "激活 ConfigMaster 的 GUID、pathHint 或资产身份无效。",
                    bindingValid ? $"guid={masterGuid}; path={masterPath}" : bindingError);
                if (!bindingValid) return null;

                result.platform = master.CurrentPlatform.ToString();
                result.channel = master.CurrentChannel.ToString();
                result.developMode = master.CurrentDevelopMode.ToString();
                bool coordinateValid = master.CurrentPlatform != PlatformType.None &&
                                       master.TryGetEntry(master.CurrentPlatform, master.CurrentChannel, out _);
                AddRule(rules, "NOVA-BUILD-005", "config",
                    coordinateValid ? BuildReadinessRuleStatus.Pass : BuildReadinessRuleStatus.Error,
                    coordinateValid ? "ConfigMaster 当前三维坐标存在。" : "ConfigMaster 当前三维坐标无效或缺少矩阵行。",
                    $"coordinate={result.platform}/{result.channel}/{result.developMode}");

                if (TryMapTargetPlatform(target, out PlatformType expectedPlatform))
                {
                    bool matched = master.CurrentPlatform == expectedPlatform;
                    AddRule(rules, "NOVA-BUILD-006", "config",
                        matched ? BuildReadinessRuleStatus.Pass : BuildReadinessRuleStatus.Error,
                        matched ? "Config 平台与 BuildTarget 一致。" : "Config 平台与 BuildTarget 不一致。",
                        $"target={target}; configPlatform={master.CurrentPlatform}");
                }
                else
                {
                    AddRule(rules, "NOVA-BUILD-006", "config", BuildReadinessRuleStatus.NotApplicable,
                        "当前 BuildTarget 没有对应的 Nova PlatformType。",
                        $"target={target}; configPlatform={master.CurrentPlatform}");
                }

                ConfigRuntimeSO runtime = master.ExportTarget;
                result.runtimeConfigPath = runtime == null ? string.Empty : NormalizePath(AssetDatabase.GetAssetPath(runtime));
                bool runtimeMatched = runtime != null && runtime.Platform == master.CurrentPlatform &&
                                      runtime.Channel == master.CurrentChannel && runtime.DevelopMode == master.CurrentDevelopMode;
                AddRule(rules, "NOVA-BUILD-010", "config",
                    runtimeMatched ? BuildReadinessRuleStatus.Pass : BuildReadinessRuleStatus.Error,
                    runtimeMatched ? "ConfigRuntimeSO 与当前 ConfigMaster 坐标一致。" : "ConfigRuntimeSO 缺失或导出坐标已漂移。",
                    $"runtime={result.runtimeConfigPath}; coordinate={runtime?.Platform}/{runtime?.Channel}/{runtime?.DevelopMode}");
                return master;
            }

            /// <summary>
            /// 检查已保存场景中的 Asset/App 渠道快照是否与本次导出的 ConfigRuntime 一致。
            /// </summary>
            private static void InspectSceneChannels(string[] scenePaths, ConfigRuntimeSO runtime,
                ICollection<BuildReadinessRule> rules)
            {
                if (runtime == null)
                {
                    AddRule(rules, "NOVA-BUILD-012", "config", BuildReadinessRuleStatus.Error,
                        "无法检查场景渠道快照，请先重新导出 ConfigRuntimeSO。",
                        "ConfigRuntimeSO=null");
                    return;
                }

                rules.Add(InspectSceneChannelsForDiagnostics(scenePaths, runtime.Channel));
            }

            /// <summary>
            /// 只读打开指定场景并生成渠道快照一致性规则，供构建检查与回归测试共用。
            /// </summary>
            internal static BuildReadinessRule InspectSceneChannelsForDiagnostics(
                string[] scenePaths, ChannelType runtimeChannel)
            {
                var evidence = new List<string>();
                int componentCount = 0;
                int mismatchCount = 0;

                foreach (string rawPath in scenePaths ?? Array.Empty<string>())
                {
                    string path = NormalizePath(rawPath);
                    if (string.IsNullOrWhiteSpace(path))
                        continue;

                    Scene scene = default;
                    try
                    {
                        scene = EditorSceneManager.OpenPreviewScene(path);
                        foreach (GameObject root in scene.GetRootGameObjects())
                        {
                            foreach (AssetComponent component in root.GetComponentsInChildren<AssetComponent>(true))
                            {
                                InspectComponentChannel(component, path, runtimeChannel,
                                    evidence, ref componentCount, ref mismatchCount);
                            }
                            foreach (AppComponent component in root.GetComponentsInChildren<AppComponent>(true))
                            {
                                InspectComponentChannel(component, path, runtimeChannel,
                                    evidence, ref componentCount, ref mismatchCount);
                            }
                        }
                    }
                    catch (Exception exception)
                    {
                        mismatchCount++;
                        evidence.Add($"scene={path}; error={exception.Message}");
                    }
                    finally
                    {
                        if (scene.IsValid())
                            EditorSceneManager.ClosePreviewScene(scene);
                    }
                }

                if (componentCount == 0 && mismatchCount == 0)
                {
                    return CreateRule("NOVA-BUILD-012", "config", BuildReadinessRuleStatus.NotApplicable,
                        "启用场景中没有可检查的 Asset/App 渠道快照。",
                        $"runtimeChannel={runtimeChannel}; components=0");
                }

                bool matched = mismatchCount == 0;
                return CreateRule("NOVA-BUILD-012", "config",
                    matched ? BuildReadinessRuleStatus.Pass : BuildReadinessRuleStatus.Error,
                    matched
                        ? "场景中的 Asset/App 渠道快照与 ConfigRuntimeSO 一致。"
                        : "场景渠道快照与 ConfigRuntimeSO 不一致，请在 Nova/Open Config 重新导出并保存场景后再构建。",
                    $"runtimeChannel={runtimeChannel}; components={componentCount}; mismatches={mismatchCount}; " +
                    string.Join("; ", evidence));
            }

            /// <summary>
            /// 读取单个 Asset/App 组件的隐藏渠道快照并累计一致性证据。
            /// </summary>
            private static void InspectComponentChannel(Component component, string scenePath,
                ChannelType runtimeChannel, ICollection<string> evidence,
                ref int componentCount, ref int mismatchCount)
            {
                componentCount++;
                var serializedComponent = new SerializedObject(component);
                SerializedProperty channelProperty = serializedComponent.FindProperty("m_Channel");
                string componentPath = AnimationUtility.CalculateTransformPath(component.transform, null);
                if (channelProperty == null)
                {
                    mismatchCount++;
                    evidence.Add($"scene={scenePath}; component={component.GetType().Name}; object={componentPath}; channel=<missing>");
                    return;
                }

                var sceneChannel = (ChannelType)channelProperty.enumValueIndex;
                if (sceneChannel != runtimeChannel)
                    mismatchCount++;
                evidence.Add($"scene={scenePath}; component={component.GetType().Name}; object={componentPath}; channel={sceneChannel}");
            }

            /// <summary>
            /// 检查当前坐标解析出的 YooAsset 资产和目标 Package 结构，不加载构建缓存。
            /// </summary>
            private static void InspectBundle(ConfigMasterSO master, string requestedPackageName,
                BuildReadinessReport result, ICollection<BuildReadinessRule> rules)
            {
                EditorUtil.Config.DimensionalResolver.YooAssetResult resolved =
                    EditorUtil.Config.DimensionalResolver.ResolveYooAsset(
                        master, master.CurrentPlatform, master.CurrentChannel, master.CurrentDevelopMode);
                result.yooAssetSettingsPath = NormalizePath(resolved.YooAssetSettingsPath);
                result.bundleCollectorPath = NormalizePath(resolved.BundleCollectorSettingPath);
                YooAssetSettings settings = string.IsNullOrEmpty(result.yooAssetSettingsPath)
                    ? null
                    : AssetDatabase.LoadAssetAtPath<YooAssetSettings>(result.yooAssetSettingsPath);
                BundleCollectorSetting collector = string.IsNullOrEmpty(result.bundleCollectorPath)
                    ? null
                    : AssetDatabase.LoadAssetAtPath<BundleCollectorSetting>(result.bundleCollectorPath);
                bool assetsReady = settings != null && collector != null;
                AddRule(rules, "NOVA-BUILD-007", "bundle",
                    assetsReady ? BuildReadinessRuleStatus.Pass : BuildReadinessRuleStatus.Error,
                    assetsReady ? "当前坐标已解析到 YooAssetSettings 与 BundleCollectorSetting。" : "当前坐标缺少可加载的 YooAsset 配置资产。",
                    $"settings={result.yooAssetSettingsPath}; collector={result.bundleCollectorPath}");

                result.packageName = string.IsNullOrWhiteSpace(requestedPackageName)
                    ? EditorUtil.Placeholder.ResolveDefaultPackageName()
                    : requestedPackageName;
                int packageCount = collector?.Packages?.Count(item => item != null &&
                    string.Equals(item.PackageName, result.packageName, StringComparison.Ordinal)) ?? 0;
                BundleCollectorPackage package = packageCount == 1 ? collector.GetPackage(result.packageName) : null;
                string packageError = null;
                bool packageReady = packageCount == 1 && TryValidateBundlePackageStructure(package, out packageError);
                AddRule(rules, "NOVA-BUILD-008", "bundle",
                    packageReady ? BuildReadinessRuleStatus.Pass : BuildReadinessRuleStatus.Error,
                    packageReady ? "目标 YooAsset Package 的 Collector 结构有效。" : "目标 YooAsset Package 缺失、不唯一或结构无效。",
                    packageCount == 1 ? packageError ?? $"package={result.packageName}" :
                    $"package={result.packageName ?? "<empty>"}; count={packageCount}");
            }

            /// <summary>
            /// 检查 HybridCLR 启用、安装、场景和 link.xml 配置一致性，不生成任何产物。
            /// </summary>
            private static void InspectHybridClr(ConfigMasterSO master, BuildReadinessReport result,
                ICollection<BuildReadinessRule> rules)
            {
                try
                {
                    result.hybridClrEnabled = SettingsUtil.Enable;
                    result.hybridClrInstalled = result.hybridClrEnabled && new InstallerController().HasInstalledHybridCLR();
                }
                catch (Exception exception)
                {
                    AddRule(rules, "NOVA-BUILD-009", "hybridclr", BuildReadinessRuleStatus.Error,
                        "HybridCLR 设置或安装状态无法读取。", exception.Message);
                    return;
                }
                if (!result.hybridClrEnabled)
                {
                    AddRule(rules, "NOVA-BUILD-009", "hybridclr", BuildReadinessRuleStatus.NotApplicable,
                        "当前项目未启用 HybridCLR。", "SettingsUtil.Enable=false");
                    return;
                }

                AddRule(rules, "NOVA-BUILD-009", "hybridclr",
                    result.hybridClrInstalled ? BuildReadinessRuleStatus.Pass : BuildReadinessRuleStatus.Error,
                    result.hybridClrInstalled ? "HybridCLR 已启用并完成安装。" : "HybridCLR 已启用但安装状态未就绪。",
                    $"enabled={result.hybridClrEnabled}; installed={result.hybridClrInstalled}");
                if (!result.hybridClrInstalled) return;

                EditorUtil.Config.DimensionalResolver.HybridCLRResult hybrid =
                    EditorUtil.Config.DimensionalResolver.ResolveHybridCLR(
                        master, master.CurrentPlatform, master.CurrentChannel, master.CurrentDevelopMode);
                string configLink = string.IsNullOrWhiteSpace(hybrid.LinkXmlTargetPath)
                    ? "Assets/link.xml"
                    : NormalizePath(hybrid.LinkXmlTargetPath);
                string settingsLink = NormalizePath(IOPath.Combine("Assets", SettingsUtil.HybridCLRSettings.outputLinkFile ?? string.Empty));
                result.linkXmlPath = configLink;
                bool linkMatched = string.Equals(configLink, settingsLink, StringComparison.Ordinal);
                AddRule(rules, "NOVA-BUILD-011", "hybridclr",
                    linkMatched ? BuildReadinessRuleStatus.Pass : BuildReadinessRuleStatus.Error,
                    linkMatched ? "ConfigMaster 与 HybridCLR 的 link.xml 输出路径一致。" : "ConfigMaster 与 HybridCLR 的 link.xml 输出路径不一致。",
                    $"config={configLink}; hybridclr={settingsLink}");
            }

            /// <summary>
            /// 验证 YooAsset Package、Group 与 Collector 的必需规则均可由当前版本解析。
            /// </summary>
            internal static bool TryValidateBundlePackageStructure(BundleCollectorPackage package, out string error)
            {
                error = null;
                if (package == null)
                {
                    error = "Package 为空。";
                    return false;
                }
                if (string.IsNullOrWhiteSpace(package.IgnoreRuleName) ||
                    !BundleCollectorSettingData.HasAssetIgnoreRuleName(package.IgnoreRuleName))
                {
                    error = "IgnoreRuleName 无效：" + package.IgnoreRuleName;
                    return false;
                }
                if (package.Groups == null)
                {
                    error = "Groups 为空。";
                    return false;
                }

                foreach (BundleCollectorGroup group in package.Groups)
                {
                    if (group == null || string.IsNullOrWhiteSpace(group.ActiveRuleName) ||
                        !BundleCollectorSettingData.HasGroupActiveRuleName(group.ActiveRuleName) || group.Collectors == null)
                    {
                        error = "Group 或 ActiveRuleName/Collectors 无效。";
                        return false;
                    }
                    foreach (BundleCollector collector in group.Collectors)
                    {
                        if (collector == null || string.IsNullOrWhiteSpace(collector.CollectPath) ||
                            string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(collector.CollectPath)) ||
                            collector.CollectorType == ECollectorType.None ||
                            !BundleCollectorSettingData.HasBundlePackRuleName(collector.PackRuleName) ||
                            !BundleCollectorSettingData.HasAssetFilterRuleName(collector.FilterRuleName) ||
                            !BundleCollectorSettingData.HasAddressRuleName(collector.AddressRuleName))
                        {
                            error = "Collector 路径或规则无效：" + collector?.CollectPath;
                            return false;
                        }
                    }
                }
                return true;
            }

            /// <summary>
            /// 将 BuildTarget 映射为 Nova 配置平台；桌面等未建模平台返回 false。
            /// </summary>
            private static bool TryMapTargetPlatform(BuildTarget target, out PlatformType platform)
            {
                platform = EditorUtil.Config.ActivePlatform.FromBuildTarget(target);
                return platform != PlatformType.None;
            }

            /// <summary>
            /// 将现有 ProjectGuard 严重度映射到构建就绪规则状态。
            /// </summary>
            private static BuildReadinessRuleStatus ToBuildStatus(NovaGuardSeverity severity)
            {
                return severity == NovaGuardSeverity.Error
                    ? BuildReadinessRuleStatus.Error
                    : severity == NovaGuardSeverity.Warning
                        ? BuildReadinessRuleStatus.Warning
                        : BuildReadinessRuleStatus.Pass;
            }

            /// <summary>
            /// 添加一条完整、稳定且不含敏感配置值的规则证据。
            /// </summary>
            private static void AddRule(ICollection<BuildReadinessRule> rules, string id, string area,
                BuildReadinessRuleStatus status, string message, string evidence)
            {
                rules.Add(CreateRule(id, area, status, message, evidence));
            }

            /// <summary>
            /// 创建一条完整、稳定且不含敏感配置值的规则。
            /// </summary>
            private static BuildReadinessRule CreateRule(string id, string area,
                BuildReadinessRuleStatus status, string message, string evidence)
            {
                return new BuildReadinessRule
                {
                    id = id,
                    area = area,
                    status = status.ToString(),
                    message = message ?? string.Empty,
                    evidence = evidence ?? string.Empty,
                };
            }

            /// <summary>
            /// 只保留既有 Guard 消息的人类摘要，避免把多行导航上下文重复嵌入 Action 结果。
            /// </summary>
            private static string FirstLine(string value)
            {
                if (string.IsNullOrWhiteSpace(value)) return string.Empty;
                int end = value.IndexOf('\n');
                return end < 0 ? value : value.Substring(0, end);
            }

            /// <summary>
            /// 将项目内绝对路径转换为稳定的项目相对证据。
            /// </summary>
            private static string ToProjectRelative(string path, string projectRoot)
            {
                string full = IOPath.GetFullPath(path);
                string root = IOPath.GetFullPath(projectRoot).TrimEnd(IOPath.DirectorySeparatorChar, IOPath.AltDirectorySeparatorChar);
                return full.StartsWith(root + IOPath.DirectorySeparatorChar, StringComparison.Ordinal)
                    ? full.Substring(root.Length + 1).Replace('\\', '/')
                    : full.Replace('\\', '/');
            }
        }
    }
}
