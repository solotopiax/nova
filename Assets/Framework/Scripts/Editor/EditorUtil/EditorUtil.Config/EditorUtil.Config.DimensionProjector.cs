/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.Config.DimensionProjector.cs
 * author:    taoye
 * created:   2026/6/1
 * descrip:   Config 面板维度投影器；按掩码广播普通编辑，并对维度切换执行全矩阵原子投影
 ***************************************************************/

using System;
using System.Collections.Generic;
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
            /// Config 面板维度投影器；普通编辑按当前掩码广播同组数据，维度切换则对整个矩阵的全部逻辑组做原子分裂或合并。
            /// <para>全部操作作用于调用方传入的 SerializedObject（m_WorkingCopy），不落盘，不设 m_IsDirty，由 ConfigWindow 负责后续脏标记。</para>
            /// <para>覆盖 App、Privacy、SDK、Kit、Namespace、HybridCLR、YooAsset 与 CDN 八类面板。</para>
            /// </summary>
            public static class DimensionProjector
            {
                // -------------------------------------------------------
                // 公开枚举：矩阵类与顶层 Override 类面板
                // -------------------------------------------------------

                /// <summary>
                /// 配置面板种类；区分四种矩阵面板与四种顶层 Override 面板。
                /// </summary>
                public enum PanelKind
                {
                    /// <summary>
                    /// 公共配置（AppConfigs），对应 m_Entries[i].AppConfigsByMode[m].Config。
                    /// </summary>
                    AppConfigs,
                    /// <summary>
                    /// 隐私配置，对应 m_Entries[i].PrivacyConfigsByMode[m].Config。
                    /// </summary>
                    PrivacyConfigs,
                    /// <summary>
                    /// SDK Plugin 配置（ISDKPluginConfig 实现），对应 m_Entries[i].SDKConfigsByMode[m].SDKConfigs[idx]。
                    /// typeName 字段生效。
                    /// </summary>
                    SDK,
                    /// <summary>
                    /// Kit 配置（IKitConfig 实现），对应 m_Entries[i].KitConfigsByMode[m].KitConfigs[idx]。
                    /// typeName 字段生效。
                    /// </summary>
                    Kit,
                    /// <summary>
                    /// 顶层 Namespace 字段，底座为 ConfigMasterSO.NamespaceOverrides 列表 + 顶层 Namespace 默认字段。
                    /// </summary>
                    Namespace,
                    /// <summary>
                    /// 顶层 HybridCLR 面板字段组（AotMetadataDlls / StartupGameDlls / RunningGameDlls / LinkXmlTargetPath / GameEntranceProcedureName），
                    /// 底座为 ConfigMasterSO.HybridEditorConfigsOverrides 列表 + 各顶层默认字段（仅 Editor 期消费）。
                    /// </summary>
                    HybridEditorConfigs,
                    /// <summary>
                    /// 顶层 YooAsset 两路径字段（YooAssetSettingsPath / BundleCollectorSettingPath），
                    /// 底座为 ConfigMasterSO.YooAssetEditorConfigsOverrides 列表 + 各顶层默认字段（仅 Editor 期消费）。
                    /// </summary>
                    YooAssetEditorConfigs,
                    /// <summary>
                    /// 顶层 CDN 部署面板，底座为 ConfigMasterSO.CDNEditorConfigsOverrides + 顶层 CDNEditorConfigs（仅 Editor 期消费）。
                    /// </summary>
                    CDNEditorConfigs,
                }

                // -------------------------------------------------------
                // 公开坐标结构（轻量值类型，避免散参）
                // -------------------------------------------------------

                /// <summary>
                /// 三维坐标（Platform × Channel × DevelopMode）；标识矩阵中的一个格子。
                /// </summary>
                public readonly struct Coord
                {
                    /// <summary>
                    /// 目标平台。
                    /// </summary>
                    public readonly PlatformType Platform;
                    /// <summary>
                    /// 目标渠道。
                    /// </summary>
                    public readonly ChannelType Channel;
                    /// <summary>
                    /// 目标开发模式。
                    /// </summary>
                    public readonly DevelopMode Mode;

                    /// <summary>
                    /// 构造三维坐标。
                    /// </summary>
                    /// <param name="platform">平台。</param>
                    /// <param name="channel">渠道。</param>
                    /// <param name="mode">开发模式。</param>
                    public Coord(PlatformType platform, ChannelType channel, DevelopMode mode)
                    {
                        Platform = platform;
                        Channel = channel;
                        Mode = mode;
                    }
                }

                // -------------------------------------------------------
                // 公开枚举：维度轴（供 OnDimensionEnabled / Disabled 使用）
                // -------------------------------------------------------

                /// <summary>
                /// 可切换的维度轴；对应 PanelDimensionMask 的三个 bool 字段。
                /// </summary>
                public enum DimensionAxis
                {
                    /// <summary>
                    /// 平台轴（ByPlatform）。
                    /// </summary>
                    Platform,
                    /// <summary>
                    /// 渠道轴（ByChannel）。
                    /// </summary>
                    Channel,
                    /// <summary>
                    /// 开发模式轴（ByDevelopMode）。
                    /// </summary>
                    DevelopMode,
                }

                // -------------------------------------------------------
                // 公开操作
                // -------------------------------------------------------

                /// <summary>
                /// 加维分裂：冻结旧掩码下每个逻辑组的快照，再为新轴的全部取值建立独立副本。
                /// <para>整个矩阵一次完成；不会只处理顶部当前平台、渠道或模式所在的单一切片。</para>
                /// </summary>
                /// <param name="master">编辑期 ConfigMasterSO 实例（工作副本）。</param>
                /// <param name="masterSO">对应 master 的 SerializedObject（工作副本），SDK/Kit 路径必需。</param>
                /// <param name="panelKind">面板种类。</param>
                /// <param name="typeName">SDK 或 Kit 的配置类型全名；其它面板忽略。</param>
                /// <param name="curCoord">当前坐标格。</param>
                /// <param name="axis">要启用的维度轴。</param>
                public static void OnDimensionEnabled(ConfigMasterSO master, SerializedObject masterSO, PanelKind panelKind, string typeName, Coord curCoord, DimensionAxis axis)
                {
                    ProjectDimensionAtomically(master, masterSO, panelKind, typeName, curCoord, axis, true);
                }

                /// <summary>
                /// 减维合并：对新掩码下的每个剩余逻辑组，分别保留顶部当前轴取值对应的旧分支。
                /// <para>整个矩阵一次完成；不会拿顶部当前完整坐标覆盖其它剩余逻辑组。</para>
                /// </summary>
                /// <param name="master">编辑期 ConfigMasterSO 实例（工作副本）。</param>
                /// <param name="masterSO">对应 master 的 SerializedObject（工作副本），SDK/Kit 路径必需。</param>
                /// <param name="panelKind">面板种类。</param>
                /// <param name="typeName">SDK 或 Kit 的配置类型全名；其它面板忽略。</param>
                /// <param name="curCoord">当前坐标格。</param>
                /// <param name="axis">要禁用的维度轴。</param>
                public static void OnDimensionDisabled(ConfigMasterSO master, SerializedObject masterSO, PanelKind panelKind, string typeName, Coord curCoord, DimensionAxis axis)
                {
                    ProjectDimensionAtomically(master, masterSO, panelKind, typeName, curCoord, axis, false);
                }

                /// <summary>
                /// 按当前坐标将 Namespace 值写入对应 Override 条目（仅写当前格，不广播同组）。
                /// <para>步骤：按 NamespaceMask 裁剪坐标到勾选轴 → UpsertNamespaceOverride 写入或更新对应 Override 条目。</para>
                /// <para>调用方应在 NamespaceMask 非全不勾时使用；全不勾（IsGlobal）时直接写顶层 Namespace 字段即可。</para>
                /// </summary>
                /// <param name="master">编辑期 ConfigMasterSO 实例（工作副本）。</param>
                /// <param name="curCoord">当前坐标格。</param>
                /// <param name="value">要写入的 Namespace 字符串值。</param>
                public static void SetNamespaceAtCoord(ConfigMasterSO master, Coord curCoord, string value)
                {
                    if (master == null) return;
                    PanelDimensionMask mask = master.NamespaceMask;
                    Coord clipped = ClipCoordToMask(mask, curCoord);
                    UpsertNamespaceOverride(master, mask, clipped, value);
                }

                /// <summary>
                /// 编辑后维持组内一致：将当前坐标格的值深拷贝广播到同组其余格。
                /// <para>ConfigWindow 侦测到字段变更后调用，确保同组格数据与当前格保持一致。</para>
                /// </summary>
                /// <param name="master">编辑期 ConfigMasterSO 实例（工作副本）。</param>
                /// <param name="masterSO">对应 master 的 SerializedObject（工作副本），SDK/Kit 路径必需。</param>
                /// <param name="panelKind">面板种类。</param>
                /// <param name="typeName">SDK 或 Kit 的配置类型全名；其它面板忽略。</param>
                /// <param name="curCoord">当前坐标格（值来源）。</param>
                public static void BroadcastWithinGroup(ConfigMasterSO master, SerializedObject masterSO, PanelKind panelKind, string typeName, Coord curCoord)
                {
                    if (master == null) return;

                    // 顶层类走独立分支
                    if (panelKind == PanelKind.Namespace) { BroadcastNamespace(master, curCoord); return; }
#if UNITY_EDITOR
                    if (panelKind == PanelKind.HybridEditorConfigs) { BroadcastHybridCLR(master, curCoord); return; }
                    if (panelKind == PanelKind.YooAssetEditorConfigs) { BroadcastYooAsset(master, curCoord); return; }
                    if (panelKind == PanelKind.CDNEditorConfigs) { BroadcastCdn(master, curCoord); return; }
#endif

                    PanelDimensionMask mask = GetMask(master, panelKind, typeName);

                    if (panelKind == PanelKind.AppConfigs)
                    {
                        AppConfigs srcValue = DeepCloneAppConfigs(GetAppConfigsFromMaster(master, curCoord));
                        foreach (Coord memberCoord in GroupMembers(master, mask, curCoord))
                        {
                            if (IsSameCoord(memberCoord, curCoord)) continue;
                            FillGroupAppConfigs(master, memberCoord, DeepCloneAppConfigs(srcValue));
                        }
                    }
                    else if (panelKind == PanelKind.PrivacyConfigs)
                    {
                        PrivacyConfigs srcValue = DeepClonePrivacyConfigs(GetPrivacyConfigsFromMaster(master, curCoord));
                        foreach (Coord memberCoord in GroupMembers(master, mask, curCoord))
                        {
                            if (IsSameCoord(memberCoord, curCoord)) continue;
                            FillGroupPrivacyConfigs(master, memberCoord, DeepClonePrivacyConfigs(srcValue));
                        }
                    }
                    else
                    {
                        object snapshot = GetManagedConfigAtCoord(master, panelKind, typeName, curCoord);
                        if (snapshot == null)
                            throw new InvalidOperationException($"同步失败：{curCoord.Platform}/{curCoord.Channel}/{curCoord.Mode} 缺少配置 {typeName}。");
                        foreach (Coord memberCoord in GroupMembers(master, mask, curCoord))
                        {
                            if (IsSameCoord(memberCoord, curCoord)) continue;
                            SetManagedConfigAtCoord(master, panelKind, typeName, memberCoord, DeepCloneManagedRef(snapshot));
                        }
                        masterSO?.Update();
                    }
                }

                /// <summary>
                /// 按现有维度掩码重新投影校验报告点名的配置组，修复从旧项目复制或外部合并后遗留的组内不一致。
                /// <para>未勾选轴以当前编辑坐标对应的分支为准；已勾选轴继续保留各逻辑组自己的值。</para>
                /// <para>SDK / Kit 仅在每个逻辑组都存在来源实例时归一；同组缺失物理格会继承来源实例。</para>
                /// <para>整个操作作用于 WorkingCopy 且具备原子回滚，不直接写入资产。</para>
                /// </summary>
                internal static void NormalizeInvalidGroups(
                    ConfigMasterSO master,
                    SerializedObject masterSO,
                    Coord curCoord,
                    IReadOnlyList<Validator.ValidationIssue> issues)
                {
                    if (master == null || issues == null || issues.Count == 0) return;
                    masterSO?.ApplyModifiedProperties();

                    ConfigMasterSO backup = UnityEngine.Object.Instantiate(master);
                    string originalName = master.name;
                    try
                    {
                        HashSet<string> repaired = new();
                        for (int i = 0; i < issues.Count; i++)
                        {
                            string path = issues[i].Path;
                            if (path.StartsWith("AppConfigs[", StringComparison.Ordinal) && repaired.Add("AppConfigs"))
                                NormalizePanel(master, PanelKind.AppConfigs, null, curCoord);
                            else if (path.StartsWith("PrivacyConfigs[", StringComparison.Ordinal) && repaired.Add("PrivacyConfigs"))
                                NormalizePanel(master, PanelKind.PrivacyConfigs, null, curCoord);
                            else if (TryReadTypedIssue(path, "SDK", out string sdkType) && repaired.Add("SDK|" + sdkType))
                                NormalizeTypedPanelIfComplete(master, PanelKind.SDK, sdkType, curCoord);
                            else if (TryReadTypedIssue(path, "Kit", out string kitType) && repaired.Add("Kit|" + kitType))
                                NormalizeTypedPanelIfComplete(master, PanelKind.Kit, kitType, curCoord);
                        }
                        masterSO?.Update();
                    }
                    catch
                    {
                        EditorUtility.CopySerialized(backup, master);
                        master.name = originalName;
                        masterSO?.Update();
                        throw;
                    }
                    finally
                    {
                        UnityEngine.Object.DestroyImmediate(backup);
                    }
                }

                /// <summary>
                /// 判断维度问题中是否至少有一项可通过当前坐标安全归一。
                /// 结构缺项、空项与重复 Override 不在自动修复范围内。
                /// </summary>
                internal static bool HasRepairableIssues(IReadOnlyList<Validator.ValidationIssue> issues)
                {
                    if (issues == null) return false;
                    for (int i = 0; i < issues.Count; i++)
                    {
                        string path = issues[i].Path ?? string.Empty;
                        if (path.StartsWith("AppConfigs[", StringComparison.Ordinal) ||
                            path.StartsWith("PrivacyConfigs[", StringComparison.Ordinal) ||
                            TryReadTypedIssue(path, "SDK", out _) ||
                            TryReadTypedIssue(path, "Kit", out _)) return true;
                    }
                    return false;
                }

                /// <summary>
                /// 原子地对整个配置面板切换一条维度轴；任何异常都会恢复切换前的完整工作副本。
                /// </summary>
                /// <param name="master">待修改的工作副本。</param>
                /// <param name="masterSO">绑定工作副本的 SerializedObject。</param>
                /// <param name="panelKind">面板种类。</param>
                /// <param name="typeName">SDK 或 Kit 类型全名。</param>
                /// <param name="curCoord">顶部当前坐标，决定新增或被合并轴的值来源。</param>
                /// <param name="axis">要切换的轴。</param>
                /// <param name="enabled">切换后是否启用该轴。</param>
                private static void ProjectDimensionAtomically(
                    ConfigMasterSO master,
                    SerializedObject masterSO,
                    PanelKind panelKind,
                    string typeName,
                    Coord curCoord,
                    DimensionAxis axis,
                    bool enabled)
                {
                    if (master == null) return;
                    masterSO?.ApplyModifiedProperties();

                    ConfigMasterSO backup = UnityEngine.Object.Instantiate(master);
                    string originalName = master.name;
                    try
                    {
                        ProjectAllLogicalGroups(master, panelKind, typeName, curCoord, axis, enabled);
                        masterSO?.Update();
                    }
                    catch
                    {
                        EditorUtility.CopySerialized(backup, master);
                        master.name = originalName;
                        masterSO?.Update();
                        throw;
                    }
                    finally
                    {
                        UnityEngine.Object.DestroyImmediate(backup);
                    }
                }

                /// <summary>
                /// 按新旧掩码对面板的全部逻辑组做一次完整投影，不仅处理当前坐标切片。
                /// </summary>
                private static void ProjectAllLogicalGroups(
                    ConfigMasterSO master,
                    PanelKind panelKind,
                    string typeName,
                    Coord curCoord,
                    DimensionAxis axis,
                    bool enabled)
                {
                    PanelDimensionMask liveMask = GetMask(master, panelKind, typeName);
                    PanelDimensionMask oldMask = CloneMask(liveMask);
                    PanelDimensionMask newMask = CloneMask(liveMask);
                    SetAxis(newMask, axis, enabled);

                    List<ProjectionValue> values = new();
                    foreach (Coord representative in EnumerateLogicalRepresentatives(master, newMask, curCoord))
                    {
                        Coord source = ResolveProjectionSource(oldMask, representative, curCoord);
                        values.Add(new ProjectionValue(representative, CapturePanelValue(master, panelKind, typeName, source)));
                    }
                    if (values.Count == 0)
                        throw new InvalidOperationException("维度切换失败：ConfigMaster 中没有可用的平台、渠道和开发模式坐标。");

                    SetAxis(liveMask, axis, enabled);
                    if (panelKind == PanelKind.Namespace || panelKind == PanelKind.HybridEditorConfigs ||
                        panelKind == PanelKind.YooAssetEditorConfigs || panelKind == PanelKind.CDNEditorConfigs)
                    {
                        RebuildTopLevelGroups(master, panelKind, newMask, values);
                        return;
                    }

                    for (int i = 0; i < values.Count; i++)
                    {
                        foreach (Coord member in GroupMembers(master, newMask, values[i].Representative))
                            ApplyMatrixValue(master, panelKind, typeName, member, values[i].Value);
                    }
                }

                /// <summary>
                /// 在不改变掩码的前提下，用当前坐标补齐每个逻辑组的物理格。
                /// </summary>
                private static void NormalizePanel(
                    ConfigMasterSO master,
                    PanelKind panelKind,
                    string typeName,
                    Coord curCoord)
                {
                    PanelDimensionMask mask = CloneMask(GetMask(master, panelKind, typeName));
                    List<ProjectionValue> values = CaptureLogicalGroupValues(master, panelKind, typeName, mask, curCoord);
                    if (values.Count == 0)
                        throw new InvalidOperationException("配置归一失败：ConfigMaster 中没有可用的平台、渠道和开发模式坐标。");

                    if (panelKind == PanelKind.Namespace || panelKind == PanelKind.HybridEditorConfigs ||
                        panelKind == PanelKind.YooAssetEditorConfigs || panelKind == PanelKind.CDNEditorConfigs)
                    {
                        RebuildTopLevelGroups(master, panelKind, mask, values);
                        return;
                    }

                    for (int i = 0; i < values.Count; i++)
                    {
                        foreach (Coord member in GroupMembers(master, mask, values[i].Representative))
                            ApplyMatrixValue(master, panelKind, typeName, member, values[i].Value);
                    }
                }

                /// <summary>
                /// 对已启用的 SDK / Kit 类型做可恢复归一；来源缺失的类型保持原状，由既有必填校验报告。
                /// </summary>
                private static void NormalizeTypedPanelIfComplete(
                    ConfigMasterSO master,
                    PanelKind panelKind,
                    string typeName,
                    Coord curCoord)
                {
                    if (string.IsNullOrEmpty(typeName)) return;
                    PanelDimensionMask mask = CloneMask(GetMask(master, panelKind, typeName));
                    if (!CanCaptureEveryTypedGroup(master, panelKind, typeName, mask, curCoord)) return;
                    NormalizePanel(master, panelKind, typeName, curCoord);
                }

                /// <summary>
                /// 从 Validator 的 SDK[type] / Kit[type] 路径中提取精确类型名。
                /// </summary>
                private static bool TryReadTypedIssue(string path, string prefix, out string typeName)
                {
                    typeName = null;
                    string marker = prefix + "[";
                    if (string.IsNullOrEmpty(path) || !path.StartsWith(marker, StringComparison.Ordinal)) return false;
                    int end = path.IndexOf(']', marker.Length);
                    if (end <= marker.Length) return false;
                    // 仅接受组值冲突路径 SDK[type][logical-key]。SDK[type]/coord 是同格重复实例，
                    // 属于结构损坏，必须保持 fail-closed，不能尝试投影第一项。
                    if (end + 1 >= path.Length || path[end + 1] != '[') return false;
                    typeName = path.Substring(marker.Length, end - marker.Length);
                    return true;
                }

                /// <summary>
                /// SDK / Kit 归一前确认每个逻辑组都有明确来源；同组缺失的物理格会继承该来源，
                /// 但不会在整个逻辑组都缺失时凭空构造默认实例。
                /// </summary>
                private static bool CanCaptureEveryTypedGroup(
                    ConfigMasterSO master,
                    PanelKind panelKind,
                    string typeName,
                    PanelDimensionMask mask,
                    Coord curCoord)
                {
                    foreach (Coord representative in EnumerateLogicalRepresentatives(master, mask, curCoord))
                    {
                        Coord source = ResolveProjectionSource(mask, representative, curCoord);
                        if (GetManagedConfigAtCoord(master, panelKind, typeName, source) == null) return false;
                    }
                    return true;
                }

                /// <summary>
                /// 捕获现有掩码下每个逻辑组的独立来源快照。
                /// </summary>
                private static List<ProjectionValue> CaptureLogicalGroupValues(
                    ConfigMasterSO master,
                    PanelKind panelKind,
                    string typeName,
                    PanelDimensionMask mask,
                    Coord curCoord)
                {
                    List<ProjectionValue> values = new();
                    foreach (Coord representative in EnumerateLogicalRepresentatives(master, mask, curCoord))
                    {
                        Coord source = ResolveProjectionSource(mask, representative, curCoord);
                        values.Add(new ProjectionValue(representative, CapturePanelValue(master, panelKind, typeName, source)));
                    }
                    return values;
                }

                /// <summary>
                /// 为新掩码枚举全部逻辑组代表坐标；未勾选轴固定为顶部当前值。
                /// </summary>
                private static IEnumerable<Coord> EnumerateLogicalRepresentatives(
                    ConfigMasterSO master,
                    PanelDimensionMask mask,
                    Coord curCoord)
                {
                    HashSet<string> seen = new();
                    foreach (Coord coord in EnumerateAllCoords(master))
                    {
                        Coord representative = new(
                            mask.ByPlatform ? coord.Platform : curCoord.Platform,
                            mask.ByChannel ? coord.Channel : curCoord.Channel,
                            mask.ByDevelopMode ? coord.Mode : curCoord.Mode);
                        string key = BuildLogicalKey(mask, representative);
                        if (seen.Add(key)) yield return representative;
                    }
                }

                /// <summary>
                /// 枚举 ConfigMaster 已存在的全部有效物理坐标，DevelopMode 从枚举定义实时取值。
                /// </summary>
                private static IEnumerable<Coord> EnumerateAllCoords(ConfigMasterSO master)
                {
                    Array modes = Enum.GetValues(typeof(DevelopMode));
                    IReadOnlyList<PlatformChannelEntry> entries = master.GetAllEntries();
                    for (int i = 0; i < entries.Count; i++)
                    {
                        PlatformChannelEntry entry = entries[i];
                        if (entry == null || entry.Platform == PlatformType.None) continue;
                        foreach (DevelopMode mode in modes)
                            yield return new Coord(entry.Platform, entry.Channel, mode);
                    }
                }

                /// <summary>
                /// 按旧掩码确定新逻辑组的数据来源：旧的独立轴沿用该组值，旧的共用轴使用顶部当前值。
                /// </summary>
                private static Coord ResolveProjectionSource(PanelDimensionMask oldMask, Coord representative, Coord curCoord)
                {
                    return new Coord(
                        oldMask.ByPlatform ? representative.Platform : curCoord.Platform,
                        oldMask.ByChannel ? representative.Channel : curCoord.Channel,
                        oldMask.ByDevelopMode ? representative.Mode : curCoord.Mode);
                }

                /// <summary>
                /// 生成仅包含已勾选轴的稳定逻辑键。
                /// </summary>
                private static string BuildLogicalKey(PanelDimensionMask mask, Coord coord)
                {
                    return $"{(mask.ByPlatform ? (int)coord.Platform : -1)}|" +
                           $"{(mask.ByChannel ? (int)coord.Channel : -1)}|" +
                           $"{(mask.ByDevelopMode ? (int)coord.Mode : -1)}";
                }

                /// <summary>
                /// 复制维度掩码，避免计算新旧分组时共享同一实例。
                /// </summary>
                private static PanelDimensionMask CloneMask(PanelDimensionMask source)
                {
                    return new PanelDimensionMask
                    {
                        ByPlatform = source.ByPlatform,
                        ByChannel = source.ByChannel,
                        ByDevelopMode = source.ByDevelopMode,
                    };
                }

                /// <summary>
                /// 捕获指定面板在源坐标的独立快照。
                /// </summary>
                private static object CapturePanelValue(ConfigMasterSO master, PanelKind panelKind, string typeName, Coord source)
                {
                    switch (panelKind)
                    {
                        case PanelKind.AppConfigs:
                            return DeepCloneAppConfigs(GetAppConfigsFromMaster(master, source));
                        case PanelKind.PrivacyConfigs:
                            return DeepClonePrivacyConfigs(GetPrivacyConfigsFromMaster(master, source));
                        case PanelKind.SDK:
                        case PanelKind.Kit:
                            object managed = GetManagedConfigAtCoord(master, panelKind, typeName, source);
                            if (managed == null)
                                throw new InvalidOperationException($"维度切换失败：{source.Platform}/{source.Channel}/{source.Mode} 缺少配置 {typeName}。");
                            return DeepCloneManagedRef(managed);
                        case PanelKind.Namespace:
                            return DimensionalResolver.ResolveNamespace(master, source.Platform, source.Channel, source.Mode);
#if UNITY_EDITOR
                        case PanelKind.HybridEditorConfigs:
                            return DimensionalResolver.ResolveHybridCLR(master, source.Platform, source.Channel, source.Mode);
                        case PanelKind.YooAssetEditorConfigs:
                            return DimensionalResolver.ResolveYooAsset(master, source.Platform, source.Channel, source.Mode);
                        case PanelKind.CDNEditorConfigs:
                            return DeepCloneCdn(DimensionalResolver.ResolveCDNEditorConfigs(master, source.Platform, source.Channel, source.Mode));
#endif
                        default:
                            throw new ArgumentOutOfRangeException(nameof(panelKind), panelKind, null);
                    }
                }

                /// <summary>
                /// 将矩阵面板快照写入指定物理格，每格都使用独立深拷贝。
                /// </summary>
                private static void ApplyMatrixValue(ConfigMasterSO master, PanelKind panelKind, string typeName, Coord target, object value)
                {
                    if (panelKind == PanelKind.AppConfigs)
                    {
                        FillGroupAppConfigs(master, target, DeepCloneAppConfigs((AppConfigs)value));
                        return;
                    }
                    if (panelKind == PanelKind.PrivacyConfigs)
                    {
                        FillGroupPrivacyConfigs(master, target, DeepClonePrivacyConfigs((PrivacyConfigs)value));
                        return;
                    }
                    SetManagedConfigAtCoord(master, panelKind, typeName, target, DeepCloneManagedRef(value));
                }

                /// <summary>
                /// 重建顶层面板的逻辑组，保证新掩码下每个键恰好一条 Override。
                /// </summary>
                private static void RebuildTopLevelGroups(
                    ConfigMasterSO master,
                    PanelKind panelKind,
                    PanelDimensionMask newMask,
                    IReadOnlyList<ProjectionValue> values)
                {
                    switch (panelKind)
                    {
                        case PanelKind.Namespace:
                            master.NamespaceOverrides.Clear();
                            if (newMask.IsGlobal) master.Namespace = (string)values[0].Value;
                            else for (int i = 0; i < values.Count; i++)
                                UpsertNamespaceOverride(master, newMask, ClipCoordToMask(newMask, values[i].Representative), (string)values[i].Value);
                            break;
#if UNITY_EDITOR
                        case PanelKind.HybridEditorConfigs:
                            master.HybridEditorConfigsOverrides.Clear();
                            if (newMask.IsGlobal) ApplyHybridCLRTopLevel(master, (DimensionalResolver.HybridCLRResult)values[0].Value);
                            else for (int i = 0; i < values.Count; i++)
                                UpsertHybridEditorConfigsOverride(master, newMask, ClipCoordToMask(newMask, values[i].Representative), (DimensionalResolver.HybridCLRResult)values[i].Value);
                            break;
                        case PanelKind.YooAssetEditorConfigs:
                            master.YooAssetEditorConfigsOverrides.Clear();
                            if (newMask.IsGlobal) ApplyYooAssetTopLevel(master, (DimensionalResolver.YooAssetResult)values[0].Value);
                            else for (int i = 0; i < values.Count; i++)
                                UpsertYooAssetEditorConfigsOverride(master, newMask, ClipCoordToMask(newMask, values[i].Representative), (DimensionalResolver.YooAssetResult)values[i].Value);
                            break;
                        case PanelKind.CDNEditorConfigs:
                            master.CDNEditorConfigsOverrides.Clear();
                            if (newMask.IsGlobal) master.CDNEditorConfigs = DeepCloneCdn((CDNEditorConfigs)values[0].Value);
                            else for (int i = 0; i < values.Count; i++)
                                UpsertCdnOverride(master, newMask, ClipCoordToMask(newMask, values[i].Representative), (CDNEditorConfigs)values[i].Value);
                            break;
#endif
                    }
                }

                /// <summary>
                /// 取得 SDK 或 Kit 在指定坐标的类型配置。
                /// </summary>
                private static object GetManagedConfigAtCoord(ConfigMasterSO master, PanelKind panelKind, string typeName, Coord coord)
                {
                    if (!master.TryGetEntry(coord.Platform, coord.Channel, out PlatformChannelEntry entry)) return null;
                    if (panelKind == PanelKind.SDK)
                    {
                        List<ISDKPluginConfig> configs = entry.GetSDKConfigs(coord.Mode);
                        for (int i = 0; i < configs.Count; i++)
                            if (configs[i] != null && configs[i].GetType().FullName == typeName) return configs[i];
                    }
                    else
                    {
                        List<IKitConfig> configs = entry.GetKitConfigs(coord.Mode);
                        for (int i = 0; i < configs.Count; i++)
                            if (configs[i] != null && configs[i].GetType().FullName == typeName) return configs[i];
                    }
                    return null;
                }

                /// <summary>
                /// 替换或补入 SDK/Kit 指定类型的独立配置实例。
                /// </summary>
                private static void SetManagedConfigAtCoord(ConfigMasterSO master, PanelKind panelKind, string typeName, Coord coord, object value)
                {
                    if (!master.TryGetEntry(coord.Platform, coord.Channel, out PlatformChannelEntry entry))
                        throw new InvalidOperationException($"维度切换失败：缺少矩阵行 {coord.Platform}/{coord.Channel}。");
                    if (panelKind == PanelKind.SDK)
                    {
                        List<ISDKPluginConfig> configs = entry.GetSDKConfigs(coord.Mode);
                        int index = configs.FindIndex(item => item != null && item.GetType().FullName == typeName);
                        ISDKPluginConfig typed = value as ISDKPluginConfig
                            ?? throw new InvalidOperationException($"维度切换失败：{typeName} 不是有效的 SDK 配置。");
                        if (index >= 0) configs[index] = typed;
                        else configs.Add(typed);
                        return;
                    }

                    List<IKitConfig> kitConfigs = entry.GetKitConfigs(coord.Mode);
                    int kitIndex = kitConfigs.FindIndex(item => item != null && item.GetType().FullName == typeName);
                    IKitConfig kit = value as IKitConfig
                        ?? throw new InvalidOperationException($"维度切换失败：{typeName} 不是有效的 Kit 配置。");
                    if (kitIndex >= 0) kitConfigs[kitIndex] = kit;
                    else kitConfigs.Add(kit);
                }

                /// <summary>
                /// 将 HybridCLR 快照写回顶层全局配置。
                /// </summary>
                private static void ApplyHybridCLRTopLevel(ConfigMasterSO master, DimensionalResolver.HybridCLRResult value)
                {
                    master.HybridEditorConfigs.AotMetadataDlls = DeepCloneDllList(value.AotMetadataDlls);
                    master.HybridEditorConfigs.StartupGameDlls = DeepCloneDllList(value.StartupGameDlls);
                    master.HybridEditorConfigs.RunningGameDlls = DeepCloneDllList(value.RunningGameDlls);
                    master.HybridEditorConfigs.LinkXmlTargetPath = value.LinkXmlTargetPath;
                    master.HybridEditorConfigs.GameEntranceProcedureName = value.GameEntranceProcedureName;
                }

                /// <summary>
                /// 将 YooAsset 快照写回顶层全局配置。
                /// </summary>
                private static void ApplyYooAssetTopLevel(ConfigMasterSO master, DimensionalResolver.YooAssetResult value)
                {
                    master.YooAssetEditorConfigs.YooAssetSettingsPath = value.YooAssetSettingsPath;
                    master.YooAssetEditorConfigs.BundleCollectorSettingPath = value.BundleCollectorSettingPath;
                    master.YooAssetEditorConfigs.YooFolderName = value.YooFolderName;
                    master.YooAssetEditorConfigs.PackageFilePrefix = value.PackageFilePrefix;
                }

                /// <summary>
                /// 单个新逻辑组及其在旧掩码下捕获的数据快照。
                /// </summary>
                private sealed class ProjectionValue
                {
                    public readonly Coord Representative;
                    public readonly object Value;

                    public ProjectionValue(Coord representative, object value)
                    {
                        Representative = representative;
                        Value = value;
                    }
                }

                // -------------------------------------------------------
                // 私有核心：GroupMembers 枚举
                // -------------------------------------------------------

                /// <summary>
                /// 枚举 mask 下与 coord 属于同一组的所有格（含 coord 自身）；
                /// 同组条件：未勾选的轴包含该轴所有取值，勾选的轴只匹配 coord 的当前取值。
                /// <para>遍历 m_Entries（Platform×Channel）× {Debug, Release} 全量后按掩码过滤。</para>
                /// </summary>
                /// <param name="master">编辑期 ConfigMasterSO 实例。</param>
                /// <param name="mask">当前面板掩码。</param>
                /// <param name="coord">参照坐标格。</param>
                /// <returns>所有同组格坐标（含 coord 自身）。</returns>
                private static IEnumerable<Coord> GroupMembers(ConfigMasterSO master, PanelDimensionMask mask, Coord coord)
                {
                    IReadOnlyList<PlatformChannelEntry> entries = master.GetAllEntries();
                    Array modes = Enum.GetValues(typeof(DevelopMode));
                    for (int i = 0; i < entries.Count; i++)
                    {
                        PlatformChannelEntry entry = entries[i];
                        if (entry.Platform == PlatformType.None) continue;
                        if (mask.ByPlatform && entry.Platform != coord.Platform) continue;
                        if (mask.ByChannel && entry.Channel != coord.Channel) continue;
                        foreach (DevelopMode mode in modes)
                        {
                            if (mask.ByDevelopMode && mode != coord.Mode) continue;
                            yield return new Coord(entry.Platform, entry.Channel, mode);
                        }
                    }
                }

                // -------------------------------------------------------
                // 私有：FillGroup（Common 与 SerializedRef 两路）归约体
                // -------------------------------------------------------

                /// <summary>
                /// 向 targetCoord 格写入 AppConfigs 深拷贝值；对应行不存在时静默跳过。
                /// </summary>
                /// <param name="master">编辑期 ConfigMasterSO 实例。</param>
                /// <param name="targetCoord">目标格坐标。</param>
                /// <param name="value">要写入的 AppConfigs 深拷贝值。</param>
                private static void FillGroupAppConfigs(ConfigMasterSO master, Coord targetCoord, AppConfigs value)
                {
                    if (!master.TryGetEntry(targetCoord.Platform, targetCoord.Channel, out PlatformChannelEntry entry)) return;
                    AppConfigs dst = entry.GetAppConfigs(targetCoord.Mode);
                    if (dst == null || value == null) return;
                    dst.AppID = value.AppID;
                    dst.AppAesKey = value.AppAesKey;
                    dst.AppAesIV = value.AppAesIV;
                    dst.CustomConfigCmdName = value.CustomConfigCmdName;
                    dst.CustomName = value.CustomName;
                }

                /// <summary>
                /// 向目标坐标写入隐私配置深拷贝值；对应矩阵行不存在时跳过。
                /// </summary>
                /// <param name="master">编辑期 ConfigMasterSO 实例。</param>
                /// <param name="targetCoord">目标三维坐标。</param>
                /// <param name="value">待写入的隐私配置。</param>
                private static void FillGroupPrivacyConfigs(ConfigMasterSO master, Coord targetCoord, PrivacyConfigs value)
                {
                    if (!master.TryGetEntry(targetCoord.Platform, targetCoord.Channel, out PlatformChannelEntry entry)) return;
                    PrivacyConfigs dst = entry.GetPrivacyConfigs(targetCoord.Mode);
                    if (dst == null || value == null) return;
                    dst.AESKey = value.AESKey;
                    dst.AESIV = value.AESIV;
                }

                /// <summary>
                /// 向 targetCoord 格写入 SerializeReference 深拷贝；
                /// 通过 boxedValue 深拷贝保留多态类型；目标格元素不存在时先 EnsureConfigInstance 补位再拷贝。
                /// </summary>
                /// <param name="master">编辑期 ConfigMasterSO 实例（C# 层）。</param>
                /// <param name="masterSO">工作副本 SerializedObject，用于定位目标格 SerializedProperty。</param>
                /// <param name="panelKind">面板种类（SDK 或 Kit）。</param>
                /// <param name="typeName">配置类型全名。</param>
                /// <param name="targetCoord">目标格坐标。</param>
                /// <param name="srcElemProp">源格对应的 SerializedProperty 元素；null 时静默跳过。</param>
                private static void FillGroupSerializedRef(ConfigMasterSO master, SerializedObject masterSO, PanelKind panelKind, string typeName, Coord targetCoord, SerializedProperty srcElemProp)
                {
                    if (srcElemProp == null) return;
                    if (!master.TryGetEntry(targetCoord.Platform, targetCoord.Channel, out PlatformChannelEntry entry)) return;

                    int entryIndex = master.EditorEntries.IndexOf(entry);
                    if (entryIndex < 0) return;

                    SerializedProperty entriesProp = masterSO.FindProperty("m_Entries");
                    if (entriesProp == null) return;

                    SerializedProperty targetEntryProp = entriesProp.GetArrayElementAtIndex(entryIndex);
                    string byModeFieldName = panelKind == PanelKind.SDK ? "SDKConfigsByMode" : "KitConfigsByMode";
                    SerializedProperty byModeProp = targetEntryProp.FindPropertyRelative(byModeFieldName);
                    if (byModeProp == null) return;

                    int modeIdx = FindModeIndex(byModeProp, targetCoord.Mode);
                    if (modeIdx < 0) return;

                    string configsFieldName = panelKind == PanelKind.SDK ? "SDKConfigs" : "KitConfigs";
                    SerializedProperty configsProp = byModeProp.GetArrayElementAtIndex(modeIdx).FindPropertyRelative(configsFieldName);
                    if (configsProp == null) return;

                    int targetIdx = FindConfigIndexInList(entry, panelKind, typeName, targetCoord.Mode);
                    if (targetIdx < 0)
                    {
                        // EnsureInstance：C# 层补位后重新 Update 刷新 SO
                        EnsureConfigInstance(entry, panelKind, typeName, targetCoord.Mode);
                        masterSO.Update();
                        configsProp = byModeProp.GetArrayElementAtIndex(modeIdx).FindPropertyRelative(configsFieldName);
                        if (configsProp == null) return;
                        targetIdx = FindConfigIndexInList(entry, panelKind, typeName, targetCoord.Mode);
                        if (targetIdx < 0) return;
                    }

                    SerializedProperty dstElemProp = configsProp.GetArrayElementAtIndex(targetIdx);
                    // boxedValue 对 SerializeReference 在内存态返回/写入的是同一对象引用（非深拷贝），
                    // 直接赋值会令同组各格共享实例、跨格编辑互相污染（实测 ReferenceEquals=True）；
                    // 必须经 DeepCloneManagedRef 产生内存态独立的深拷贝，再写回目标格，保留多态且实例独立
                    dstElemProp.boxedValue = DeepCloneManagedRef(srcElemProp.boxedValue);
                }

                // -------------------------------------------------------
                // 私有辅助：掩码取数 / 轴设值
                // -------------------------------------------------------

                /// <summary>
                /// 按面板种类取得对应的 PanelDimensionMask；AppConfigs 使用 master.AppConfigsMask，SDK/Kit 分别调用 GetSDKMask/GetKitMask。
                /// </summary>
                /// <param name="master">编辑期 ConfigMasterSO 实例。</param>
                /// <param name="panelKind">面板种类。</param>
                /// <param name="typeName">SDK/Kit 类型全名；其它面板忽略。</param>
                /// <returns>对应的 PanelDimensionMask 实例，永不为 null。</returns>
                private static PanelDimensionMask GetMask(ConfigMasterSO master, PanelKind panelKind, string typeName)
                {
                    switch (panelKind)
                    {
                        case PanelKind.SDK: return master.GetSDKMask(typeName);
                        case PanelKind.Kit: return master.GetKitMask(typeName);
                        case PanelKind.PrivacyConfigs: return master.PrivacyConfigsMask;
                        case PanelKind.Namespace: return master.NamespaceMask;
#if UNITY_EDITOR
                        case PanelKind.HybridEditorConfigs: return master.HybridEditorConfigsMask;
                        case PanelKind.YooAssetEditorConfigs: return master.YooAssetEditorConfigsMask;
                        case PanelKind.CDNEditorConfigs: return master.CDNEditorConfigsMask;
#endif
                        default: return master.AppConfigsMask;
                    }
                }

                /// <summary>
                /// 设置 mask 指定轴的 bool 值。
                /// </summary>
                /// <param name="mask">要修改的掩码。</param>
                /// <param name="axis">目标轴。</param>
                /// <param name="value">目标值。</param>
                private static void SetAxis(PanelDimensionMask mask, DimensionAxis axis, bool value)
                {
                    switch (axis)
                    {
                        case DimensionAxis.Platform: mask.ByPlatform = value; break;
                        case DimensionAxis.Channel: mask.ByChannel = value; break;
                        case DimensionAxis.DevelopMode: mask.ByDevelopMode = value; break;
                    }
                }

                // -------------------------------------------------------
                // 私有辅助：SerializeReference 深拷贝（内存态独立，JsonUtility round-trip）
                // -------------------------------------------------------

                /// <summary>
                /// 深拷贝 SerializeReference 多态对象，产生与源完全独立的新实例（内存态独立，非仅 CopySerialized 存盘后独立）。
                /// 经 JsonUtility round-trip 实现：FromJsonOverwrite 写入 Activator 新建的同类型实例，保留运行时多态类型 + 值字段一致 + 实例独立。
                /// 约束：SDK/Kit 配置类必须是可被 JsonUtility 序列化的叶子数据（[Serializable] 简单值字段），
                /// 禁止内嵌 [SerializeReference] 多态字段——JsonUtility 不保留嵌套多态，届时会丢失子引用类型。
                /// 替代历史方案：dstProp.boxedValue = srcProp.boxedValue 对 managed reference 在内存态共享同一引用（非深拷贝），
                /// 会导致同组各格共享对象、跨格编辑互相污染，已废止。
                /// </summary>
                /// <param name="src">源 SerializeReference 对象；为 null 时返回 null。</param>
                /// <returns>与 src 字段一致、类型相同、实例独立的深拷贝；src 为 null 时返回 null。</returns>
                internal static object DeepCloneManagedRef(object src)
                {
                    if (src == null) return null;
                    Type type = src.GetType();
                    object copy = Activator.CreateInstance(type);
                    JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(src), copy);
                    return copy;
                }

                // -------------------------------------------------------
                // 私有辅助：Common 深拷贝（对齐 Exporter.CloneAppConfigs 逐字段模式）
                // -------------------------------------------------------

                /// <summary>
                /// 深拷贝 AppConfigs；src 为 null 时返回 null，与 Exporter.CloneAppConfigs 逐字段模式一致。
                /// </summary>
                /// <param name="src">待拷贝的源实例。</param>
                /// <returns>字段值与 src 相同的新 AppConfigs 实例；src 为 null 时返回 null。</returns>
                private static AppConfigs DeepCloneAppConfigs(AppConfigs src)
                {
                    if (src == null) return null;
                    return new AppConfigs
                    {
                        AppID = src.AppID,
                        AppAesKey = src.AppAesKey,
                        AppAesIV = src.AppAesIV,
                        CustomConfigCmdName = src.CustomConfigCmdName,
                        CustomName = src.CustomName,
                    };
                }

                /// <summary>
                /// 深拷贝隐私配置，避免维度组之间共享引用。
                /// </summary>
                /// <param name="src">源隐私配置。</param>
                /// <returns>字段值相同的独立实例；源为空时返回 null。</returns>
                private static PrivacyConfigs DeepClonePrivacyConfigs(PrivacyConfigs src)
                {
                    if (src == null) return null;
                    return new PrivacyConfigs { AESKey = src.AESKey, AESIV = src.AESIV };
                }

                /// <summary>
                /// 从 master C# 层取指定坐标格的 AppConfigs；行不存在时返回 null。
                /// </summary>
                /// <param name="master">编辑期 ConfigMasterSO 实例。</param>
                /// <param name="coord">目标坐标格。</param>
                /// <returns>对应 AppConfigs，行不存在时返回 null。</returns>
                private static AppConfigs GetAppConfigsFromMaster(ConfigMasterSO master, Coord coord)
                {
                    if (!master.TryGetEntry(coord.Platform, coord.Channel, out PlatformChannelEntry entry)) return null;
                    return entry.GetAppConfigs(coord.Mode);
                }

                /// <summary>
                /// 从现有矩阵行只读取得当前坐标隐私配置，不创建缺失行。
                /// </summary>
                /// <param name="master">编辑期 ConfigMasterSO 实例。</param>
                /// <param name="coord">目标三维坐标。</param>
                /// <returns>命中的隐私配置；矩阵行不存在时返回 null。</returns>
                private static PrivacyConfigs GetPrivacyConfigsFromMaster(ConfigMasterSO master, Coord coord)
                {
                    if (!master.TryGetEntry(coord.Platform, coord.Channel, out PlatformChannelEntry entry)) return null;
                    return entry.GetPrivacyConfigs(coord.Mode);
                }

                // -------------------------------------------------------
                // 私有辅助：SerializedProperty 路径定位
                // -------------------------------------------------------

                /// <summary>
                /// 从 SerializedObject 定位到 coord 格中 typeName 类型对应的元素 SerializedProperty；
                /// 路径：m_Entries[entryIndex].SDKConfigsByMode[modeIndex].SDKConfigs[idx]（Kit 类同）。
                /// 元素不存在时返回 null。
                /// </summary>
                /// <param name="masterSO">工作副本 SerializedObject（已 Update）。</param>
                /// <param name="master">工作副本 C# 实例（用于索引计算）。</param>
                /// <param name="panelKind">面板种类（SDK 或 Kit）。</param>
                /// <param name="typeName">配置类型全名。</param>
                /// <param name="coord">目标坐标格。</param>
                /// <returns>对应元素的 SerializedProperty，或 null。</returns>
                private static SerializedProperty FindSerializedRefProp(SerializedObject masterSO, ConfigMasterSO master, PanelKind panelKind, string typeName, Coord coord)
                {
                    if (!master.TryGetEntry(coord.Platform, coord.Channel, out PlatformChannelEntry entry)) return null;
                    int entryIndex = master.EditorEntries.IndexOf(entry);
                    if (entryIndex < 0) return null;

                    SerializedProperty entriesProp = masterSO.FindProperty("m_Entries");
                    if (entriesProp == null) return null;

                    SerializedProperty entryProp = entriesProp.GetArrayElementAtIndex(entryIndex);
                    string byModeFieldName = panelKind == PanelKind.SDK ? "SDKConfigsByMode" : "KitConfigsByMode";
                    SerializedProperty byModeProp = entryProp.FindPropertyRelative(byModeFieldName);
                    if (byModeProp == null) return null;

                    int modeIdx = FindModeIndex(byModeProp, coord.Mode);
                    if (modeIdx < 0) return null;

                    string configsFieldName = panelKind == PanelKind.SDK ? "SDKConfigs" : "KitConfigs";
                    SerializedProperty configsProp = byModeProp.GetArrayElementAtIndex(modeIdx).FindPropertyRelative(configsFieldName);
                    if (configsProp == null) return null;

                    int elemIdx = FindConfigIndexInList(entry, panelKind, typeName, coord.Mode);
                    if (elemIdx < 0) return null;

                    return configsProp.GetArrayElementAtIndex(elemIdx);
                }

                /// <summary>
                /// 在 SDKConfigsByMode / KitConfigsByMode 数组中查找指定 DevelopMode 的分组索引。
                /// </summary>
                /// <param name="byModeProp">SDKConfigsByMode 或 KitConfigsByMode 的 SerializedProperty。</param>
                /// <param name="mode">目标开发模式。</param>
                /// <returns>命中的数组索引，未命中返回 -1。</returns>
                private static int FindModeIndex(SerializedProperty byModeProp, DevelopMode mode)
                {
                    for (int m = 0; m < byModeProp.arraySize; m++)
                    {
                        SerializedProperty modeEntryProp = byModeProp.GetArrayElementAtIndex(m);
                        SerializedProperty modeProp = modeEntryProp.FindPropertyRelative("Mode");
                        if (modeProp != null && (DevelopMode)modeProp.enumValueIndex == mode)
                            return m;
                    }
                    return -1;
                }

                /// <summary>
                /// 在 entry 的 C# 层 SDK/Kit 配置列表中查找 typeName 类型对应元素的下标。
                /// </summary>
                /// <param name="entry">目标矩阵行。</param>
                /// <param name="panelKind">面板种类（SDK 或 Kit）。</param>
                /// <param name="typeName">配置类型全名。</param>
                /// <param name="mode">目标开发模式。</param>
                /// <returns>命中元素的下标；未命中返回 -1。</returns>
                private static int FindConfigIndexInList(PlatformChannelEntry entry, PanelKind panelKind, string typeName, DevelopMode mode)
                {
                    if (panelKind == PanelKind.SDK)
                    {
                        List<ISDKPluginConfig> list = entry.GetSDKConfigs(mode);
                        for (int i = 0; i < list.Count; i++)
                        {
                            if (list[i] != null && list[i].GetType().FullName == typeName) return i;
                        }
                    }
                    else if (panelKind == PanelKind.Kit)
                    {
                        List<IKitConfig> list = entry.GetKitConfigs(mode);
                        for (int i = 0; i < list.Count; i++)
                        {
                            if (list[i] != null && list[i].GetType().FullName == typeName) return i;
                        }
                    }
                    return -1;
                }

                /// <summary>
                /// EnsureConfigInstance：通过反射找到 typeName 对应类型，创建无参实例并追加到目标格的配置列表。
                /// 类型未找到或无无参构造器时输出警告并静默跳过，不抛出异常。
                /// </summary>
                /// <param name="entry">目标矩阵行。</param>
                /// <param name="panelKind">面板种类（SDK 或 Kit）。</param>
                /// <param name="typeName">配置类型全名。</param>
                /// <param name="mode">目标开发模式。</param>
                private static void EnsureConfigInstance(PlatformChannelEntry entry, PanelKind panelKind, string typeName, DevelopMode mode)
                {
                    Type type = FindTypeByName(typeName);
                    if (type == null)
                    {
                        Log.Warning(LogTag.Editor, "[DimensionProjector] EnsureConfigInstance: 未找到类型 {0}，跳过补位。", typeName);
                        return;
                    }
                    try
                    {
                        object instance = Activator.CreateInstance(type);
                        if (panelKind == PanelKind.SDK && instance is ISDKPluginConfig sdkCfg)
                        {
                            entry.GetSDKConfigs(mode).Add(sdkCfg);
                        }
                        else if (panelKind == PanelKind.Kit && instance is IKitConfig kitCfg)
                        {
                            entry.GetKitConfigs(mode).Add(kitCfg);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(LogTag.Editor, "[DimensionProjector] EnsureConfigInstance: 创建 {0} 实例失败：{1}", typeName, ex.Message);
                    }
                }

                // -------------------------------------------------------
                // 私有辅助：坐标比较 / 类型查找
                // -------------------------------------------------------

                /// <summary>
                /// 判断两个坐标是否指向同一格（三轴全部相同）。
                /// </summary>
                /// <param name="a">坐标 A。</param>
                /// <param name="b">坐标 B。</param>
                /// <returns>三轴全部相同时返回 true。</returns>
                private static bool IsSameCoord(Coord a, Coord b)
                {
                    return a.Platform == b.Platform && a.Channel == b.Channel && a.Mode == b.Mode;
                }

                /// <summary>
                /// 在已加载的所有程序集中按全名查找类型；未命中返回 null。
                /// </summary>
                /// <param name="typeName">类型全名（命名空间.类名）。</param>
                /// <returns>找到的 Type，或 null。</returns>
                private static Type FindTypeByName(string typeName)
                {
                    foreach (System.Reflection.Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        Type t = asm.GetType(typeName);
                        if (t != null) return t;
                    }
                    return null;
                }

                // -------------------------------------------------------
                // 私有辅助：顶层类 Override 列表操作（Namespace / HybridCLR / YooAsset）
                // -------------------------------------------------------

                /// <summary>
                /// 按掩码将坐标裁剪到勾选轴分量，未勾选轴填 None 哨兵（DevelopMode 无 None 则保留原值）。
                /// <para>用于 Override 条目的坐标写入，确保存值规则与匹配算法对称。</para>
                /// </summary>
                /// <param name="mask">当前掩码。</param>
                /// <param name="coord">原始坐标。</param>
                /// <returns>裁剪后的坐标。</returns>
                private static Coord ClipCoordToMask(PanelDimensionMask mask, Coord coord)
                {
                    PlatformType p = mask.ByPlatform ? coord.Platform : PlatformType.None;
                    ChannelType c = mask.ByChannel ? coord.Channel : ChannelType.None;
                    DevelopMode m = mask.ByDevelopMode ? coord.Mode : default;
                    return new Coord(p, c, m);
                }

                /// <summary>
                /// 判断 Override 条目坐标是否与给定坐标在当前掩码下属于同一组（即同组匹配）。
                /// <para>勾选轴要求分量相等，未勾选轴跳过（无论 override 存的是什么值）。</para>
                /// </summary>
                /// <param name="mask">当前掩码。</param>
                /// <param name="entryCoord">Override 条目坐标。</param>
                /// <param name="targetCoord">参照坐标。</param>
                /// <returns>勾选轴分量全部匹配时返回 true。</returns>
                private static bool IsOverrideInGroup(PanelDimensionMask mask, Coord entryCoord, Coord targetCoord)
                {
                    if (mask.ByPlatform && entryCoord.Platform != targetCoord.Platform) return false;
                    if (mask.ByChannel && entryCoord.Channel != targetCoord.Channel) return false;
                    if (mask.ByDevelopMode && entryCoord.Mode != targetCoord.Mode) return false;
                    return true;
                }

                /// <summary>
                /// 从 NamespaceOverride 条目取坐标。
                /// </summary>
                private static Coord OverrideCoord(NamespaceOverride o) => new Coord(o.Platform, o.Channel, o.DevelopMode);

                // ——— Namespace 顶层类操作 ———

                /// <summary>
                /// Namespace 面板广播：将当前坐标的值同步到同组所有 Override 条目。
                /// </summary>
                /// <param name="master">编辑期 ConfigMasterSO 实例（工作副本）。</param>
                /// <param name="curCoord">当前坐标格（值来源）。</param>
                private static void BroadcastNamespace(ConfigMasterSO master, Coord curCoord)
                {
                    PanelDimensionMask mask = master.NamespaceMask;
                    if (!mask.ByPlatform && !mask.ByChannel && !mask.ByDevelopMode) return;
                    string snapshot = DimensionalResolver.ResolveNamespace(master, curCoord.Platform, curCoord.Channel, curCoord.Mode);
                    Coord clipped = ClipCoordToMask(mask, curCoord);
                    foreach (NamespaceOverride o in master.NamespaceOverrides)
                    {
                        if (!IsOverrideInGroup(mask, OverrideCoord(o), clipped)) continue;
                        o.Value = snapshot;
                    }
                }

                /// <summary>
                /// 在 NamespaceOverrides 列表中按裁剪坐标找到同组首个条目并覆写 Value，无则追加新条目。
                /// </summary>
                private static void UpsertNamespaceOverride(ConfigMasterSO master, PanelDimensionMask mask, Coord clipped, string value)
                {
                    for (int i = 0; i < master.NamespaceOverrides.Count; i++)
                    {
                        if (!IsOverrideInGroup(mask, OverrideCoord(master.NamespaceOverrides[i]), clipped)) continue;
                        master.NamespaceOverrides[i].Value = value;
                        return;
                    }
                    master.NamespaceOverrides.Add(new NamespaceOverride
                    {
                        Platform = clipped.Platform,
                        Channel = clipped.Channel,
                        DevelopMode = clipped.Mode,
                        Value = value,
                    });
                }

#if UNITY_EDITOR
                // ——— HybridCLR 顶层类操作 ———

                /// <summary>
                /// 从 HybridEditorConfigsOverride 条目取坐标。
                /// </summary>
                private static Coord OverrideCoord(HybridEditorConfigsOverride o) => new Coord(o.Platform, o.Channel, o.DevelopMode);

                /// <summary>
                /// 从 YooAssetEditorConfigsOverride 条目取坐标。
                /// </summary>
                private static Coord OverrideCoord(YooAssetEditorConfigsOverride o) => new Coord(o.Platform, o.Channel, o.DevelopMode);

                /// <summary>
                /// 从 CDNEditorConfigsOverride 条目取坐标。
                /// </summary>
                private static Coord OverrideCoord(CDNEditorConfigsOverride o) => new Coord(o.Platform, o.Channel, o.DevelopMode);

                /// <summary>
                /// HybridCLR 面板广播：将当前坐标的值同步到同组所有 Override 条目。
                /// </summary>
                /// <param name="master">编辑期 ConfigMasterSO 实例（工作副本）。</param>
                /// <param name="curCoord">当前坐标格（值来源）。</param>
                private static void BroadcastHybridCLR(ConfigMasterSO master, Coord curCoord)
                {
                    PanelDimensionMask mask = master.HybridEditorConfigsMask;
                    if (!mask.ByPlatform && !mask.ByChannel && !mask.ByDevelopMode) return;
                    DimensionalResolver.HybridCLRResult snapshot = DimensionalResolver.ResolveHybridCLR(master, curCoord.Platform, curCoord.Channel, curCoord.Mode);
                    Coord clipped = ClipCoordToMask(mask, curCoord);
                    foreach (HybridEditorConfigsOverride o in master.HybridEditorConfigsOverrides)
                    {
                        if (!IsOverrideInGroup(mask, OverrideCoord(o), clipped)) continue;
                        ApplyHybridCLRResult(o, snapshot);
                    }
                }

                /// <summary>
                /// 在 HybridEditorConfigsOverrides 列表中按裁剪坐标找到同组首个条目并覆写多字段，无则追加新条目。
                /// </summary>
                private static void UpsertHybridEditorConfigsOverride(ConfigMasterSO master, PanelDimensionMask mask, Coord clipped, DimensionalResolver.HybridCLRResult snapshot)
                {
                    for (int i = 0; i < master.HybridEditorConfigsOverrides.Count; i++)
                    {
                        if (!IsOverrideInGroup(mask, OverrideCoord(master.HybridEditorConfigsOverrides[i]), clipped)) continue;
                        ApplyHybridCLRResult(master.HybridEditorConfigsOverrides[i], snapshot);
                        return;
                    }
                    HybridEditorConfigsOverride entry = new HybridEditorConfigsOverride
                    {
                        Platform = clipped.Platform,
                        Channel = clipped.Channel,
                        DevelopMode = clipped.Mode,
                    };
                    ApplyHybridCLRResult(entry, snapshot);
                    master.HybridEditorConfigsOverrides.Add(entry);
                }

                /// <summary>
                /// 只读查找当前坐标在 HybridEditorConfigsOverrides 中匹配的首个条目索引。
                /// 不创建条目；IsGlobal 或无命中时返回 -1。供面板 Dll 列表在不触发懒创建的前提下
                /// 解析显示用的 SerializedProperty 路径（无命中时调用方回落顶层字段）。
                /// </summary>
                /// <param name="master">编辑期 ConfigMasterSO 实例（工作副本）。</param>
                /// <param name="curCoord">当前坐标格。</param>
                /// <returns>命中条目索引；IsGlobal 或无命中时返回 -1。</returns>
                public static int FindHybridEditorConfigsOverrideIndexAtCoord(ConfigMasterSO master, Coord curCoord)
                {
                    if (master == null) return -1;
                    PanelDimensionMask mask = master.HybridEditorConfigsMask;
                    if (!mask.ByPlatform && !mask.ByChannel && !mask.ByDevelopMode) return -1;
                    Coord clipped = ClipCoordToMask(mask, curCoord);
                    for (int i = 0; i < master.HybridEditorConfigsOverrides.Count; i++)
                    {
                        if (!IsOverrideInGroup(mask, OverrideCoord(master.HybridEditorConfigsOverrides[i]), clipped)) continue;
                        return i;
                    }
                    return -1;
                }

                /// <summary>
                /// 确保当前坐标在 HybridEditorConfigsOverrides 中存在对应条目并返回其索引，供面板 Dll 列表绑定 SerializedProperty。
                /// 进入坐标即建份语义：mask 非全局且无命中条目时，新建条目并以当前坐标 ResolveHybridCLR 结果
                /// （含顶层回落的 Dll 列表快照与字符串字段）填充，使 ReorderableList 绑定 Override 内嵌列表时
                /// 显示与顶层一致，所有后续写入（增删改/拖拽排序/选择按钮）天然落该坐标份而不污染全局顶层。
                /// IsGlobal 时返回 -1，调用方应改绑顶层字段。
                /// </summary>
                /// <param name="master">编辑期 ConfigMasterSO 实例（工作副本）。</param>
                /// <param name="curCoord">当前坐标格。</param>
                /// <returns>命中或新建条目的索引；IsGlobal 时返回 -1。</returns>
                public static int EnsureHybridEditorConfigsOverrideIndexAtCoord(ConfigMasterSO master, Coord curCoord)
                {
                    if (master == null) return -1;
                    PanelDimensionMask mask = master.HybridEditorConfigsMask;
                    if (!mask.ByPlatform && !mask.ByChannel && !mask.ByDevelopMode) return -1;
                    Coord clipped = ClipCoordToMask(mask, curCoord);
                    for (int i = 0; i < master.HybridEditorConfigsOverrides.Count; i++)
                    {
                        if (!IsOverrideInGroup(mask, OverrideCoord(master.HybridEditorConfigsOverrides[i]), clipped)) continue;
                        return i;
                    }
                    // 新建条目并以当前坐标 Resolve 结果填充（Dll 列表深拷贝顶层快照 + 字符串字段）
                    DimensionalResolver.HybridCLRResult snapshot = DimensionalResolver.ResolveHybridCLR(master, curCoord.Platform, curCoord.Channel, curCoord.Mode);
                    HybridEditorConfigsOverride entry = new HybridEditorConfigsOverride
                    {
                        Platform = clipped.Platform,
                        Channel = clipped.Channel,
                        DevelopMode = clipped.Mode,
                    };
                    ApplyHybridCLRResult(entry, snapshot);
                    master.HybridEditorConfigsOverrides.Add(entry);
                    return master.HybridEditorConfigsOverrides.Count - 1;
                }

                /// <summary>
                /// 确保当前坐标在 HybridEditorConfigsOverrides 中存在对应条目并返回其引用，供面板字段控件直接写入单字段。
                /// 当 HybridEditorConfigsMask 为全不勾（IsGlobal）时返回 null，调用方应改写顶层字段。
                /// </summary>
                /// <param name="master">编辑期 ConfigMasterSO 实例（工作副本）。</param>
                /// <param name="curCoord">当前坐标格。</param>
                /// <returns>命中或新建的 HybridEditorConfigsOverride 条目引用；IsGlobal 时返回 null。</returns>
                public static HybridEditorConfigsOverride EnsureHybridEditorConfigsOverrideAtCoord(ConfigMasterSO master, Coord curCoord)
                {
                    int idx = EnsureHybridEditorConfigsOverrideIndexAtCoord(master, curCoord);
                    return idx < 0 ? null : master.HybridEditorConfigsOverrides[idx];
                }

                /// <summary>
                /// 将 HybridCLRResult 多字段写入 Override 条目（深拷贝 List，避免共享引用）。
                /// </summary>
                /// <param name="target">目标 Override 条目。</param>
                /// <param name="result">值来源快照。</param>
                private static void ApplyHybridCLRResult(HybridEditorConfigsOverride target, DimensionalResolver.HybridCLRResult result)
                {
                    target.AotMetadataDlls = DeepCloneDllList(result.AotMetadataDlls);
                    target.StartupGameDlls = DeepCloneDllList(result.StartupGameDlls);
                    target.RunningGameDlls = DeepCloneDllList(result.RunningGameDlls);
                    target.LinkXmlTargetPath = result.LinkXmlTargetPath;
                    target.GameEntranceProcedureName = result.GameEntranceProcedureName;
                }

                // ——— YooAsset 顶层类操作 ———

                /// <summary>
                /// YooAsset 面板广播：将当前坐标的值同步到同组所有 Override 条目。
                /// </summary>
                /// <param name="master">编辑期 ConfigMasterSO 实例（工作副本）。</param>
                /// <param name="curCoord">当前坐标格（值来源）。</param>
                private static void BroadcastYooAsset(ConfigMasterSO master, Coord curCoord)
                {
                    PanelDimensionMask mask = master.YooAssetEditorConfigsMask;
                    if (!mask.ByPlatform && !mask.ByChannel && !mask.ByDevelopMode) return;
                    DimensionalResolver.YooAssetResult snapshot = DimensionalResolver.ResolveYooAsset(master, curCoord.Platform, curCoord.Channel, curCoord.Mode);
                    Coord clipped = ClipCoordToMask(mask, curCoord);
                    foreach (YooAssetEditorConfigsOverride o in master.YooAssetEditorConfigsOverrides)
                    {
                        if (!IsOverrideInGroup(mask, OverrideCoord(o), clipped)) continue;
                        o.YooAssetSettingsPath = snapshot.YooAssetSettingsPath;
                        o.BundleCollectorSettingPath = snapshot.BundleCollectorSettingPath;
                        o.YooFolderName = snapshot.YooFolderName;
                        o.PackageFilePrefix = snapshot.PackageFilePrefix;
                    }
                }

                /// <summary>
                /// 在 YooAssetEditorConfigsOverrides 列表中按裁剪坐标找到同组首个条目并覆写两路径，无则追加新条目。
                /// </summary>
                private static void UpsertYooAssetEditorConfigsOverride(ConfigMasterSO master, PanelDimensionMask mask, Coord clipped, DimensionalResolver.YooAssetResult snapshot)
                {
                    for (int i = 0; i < master.YooAssetEditorConfigsOverrides.Count; i++)
                    {
                        if (!IsOverrideInGroup(mask, OverrideCoord(master.YooAssetEditorConfigsOverrides[i]), clipped)) continue;
                        master.YooAssetEditorConfigsOverrides[i].YooAssetSettingsPath = snapshot.YooAssetSettingsPath;
                        master.YooAssetEditorConfigsOverrides[i].BundleCollectorSettingPath = snapshot.BundleCollectorSettingPath;
                        master.YooAssetEditorConfigsOverrides[i].YooFolderName = snapshot.YooFolderName;
                        master.YooAssetEditorConfigsOverrides[i].PackageFilePrefix = snapshot.PackageFilePrefix;
                        return;
                    }
                    master.YooAssetEditorConfigsOverrides.Add(new YooAssetEditorConfigsOverride
                    {
                        Platform = clipped.Platform,
                        Channel = clipped.Channel,
                        DevelopMode = clipped.Mode,
                        YooAssetSettingsPath = snapshot.YooAssetSettingsPath,
                        BundleCollectorSettingPath = snapshot.BundleCollectorSettingPath,
                        YooFolderName = snapshot.YooFolderName,
                        PackageFilePrefix = snapshot.PackageFilePrefix,
                    });
                }

                /// <summary>
                /// 确保当前坐标在 YooAssetEditorConfigsOverrides 中存在对应条目并返回其引用，供面板路径控件直接写入单字段。
                /// 当 YooAssetEditorConfigsMask 为全不勾（IsGlobal）时返回 null，调用方应改写顶层字段。
                /// </summary>
                /// <param name="master">编辑期 ConfigMasterSO 实例（工作副本）。</param>
                /// <param name="curCoord">当前坐标格。</param>
                /// <returns>命中或新建的 YooAssetEditorConfigsOverride 条目引用；IsGlobal 时返回 null。</returns>
                public static YooAssetEditorConfigsOverride EnsureYooAssetEditorConfigsOverrideAtCoord(ConfigMasterSO master, Coord curCoord)
                {
                    if (master == null) return null;
                    PanelDimensionMask mask = master.YooAssetEditorConfigsMask;
                    if (!mask.ByPlatform && !mask.ByChannel && !mask.ByDevelopMode) return null;
                    Coord clipped = ClipCoordToMask(mask, curCoord);
                    for (int i = 0; i < master.YooAssetEditorConfigsOverrides.Count; i++)
                    {
                        if (!IsOverrideInGroup(mask, OverrideCoord(master.YooAssetEditorConfigsOverrides[i]), clipped)) continue;
                        return master.YooAssetEditorConfigsOverrides[i];
                    }
                    YooAssetEditorConfigsOverride entry = new YooAssetEditorConfigsOverride
                    {
                        Platform = clipped.Platform,
                        Channel = clipped.Channel,
                        DevelopMode = clipped.Mode,
                    };
                    master.YooAssetEditorConfigsOverrides.Add(entry);
                    return entry;
                }

                /// <summary>
                /// 深拷贝 DllMasterAssetEntry 列表；DllMasterAssetEntry 是 struct（三字段均为不可变 string），
                /// new List(original) 逐元素值拷贝即为安全深拷贝，禁共享引用。
                /// </summary>
                /// <param name="source">源列表，可为 null。</param>
                /// <returns>独立的新 List，source 为 null 时返回空列表。</returns>
                private static List<DllMasterAssetEntry> DeepCloneDllList(List<DllMasterAssetEntry> source)
                {
                    if (source == null) return new List<DllMasterAssetEntry>();
                    return new List<DllMasterAssetEntry>(source);
                }

                // ——— CDN 顶层类操作 ———

                /// <summary>
                /// CDN 面板广播：将当前坐标的 CDN 部署整套配置同步到同组所有 Override 条目。
                /// </summary>
                /// <param name="master">编辑期 ConfigMasterSO 实例（工作副本）。</param>
                /// <param name="curCoord">当前坐标格（值来源）。</param>
                private static void BroadcastCdn(ConfigMasterSO master, Coord curCoord)
                {
                    PanelDimensionMask mask = master.CDNEditorConfigsMask;
                    if (!mask.ByPlatform && !mask.ByChannel && !mask.ByDevelopMode) return;
                    CDNEditorConfigs snapshot = DimensionalResolver.ResolveCDNEditorConfigs(master, curCoord.Platform, curCoord.Channel, curCoord.Mode);
                    Coord clipped = ClipCoordToMask(mask, curCoord);
                    foreach (CDNEditorConfigsOverride o in master.CDNEditorConfigsOverrides)
                    {
                        if (!IsOverrideInGroup(mask, OverrideCoord(o), clipped)) continue;
                        ApplyCdnResult(o, snapshot);
                    }
                }

                /// <summary>
                /// 在 CDNEditorConfigsOverrides 列表中按裁剪坐标找到同组首个条目并覆写整套配置，无则追加新条目。
                /// </summary>
                private static void UpsertCdnOverride(ConfigMasterSO master, PanelDimensionMask mask, Coord clipped, CDNEditorConfigs snapshot)
                {
                    for (int i = 0; i < master.CDNEditorConfigsOverrides.Count; i++)
                    {
                        if (!IsOverrideInGroup(mask, OverrideCoord(master.CDNEditorConfigsOverrides[i]), clipped)) continue;
                        ApplyCdnResult(master.CDNEditorConfigsOverrides[i], snapshot);
                        return;
                    }
                    CDNEditorConfigsOverride entry = new CDNEditorConfigsOverride
                    {
                        Platform = clipped.Platform,
                        Channel = clipped.Channel,
                        DevelopMode = clipped.Mode,
                    };
                    ApplyCdnResult(entry, snapshot);
                    master.CDNEditorConfigsOverrides.Add(entry);
                }

                /// <summary>
                /// 确保当前坐标在 CDNEditorConfigsOverrides 中存在对应条目并返回其引用，供面板字段控件直接写入单字段。
                /// 进入坐标即建份语义：mask 非全局且无命中条目时，新建条目并以当前坐标 ResolveCDNEditorConfigs 结果
                /// （含顶层回落的整套配置快照）填充，使进入坐标后显示与顶层一致，
                /// 所有后续写入天然落该坐标份而不污染全局顶层。
                /// 当 CDNEditorConfigsMask 为全不勾（IsGlobal）时返回 null，调用方应改写顶层 CDNEditorConfigs 字段。
                /// </summary>
                /// <param name="master">编辑期 ConfigMasterSO 实例（工作副本）。</param>
                /// <param name="curCoord">当前坐标格。</param>
                /// <returns>命中或新建的 CDNEditorConfigsOverride 条目引用；IsGlobal 时返回 null。</returns>
                public static CDNEditorConfigsOverride EnsureCDNEditorConfigsOverrideAtCoord(ConfigMasterSO master, Coord curCoord)
                {
                    if (master == null) return null;
                    PanelDimensionMask mask = master.CDNEditorConfigsMask;
                    if (!mask.ByPlatform && !mask.ByChannel && !mask.ByDevelopMode) return null;
                    Coord clipped = ClipCoordToMask(mask, curCoord);
                    for (int i = 0; i < master.CDNEditorConfigsOverrides.Count; i++)
                    {
                        if (!IsOverrideInGroup(mask, OverrideCoord(master.CDNEditorConfigsOverrides[i]), clipped)) continue;
                        return master.CDNEditorConfigsOverrides[i];
                    }
                    CDNEditorConfigs snapshot = DimensionalResolver.ResolveCDNEditorConfigs(master, curCoord.Platform, curCoord.Channel, curCoord.Mode);
                    CDNEditorConfigsOverride entry = new CDNEditorConfigsOverride
                    {
                        Platform = clipped.Platform,
                        Channel = clipped.Channel,
                        DevelopMode = clipped.Mode,
                    };
                    ApplyCdnResult(entry, snapshot);
                    master.CDNEditorConfigsOverrides.Add(entry);
                    return entry;
                }

                /// <summary>
                /// 将 CDNEditorConfigs 整套快照写入 Override 条目（深拷贝新实例，禁共享引用）。
                /// </summary>
                /// <param name="target">目标 Override 条目。</param>
                /// <param name="result">值来源快照。</param>
                private static void ApplyCdnResult(CDNEditorConfigsOverride target, CDNEditorConfigs result)
                {
                    target.Config = DeepCloneCdn(result);
                }

                /// <summary>
                /// 深拷贝 CDNEditorConfigs；src 为 null 时返回新实例，否则逐字段拷贝全部 string，禁共享引用。
                /// </summary>
                /// <param name="src">待拷贝的源实例。</param>
                /// <returns>字段值与 src 相同的新 CDNEditorConfigs 实例；src 为 null 时返回新实例。</returns>
                private static CDNEditorConfigs DeepCloneCdn(CDNEditorConfigs src)
                {
                    if (src == null) return new CDNEditorConfigs();
                    return new CDNEditorConfigs
                    {
                        Endpoint = src.Endpoint,
                        AccessKeyID = src.AccessKeyID,
                        AccessKeySecret = src.AccessKeySecret,
                        PresetOSSPath = src.PresetOSSPath,
                        VersionCheckLocalFilePath = src.VersionCheckLocalFilePath,
                        VersionCheckRemoteFilePath = src.VersionCheckRemoteFilePath,
                        LocalDirectory = src.LocalDirectory,
                        AutoLinkLatestVersion = src.AutoLinkLatestVersion,
                        RemotePathSuffix = src.RemotePathSuffix,
                        AssetCheckWhitelistDeviceIDs = src.AssetCheckWhitelistDeviceIDs != null
                            ? new List<string>(src.AssetCheckWhitelistDeviceIDs)
                            : new List<string>(),
                        AssetCheckWhitelistRemoteFilePath = src.AssetCheckWhitelistRemoteFilePath,
                        AutoLinkLatestAssetCheckVersionFiles = src.AutoLinkLatestAssetCheckVersionFiles,
                        AssetCheckManifestBytesLocalFilePath = src.AssetCheckManifestBytesLocalFilePath,
                        AssetCheckManifestHashLocalFilePath = src.AssetCheckManifestHashLocalFilePath,
                        AssetCheckPackageVersionLocalFilePath = src.AssetCheckPackageVersionLocalFilePath,
                        AssetCheckVersionRemoteDirectory = src.AssetCheckVersionRemoteDirectory,
                        ZoneID = src.ZoneID,
                        PurgeURL = src.PurgeURL,
                        Token = src.Token,
                        CachePaths = src.CachePaths,
                    };
                }
#endif
            }
        }
    }
}
