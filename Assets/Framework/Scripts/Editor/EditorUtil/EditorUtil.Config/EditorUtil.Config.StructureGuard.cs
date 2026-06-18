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
                /// 同步 ConfigMasterSO 矩阵与当前枚举成员：新增成员补空行，已废弃成员移除行；完成后 SetDirty。
                /// <para>忽略 PlatformType.None 与 ChannelType.None；重复组合只保留第一条。</para>
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

                    var entries = master.EditorEntries;
                    HashSet<(PlatformType, ChannelType)> present = new();
                    for (int i = entries.Count - 1; i >= 0; i--)
                    {
                        var e = entries[i];
                        var key = (e.Platform, e.Channel);
                        if (!wanted.Contains(key) || present.Contains(key))
                        {
                            master.EditorRemoveEntryAt(i);
                            continue;
                        }
                        present.Add(key);
                    }

                    foreach (var key in wanted)
                    {
                        if (present.Contains(key)) continue;
                        master.EditorAddEntry(new PlatformChannelEntry { Platform = key.Item1, Channel = key.Item2 });
                    }

                    // 若 CommonMask 全局唯一（IsGlobal=true），将第一个有效格的 CommonConfig 广播到所有新增格，
                    // 确保新格不以零值破坏 IsGlobal 一致性语义。
                    if (master.CommonMask.IsGlobal)
                    {
                        CommonConfig seedDebug = null;
                        CommonConfig seedRelease = null;
                        var allEntries = master.EditorEntries;
                        for (int i = 0; i < allEntries.Count; i++)
                        {
                            if (allEntries[i].Platform == PlatformType.None || allEntries[i].Channel == ChannelType.None) continue;
                            CommonConfig cd = allEntries[i].GetCommon(DevelopMode.Debug);
                            CommonConfig cr = allEntries[i].GetCommon(DevelopMode.Release);
                            // 取第一个非空字段格作为种子（AppID 非空说明该格已编辑过）
                            if (!string.IsNullOrEmpty(cd?.AppID) || !string.IsNullOrEmpty(cd?.AppAesKey))
                            {
                                seedDebug = cd;
                                seedRelease = cr;
                                break;
                            }
                        }
                        if (seedDebug != null)
                        {
                            for (int i = 0; i < allEntries.Count; i++)
                            {
                                if (allEntries[i].Platform == PlatformType.None || allEntries[i].Channel == ChannelType.None) continue;
                                if (present.Contains((allEntries[i].Platform, allEntries[i].Channel))) continue; // 跳过原有格，只处理新增格
                                CommonConfig dst = allEntries[i].GetCommon(DevelopMode.Debug);
                                if (dst != null && string.IsNullOrEmpty(dst.AppID))
                                {
                                    dst.AppID = seedDebug.AppID;
                                    dst.AppAesKey = seedDebug.AppAesKey;
                                    dst.AppAesIV = seedDebug.AppAesIV;
                                }
                                CommonConfig dstR = allEntries[i].GetCommon(DevelopMode.Release);
                                if (dstR != null && seedRelease != null && string.IsNullOrEmpty(dstR.AppID))
                                {
                                    dstR.AppID = seedRelease.AppID;
                                    dstR.AppAesKey = seedRelease.AppAesKey;
                                    dstR.AppAesIV = seedRelease.AppAesIV;
                                }
                            }
                        }
                    }

                    EditorUtility.SetDirty(master);
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
