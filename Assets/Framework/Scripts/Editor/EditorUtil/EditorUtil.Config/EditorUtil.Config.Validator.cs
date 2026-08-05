/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.Config.Validator.cs
 * author:    taoye
 * created:   2026/4/29
 * descrip:   AppConfigs / PluginConfig 必填字段校验；返回问题列表供 ConfigWindow 弹窗展示
 ***************************************************************/

using System.Collections.Generic;
using NovaFramework.Runtime;
using UnityEditor;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        public static partial class Config
        {
            /// <summary>
            /// AppConfigs / PluginConfig 必填字段校验；返回问题列表供 ConfigWindow 弹窗展示。
            /// </summary>
            public static class Validator
            {
                /// <summary>
                /// 校验问题的严重度级别。
                /// <para>Warning 表示建议修正但不阻断流程；Error 表示必须修正否则配置不可用。</para>
                /// </summary>
                public enum Severity
                {
                    /// <summary>
                    /// 建议修正；不影响当前流程继续执行。
                    /// </summary>
                    Warning,

                    /// <summary>
                    /// 必须修正；配置存在致命缺陷。
                    /// </summary>
                    Error,
                }

                /// <summary>
                /// 一条校验问题；包含字段路径、人读消息与严重度。
                /// <para>使用 readonly struct 保证不可变性，可安全放入列表传递。</para>
                /// </summary>
                public readonly struct ValidationIssue
                {
                    /// <summary>
                    /// 问题所在字段的路径，格式如 "AppConfigs.AppID" 或 "SDKConfigs[0]"。
                    /// </summary>
                    public readonly string Path;

                    /// <summary>
                    /// 面向用户的问题描述文本。
                    /// </summary>
                    public readonly string Message;

                    /// <summary>
                    /// 问题严重度；决定 ConfigWindow 以何种样式呈现该条目。
                    /// </summary>
                    public readonly Severity Level;

                    /// <summary>
                    /// 构造一条校验问题。
                    /// </summary>
                    /// <param name="path">问题所在字段路径。</param>
                    /// <param name="message">面向用户的描述文本。</param>
                    /// <param name="level">问题严重度。</param>
                    public ValidationIssue(string path, string message, Severity level)
                    {
                        Path = path;
                        Message = message;
                        Level = level;
                    }
                }

                /// <summary>
                /// 对指定 Platform×Channel×DevelopMode 组合执行全量校验，返回所有发现的问题列表。
                /// <para>校验范围：ConfigMasterSO 空值检查、AppConfigs 必填字段、目标矩阵行存在性及 SDKConfigs / KitConfigs 空引用；其中已启用类型的空引用为 Error，未启用类型的失效残留为 Warning（不阻断导出）。</para>
                /// </summary>
                /// <param name="master">待校验的 ConfigMasterSO 实例；传入 null 时直接返回含根 Error 的列表。</param>
                /// <param name="platform">目标平台。</param>
                /// <param name="channel">目标渠道。</param>
                /// <param name="mode">目标开发模式（Debug / Release）。</param>
                /// <returns>发现的问题列表；无问题时返回空列表。</returns>
                public static IReadOnlyList<ValidationIssue> Validate(ConfigMasterSO master, PlatformType platform, ChannelType channel, DevelopMode mode)
                {
                    List<ValidationIssue> issues = new();

                    if (master == null)
                    {
                        issues.Add(new ValidationIssue("<root>", "ConfigMaster 为空。", Severity.Error));
                        return issues;
                    }

                    // 顶层维度化校验路径：经 DimensionalResolver 取当前坐标生效值，避免全不勾/勾选两态校验错位
                    RequireNotEmpty(issues, "Namespace", DimensionalResolver.ResolveNamespace(master, platform, channel, mode));
                    ValidateAppConfigs(master.GetAppConfigs(platform, channel, mode), issues);

                    if (master.TryGetEntry(platform, channel, out var entry))
                    {
                        string rowPath = $"Entries[{platform}/{channel}]";
                        List<ISDKPluginConfig> sdkConfigs = entry.GetSDKConfigs(mode);
                        HashSet<string> missingEnabledSdkTypes = ValidateEnabledConfigTypes(
                            sdkConfigs, master.EnabledSDKs, $"{rowPath}.SDKConfigs", "SDK", issues);
                        List<IKitConfig> kitConfigs = entry.GetKitConfigs(mode);
                        HashSet<string> missingEnabledKitTypes = ValidateEnabledConfigTypes(
                            kitConfigs, master.EnabledKits, $"{rowPath}.KitConfigs", "Kit", issues);
                        string singleMissingTypeName = ResolveSingleMissingPluginTypeName(master);

                        for (int i = 0; i < sdkConfigs.Count; i++)
                        {
                            if (sdkConfigs[i] == null)
                            {
                                string typeName = singleMissingTypeName;
                                bool typeResolved = !string.IsNullOrEmpty(typeName);
                                bool enabled = typeResolved && master.EnabledSDKs?.Contains(typeName) == true;
                                if (enabled && missingEnabledSdkTypes.Contains(typeName)) continue;
                                issues.Add(new ValidationIssue(
                                    $"{rowPath}.SDKConfigs[{i}]",
                                    DescribeMissingRef(typeName, true, enabled, typeResolved),
                                    !typeResolved || enabled ? Severity.Error : Severity.Warning));
                            }
                        }

                        for (int i = 0; i < kitConfigs.Count; i++)
                        {
                            if (kitConfigs[i] == null)
                            {
                                string typeName = singleMissingTypeName;
                                bool typeResolved = !string.IsNullOrEmpty(typeName);
                                bool enabled = typeResolved && master.EnabledKits?.Contains(typeName) == true;
                                if (enabled && missingEnabledKitTypes.Contains(typeName)) continue;
                                issues.Add(new ValidationIssue(
                                    $"{rowPath}.KitConfigs[{i}]",
                                    DescribeMissingRef(typeName, false, enabled, typeResolved),
                                    !typeResolved || enabled ? Severity.Error : Severity.Warning));
                            }
                        }

                    }
                    else
                    {
                        issues.Add(new ValidationIssue(
                            $"Entries[{platform}/{channel}]", "未找到对应 Platform×Channel 行；请在结构巡检中补齐。", Severity.Error));
                    }

                    return issues;
                }

                /// <summary>
                /// 为 SerializeReference 槽位的 null 占位生成面向用户的描述：
                /// 说明成因（插件包未安装或类型被移除）与影响面（已启用时导出缺配置；未启用时仅为失效残留、不参与导出）。
                /// <para>修复方式的统一说明由 ConfigWindow 校验对话框补充，避免逐条重复。</para>
                /// </summary>
                /// <param name="typeName">null 槽位在资产中记录的原始类型名；读不到时为空字符串。</param>
                /// <param name="isSdk">true 表示 SDK 配置，false 表示 Kit 配置。</param>
                /// <param name="enabled">该类型是否在当前 Master 的启用名单（EnabledSDKs / EnabledKits）中。</param>
                /// <param name="typeResolved">是否已从 Unity 缺失托管引用元数据中恢复原始类型。</param>
                /// <returns>人读描述文本。</returns>
                private static string DescribeMissingRef(string typeName, bool isSdk, bool enabled, bool typeResolved)
                {
                    string label = isSdk ? "SDK" : "Kit";
                    if (!typeResolved)
                    {
                        return $"存在一项失效的 {label} 配置，但无法确认原始类型及其启用状态；为避免导出缺少已启用配置，本次导出将被阻断，请先清理空槽位或恢复对应插件包。";
                    }

                    string display = FormatTypeName(typeName);
                    if (enabled)
                    {
                        return $"「{display}」已启用，但其 {label} 配置已失效：当前工程未安装该插件包，或类型已被移除，在 Config 窗口中该项显示为空槽位，导出结果将缺少该插件配置。";
                    }

                    return $"「{display}」的 {label} 配置为失效残留：当前工程未安装该插件包，或类型已被移除；该项未启用，不参与本次导出，可暂不处理。";
                }

                /// <summary>
                /// 校验当前坐标下每个已启用类型都存在可导出的非空配置实例。
                /// <para>该完整性检查不依赖缺失 SerializeReference 能否恢复原类型，因此是防止静默漏导的最终安全门。</para>
                /// </summary>
                /// <typeparam name="TConfig">SDK 或 Kit 配置接口类型。</typeparam>
                /// <param name="configs">当前坐标的实际配置列表。</param>
                /// <param name="enabledTypeNames">当前 Master 的启用类型白名单。</param>
                /// <param name="pathPrefix">ValidationIssue 路径前缀。</param>
                /// <param name="label">面向用户的配置类别名。</param>
                /// <param name="issues">问题收集列表。</param>
                /// <returns>已启用但当前坐标缺少非空实例的类型集合。</returns>
                private static HashSet<string> ValidateEnabledConfigTypes<TConfig>(
                    IReadOnlyList<TConfig> configs,
                    IReadOnlyList<string> enabledTypeNames,
                    string pathPrefix,
                    string label,
                    List<ValidationIssue> issues)
                    where TConfig : class
                {
                    HashSet<string> actualTypeNames = new();
                    for (int i = 0; i < configs.Count; i++)
                    {
                        if (configs[i] != null) actualTypeNames.Add(configs[i].GetType().FullName);
                    }

                    HashSet<string> missingTypeNames = new();
                    if (enabledTypeNames == null) return missingTypeNames;
                    for (int i = 0; i < enabledTypeNames.Count; i++)
                    {
                        string typeName = enabledTypeNames[i];
                        if (string.IsNullOrEmpty(typeName) || actualTypeNames.Contains(typeName)) continue;
                        if (!missingTypeNames.Add(typeName)) continue;
                        issues.Add(new ValidationIssue(
                            $"{pathPrefix}[{typeName}]",
                            $"「{FormatTypeName(typeName)}」已启用，但当前坐标没有可导出的 {label} 配置实例；请恢复对应插件包及配置，或取消启用后再导出。",
                            Severity.Error));
                    }
                    return missingTypeNames;
                }

                /// <summary>
                /// 将类型全名格式化为「类名 (命名空间)」的展示文本；无命名空间时仅返回类名。
                /// </summary>
                /// <param name="fullTypeName">类型全名（命名空间.类名）。</param>
                /// <returns>展示文本。</returns>
                private static string FormatTypeName(string fullTypeName)
                {
                    int dot = fullTypeName.LastIndexOf('.');
                    if (dot < 0) return fullTypeName;
                    return $"{fullTypeName.Substring(dot + 1)} ({fullTypeName.Substring(0, dot)})";
                }

                /// <summary>
                /// 在整个 Master 仅存在一个缺失托管引用且它也是唯一 Plugin 空槽位时，恢复其原始类型全名。
                /// <para>Unity 对缺失槽位只暴露 managedReferenceId=-2，无法与多个缺失类型逐槽关联；多项场景返回空字符串并走保守阻断，禁止猜测映射。</para>
                /// </summary>
                /// <param name="master">包含 SerializeReference 数据的 ConfigMasterSO。</param>
                /// <returns>可无歧义恢复时返回原始类型全名，否则返回空字符串。</returns>
                private static string ResolveSingleMissingPluginTypeName(ConfigMasterSO master)
                {
                    ManagedReferenceMissingType[] missingTypes = SerializationUtility.GetManagedReferencesWithMissingTypes(master);
                    if (missingTypes.Length != 1 || CountNullPluginSlots(master) != 1) return string.Empty;

                    ManagedReferenceMissingType missing = missingTypes[0];
                    if (string.IsNullOrEmpty(missing.className)) return string.Empty;
                    return string.IsNullOrEmpty(missing.namespaceName)
                        ? missing.className
                        : $"{missing.namespaceName}.{missing.className}";
                }

                /// <summary>
                /// 统计整个 Master 的 SDK / Kit 配置列表中的 null 槽位数量，用于判断缺失类型恢复是否无歧义。
                /// </summary>
                /// <param name="master">待统计的 ConfigMasterSO。</param>
                /// <returns>全部平台、渠道和模式下的 Plugin 空槽位总数。</returns>
                private static int CountNullPluginSlots(ConfigMasterSO master)
                {
                    int count = 0;
                    IReadOnlyList<PlatformChannelEntry> entries = master.EditorEntries;
                    for (int i = 0; i < entries.Count; i++)
                    {
                        PlatformChannelEntry entry = entries[i];
                        for (int m = 0; m < entry.SDKConfigsByMode.Count; m++)
                        {
                            List<ISDKPluginConfig> configs = entry.SDKConfigsByMode[m].SDKConfigs;
                            for (int c = 0; c < configs.Count; c++)
                            {
                                if (configs[c] == null) count++;
                            }
                        }
                        for (int m = 0; m < entry.KitConfigsByMode.Count; m++)
                        {
                            List<IKitConfig> configs = entry.KitConfigsByMode[m].KitConfigs;
                            for (int c = 0; c < configs.Count; c++)
                            {
                                if (configs[c] == null) count++;
                            }
                        }
                    }
                    return count;
                }

                /// <summary>
                /// 校验 AppConfigs 全部必填字段；问题追加至 issues 列表。
                /// </summary>
                /// <param name="common">待校验的 AppConfigs 实例；为 null 时追加 Error 后直接返回。</param>
                /// <param name="issues">问题收集列表；校验发现的所有条目追加至此。</param>
                private static void ValidateAppConfigs(AppConfigs common, List<ValidationIssue> issues)
                {
                    if (common == null)
                    {
                        issues.Add(new ValidationIssue("AppConfigs", "AppConfigs 为 null。", Severity.Error));
                        return;
                    }

                    RequireNotEmpty(issues, "AppConfigs.AppID", common.AppID);
                    RequireNotEmpty(issues, "AppConfigs.AppAesKey", common.AppAesKey);
                    RequireNotEmpty(issues, "AppConfigs.AppAesIV", common.AppAesIV);
                }

                /// <summary>
                /// 若 value 为 null 或空字符串，向 issues 追加一条路径为 path 的必填 Error。
                /// </summary>
                /// <param name="issues">问题收集列表。</param>
                /// <param name="path">字段路径，用于 ValidationIssue.Path。</param>
                /// <param name="value">待检测的字段值。</param>
                private static void RequireNotEmpty(List<ValidationIssue> issues, string path, string value)
                {
                    if (string.IsNullOrEmpty(value))
                    {
                        issues.Add(new ValidationIssue(path, "必填字段为空。", Severity.Error));
                    }
                }
            }
        }
    }
}
