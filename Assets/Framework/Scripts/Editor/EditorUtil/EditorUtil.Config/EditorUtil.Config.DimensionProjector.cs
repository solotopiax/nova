/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.Config.DimensionProjector.cs
 * author:    taoye
 * created:   2026/6/1
 * descrip:   Config 面板维度投影器；按掩码将当前坐标格的值在同组格之间广播/分裂/合并，全部作用于 m_WorkingCopy（不落盘）
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
            /// Config 面板维度投影器；按 PanelDimensionMask 把当前坐标格的值在"被掩码视为同一份的格子集合"内广播/分裂/合并。
            /// <para>全部操作作用于调用方传入的 SerializedObject（m_WorkingCopy），不落盘，不设 m_IsDirty，由 ConfigWindow 负责后续脏标记。</para>
            /// <para>Phase 2-B 只处理矩阵三类（Common / SDK / Kit）；顶层类（Namespace / HybridCLR / YooAsset）在 Phase 3 扩充，PanelKind 枚举已留好扩展位。</para>
            /// </summary>
            public static class DimensionProjector
            {
                // -------------------------------------------------------
                // 公开枚举：面板种类（Phase 2-B 矩阵三类，Phase 3 扩充顶层类）
                // -------------------------------------------------------

                /// <summary>
                /// 配置面板种类；区分 Common / SDK / Kit 三种矩阵面板，以及顶层三类面板（Namespace / HybridCLR / YooAsset）。
                /// </summary>
                public enum PanelKind
                {
                    /// <summary>
                    /// 公共配置（CommonConfig），对应 m_Entries[i].CommonByMode[m].Config。
                    /// </summary>
                    Common,
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
                    /// 顶层 HybridCLR 面板字段组（AotMetadataDlls / GameDlls / LinkXmlTargetPath / GameEntranceProcedureName），
                    /// 底座为 ConfigMasterSO.HybridCLROverrides 列表 + 各顶层默认字段（仅 Editor 期消费）。
                    /// </summary>
                    HybridCLR,
                    /// <summary>
                    /// 顶层 YooAsset 两路径字段（YooAssetSettingsPath / BundleCollectorSettingPath），
                    /// 底座为 ConfigMasterSO.YooAssetOverrides 列表 + 各顶层默认字段（仅 Editor 期消费）。
                    /// </summary>
                    YooAsset,
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
                // 三个公开操作
                // -------------------------------------------------------

                /// <summary>
                /// 加维分裂：启用指定轴后，将当前坐标格的值深拷贝广播到该轴所有取值的同组格中。
                /// <para>步骤：读当前坐标格深拷贝快照 → mask.[axis]=true → 遍历该轴所有取值 v，对目标坐标格写入深拷贝。</para>
                /// </summary>
                /// <param name="master">编辑期 ConfigMasterSO 实例（工作副本）。</param>
                /// <param name="masterSO">对应 master 的 SerializedObject（工作副本），SDK/Kit 路径必需。</param>
                /// <param name="panelKind">面板种类。</param>
                /// <param name="typeName">SDK 或 Kit 的配置类型全名；panelKind 为 Common 时忽略。</param>
                /// <param name="curCoord">当前坐标格。</param>
                /// <param name="axis">要启用的维度轴。</param>
                public static void OnDimensionEnabled(ConfigMasterSO master, SerializedObject masterSO, PanelKind panelKind, string typeName, Coord curCoord, DimensionAxis axis)
                {
                    if (master == null) return;

                    // 顶层类走独立分支（不经过矩阵 m_Entries 路径）
                    if (panelKind == PanelKind.Namespace) { OnNamespaceEnabled(master, curCoord, axis); return; }
#if UNITY_EDITOR
                    if (panelKind == PanelKind.HybridCLR) { OnHybridCLREnabled(master, curCoord, axis); return; }
                    if (panelKind == PanelKind.YooAsset) { OnYooAssetEnabled(master, curCoord, axis); return; }
#endif

                    PanelDimensionMask mask = GetMask(master, panelKind, typeName);

                    if (panelKind == PanelKind.Common)
                    {
                        CommonConfig snapshot = DeepCloneCommon(GetCommonFromMaster(master, curCoord));
                        SetAxis(mask, axis, true);
                        foreach (Coord targetCoord in EnumerateAxisCoords(curCoord, axis))
                        {
                            // 跳过当前格自身，与 BroadcastWithinGroup 的守卫写法对齐，语义一致且避免 SerializedProperty 别名自覆写
                            if (IsSameCoord(targetCoord, curCoord)) continue;
                            FillGroupCommon(master, targetCoord, DeepCloneCommon(snapshot));
                        }
                    }
                    else
                    {
                        // SetAxis 必须先于 masterSO.Update()：mask 是 m_WorkingCopy 上绕过 SerializedProperty 直改的 C# 字段；
                        // 若先 Update 再 SetAxis，SO 缓存中 mask 仍是旧值（stale），后续 ApplyModifiedPropertiesWithoutUndo
                        // 会把整棵 SO 缓存回写 native，导致新 mask 值被旧缓存覆盖（clobber），toggle 表现为勾选后复原。
                        SetAxis(mask, axis, true);
                        masterSO.Update();
                        SerializedProperty srcProp = FindSerializedRefProp(masterSO, master, panelKind, typeName, curCoord);
                        foreach (Coord targetCoord in EnumerateAxisCoords(curCoord, axis))
                        {
                            // 跳过当前格自身；FillGroupSerializedRef 第一步 dstElemProp.managedReferenceValue = null 若 dst == src（别名）会清空源数据
                            if (IsSameCoord(targetCoord, curCoord)) continue;
                            FillGroupSerializedRef(master, masterSO, panelKind, typeName, targetCoord, srcProp);
                        }
                        masterSO.ApplyModifiedPropertiesWithoutUndo();
                    }
                }

                /// <summary>
                /// 减维合并：禁用指定轴后，将当前坐标格的值深拷贝广播到新 mask 下所有同组格（其余格数据丢弃）。
                /// <para>步骤：读当前坐标格深拷贝快照 → mask.[axis]=false → members=GroupMembers(新 mask, curCoord) → 全体写入深拷贝。</para>
                /// </summary>
                /// <param name="master">编辑期 ConfigMasterSO 实例（工作副本）。</param>
                /// <param name="masterSO">对应 master 的 SerializedObject（工作副本），SDK/Kit 路径必需。</param>
                /// <param name="panelKind">面板种类。</param>
                /// <param name="typeName">SDK 或 Kit 的配置类型全名；panelKind 为 Common 时忽略。</param>
                /// <param name="curCoord">当前坐标格。</param>
                /// <param name="axis">要禁用的维度轴。</param>
                public static void OnDimensionDisabled(ConfigMasterSO master, SerializedObject masterSO, PanelKind panelKind, string typeName, Coord curCoord, DimensionAxis axis)
                {
                    if (master == null) return;

                    // 顶层类走独立分支
                    if (panelKind == PanelKind.Namespace) { OnNamespaceDisabled(master, curCoord, axis); return; }
#if UNITY_EDITOR
                    if (panelKind == PanelKind.HybridCLR) { OnHybridCLRDisabled(master, curCoord, axis); return; }
                    if (panelKind == PanelKind.YooAsset) { OnYooAssetDisabled(master, curCoord, axis); return; }
#endif

                    PanelDimensionMask mask = GetMask(master, panelKind, typeName);

                    if (panelKind == PanelKind.Common)
                    {
                        CommonConfig snapshot = DeepCloneCommon(GetCommonFromMaster(master, curCoord));
                        SetAxis(mask, axis, false);
                        foreach (Coord memberCoord in GroupMembers(master, mask, curCoord))
                        {
                            // 跳过当前格自身，与 BroadcastWithinGroup 的守卫写法对齐，语义一致且避免 SerializedProperty 别名自覆写
                            if (IsSameCoord(memberCoord, curCoord)) continue;
                            FillGroupCommon(master, memberCoord, DeepCloneCommon(snapshot));
                        }
                    }
                    else
                    {
                        // SetAxis 必须先于 masterSO.Update()：mask 是 m_WorkingCopy 上绕过 SerializedProperty 直改的 C# 字段；
                        // 若先 Update 再 SetAxis，SO 缓存中 mask 仍是旧值（stale），后续 ApplyModifiedPropertiesWithoutUndo
                        // 会把整棵 SO 缓存回写 native，导致新 mask 值被旧缓存覆盖（clobber），toggle 表现为勾选后复原。
                        SetAxis(mask, axis, false);
                        masterSO.Update();
                        SerializedProperty srcProp = FindSerializedRefProp(masterSO, master, panelKind, typeName, curCoord);
                        foreach (Coord memberCoord in GroupMembers(master, mask, curCoord))
                        {
                            // 跳过当前格自身；FillGroupSerializedRef 第一步 dstElemProp.managedReferenceValue = null 若 dst == src（别名）会清空源数据
                            if (IsSameCoord(memberCoord, curCoord)) continue;
                            FillGroupSerializedRef(master, masterSO, panelKind, typeName, memberCoord, srcProp);
                        }
                        masterSO.ApplyModifiedPropertiesWithoutUndo();
                    }
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
                /// <param name="typeName">SDK 或 Kit 的配置类型全名；panelKind 为 Common 时忽略。</param>
                /// <param name="curCoord">当前坐标格（值来源）。</param>
                public static void BroadcastWithinGroup(ConfigMasterSO master, SerializedObject masterSO, PanelKind panelKind, string typeName, Coord curCoord)
                {
                    if (master == null) return;

                    // 顶层类走独立分支
                    if (panelKind == PanelKind.Namespace) { BroadcastNamespace(master, curCoord); return; }
#if UNITY_EDITOR
                    if (panelKind == PanelKind.HybridCLR) { BroadcastHybridCLR(master, curCoord); return; }
                    if (panelKind == PanelKind.YooAsset) { BroadcastYooAsset(master, curCoord); return; }
#endif

                    PanelDimensionMask mask = GetMask(master, panelKind, typeName);

                    if (panelKind == PanelKind.Common)
                    {
                        CommonConfig srcValue = DeepCloneCommon(GetCommonFromMaster(master, curCoord));
                        foreach (Coord memberCoord in GroupMembers(master, mask, curCoord))
                        {
                            if (IsSameCoord(memberCoord, curCoord)) continue;
                            FillGroupCommon(master, memberCoord, DeepCloneCommon(srcValue));
                        }
                    }
                    else
                    {
                        // 调用方（DrawSDKPanel / DrawKitPanel）已在 BroadcastWithinGroup 前执行 ApplyModifiedProperties()，
                        // SO 与底层 native 已同步，此处无需再 Update()——多余的 Update 会重载整个 SO，
                        // 破坏 PropertyField 正在编辑的 SerializeReference 控件状态与键盘焦点，导致 Bug2。
                        SerializedProperty srcProp = FindSerializedRefProp(masterSO, master, panelKind, typeName, curCoord);
                        foreach (Coord memberCoord in GroupMembers(master, mask, curCoord))
                        {
                            if (IsSameCoord(memberCoord, curCoord)) continue;
                            FillGroupSerializedRef(master, masterSO, panelKind, typeName, memberCoord, srcProp);
                        }
                        masterSO.ApplyModifiedPropertiesWithoutUndo();
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
                    DevelopMode[] modes = { DevelopMode.Debug, DevelopMode.Release };
                    for (int i = 0; i < entries.Count; i++)
                    {
                        PlatformChannelEntry entry = entries[i];
                        if (entry.Platform == PlatformType.None || entry.Channel == ChannelType.None) continue;
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
                // 私有：OnDimensionEnabled 用轴方向枚举
                // -------------------------------------------------------

                /// <summary>
                /// 枚举 axis 轴方向所有取值下的坐标；用于 OnDimensionEnabled 将当前格值广播到轴向所有格。
                /// </summary>
                /// <param name="baseCoord">参照坐标（轴以外维度固定）。</param>
                /// <param name="axis">要遍历的轴。</param>
                /// <returns>轴方向所有取值的坐标序列。</returns>
                private static IEnumerable<Coord> EnumerateAxisCoords(Coord baseCoord, DimensionAxis axis)
                {
                    switch (axis)
                    {
                        case DimensionAxis.Platform:
                            foreach (PlatformType p in Enum.GetValues(typeof(PlatformType)))
                            {
                                if (p != PlatformType.None) yield return new Coord(p, baseCoord.Channel, baseCoord.Mode);
                            }
                            break;
                        case DimensionAxis.Channel:
                            foreach (ChannelType c in Enum.GetValues(typeof(ChannelType)))
                            {
                                if (c != ChannelType.None) yield return new Coord(baseCoord.Platform, c, baseCoord.Mode);
                            }
                            break;
                        case DimensionAxis.DevelopMode:
                            yield return new Coord(baseCoord.Platform, baseCoord.Channel, DevelopMode.Debug);
                            yield return new Coord(baseCoord.Platform, baseCoord.Channel, DevelopMode.Release);
                            break;
                    }
                }

                // -------------------------------------------------------
                // 私有：FillGroup（Common 与 SerializedRef 两路）归约体
                // -------------------------------------------------------

                /// <summary>
                /// 向 targetCoord 格写入 CommonConfig 深拷贝值；对应行不存在时静默跳过。
                /// </summary>
                /// <param name="master">编辑期 ConfigMasterSO 实例。</param>
                /// <param name="targetCoord">目标格坐标。</param>
                /// <param name="value">要写入的 CommonConfig 深拷贝值。</param>
                private static void FillGroupCommon(ConfigMasterSO master, Coord targetCoord, CommonConfig value)
                {
                    if (!master.TryGetEntry(targetCoord.Platform, targetCoord.Channel, out PlatformChannelEntry entry)) return;
                    CommonConfig dst = entry.GetCommon(targetCoord.Mode);
                    if (dst == null || value == null) return;
                    dst.AppID = value.AppID;
                    dst.AppAesKey = value.AppAesKey;
                    dst.AppAesIV = value.AppAesIV;
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
                /// 按面板种类取对应的 PanelDimensionMask；Common 返回 master.CommonMask，SDK/Kit 分别调用 GetSDKMask/GetKitMask。
                /// </summary>
                /// <param name="master">编辑期 ConfigMasterSO 实例。</param>
                /// <param name="panelKind">面板种类。</param>
                /// <param name="typeName">SDK/Kit 类型全名；Common 时忽略。</param>
                /// <returns>对应的 PanelDimensionMask 实例，永不为 null。</returns>
                private static PanelDimensionMask GetMask(ConfigMasterSO master, PanelKind panelKind, string typeName)
                {
                    switch (panelKind)
                    {
                        case PanelKind.SDK: return master.GetSDKMask(typeName);
                        case PanelKind.Kit: return master.GetKitMask(typeName);
                        case PanelKind.Namespace: return master.NamespaceMask;
#if UNITY_EDITOR
                        case PanelKind.HybridCLR: return master.HybridCLRMask;
                        case PanelKind.YooAsset: return master.YooAssetMask;
#endif
                        default: return master.CommonMask;
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
                private static object DeepCloneManagedRef(object src)
                {
                    if (src == null) return null;
                    Type type = src.GetType();
                    object copy = Activator.CreateInstance(type);
                    JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(src), copy);
                    return copy;
                }

                // -------------------------------------------------------
                // 私有辅助：Common 深拷贝（对齐 Exporter.CloneCommon 逐字段模式）
                // -------------------------------------------------------

                /// <summary>
                /// 深拷贝 CommonConfig；src 为 null 时返回 null，与 Exporter.CloneCommon 逐字段模式一致。
                /// </summary>
                /// <param name="src">待拷贝的源实例。</param>
                /// <returns>字段值与 src 相同的新 CommonConfig 实例；src 为 null 时返回 null。</returns>
                private static CommonConfig DeepCloneCommon(CommonConfig src)
                {
                    if (src == null) return null;
                    return new CommonConfig
                    {
                        AppID = src.AppID,
                        AppAesKey = src.AppAesKey,
                        AppAesIV = src.AppAesIV,
                    };
                }

                /// <summary>
                /// 从 master C# 层取指定坐标格的 CommonConfig；行不存在时返回 null。
                /// </summary>
                /// <param name="master">编辑期 ConfigMasterSO 实例。</param>
                /// <param name="coord">目标坐标格。</param>
                /// <returns>对应 CommonConfig，行不存在时返回 null。</returns>
                private static CommonConfig GetCommonFromMaster(ConfigMasterSO master, Coord coord)
                {
                    if (!master.TryGetEntry(coord.Platform, coord.Channel, out PlatformChannelEntry entry)) return null;
                    return entry.GetCommon(coord.Mode);
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
                    DevelopMode m = mask.ByDevelopMode ? coord.Mode : coord.Mode; // DevelopMode 无 None 哨兵，始终保留（匹配时由 mask 控制是否参与比对）
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
                /// Namespace 面板加维分裂：将当前坐标值广播到轴向所有 Override 条目（同组覆盖，无条目则新建）。
                /// <para>步骤：读当前值快照 → mask.ByXxx=true → 遍历轴向所有坐标 → 同组更新/创建 Override 条目。</para>
                /// </summary>
                /// <param name="master">编辑期 ConfigMasterSO 实例（工作副本）。</param>
                /// <param name="curCoord">当前坐标格。</param>
                /// <param name="axis">要启用的维度轴。</param>
                private static void OnNamespaceEnabled(ConfigMasterSO master, Coord curCoord, DimensionAxis axis)
                {
                    PanelDimensionMask mask = master.NamespaceMask;
                    string snapshot = DimensionalResolver.ResolveNamespace(master, curCoord.Platform, curCoord.Channel, curCoord.Mode);
                    SetAxis(mask, axis, true);
                    foreach (Coord targetCoord in EnumerateAxisCoords(curCoord, axis))
                    {
                        if (IsSameCoord(targetCoord, curCoord)) continue;
                        Coord clipped = ClipCoordToMask(mask, targetCoord);
                        UpsertNamespaceOverride(master, mask, clipped, snapshot);
                    }
                    // 确保当前坐标也有一条 Override 条目（否则全局默认值会优先于预期值）
                    UpsertNamespaceOverride(master, mask, ClipCoordToMask(mask, curCoord), snapshot);
                }

                /// <summary>
                /// Namespace 面板减维合并：以当前坐标值覆盖全组，裁剪 Override 列表至新 mask 下的代表格。
                /// <para>步骤：读当前值快照 → mask.ByXxx=false → 移除与新 mask 同组的旧条目 → 保留代表格单条。</para>
                /// </summary>
                /// <param name="master">编辑期 ConfigMasterSO 实例（工作副本）。</param>
                /// <param name="curCoord">当前坐标格。</param>
                /// <param name="axis">要禁用的维度轴。</param>
                private static void OnNamespaceDisabled(ConfigMasterSO master, Coord curCoord, DimensionAxis axis)
                {
                    PanelDimensionMask mask = master.NamespaceMask;
                    string snapshot = DimensionalResolver.ResolveNamespace(master, curCoord.Platform, curCoord.Channel, curCoord.Mode);
                    SetAxis(mask, axis, false);
                    // 新 mask 下删除全部同组旧条目，再写入代表坐标单条
                    Coord clipped = ClipCoordToMask(mask, curCoord);
                    master.NamespaceOverrides.RemoveAll(o => IsOverrideInGroup(mask, OverrideCoord(o), clipped));
                    if (mask.ByPlatform || mask.ByChannel || mask.ByDevelopMode)
                    {
                        UpsertNamespaceOverride(master, mask, clipped, snapshot);
                    }
                    else
                    {
                        // 全不勾（IsGlobal）：Override 列表已清空，将减维前当前坐标那份回写顶层默认字段，
                        // 语义：全局唯一值 = 减维时用户正在编辑的那份（设计单 D2 / 减维语义）
                        master.Namespace = snapshot;
                    }
                }

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
                /// 从 HybridCLROverride 条目取坐标。
                /// </summary>
                private static Coord OverrideCoord(HybridCLROverride o) => new Coord(o.Platform, o.Channel, o.DevelopMode);

                /// <summary>
                /// 从 YooAssetOverride 条目取坐标。
                /// </summary>
                private static Coord OverrideCoord(YooAssetOverride o) => new Coord(o.Platform, o.Channel, o.DevelopMode);

                /// <summary>
                /// HybridCLR 面板加维分裂：将当前坐标值广播到轴向所有 Override 条目。
                /// </summary>
                /// <param name="master">编辑期 ConfigMasterSO 实例（工作副本）。</param>
                /// <param name="curCoord">当前坐标格。</param>
                /// <param name="axis">要启用的维度轴。</param>
                private static void OnHybridCLREnabled(ConfigMasterSO master, Coord curCoord, DimensionAxis axis)
                {
                    PanelDimensionMask mask = master.HybridCLRMask;
                    DimensionalResolver.HybridCLRResult snapshot = DimensionalResolver.ResolveHybridCLR(master, curCoord.Platform, curCoord.Channel, curCoord.Mode);
                    SetAxis(mask, axis, true);
                    foreach (Coord targetCoord in EnumerateAxisCoords(curCoord, axis))
                    {
                        if (IsSameCoord(targetCoord, curCoord)) continue;
                        Coord clipped = ClipCoordToMask(mask, targetCoord);
                        UpsertHybridCLROverride(master, mask, clipped, snapshot);
                    }
                    UpsertHybridCLROverride(master, mask, ClipCoordToMask(mask, curCoord), snapshot);
                }

                /// <summary>
                /// HybridCLR 面板减维合并：以当前坐标值覆盖全组，裁剪 Override 列表至新 mask 下的代表格。
                /// </summary>
                /// <param name="master">编辑期 ConfigMasterSO 实例（工作副本）。</param>
                /// <param name="curCoord">当前坐标格。</param>
                /// <param name="axis">要禁用的维度轴。</param>
                private static void OnHybridCLRDisabled(ConfigMasterSO master, Coord curCoord, DimensionAxis axis)
                {
                    PanelDimensionMask mask = master.HybridCLRMask;
                    DimensionalResolver.HybridCLRResult snapshot = DimensionalResolver.ResolveHybridCLR(master, curCoord.Platform, curCoord.Channel, curCoord.Mode);
                    SetAxis(mask, axis, false);
                    Coord clipped = ClipCoordToMask(mask, curCoord);
                    master.HybridCLROverrides.RemoveAll(o => IsOverrideInGroup(mask, OverrideCoord(o), clipped));
                    if (mask.ByPlatform || mask.ByChannel || mask.ByDevelopMode)
                    {
                        UpsertHybridCLROverride(master, mask, clipped, snapshot);
                    }
                    else
                    {
                        // 全不勾（IsGlobal）：Override 列表已清空，将减维前当前坐标那份回写顶层默认字段
                        master.AotMetadataDlls = DeepCloneDllList(snapshot.AotMetadataDlls);
                        master.GameDlls = DeepCloneDllList(snapshot.GameDlls);
                        master.LinkXmlTargetPath = snapshot.LinkXmlTargetPath;
                        master.GameEntranceProcedureName = snapshot.GameEntranceProcedureName;
                    }
                }

                /// <summary>
                /// HybridCLR 面板广播：将当前坐标的值同步到同组所有 Override 条目。
                /// </summary>
                /// <param name="master">编辑期 ConfigMasterSO 实例（工作副本）。</param>
                /// <param name="curCoord">当前坐标格（值来源）。</param>
                private static void BroadcastHybridCLR(ConfigMasterSO master, Coord curCoord)
                {
                    PanelDimensionMask mask = master.HybridCLRMask;
                    if (!mask.ByPlatform && !mask.ByChannel && !mask.ByDevelopMode) return;
                    DimensionalResolver.HybridCLRResult snapshot = DimensionalResolver.ResolveHybridCLR(master, curCoord.Platform, curCoord.Channel, curCoord.Mode);
                    Coord clipped = ClipCoordToMask(mask, curCoord);
                    foreach (HybridCLROverride o in master.HybridCLROverrides)
                    {
                        if (!IsOverrideInGroup(mask, OverrideCoord(o), clipped)) continue;
                        ApplyHybridCLRResult(o, snapshot);
                    }
                }

                /// <summary>
                /// 在 HybridCLROverrides 列表中按裁剪坐标找到同组首个条目并覆写多字段，无则追加新条目。
                /// </summary>
                private static void UpsertHybridCLROverride(ConfigMasterSO master, PanelDimensionMask mask, Coord clipped, DimensionalResolver.HybridCLRResult snapshot)
                {
                    for (int i = 0; i < master.HybridCLROverrides.Count; i++)
                    {
                        if (!IsOverrideInGroup(mask, OverrideCoord(master.HybridCLROverrides[i]), clipped)) continue;
                        ApplyHybridCLRResult(master.HybridCLROverrides[i], snapshot);
                        return;
                    }
                    HybridCLROverride entry = new HybridCLROverride
                    {
                        Platform = clipped.Platform,
                        Channel = clipped.Channel,
                        DevelopMode = clipped.Mode,
                    };
                    ApplyHybridCLRResult(entry, snapshot);
                    master.HybridCLROverrides.Add(entry);
                }

                /// <summary>
                /// 确保当前坐标在 HybridCLROverrides 中存在对应条目并返回其引用，供面板字段控件直接写入单字段。
                /// 当 HybridCLRMask 为全不勾（IsGlobal）时返回 null，调用方应改写顶层字段。
                /// </summary>
                /// <param name="master">编辑期 ConfigMasterSO 实例（工作副本）。</param>
                /// <param name="curCoord">当前坐标格。</param>
                /// <returns>命中或新建的 HybridCLROverride 条目引用；IsGlobal 时返回 null。</returns>
                public static HybridCLROverride EnsureHybridCLROverrideAtCoord(ConfigMasterSO master, Coord curCoord)
                {
                    if (master == null) return null;
                    PanelDimensionMask mask = master.HybridCLRMask;
                    if (!mask.ByPlatform && !mask.ByChannel && !mask.ByDevelopMode) return null;
                    Coord clipped = ClipCoordToMask(mask, curCoord);
                    for (int i = 0; i < master.HybridCLROverrides.Count; i++)
                    {
                        if (!IsOverrideInGroup(mask, OverrideCoord(master.HybridCLROverrides[i]), clipped)) continue;
                        return master.HybridCLROverrides[i];
                    }
                    HybridCLROverride entry = new HybridCLROverride
                    {
                        Platform = clipped.Platform,
                        Channel = clipped.Channel,
                        DevelopMode = clipped.Mode,
                    };
                    master.HybridCLROverrides.Add(entry);
                    return entry;
                }

                /// <summary>
                /// 将 HybridCLRResult 多字段写入 Override 条目（深拷贝 List，避免共享引用）。
                /// </summary>
                /// <param name="target">目标 Override 条目。</param>
                /// <param name="result">值来源快照。</param>
                private static void ApplyHybridCLRResult(HybridCLROverride target, DimensionalResolver.HybridCLRResult result)
                {
                    target.AotMetadataDlls = DeepCloneDllList(result.AotMetadataDlls);
                    target.GameDlls = DeepCloneDllList(result.GameDlls);
                    target.LinkXmlTargetPath = result.LinkXmlTargetPath;
                    target.GameEntranceProcedureName = result.GameEntranceProcedureName;
                }

                // ——— YooAsset 顶层类操作 ———

                /// <summary>
                /// YooAsset 面板加维分裂：将当前坐标值广播到轴向所有 Override 条目。
                /// </summary>
                /// <param name="master">编辑期 ConfigMasterSO 实例（工作副本）。</param>
                /// <param name="curCoord">当前坐标格。</param>
                /// <param name="axis">要启用的维度轴。</param>
                private static void OnYooAssetEnabled(ConfigMasterSO master, Coord curCoord, DimensionAxis axis)
                {
                    PanelDimensionMask mask = master.YooAssetMask;
                    DimensionalResolver.YooAssetResult snapshot = DimensionalResolver.ResolveYooAsset(master, curCoord.Platform, curCoord.Channel, curCoord.Mode);
                    SetAxis(mask, axis, true);
                    foreach (Coord targetCoord in EnumerateAxisCoords(curCoord, axis))
                    {
                        if (IsSameCoord(targetCoord, curCoord)) continue;
                        Coord clipped = ClipCoordToMask(mask, targetCoord);
                        UpsertYooAssetOverride(master, mask, clipped, snapshot);
                    }
                    UpsertYooAssetOverride(master, mask, ClipCoordToMask(mask, curCoord), snapshot);
                }

                /// <summary>
                /// YooAsset 面板减维合并：以当前坐标值覆盖全组，裁剪 Override 列表至新 mask 下的代表格。
                /// </summary>
                /// <param name="master">编辑期 ConfigMasterSO 实例（工作副本）。</param>
                /// <param name="curCoord">当前坐标格。</param>
                /// <param name="axis">要禁用的维度轴。</param>
                private static void OnYooAssetDisabled(ConfigMasterSO master, Coord curCoord, DimensionAxis axis)
                {
                    PanelDimensionMask mask = master.YooAssetMask;
                    DimensionalResolver.YooAssetResult snapshot = DimensionalResolver.ResolveYooAsset(master, curCoord.Platform, curCoord.Channel, curCoord.Mode);
                    SetAxis(mask, axis, false);
                    Coord clipped = ClipCoordToMask(mask, curCoord);
                    master.YooAssetOverrides.RemoveAll(o => IsOverrideInGroup(mask, OverrideCoord(o), clipped));
                    if (mask.ByPlatform || mask.ByChannel || mask.ByDevelopMode)
                    {
                        UpsertYooAssetOverride(master, mask, clipped, snapshot);
                    }
                    else
                    {
                        // 全不勾（IsGlobal）：Override 列表已清空，将减维前当前坐标那份回写顶层默认字段
                        master.YooAssetSettingsPath = snapshot.YooAssetSettingsPath;
                        master.BundleCollectorSettingPath = snapshot.BundleCollectorSettingPath;
                    }
                }

                /// <summary>
                /// YooAsset 面板广播：将当前坐标的值同步到同组所有 Override 条目。
                /// </summary>
                /// <param name="master">编辑期 ConfigMasterSO 实例（工作副本）。</param>
                /// <param name="curCoord">当前坐标格（值来源）。</param>
                private static void BroadcastYooAsset(ConfigMasterSO master, Coord curCoord)
                {
                    PanelDimensionMask mask = master.YooAssetMask;
                    if (!mask.ByPlatform && !mask.ByChannel && !mask.ByDevelopMode) return;
                    DimensionalResolver.YooAssetResult snapshot = DimensionalResolver.ResolveYooAsset(master, curCoord.Platform, curCoord.Channel, curCoord.Mode);
                    Coord clipped = ClipCoordToMask(mask, curCoord);
                    foreach (YooAssetOverride o in master.YooAssetOverrides)
                    {
                        if (!IsOverrideInGroup(mask, OverrideCoord(o), clipped)) continue;
                        o.YooAssetSettingsPath = snapshot.YooAssetSettingsPath;
                        o.BundleCollectorSettingPath = snapshot.BundleCollectorSettingPath;
                    }
                }

                /// <summary>
                /// 在 YooAssetOverrides 列表中按裁剪坐标找到同组首个条目并覆写两路径，无则追加新条目。
                /// </summary>
                private static void UpsertYooAssetOverride(ConfigMasterSO master, PanelDimensionMask mask, Coord clipped, DimensionalResolver.YooAssetResult snapshot)
                {
                    for (int i = 0; i < master.YooAssetOverrides.Count; i++)
                    {
                        if (!IsOverrideInGroup(mask, OverrideCoord(master.YooAssetOverrides[i]), clipped)) continue;
                        master.YooAssetOverrides[i].YooAssetSettingsPath = snapshot.YooAssetSettingsPath;
                        master.YooAssetOverrides[i].BundleCollectorSettingPath = snapshot.BundleCollectorSettingPath;
                        return;
                    }
                    master.YooAssetOverrides.Add(new YooAssetOverride
                    {
                        Platform = clipped.Platform,
                        Channel = clipped.Channel,
                        DevelopMode = clipped.Mode,
                        YooAssetSettingsPath = snapshot.YooAssetSettingsPath,
                        BundleCollectorSettingPath = snapshot.BundleCollectorSettingPath,
                    });
                }

                /// <summary>
                /// 确保当前坐标在 YooAssetOverrides 中存在对应条目并返回其引用，供面板路径控件直接写入单字段。
                /// 当 YooAssetMask 为全不勾（IsGlobal）时返回 null，调用方应改写顶层字段。
                /// </summary>
                /// <param name="master">编辑期 ConfigMasterSO 实例（工作副本）。</param>
                /// <param name="curCoord">当前坐标格。</param>
                /// <returns>命中或新建的 YooAssetOverride 条目引用；IsGlobal 时返回 null。</returns>
                public static YooAssetOverride EnsureYooAssetOverrideAtCoord(ConfigMasterSO master, Coord curCoord)
                {
                    if (master == null) return null;
                    PanelDimensionMask mask = master.YooAssetMask;
                    if (!mask.ByPlatform && !mask.ByChannel && !mask.ByDevelopMode) return null;
                    Coord clipped = ClipCoordToMask(mask, curCoord);
                    for (int i = 0; i < master.YooAssetOverrides.Count; i++)
                    {
                        if (!IsOverrideInGroup(mask, OverrideCoord(master.YooAssetOverrides[i]), clipped)) continue;
                        return master.YooAssetOverrides[i];
                    }
                    YooAssetOverride entry = new YooAssetOverride
                    {
                        Platform = clipped.Platform,
                        Channel = clipped.Channel,
                        DevelopMode = clipped.Mode,
                    };
                    master.YooAssetOverrides.Add(entry);
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
#endif
            }
        }
    }
}
