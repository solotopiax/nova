/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.Config.Validator.cs
 * author:    taoye
 * created:   2026/4/29
 * descrip:   AppConfigs / PluginConfig 必填字段校验；返回问题列表供 ConfigWindow 弹窗展示
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Text;
using NovaFramework.Runtime;
using UnityEditor;
using UnityEngine;

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
                    ValidatePrivacyConfigs(master.GetPrivacyConfigs(platform, channel, mode), issues);

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
                /// 校验 ConfigMaster 完整三维矩阵的结构与分组一致性；方法只读，不会自动广播或修复数据。
                /// </summary>
                /// <param name="master">待检查的设计态配置。</param>
                /// <returns>所有会导致分组取值不确定或未勾选维度不一致的错误。</returns>
                public static IReadOnlyList<ValidationIssue> ValidateDimensionInvariants(ConfigMasterSO master)
                {
                    List<ValidationIssue> issues = new();
                    if (master == null)
                    {
                        issues.Add(new ValidationIssue("<root>", "ConfigMaster 为空。", Severity.Error));
                        return issues;
                    }

                    ValidateMatrixShape(master, issues);
                    ValidateMatrixValues(master, master.AppConfigsMask, "AppConfigs", ReadAppConfig, issues);
                    ValidateMatrixValues(master, master.PrivacyConfigsMask, "PrivacyConfigs", ReadPrivacyConfig, issues);
                    ValidateTypedMatrixValues(master, true, issues);
                    ValidateTypedMatrixValues(master, false, issues);
                    ValidateOverrideKeys(master.NamespaceOverrides, master.NamespaceMask, "NamespaceOverrides", o => o.Platform, o => o.Channel, o => o.DevelopMode, issues);
                    ValidateOverrideKeys(master.HybridEditorConfigsOverrides, master.HybridEditorConfigsMask, "HybridEditorConfigsOverrides", o => o.Platform, o => o.Channel, o => o.DevelopMode, issues);
                    ValidateOverrideKeys(master.YooAssetEditorConfigsOverrides, master.YooAssetEditorConfigsMask, "YooAssetEditorConfigsOverrides", o => o.Platform, o => o.Channel, o => o.DevelopMode, issues);
                    ValidateOverrideKeys(master.CDNEditorConfigsOverrides, master.CDNEditorConfigsMask, "CDNEditorConfigsOverrides", o => o.Platform, o => o.Channel, o => o.DevelopMode, issues);
                    return issues;
                }

                /// <summary>
                /// 将维度一致性问题格式化为技术诊断文本，供异常、Console 与 Editor.log 使用。
                /// 用户弹窗不得直接展示该文本，其中包含字段路径、逻辑键与精确冲突坐标。
                /// </summary>
                /// <param name="issues">维度一致性问题。</param>
                /// <returns>包含定位路径与原因的多行技术诊断文本。</returns>
                public static string BuildDimensionInvariantMessage(IReadOnlyList<ValidationIssue> issues)
                {
                    StringBuilder builder = new();
                    builder.AppendLine("配置矩阵存在不一致，为避免不同平台导出新旧混合数据，本次操作已停止：");
                    for (int i = 0; i < issues.Count; i++)
                        builder.AppendLine($"- {issues[i].Path}: {issues[i].Message}");
                    return builder.ToString();
                }

                /// <summary>
                /// 检查已有 Platform×Channel 行及各 DevelopMode 容器是否唯一且内部完整。
                /// 不要求资产预先包含未来或尚未保存的全部枚举组合；缺少目标行仍由坐标级导出校验负责。
                /// </summary>
                private static void ValidateMatrixShape(ConfigMasterSO master, List<ValidationIssue> issues)
                {
                    HashSet<string> rows = new();
                    IReadOnlyList<PlatformChannelEntry> entries = master.EditorEntries;
                    for (int i = 0; i < entries.Count; i++)
                    {
                        PlatformChannelEntry entry = entries[i];
                        if (entry == null)
                        {
                            issues.Add(new ValidationIssue($"Entries[{i}]", "矩阵行为空。", Severity.Error));
                            continue;
                        }
                        string rowKey = $"{entry.Platform}|{entry.Channel}";
                        if (!rows.Add(rowKey))
                            issues.Add(new ValidationIssue($"Entries[{i}]", $"存在重复矩阵行 {entry.Platform}/{entry.Channel}。", Severity.Error));
                        ValidateModeKeys(entry.AppConfigsByMode, $"Entries[{rowKey}].AppConfigsByMode", item => item.Mode, issues);
                        ValidateModeKeys(entry.PrivacyConfigsByMode, $"Entries[{rowKey}].PrivacyConfigsByMode", item => item.Mode, issues);
                        ValidateModeKeys(entry.SDKConfigsByMode, $"Entries[{rowKey}].SDKConfigsByMode", item => item.Mode, issues);
                        ValidateModeKeys(entry.KitConfigsByMode, $"Entries[{rowKey}].KitConfigsByMode", item => item.Mode, issues);
                    }

                }

                /// <summary>
                /// 检查按 DevelopMode 分组的列表是否包含枚举全集且没有重复项。
                /// </summary>
                private static void ValidateModeKeys<T>(IReadOnlyList<T> entries, string path, Func<T, DevelopMode> getMode, List<ValidationIssue> issues)
                {
                    HashSet<DevelopMode> modes = new();
                    if (entries != null)
                    {
                        for (int i = 0; i < entries.Count; i++)
                        {
                            if (entries[i] == null)
                            {
                                issues.Add(new ValidationIssue($"{path}[{i}]", "开发模式分组为空。", Severity.Error));
                                continue;
                            }
                            DevelopMode mode = getMode(entries[i]);
                            if (!modes.Add(mode))
                                issues.Add(new ValidationIssue($"{path}[{i}]", $"存在重复的开发模式 {mode}。", Severity.Error));
                        }
                    }
                    foreach (DevelopMode mode in Enum.GetValues(typeof(DevelopMode)))
                    {
                        if (!modes.Contains(mode))
                            issues.Add(new ValidationIssue(path, $"缺少开发模式 {mode} 的配置分组。", Severity.Error));
                    }
                }

                /// <summary>
                /// 检查 App/Privacy 在每个逻辑组内是否保持完全一致。
                /// </summary>
                private static void ValidateMatrixValues(
                    ConfigMasterSO master,
                    PanelDimensionMask mask,
                    string panelName,
                    Func<PlatformChannelEntry, DevelopMode, object> read,
                    List<ValidationIssue> issues)
                {
                    Dictionary<string, DimensionValue> expected = new();
                    IReadOnlyList<PlatformChannelEntry> entries = master.EditorEntries;
                    for (int i = 0; i < entries.Count; i++)
                    {
                        PlatformChannelEntry entry = entries[i];
                        if (entry == null || entry.Platform == PlatformType.None) continue;
                        foreach (DevelopMode mode in Enum.GetValues(typeof(DevelopMode)))
                        {
                            object value = read(entry, mode);
                            if (value == null) continue;
                            CheckGroupValue(expected, mask, panelName, entry.Platform, entry.Channel, mode, value, issues);
                        }
                    }
                }

                /// <summary>
                /// 检查已启用 SDK/Kit 类型的现有实例在逻辑组内是否一致。
                /// 未启用类型的历史残留不参与导出，因此不阻断；某坐标缺实例由该坐标的既有导出校验负责。
                /// </summary>
                private static void ValidateTypedMatrixValues(ConfigMasterSO master, bool sdk, List<ValidationIssue> issues)
                {
                    IReadOnlyList<string> enabledTypes = sdk ? master.EnabledSDKs : master.EnabledKits;
                    HashSet<string> typeNames = enabledTypes != null
                        ? new HashSet<string>(enabledTypes)
                        : new HashSet<string>();
                    List<TypedDimensionMask> masks = sdk ? master.SDKMasks : master.KitMasks;

                    foreach (string typeName in typeNames)
                    {
                        PanelDimensionMask mask = FindTypedMask(masks, typeName);
                        Dictionary<string, DimensionValue> expected = new();
                        foreach (PlatformChannelEntry entry in master.EditorEntries)
                        {
                            if (entry == null || entry.Platform == PlatformType.None) continue;
                            foreach (DevelopMode mode in Enum.GetValues(typeof(DevelopMode)))
                            {
                                object value = ReadTypedConfig(entry, mode, typeName, sdk, out int count);
                                string path = $"{(sdk ? "SDK" : "Kit")}[{typeName}]/{entry.Platform}/{entry.Channel}/{mode}";
                                if (count > 1)
                                    issues.Add(new ValidationIssue(path, "同一坐标存在重复类型配置。", Severity.Error));
                                if (value == null) continue;
                                CheckGroupValue(
                                    expected,
                                    mask,
                                    $"{(sdk ? "SDK" : "Kit")}[{typeName}]",
                                    entry.Platform,
                                    entry.Channel,
                                    mode,
                                    value,
                                    issues);
                            }
                        }
                    }
                }

                /// <summary>
                /// 比较同一逻辑组内的序列化值，发现不一致时记录源坐标和冲突坐标。
                /// </summary>
                private static void CheckGroupValue(
                    IDictionary<string, DimensionValue> expected,
                    PanelDimensionMask mask,
                    string panelName,
                    PlatformType platform,
                    ChannelType channel,
                    DevelopMode mode,
                    object value,
                    List<ValidationIssue> issues)
                {
                    string key = $"{(mask.ByPlatform ? (int)platform : -1)}|{(mask.ByChannel ? (int)channel : -1)}|{(mask.ByDevelopMode ? (int)mode : -1)}";
                    string json = JsonUtility.ToJson(value);
                    string coord = $"{platform}/{channel}/{mode}";
                    if (!expected.TryGetValue(key, out DimensionValue first))
                    {
                        expected[key] = new DimensionValue(coord, json);
                        return;
                    }
                    if (!string.Equals(first.Json, json, StringComparison.Ordinal))
                    {
                        issues.Add(new ValidationIssue(
                            $"{panelName}[{key}]",
                            $"未勾选维度本应共用同一份，但 {first.Coord} 与 {coord} 的内容不一致。请在 Config 窗口重新编辑该组，或通过维度开关明确拆分/合并。",
                            Severity.Error));
                    }
                }

                /// <summary>
                /// 检查 Override 列表是否含重复逻辑键；旧资产在未勾选轴保留坐标但不造成歧义时允许继续读取。
                /// </summary>
                private static void ValidateOverrideKeys<T>(
                    IReadOnlyList<T> overrides,
                    PanelDimensionMask mask,
                    string path,
                    Func<T, PlatformType> getPlatform,
                    Func<T, ChannelType> getChannel,
                    Func<T, DevelopMode> getMode,
                    List<ValidationIssue> issues)
                    where T : class
                {
                    HashSet<string> keys = new();
                    if (overrides == null) return;
                    for (int i = 0; i < overrides.Count; i++)
                    {
                        T item = overrides[i];
                        if (item == null)
                        {
                            issues.Add(new ValidationIssue($"{path}[{i}]", "Override 为空。", Severity.Error));
                            continue;
                        }
                        PlatformType platform = getPlatform(item);
                        ChannelType channel = getChannel(item);
                        DevelopMode mode = getMode(item);
                        string key = $"{(mask.ByPlatform ? (int)platform : -1)}|{(mask.ByChannel ? (int)channel : -1)}|{(mask.ByDevelopMode ? (int)mode : -1)}";
                        if (!keys.Add(key))
                            issues.Add(new ValidationIssue($"{path}[{i}]", $"存在重复逻辑组 {key}，实际取值会受列表顺序影响。", Severity.Error));
                    }
                }

                /// <summary>
                /// 从类型掩码列表只读取值，未配置时返回全局共用掩码且不修改资产。
                /// </summary>
                private static PanelDimensionMask FindTypedMask(IReadOnlyList<TypedDimensionMask> masks, string typeName)
                {
                    for (int i = 0; i < masks.Count; i++)
                    {
                        if (masks[i] != null && masks[i].TypeName == typeName) return masks[i].Mask ?? new PanelDimensionMask();
                    }
                    return new PanelDimensionMask();
                }

                /// <summary>
                /// 只读查找指定模式的 AppConfigs，不补齐缺失分组。
                /// </summary>
                private static object ReadAppConfig(PlatformChannelEntry entry, DevelopMode mode)
                {
                    for (int i = 0; i < entry.AppConfigsByMode.Count; i++)
                        if (entry.AppConfigsByMode[i] != null && entry.AppConfigsByMode[i].Mode == mode) return entry.AppConfigsByMode[i].Config;
                    return null;
                }

                /// <summary>
                /// 只读查找指定模式的 PrivacyConfigs，不补齐缺失分组。
                /// </summary>
                private static object ReadPrivacyConfig(PlatformChannelEntry entry, DevelopMode mode)
                {
                    for (int i = 0; i < entry.PrivacyConfigsByMode.Count; i++)
                        if (entry.PrivacyConfigsByMode[i] != null && entry.PrivacyConfigsByMode[i].Mode == mode) return entry.PrivacyConfigsByMode[i].Config;
                    return null;
                }

                /// <summary>
                /// 只读查找单个坐标的 SDK/Kit 类型实例，并返回同类型出现次数。
                /// </summary>
                private static object ReadTypedConfig(PlatformChannelEntry entry, DevelopMode mode, string typeName, bool sdk, out int count)
                {
                    count = 0;
                    object result = null;
                    if (sdk)
                    {
                        for (int i = 0; i < entry.SDKConfigsByMode.Count; i++)
                        {
                            DevelopModeSDKEntry modeEntry = entry.SDKConfigsByMode[i];
                            if (modeEntry == null || modeEntry.Mode != mode) continue;
                            for (int c = 0; c < modeEntry.SDKConfigs.Count; c++)
                            {
                                ISDKPluginConfig config = modeEntry.SDKConfigs[c];
                                if (config == null || config.GetType().FullName != typeName) continue;
                                count++;
                                result ??= config;
                            }
                        }
                    }
                    else
                    {
                        for (int i = 0; i < entry.KitConfigsByMode.Count; i++)
                        {
                            DevelopModeKitEntry modeEntry = entry.KitConfigsByMode[i];
                            if (modeEntry == null || modeEntry.Mode != mode) continue;
                            for (int c = 0; c < modeEntry.KitConfigs.Count; c++)
                            {
                                IKitConfig config = modeEntry.KitConfigs[c];
                                if (config == null || config.GetType().FullName != typeName) continue;
                                count++;
                                result ??= config;
                            }
                        }
                    }
                    return result;
                }

                /// <summary>
                /// 同一逻辑组中首个坐标的序列化比较基准。
                /// </summary>
                private readonly struct DimensionValue
                {
                    public readonly string Coord;
                    public readonly string Json;

                    public DimensionValue(string coord, string json)
                    {
                        Coord = coord;
                        Json = json;
                    }
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
                /// 校验隐私配置中的 AES Key/IV 均为 16 字节 UTF-8 字符串。
                /// </summary>
                /// <param name="privacy">待校验的隐私配置。</param>
                /// <param name="issues">问题收集列表。</param>
                private static void ValidatePrivacyConfigs(PrivacyConfigs privacy, List<ValidationIssue> issues)
                {
                    if (privacy == null)
                    {
                        issues.Add(new ValidationIssue("PrivacyConfigs", "PrivacyConfigs 为 null。", Severity.Error));
                        return;
                    }

                    RequireUtf8Length(issues, "PrivacyConfigs.AESKey", privacy.AESKey, 16);
                    RequireUtf8Length(issues, "PrivacyConfigs.AESIV", privacy.AESIV, 16);
                }

                /// <summary>
                /// 要求字符串按 UTF-8 编码后达到指定字节数。
                /// </summary>
                /// <param name="issues">问题收集列表。</param>
                /// <param name="path">字段路径。</param>
                /// <param name="value">待校验值。</param>
                /// <param name="expectedBytes">期望字节数。</param>
                private static void RequireUtf8Length(List<ValidationIssue> issues, string path, string value, int expectedBytes)
                {
                    int actualBytes = string.IsNullOrEmpty(value) ? 0 : Encoding.UTF8.GetByteCount(value);
                    if (actualBytes != expectedBytes)
                    {
                        issues.Add(new ValidationIssue(path, $"按 UTF-8 编码后必须为 {expectedBytes} 字节，当前为 {actualBytes} 字节。", Severity.Error));
                    }
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
