/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.ProjectGuard.ConfigRule.cs
 * author:    taoye
 * created:   2026/7/30
 * descrip:   Nova 项目规范守卫启动配置就绪规则
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NovaFramework.Runtime;
using UnityEditor;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        public static partial class ProjectGuard
        {
            private const int c_AesSecretByteLength = 16;
            private static ConfigMasterSO s_LastConfigMaster;
            private static ConfigRuntimeSO s_LastConfigRuntime;
            private static ConfigNavigationSection s_LastConfigSection;
            private static Type s_LastConfigType;
            private static bool s_HasLastConfigNavigation;

            private enum ConfigNavigationSection
            {
                App,
                Privacy,
                Namespace,
                SDK,
                Kit,
            }

            /// <summary>
            /// 从当前 Scene 的 ConfigComponent 读取运行时地址，并沿 WorkspaceActive 锚点定位配置来源。
            /// </summary>
            /// <param name="configComponent">场景中启用的配置组件。</param>
            /// <param name="scenePath">关联场景路径。</param>
            /// <param name="report">问题收集报告。</param>
            private static void ValidateConfigComponent(ConfigComponent configComponent, string scenePath,
                NovaGuardReport report)
            {
                var componentObject = new SerializedObject(configComponent);
                string assetLocation = componentObject.FindProperty("m_AssetLocation")?.stringValue;
                if (string.IsNullOrWhiteSpace(assetLocation))
                {
                    AddConfigSourceIssue(report,
                        "Nova.prefab 还没有填写运行时配置地址。",
                        "在 Nova.prefab 的 ConfigComponent 中填写 ConfigRuntimeSO Asset 地址。",
                        "技术信息：ConfigComponent.m_AssetLocation 为空。", scenePath);
                    return;
                }

                ValidateConfigSource(assetLocation, Config.WorkspaceActive.Get(), scenePath, report);
            }

            /// <summary>
            /// 以工程级激活 ConfigMaster 为唯一设计态锚点，并通过 ExportTarget 精确定位运行时导出物。
            /// </summary>
            /// <param name="assetLocation">ConfigComponent 声明的运行时配置地址。</param>
            /// <param name="master">当前激活的项目配置。</param>
            /// <param name="scenePath">关联场景路径。</param>
            /// <param name="report">问题收集报告。</param>
            private static void ValidateConfigSource(string assetLocation, ConfigMasterSO master,
                string scenePath, NovaGuardReport report)
            {
                if (master == null)
                {
                    AddConfigSourceIssue(report,
                        "当前项目还没有选择配置。",
                        "打开 Nova/Open Config，选择当前项目配置。",
                        $"技术信息：当前工程未激活 ConfigMasterSO。\n配置出处：{scenePath} → ConfigComponent.m_AssetLocation",
                        scenePath);
                    return;
                }

                string masterPath = AssetDatabase.GetAssetPath(master);
                ConfigRuntimeSO runtime = master.ExportTarget;
                if (runtime == null)
                {
                    AddConfigSourceIssue(report,
                        "当前项目配置还没有设置导出目标。",
                        "打开 Nova/Open Config，设置导出目标后重新导出。",
                        $"技术信息：当前激活 ConfigMasterSO.ExportTarget 为空。\n" +
                        $"设计态来源：{DisplayPath(masterPath)}\n" +
                        $"配置出处：{scenePath} → ConfigComponent.m_AssetLocation", masterPath);
                    return;
                }

                string runtimePath = AssetDatabase.GetAssetPath(runtime);
                if (!string.Equals(runtime.name, assetLocation, StringComparison.Ordinal))
                {
                    AddConfigSourceIssue(report,
                        "启动场景指定的配置与当前项目配置不一致。",
                        "打开 Nova/Open Config，确认导出目标，并让 Nova.prefab 使用同一份运行时配置。",
                        $"技术信息：ConfigComponent.m_AssetLocation 为 [{assetLocation}]，但当前激活 ConfigMasterSO.ExportTarget 指向 [{runtime.name}]。\n" +
                        $"配置出处：{scenePath} → ConfigComponent.m_AssetLocation\n" +
                        $"设计态来源：{DisplayPath(masterPath)}\n运行时导出：{DisplayPath(runtimePath)}", runtimePath);
                    return;
                }

                s_LastConfigMaster = master;
                s_LastConfigRuntime = runtime;
                ValidateConfigExport(runtime, master, runtimePath, masterPath, report);
                ValidateConfigRuntime(runtime, runtimePath, masterPath, report);
            }

            /// <summary>
            /// 清理上一次 Guard 留下的 ConfigWindow 导航上下文，避免场景切换后跳转旧资产。
            /// </summary>
            private static void ResetLastConfigSource()
            {
                s_LastConfigMaster = null;
                s_LastConfigRuntime = null;
                s_LastConfigSection = ConfigNavigationSection.App;
                s_LastConfigType = null;
                s_HasLastConfigNavigation = false;
            }

            /// <summary>
            /// 打开最近一次启动配置检查对应的 ConfigWindow 应用配置面板。
            /// </summary>
            private static void OpenLastConfigSource()
            {
                if (s_LastConfigMaster == null || s_LastConfigRuntime == null)
                {
                    ConfigWindow.Open();
                    return;
                }

                PlatformType platform = s_LastConfigRuntime.Platform;
                ChannelType channel = s_LastConfigRuntime.Channel;
                DevelopMode developMode = s_LastConfigRuntime.DevelopMode;
                switch (s_LastConfigSection)
                {
                    case ConfigNavigationSection.Privacy:
                        ConfigWindow.OpenPrivacyConfigSection(s_LastConfigMaster, platform, channel, developMode);
                        break;
                    case ConfigNavigationSection.Namespace:
                        ConfigWindow.OpenNamespaceConfigSection(s_LastConfigMaster, platform, channel, developMode);
                        break;
                    case ConfigNavigationSection.SDK when s_LastConfigType != null:
                        ConfigWindow.OpenSDKConfigSection(s_LastConfigMaster, platform, channel, developMode,
                            s_LastConfigType);
                        break;
                    case ConfigNavigationSection.Kit when s_LastConfigType != null:
                        ConfigWindow.OpenKitConfigSection(s_LastConfigMaster, platform, channel, developMode,
                            s_LastConfigType);
                        break;
                    default:
                        ConfigWindow.OpenAppConfigSection(s_LastConfigMaster, platform, channel, developMode);
                        break;
                }
            }

            /// <summary>
            /// 校验已导出的运行时配置是否满足启动要求，并为每个异常字段给出设计态入口与导出来源。
            /// </summary>
            /// <param name="runtime">当前 Demo 实际使用的 ConfigRuntimeSO。</param>
            /// <param name="runtimePath">运行时导出资产路径。</param>
            /// <param name="masterPath">对应 ConfigMasterSO 设计态来源路径。</param>
            /// <param name="report">问题收集报告。</param>
            private static void ValidateConfigRuntime(ConfigRuntimeSO runtime, string runtimePath,
                string masterPath, NovaGuardReport report)
            {
                if (runtime == null)
                {
                    AddConfigSourceIssue(report,
                        "找不到启动场景需要的运行时配置。",
                        "检查 Nova.prefab 的 ConfigComponent 地址和配置导出结果。",
                        "技术信息：未找到当前 Demo 实际使用的 ConfigRuntimeSO。", runtimePath);
                    return;
                }

                AppConfigs appConfigs = runtime.AppConfigs;
                if (appConfigs == null)
                {
                    AddConfigIssue(report, "NOVA-CONFIG-003", runtime, runtimePath, masterPath,
                        "AppConfigs", "导出值为 null。", "Nova/Open Config → 通用配置 → 应用配置");
                    return;
                }

                ValidateAppId(appConfigs.AppID, runtime, runtimePath, masterPath, report);
                ValidateAesField("AppAesKey", appConfigs.AppAesKey, runtime, runtimePath, masterPath, report);
                ValidateAesField("AppAesIV", appConfigs.AppAesIV, runtime, runtimePath, masterPath, report);
                PrivacyConfigs privacyConfigs = runtime.PrivacyConfigs;
                if (privacyConfigs == null)
                {
                    AddConfigIssue(report, "NOVA-CONFIG-003", runtime, runtimePath, masterPath,
                        "PrivacyConfigs", "导出值为 null。", "Nova/Open Config → 通用配置 → 隐私配置",
                        ConfigNavigationSection.Privacy);
                }
                else
                {
                    ValidatePrivacyAesField("AESKey", privacyConfigs.AESKey, runtime, runtimePath, masterPath, report);
                    ValidatePrivacyAesField("AESIV", privacyConfigs.AESIV, runtime, runtimePath, masterPath, report);
                }
                if (string.IsNullOrWhiteSpace(runtime.Namespace))
                {
                    AddConfigIssue(report, "NOVA-CONFIG-003", runtime, runtimePath, masterPath,
                        "Namespace", "必填字段为空。",
                        "Nova/Open Config → 通用配置 → 名字空间配置");
                }
                ValidateExportedPlaceholders(runtime, runtimePath, masterPath, report);
            }

            /// <summary>
            /// 校验设计态 AppConfigs 与运行时导出物是否一致，避免项目组修改 ConfigMasterSO 后漏点导出。
            /// </summary>
            /// <param name="runtime">当前运行时配置。</param>
            /// <param name="master">当前激活的项目配置。</param>
            /// <param name="runtimePath">运行时导出资产路径。</param>
            /// <param name="masterPath">对应 ConfigMasterSO 设计态来源路径。</param>
            /// <param name="report">问题收集报告。</param>
            private static void ValidateConfigExport(ConfigRuntimeSO runtime, ConfigMasterSO master,
                string runtimePath, string masterPath, NovaGuardReport report)
            {
                if (master == null)
                {
                    AddConfigSourceIssue(report,
                        "找不到运行时配置对应的项目配置。",
                        "打开 Nova/Open Config，确认当前项目配置的导出目标。",
                        $"技术信息：找不到导出到 {DisplayPath(runtimePath)} 的 ConfigMasterSO。", runtimePath);
                    return;
                }

                if (master.ExportTarget != runtime)
                {
                    AddConfigSourceIssue(report,
                        "当前项目配置的导出目标与启动场景使用的配置不一致。",
                        "打开 Nova/Open Config，确认导出目标，并让 Nova.prefab 使用同一份运行时配置。",
                        $"技术信息：ConfigMasterSO.ExportTarget 未指向当前 Demo 使用的 ConfigRuntimeSO。\n" +
                        $"设计态来源：{DisplayPath(masterPath)}\n运行时导出：{DisplayPath(runtimePath)}", runtimePath);
                    return;
                }

                if (!master.TryGetEntry(runtime.Platform, runtime.Channel, out PlatformChannelEntry entry))
                {
                    AddConfigIssue(report, "NOVA-CONFIG-004", runtime, runtimePath, masterPath,
                        "AppConfigs", "ConfigMasterSO 中不存在当前导出坐标。请补齐配置并重新导出。",
                        "Nova/Open Config → 通用配置 → 应用配置");
                    return;
                }

                AppConfigs source = null;
                for (int i = 0; i < entry.AppConfigsByMode.Count; i++)
                {
                    DevelopModeAppConfigsEntry modeEntry = entry.AppConfigsByMode[i];
                    if (modeEntry != null && modeEntry.Mode == runtime.DevelopMode)
                    {
                        source = modeEntry.Config;
                        break;
                    }
                }

                var changedFields = new List<string>();
                CompareField(changedFields, "AppID", source?.AppID, runtime.AppConfigs?.AppID);
                CompareField(changedFields, "AppAesKey", source?.AppAesKey, runtime.AppConfigs?.AppAesKey);
                CompareField(changedFields, "AppAesIV", source?.AppAesIV, runtime.AppConfigs?.AppAesIV);
                CompareField(changedFields, "CustomConfigCmdName", source?.CustomConfigCmdName,
                    runtime.AppConfigs?.CustomConfigCmdName);
                CompareField(changedFields, "CustomName", source?.CustomName, runtime.AppConfigs?.CustomName);
                if (changedFields.Count > 0)
                {
                    AddConfigIssue(report, "NOVA-CONFIG-004", runtime, runtimePath, masterPath,
                        string.Join("、", changedFields),
                        "ConfigMasterSO 已修改但 ConfigRuntimeSO 尚未重新导出。请保存并重新导出当前坐标。",
                        "Nova/Open Config → 通用配置 → 应用配置");
                }

                PrivacyConfigs sourcePrivacy = entry.GetPrivacyConfigs(runtime.DevelopMode);
                var changedPrivacyFields = new List<string>();
                ComparePrivacyField(changedPrivacyFields, "AESKey", sourcePrivacy?.AESKey, runtime.PrivacyConfigs?.AESKey);
                ComparePrivacyField(changedPrivacyFields, "AESIV", sourcePrivacy?.AESIV, runtime.PrivacyConfigs?.AESIV);
                if (changedPrivacyFields.Count > 0)
                {
                    AddConfigIssue(report, "NOVA-CONFIG-004", runtime, runtimePath, masterPath,
                        string.Join("、", changedPrivacyFields),
                        "ConfigMasterSO 隐私配置已修改但 ConfigRuntimeSO 尚未重新导出。请保存并重新导出当前坐标。",
                        "Nova/Open Config → 通用配置 → 隐私配置", ConfigNavigationSection.Privacy);
                }

                ValidateEnabledTypeExport("SDK", "EnabledSDKConfigs", master.EnabledSDKs,
                    runtime.EnabledSDKConfigs.Where(config => config != null)
                        .Select(config => config.GetType().FullName),
                    runtime, runtimePath, masterPath, report);
                ValidateEnabledTypeExport("Kit", "EnabledKitConfigs", master.EnabledKits,
                    runtime.EnabledKitConfigs.Where(config => config != null)
                        .Select(config => config.GetType().FullName),
                    runtime, runtimePath, masterPath, report);
            }

            /// <summary>
            /// 校验设计态启用白名单与运行时导出的 SDK/Kit 配置类型集合是否完全一致。
            /// </summary>
            private static void ValidateEnabledTypeExport(string groupName, string fieldPath,
                IEnumerable<string> sourceTypes, IEnumerable<string> runtimeTypes,
                ConfigRuntimeSO runtime, string runtimePath, string masterPath, NovaGuardReport report)
            {
                string[] expected = (sourceTypes ?? Array.Empty<string>())
                    .Where(typeName => !string.IsNullOrWhiteSpace(typeName))
                    .Distinct(StringComparer.Ordinal).OrderBy(typeName => typeName, StringComparer.Ordinal).ToArray();
                string[] actual = (runtimeTypes ?? Array.Empty<string>())
                    .Where(typeName => !string.IsNullOrWhiteSpace(typeName))
                    .Distinct(StringComparer.Ordinal).OrderBy(typeName => typeName, StringComparer.Ordinal).ToArray();
                string[] missing = expected.Except(actual, StringComparer.Ordinal).ToArray();
                string[] unexpected = actual.Except(expected, StringComparer.Ordinal).ToArray();
                if (missing.Length == 0 && unexpected.Length == 0)
                {
                    return;
                }

                string reason =
                    $"设计态启用列表与运行时导出不一致。缺失=[{string.Join(", ", missing)}]，多余=[{string.Join(", ", unexpected)}]。请检查启用项及对应配置并重新导出。";
                string[] affectedNames = missing.Concat(unexpected)
                    .Select(ResolveConfigDisplayName)
                    .Distinct(System.StringComparer.Ordinal)
                    .ToArray();
                string userSummary = groupName == "SDK"
                    ? $"以下 SDK 配置还没有同步到游戏：{string.Join("、", affectedNames)}。"
                    : $"以下功能配置还没有同步到游戏：{string.Join("、", affectedNames)}。";
                Type configType = ResolveLoadedType(missing.FirstOrDefault() ?? unexpected.FirstOrDefault());
                ConfigNavigationSection? section = configType == null
                    ? null
                    : groupName == "SDK" ? ConfigNavigationSection.SDK : ConfigNavigationSection.Kit;
                AddConfigIssue(report, "NOVA-CONFIG-004", runtime, runtimePath, masterPath,
                    fieldPath, reason, $"Nova/Open Config → {groupName} 配置", section, configType, userSummary);
            }

            /// <summary>
            /// 将配置类型全名转换为 ConfigWindow 中使用的 DisplayName，失败时回退为简短类型名。
            /// </summary>
            private static string ResolveConfigDisplayName(string typeName)
            {
                Type type = ResolveLoadedType(typeName);
                if (type == null) return typeName?.Split('.').LastOrDefault() ?? "未知配置";
                try
                {
                    object instance = Activator.CreateInstance(type);
                    if (instance is ISDKPluginConfig sdk && !string.IsNullOrWhiteSpace(sdk.DisplayName))
                        return sdk.DisplayName;
                    if (instance is IKitConfig kit && !string.IsNullOrWhiteSpace(kit.DisplayName))
                        return kit.DisplayName;
                }
                catch (Exception)
                {
                    // 与扫描器一致：构造失败不阻断 Guard，回退类型名。
                }
                return type.Name;
            }

            /// <summary>
            /// 比较单个应用配置字段，仅记录字段路径，不记录字段值，避免密钥进入日志。
            /// </summary>
            private static void CompareField(List<string> changedFields, string fieldName,
                string sourceValue, string runtimeValue)
            {
                if (!string.Equals(sourceValue, runtimeValue, StringComparison.Ordinal))
                {
                    changedFields.Add($"AppConfigs.{fieldName}");
                }
            }

            /// <summary>
            /// 比较隐私配置字段，仅记录路径而不记录敏感值。
            /// </summary>
            /// <param name="changedFields">变更字段收集列表。</param>
            /// <param name="fieldName">字段名。</param>
            /// <param name="sourceValue">设计态值。</param>
            /// <param name="runtimeValue">导出态值。</param>
            private static void ComparePrivacyField(List<string> changedFields, string fieldName,
                string sourceValue, string runtimeValue)
            {
                if (!string.Equals(sourceValue, runtimeValue, StringComparison.Ordinal))
                {
                    changedFields.Add($"PrivacyConfigs.{fieldName}");
                }
            }

            /// <summary>
            /// 校验 AppID 是否已经由公开包占位符替换为有效的正整数配置。
            /// </summary>
            private static void ValidateAppId(string value, ConfigRuntimeSO runtime, string runtimePath,
                string masterPath, NovaGuardReport report)
            {
                const string fieldName = "AppID";
                if (ContainsPublicPlaceholder(value))
                {
                    AddPlaceholderIssue(report, runtime, runtimePath, masterPath,
                        $"AppConfigs.{fieldName}", AppConfigEntry(fieldName));
                    return;
                }

                if (string.IsNullOrWhiteSpace(value) || !int.TryParse(value, out int appId) || appId <= 0)
                {
                    AddConfigIssue(report, "NOVA-CONFIG-003", runtime, runtimePath, masterPath,
                        $"AppConfigs.{fieldName}", "必须配置为有效的正整数 App ID。", AppConfigEntry(fieldName));
                }
            }

            /// <summary>
            /// 校验 AES 字段是否为项目真实参数且 UTF-8 长度严格等于 16 字节。
            /// </summary>
            private static void ValidateAesField(string fieldName, string value, ConfigRuntimeSO runtime,
                string runtimePath, string masterPath, NovaGuardReport report)
            {
                string fieldPath = $"AppConfigs.{fieldName}";
                string configEntry = AppConfigEntry(fieldName);
                if (ContainsPublicPlaceholder(value))
                {
                    AddPlaceholderIssue(report, runtime, runtimePath, masterPath, fieldPath, configEntry);
                    return;
                }

                int byteCount = string.IsNullOrEmpty(value) ? 0 : Encoding.UTF8.GetByteCount(value);
                if (byteCount != c_AesSecretByteLength)
                {
                    AddConfigIssue(report, "NOVA-CONFIG-003", runtime, runtimePath, masterPath,
                        fieldPath, $"当前 {byteCount} 字节，必须为 {c_AesSecretByteLength} 字节 UTF-8 字符串。",
                        configEntry);
                }
            }

            /// <summary>
            /// 校验隐私配置 AES 字段并把问题导航到隐私配置面板。
            /// </summary>
            /// <param name="fieldName">字段名。</param>
            /// <param name="value">字段值。</param>
            /// <param name="runtime">运行时配置。</param>
            /// <param name="runtimePath">运行时配置路径。</param>
            /// <param name="masterPath">设计态配置路径。</param>
            /// <param name="report">问题收集报告。</param>
            private static void ValidatePrivacyAesField(string fieldName, string value, ConfigRuntimeSO runtime,
                string runtimePath, string masterPath, NovaGuardReport report)
            {
                int byteCount = string.IsNullOrEmpty(value) ? 0 : Encoding.UTF8.GetByteCount(value);
                if (byteCount != c_AesSecretByteLength)
                {
                    AddConfigIssue(report, "NOVA-CONFIG-003", runtime, runtimePath, masterPath,
                        $"PrivacyConfigs.{fieldName}",
                        $"当前 {byteCount} 字节，必须为 {c_AesSecretByteLength} 字节 UTF-8 字符串。",
                        PrivacyConfigEntry(fieldName), ConfigNavigationSection.Privacy);
                }
            }

            /// <summary>
            /// 扫描已启用 SDK/Kit 的运行时序列化字符串，拦截发布脱敏后仍未替换的 YOUR_ 占位符。
            /// </summary>
            private static void ValidateExportedPlaceholders(ConfigRuntimeSO runtime, string runtimePath,
                string masterPath, NovaGuardReport report)
            {
                var serializedObject = new SerializedObject(runtime);
                SerializedProperty property = serializedObject.GetIterator();
                while (property.NextVisible(true))
                {
                    if (property.propertyType != SerializedPropertyType.String ||
                        property.propertyPath.StartsWith("AppConfigs.", StringComparison.Ordinal) ||
                        !ContainsPublicPlaceholder(property.stringValue))
                    {
                        continue;
                    }

                    string configEntry = ResolveConfigEntry(runtime, property.propertyPath);
                    AddConfigIssue(report, "NOVA-CONFIG-002", runtime, runtimePath, masterPath,
                        property.propertyPath,
                        "仍包含 YOUR_ 占位符，当前 Demo 所需参数尚未配置。请配置项目真实参数并重新导出。",
                        configEntry);
                }
            }

            /// <summary>
            /// 根据 ConfigRuntimeSO 序列化路径还原 ConfigWindow 中对应的配置面板入口。
            /// </summary>
            private static string ResolveConfigEntry(ConfigRuntimeSO runtime, string propertyPath)
            {
                if (TryParseArrayIndex(propertyPath, "EnabledSDKConfigs.Array.data[", out int sdkIndex) &&
                    sdkIndex >= 0 && sdkIndex < runtime.EnabledSDKConfigs.Count)
                {
                    return $"Nova/Open Config → SDK 配置 → {runtime.EnabledSDKConfigs[sdkIndex]?.DisplayName ?? "未知 SDK"}";
                }

                if (TryParseArrayIndex(propertyPath, "EnabledKitConfigs.Array.data[", out int kitIndex) &&
                    kitIndex >= 0 && kitIndex < runtime.EnabledKitConfigs.Count)
                {
                    return $"Nova/Open Config → Kit 配置 → {runtime.EnabledKitConfigs[kitIndex]?.DisplayName ?? "未知 Kit"}";
                }

                return "Nova/Open Config";
            }

            /// <summary>
            /// 从 Unity 数组序列化路径中解析目标元素索引。
            /// </summary>
            private static bool TryParseArrayIndex(string propertyPath, string prefix, out int index)
            {
                index = -1;
                if (string.IsNullOrEmpty(propertyPath) || !propertyPath.StartsWith(prefix, StringComparison.Ordinal))
                {
                    return false;
                }

                int end = propertyPath.IndexOf(']', prefix.Length);
                return end > prefix.Length && int.TryParse(
                    propertyPath.Substring(prefix.Length, end - prefix.Length), out index);
            }

            /// <summary>
            /// 添加公开包占位符问题，不输出字段当前值，避免敏感信息进入日志。
            /// </summary>
            private static void AddPlaceholderIssue(NovaGuardReport report, ConfigRuntimeSO runtime,
                string runtimePath, string masterPath, string fieldPath, string configEntry)
            {
                AddConfigIssue(report, "NOVA-CONFIG-002", runtime, runtimePath, masterPath, fieldPath,
                    "仍为公开包占位符。请配置项目真实参数并重新导出。", configEntry);
            }

            /// <summary>
            /// 添加启动配置来源问题，前两行固定提供面板所需的人话摘要，后续行保留完整技术诊断。
            /// </summary>
            /// <param name="report">问题收集报告。</param>
            /// <param name="summary">项目成员可直接理解的问题摘要。</param>
            /// <param name="action">可直接执行的处理方式。</param>
            /// <param name="technicalDetails">仅供 Console 与 Editor.log 使用的技术细节。</param>
            /// <param name="assetPath">关联的配置或场景路径。</param>
            private static void AddConfigSourceIssue(NovaGuardReport report, string summary, string action,
                string technicalDetails, string assetPath)
            {
                string message = $"启动配置未准备好：{summary}\n处理方式：{action}";
                if (!string.IsNullOrWhiteSpace(technicalDetails))
                    message += $"\n{technicalDetails}";

                report.Add(new NovaGuardIssue("NOVA-CONFIG-001", NovaGuardSeverity.Error, message, assetPath));
            }

            /// <summary>
            /// 添加带字段、配置入口、设计态来源、运行时导出物和导出坐标的启动配置问题。
            /// </summary>
            private static void AddConfigIssue(NovaGuardReport report, string ruleId, ConfigRuntimeSO runtime,
                string runtimePath, string masterPath, string fieldPath, string reason, string configEntry,
                ConfigNavigationSection? section = null, Type configType = null, string userSummary = null)
            {
                RememberConfigNavigation(runtime, fieldPath, section, configType);
                string message =
                    $"配置异常：{fieldPath}，{reason}\n" +
                    $"配置入口：{configEntry}\n";
                if (!string.IsNullOrWhiteSpace(userSummary))
                    message += $"用户提示：{userSummary}\n";
                message +=
                    $"设计态来源：{DisplayPath(masterPath)}\n" +
                    $"运行时导出：{DisplayPath(runtimePath)}\n" +
                    $"当前导出坐标：Platform={runtime.Platform}, Channel={runtime.Channel}, DevelopMode={runtime.DevelopMode}";
                report.Add(new NovaGuardIssue(ruleId, NovaGuardSeverity.Error, message, runtimePath));
            }

            /// <summary>
            /// 记录首个配置错误对应的具体面板；弹窗按钮始终跳到用户最先需要修复的位置。
            /// </summary>
            private static void RememberConfigNavigation(ConfigRuntimeSO runtime, string fieldPath,
                ConfigNavigationSection? section, Type configType)
            {
                if (s_HasLastConfigNavigation)
                {
                    return;
                }

                if (section.HasValue)
                {
                    s_LastConfigSection = section.Value;
                    s_LastConfigType = configType;
                }
                else if (string.Equals(fieldPath, "Namespace", StringComparison.Ordinal))
                {
                    s_LastConfigSection = ConfigNavigationSection.Namespace;
                }
                else if (fieldPath.StartsWith("PrivacyConfigs", StringComparison.Ordinal))
                {
                    s_LastConfigSection = ConfigNavigationSection.Privacy;
                }
                else if (TryParseArrayIndex(fieldPath, "EnabledSDKConfigs.Array.data[", out int sdkIndex) &&
                         sdkIndex >= 0 && sdkIndex < runtime.EnabledSDKConfigs.Count)
                {
                    s_LastConfigSection = ConfigNavigationSection.SDK;
                    s_LastConfigType = runtime.EnabledSDKConfigs[sdkIndex]?.GetType();
                }
                else if (TryParseArrayIndex(fieldPath, "EnabledKitConfigs.Array.data[", out int kitIndex) &&
                         kitIndex >= 0 && kitIndex < runtime.EnabledKitConfigs.Count)
                {
                    s_LastConfigSection = ConfigNavigationSection.Kit;
                    s_LastConfigType = runtime.EnabledKitConfigs[kitIndex]?.GetType();
                }
                else
                {
                    s_LastConfigSection = ConfigNavigationSection.App;
                }

                s_HasLastConfigNavigation = true;
            }

            /// <summary>
            /// 从当前已加载程序集解析 ConfigMaster 启用列表中的配置类型全名。
            /// </summary>
            private static Type ResolveLoadedType(string typeName)
            {
                if (string.IsNullOrWhiteSpace(typeName))
                {
                    return null;
                }

                foreach (System.Reflection.Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    Type type = assembly.GetType(typeName, false);
                    if (type != null)
                    {
                        return type;
                    }
                }
                return null;
            }

            /// <summary>
            /// 判断字符串是否仍包含公开包脱敏流程写入的 YOUR_ 占位符。
            /// </summary>
            private static bool ContainsPublicPlaceholder(string value)
                => !string.IsNullOrEmpty(value) && value.IndexOf("YOUR_", StringComparison.Ordinal) >= 0;

            /// <summary>
            /// 返回应用配置字段的 ConfigWindow 实际显示路径，字段名称与 SerializedProperty.displayName 同源。
            /// </summary>
            private static string AppConfigEntry(string fieldName)
                => $"Nova/Open Config → 通用配置 → 应用配置 → {ObjectNames.NicifyVariableName(fieldName)}";

            /// <summary>
            /// 返回隐私 AES 字段的 ConfigWindow 实际显示路径；标签与隐私配置面板的显式标签保持一致。
            /// </summary>
            private static string PrivacyConfigEntry(string fieldName)
            {
                switch (fieldName)
                {
                    case "AESKey":
                        return "Nova/Open Config → 通用配置 → 隐私配置 → AES-Key";
                    case "AESIV":
                        return "Nova/Open Config → 通用配置 → 隐私配置 → AES-IV";
                    default:
                        return $"Nova/Open Config → 通用配置 → 隐私配置 → {ObjectNames.NicifyVariableName(fieldName)}";
                }
            }

            /// <summary>
            /// 将空资产路径转成可读占位说明。
            /// </summary>
            private static string DisplayPath(string path)
                => string.IsNullOrEmpty(path) ? "未找到" : path;

            /// <summary>
            /// 测试入口：对指定运行时配置执行字段级启动就绪诊断。
            /// </summary>
            private static NovaGuardReport ValidateConfigRuntimeForDiagnostics(ConfigRuntimeSO runtime,
                string runtimePath, string masterPath)
            {
                var report = new NovaGuardReport();
                ValidateConfigRuntime(runtime, runtimePath, masterPath, report);
                return report;
            }

            /// <summary>
            /// 测试入口：校验设计态配置与运行时导出物的一致性。
            /// </summary>
            private static NovaGuardReport ValidateConfigExportForDiagnostics(ConfigRuntimeSO runtime,
                ConfigMasterSO master, string runtimePath, string masterPath)
            {
                var report = new NovaGuardReport();
                ValidateConfigExport(runtime, master, runtimePath, masterPath, report);
                return report;
            }

            /// <summary>
            /// 测试入口：校验 Scene 地址与激活 ConfigMaster 导出目标的来源关系。
            /// </summary>
            private static NovaGuardReport ValidateConfigSourceForDiagnostics(string assetLocation,
                ConfigMasterSO master, string scenePath)
            {
                var report = new NovaGuardReport();
                ValidateConfigSource(assetLocation, master, scenePath, report);
                return report;
            }
        }
    }
}
