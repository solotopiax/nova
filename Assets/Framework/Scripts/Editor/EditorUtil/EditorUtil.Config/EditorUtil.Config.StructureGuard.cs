/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.Config.StructureGuard.cs
 * author:    taoye
 * created:   2026/4/29
 * descrip:   ConfigMasterSO 结构巡检工具（枚举补齐、死组合清理、丢失引用检测）
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
        /// <summary>
        /// Config 模块 Editor 侧工具集合。
        /// </summary>
        public static partial class Config
        {
            /// <summary>
            /// 结构巡检：Platform×Channel 枚举补齐、死组合清理、SerializeReference 丢失检测。
            /// </summary>
            public static class StructureGuard
            {
                /// <summary>
                /// 同步 ConfigMasterSO 矩阵与当前枚举成员：新增成员按面板维度掩码继承既有逻辑组，已废弃成员移除行；完成后 SetDirty。
                /// <para>PlatformType.None 与 ChannelType.None 均为内部哨兵，不生成矩阵行；重复组合只保留第一条。</para>
                /// </summary>
                /// <param name="master">待同步的 ConfigMasterSO 实例。</param>
                public static void SyncEnumGrid(ConfigMasterSO master)
                {
                    if (master == null) return;

                    var platforms = (PlatformType[])Enum.GetValues(typeof(PlatformType));
                    var channels = (ChannelType[])Enum.GetValues(typeof(ChannelType));

                    HashSet<(PlatformType, ChannelType)> wanted = new();
                    for (int p = 0; p < platforms.Length; p++)
                    {
                        if (platforms[p] == PlatformType.None) continue;
                        for (int c = 0; c < channels.Length; c++)
                        {
                            if (channels[c] == ChannelType.None) continue;
                            wanted.Add((platforms[p], channels[c]));
                        }
                    }

                    // 旧资产可能把 None 保存为当前渠道；在补齐矩阵前归一到首个可用渠道。
                    if (master.CurrentChannel == ChannelType.None ||
                        !Enum.IsDefined(typeof(ChannelType), master.CurrentChannel))
                    {
                        master.CurrentChannel = ChannelType.Official;
                    }

                    var entries = master.EditorEntries;
                    HashSet<(PlatformType, ChannelType)> explicitValidCoordinates = new();
                    for (int i = 0; i < entries.Count; i++)
                    {
                        PlatformChannelEntry entry = entries[i];
                        if (entry == null) continue;
                        var key = (entry.Platform, entry.Channel);
                        if (wanted.Contains(key)) explicitValidCoordinates.Add(key);
                    }
                    HashSet<(PlatformType, ChannelType)> present = new();
                    List<PlatformChannelEntry> existingEntries = new();
                    for (int i = entries.Count - 1; i >= 0; i--)
                    {
                        var e = entries[i];
                        if (e == null)
                        {
                            master.EditorRemoveEntryAt(i);
                            continue;
                        }

                        // 旧版允许 Channel=None；同平台没有 Official 行时原位迁移，避免升级后丢失唯一配置。
                        var officialKey = (e.Platform, ChannelType.Official);
                        if (e.Platform != PlatformType.None && e.Channel == ChannelType.None &&
                            wanted.Contains(officialKey) && !explicitValidCoordinates.Contains(officialKey))
                        {
                            e.Channel = ChannelType.Official;
                        }
                        var key = (e.Platform, e.Channel);
                        if (!wanted.Contains(key) || present.Contains(key))
                        {
                            master.EditorRemoveEntryAt(i);
                            continue;
                        }
                        present.Add(key);
                        existingEntries.Add(e);
                    }

                    List<PlatformChannelEntry> addedEntries = new();
                    foreach (var key in wanted)
                    {
                        if (present.Contains(key)) continue;
                        PlatformChannelEntry added = new() { Platform = key.Item1, Channel = key.Item2 };
                        master.EditorAddEntry(added);
                        addedEntries.Add(added);
                    }

                    SeedAddedEntries(master, existingEntries, addedEntries);
                    EditorUtility.SetDirty(master);
                }

                /// <summary>
                /// 按各面板的维度掩码为新增矩阵行继承既有逻辑组数据，避免补空行破坏未勾选维度的一致性。
                /// 已勾选轴若是全新的枚举值且没有既有来源，则保留该独立分支的默认空配置。
                /// </summary>
                private static void SeedAddedEntries(
                    ConfigMasterSO master,
                    IReadOnlyList<PlatformChannelEntry> existingEntries,
                    IReadOnlyList<PlatformChannelEntry> addedEntries)
                {
                    if (existingEntries.Count == 0 || addedEntries.Count == 0) return;

                    HashSet<string> sdkTypes = CollectStoredTypeNames(existingEntries, true);
                    HashSet<string> kitTypes = CollectStoredTypeNames(existingEntries, false);
                    foreach (PlatformChannelEntry target in addedEntries)
                    {
                        foreach (DevelopMode targetMode in Enum.GetValues(typeof(DevelopMode)))
                        {
                            SeedAppConfigs(master, existingEntries, target, targetMode);
                            SeedPrivacyConfigs(master, existingEntries, target, targetMode);
                            SeedTypedConfigs(master, existingEntries, target, targetMode, sdkTypes, true);
                            SeedTypedConfigs(master, existingEntries, target, targetMode, kitTypes, false);
                        }
                    }
                }

                /// <summary>
                /// 为新增行的应用配置选择同逻辑组既有来源并复制完整序列化内容。
                /// </summary>
                private static void SeedAppConfigs(
                    ConfigMasterSO master,
                    IReadOnlyList<PlatformChannelEntry> existingEntries,
                    PlatformChannelEntry target,
                    DevelopMode targetMode)
                {
                    PlatformChannelEntry source = FindSourceEntry(master, existingEntries, target, master.AppConfigsMask);
                    if (source == null) return;
                    DevelopMode sourceMode = master.AppConfigsMask.ByDevelopMode ? targetMode : master.CurrentDevelopMode;
                    CopySerializable(source.GetAppConfigs(sourceMode), target.GetAppConfigs(targetMode));
                }

                /// <summary>
                /// 为新增行的隐私配置选择同逻辑组既有来源并复制完整序列化内容。
                /// </summary>
                private static void SeedPrivacyConfigs(
                    ConfigMasterSO master,
                    IReadOnlyList<PlatformChannelEntry> existingEntries,
                    PlatformChannelEntry target,
                    DevelopMode targetMode)
                {
                    PlatformChannelEntry source = FindSourceEntry(master, existingEntries, target, master.PrivacyConfigsMask);
                    if (source == null) return;
                    DevelopMode sourceMode = master.PrivacyConfigsMask.ByDevelopMode ? targetMode : master.CurrentDevelopMode;
                    CopySerializable(source.GetPrivacyConfigs(sourceMode), target.GetPrivacyConfigs(targetMode));
                }

                /// <summary>
                /// 为新增行复制指定类别的 SDK 或 Kit SerializeReference 配置，每个目标格保持独立实例。
                /// </summary>
                private static void SeedTypedConfigs(
                    ConfigMasterSO master,
                    IReadOnlyList<PlatformChannelEntry> existingEntries,
                    PlatformChannelEntry target,
                    DevelopMode targetMode,
                    IEnumerable<string> typeNames,
                    bool sdk)
                {
                    foreach (string typeName in typeNames)
                    {
                        PanelDimensionMask mask = sdk ? master.GetSDKMask(typeName) : master.GetKitMask(typeName);
                        PlatformChannelEntry source = FindSourceEntry(master, existingEntries, target, mask);
                        if (source == null) continue;
                        DevelopMode sourceMode = mask.ByDevelopMode ? targetMode : master.CurrentDevelopMode;
                        object sourceConfig = FindTypedConfig(source, sourceMode, typeName, sdk);
                        if (sourceConfig == null) continue;
                        object clone = DimensionProjector.DeepCloneManagedRef(sourceConfig);
                        if (sdk && clone is ISDKPluginConfig sdkConfig)
                            target.GetSDKConfigs(targetMode).Add(sdkConfig);
                        else if (!sdk && clone is IKitConfig kitConfig)
                            target.GetKitConfigs(targetMode).Add(kitConfig);
                    }
                }

                /// <summary>
                /// 按已勾选轴匹配目标逻辑组；未勾选轴优先采用 Config 窗口当前选择，缺失时回退同组首个既有行。
                /// </summary>
                private static PlatformChannelEntry FindSourceEntry(
                    ConfigMasterSO master,
                    IReadOnlyList<PlatformChannelEntry> existingEntries,
                    PlatformChannelEntry target,
                    PanelDimensionMask mask)
                {
                    PlatformType preferredPlatform = mask.ByPlatform ? target.Platform : master.CurrentPlatform;
                    ChannelType preferredChannel = mask.ByChannel ? target.Channel : master.CurrentChannel;
                    for (int i = 0; i < existingEntries.Count; i++)
                    {
                        PlatformChannelEntry candidate = existingEntries[i];
                        if (candidate.Platform == preferredPlatform && candidate.Channel == preferredChannel)
                            return candidate;
                    }

                    for (int i = 0; i < existingEntries.Count; i++)
                    {
                        PlatformChannelEntry candidate = existingEntries[i];
                        if (mask.ByPlatform && candidate.Platform != target.Platform) continue;
                        if (mask.ByChannel && candidate.Channel != target.Channel) continue;
                        return candidate;
                    }
                    return null;
                }

                /// <summary>
                /// 收集既有矩阵中实际存储的 SDK 或 Kit 配置类型名。
                /// </summary>
                private static HashSet<string> CollectStoredTypeNames(
                    IReadOnlyList<PlatformChannelEntry> entries,
                    bool sdk)
                {
                    HashSet<string> result = new();
                    for (int i = 0; i < entries.Count; i++)
                    {
                        foreach (DevelopMode mode in Enum.GetValues(typeof(DevelopMode)))
                        {
                            if (sdk)
                            {
                                List<ISDKPluginConfig> configs = entries[i].GetSDKConfigs(mode);
                                for (int c = 0; c < configs.Count; c++)
                                    if (configs[c] != null) result.Add(configs[c].GetType().FullName);
                            }
                            else
                            {
                                List<IKitConfig> configs = entries[i].GetKitConfigs(mode);
                                for (int c = 0; c < configs.Count; c++)
                                    if (configs[c] != null) result.Add(configs[c].GetType().FullName);
                            }
                        }
                    }
                    return result;
                }

                /// <summary>
                /// 在指定矩阵行和模式中查找目标类型的 SDK 或 Kit 配置。
                /// </summary>
                private static object FindTypedConfig(
                    PlatformChannelEntry entry,
                    DevelopMode mode,
                    string typeName,
                    bool sdk)
                {
                    if (sdk)
                    {
                        List<ISDKPluginConfig> configs = entry.GetSDKConfigs(mode);
                        for (int i = 0; i < configs.Count; i++)
                            if (configs[i] != null && configs[i].GetType().FullName == typeName) return configs[i];
                        return null;
                    }

                    List<IKitConfig> kitConfigs = entry.GetKitConfigs(mode);
                    for (int i = 0; i < kitConfigs.Count; i++)
                        if (kitConfigs[i] != null && kitConfigs[i].GetType().FullName == typeName) return kitConfigs[i];
                    return null;
                }

                /// <summary>
                /// 使用 Unity 序列化规则将源对象完整复制到已存在的目标对象。
                /// </summary>
                private static void CopySerializable(object source, object target)
                {
                    if (source == null || target == null) return;
                    JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(source), target);
                }

                /// <summary>
                /// 检测矩阵中所有 DevelopMode 的 SDKConfigs / KitConfigs 列表里因脚本删除或 GUID 丢失出现的 null 占位。
                /// <para>返回所有受影响的 Entry 索引与对应 null 占位数量；master 为 null 时返回空列表。</para>
                /// </summary>
                /// <param name="master">待检测的 ConfigMasterSO 实例。</param>
                /// <returns>每个含缺失引用的 Entry 对应一条 MissingRef，无缺失时返回空列表。</returns>
                public static List<MissingRef> DetectMissingPluginRefs(ConfigMasterSO master)
                {
                    List<MissingRef> result = new();
                    if (master == null) return result;

                    var entries = master.EditorEntries;
                    for (int i = 0; i < entries.Count; i++)
                    {
                        int missCount = 0;
                        var modeEntries = entries[i].SDKConfigsByMode;
                        for (int m = 0; m < modeEntries.Count; m++)
                        {
                            var list = modeEntries[m].SDKConfigs;
                            for (int j = 0; j < list.Count; j++)
                            {
                                if (list[j] == null) missCount++;
                            }
                        }

                        var kitModeEntries = entries[i].KitConfigsByMode;
                        for (int m = 0; m < kitModeEntries.Count; m++)
                        {
                            var list = kitModeEntries[m].KitConfigs;
                            for (int j = 0; j < list.Count; j++)
                            {
                                if (list[j] == null) missCount++;
                            }
                        }

                        if (missCount > 0)
                        {
                            result.Add(new MissingRef(i, missCount));
                        }
                    }
                    return result;
                }

                /// <summary>
                /// 清理所有 Entry 所有 DevelopMode 的 SDKConfigs / KitConfigs 列表中的 null 占位；完成后 SetDirty。
                /// <para>master 为 null 时直接返回，不执行任何操作。</para>
                /// </summary>
                /// <param name="master">待清理的 ConfigMasterSO 实例。</param>
                public static void CleanMissingPluginRefs(ConfigMasterSO master)
                {
                    if (master == null) return;

                    var entries = master.EditorEntries;
                    for (int i = 0; i < entries.Count; i++)
                    {
                        var modeEntries = entries[i].SDKConfigsByMode;
                        for (int m = 0; m < modeEntries.Count; m++)
                        {
                            modeEntries[m].SDKConfigs.RemoveAll(x => x == null);
                        }

                        var kitModeEntries = entries[i].KitConfigsByMode;
                        for (int m = 0; m < kitModeEntries.Count; m++)
                        {
                            kitModeEntries[m].KitConfigs.RemoveAll(x => x == null);
                        }
                    }
                    EditorUtility.SetDirty(master);
                }

                /// <summary>
                /// Entry 索引与缺失引用数量的只读记录。
                /// </summary>
                public readonly struct MissingRef
                {
                    /// <summary>
                    /// 该条目在 ConfigMasterSO.EditorEntries 中的索引。
                    /// </summary>
                    public readonly int EntryIndex;

                    /// <summary>
                    /// 该 Entry 所有 Mode 合计的 SDKConfigs / KitConfigs 列表中 null 占位的数量。
                    /// </summary>
                    public readonly int MissingCount;

                    /// <summary>
                    /// 构造 MissingRef 记录。
                    /// </summary>
                    /// <param name="entryIndex">Entry 在 ConfigMasterSO.EditorEntries 中的索引。</param>
                    /// <param name="missingCount">所有 Mode 合计 SDKConfigs / KitConfigs 列表中 null 占位的数量。</param>
                    public MissingRef(int entryIndex, int missingCount)
                    {
                        EntryIndex = entryIndex;
                        MissingCount = missingCount;
                    }
                }
            }
        }
    }
}
