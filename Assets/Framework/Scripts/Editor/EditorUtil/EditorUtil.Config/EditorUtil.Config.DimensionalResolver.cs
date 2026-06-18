/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.Config.DimensionalResolver.cs
 * author:    taoye
 * created:   2026/6/2
 * descrip:   顶层类维度取数器；按当前坐标 + NamespaceMask/HybridCLRMask/YooAssetMask 从 Override 列表匹配并回落顶层默认字段，纯只读，不改数据
 ***************************************************************/

using System.Collections.Generic;
using NovaFramework.Runtime;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        public static partial class Config
        {
            /// <summary>
            /// 顶层类维度取数器。
            /// <para>按当前 Platform × Channel × DevelopMode 坐标，从 ConfigMasterSO 的顶层掩码与 Override 列表中解析出最终生效值。</para>
            /// <para>匹配算法：未勾选轴的坐标分量填 None 哨兵参与比对，与 DimensionProjector 的存值规则对称。命中列表首个符合条目；无命中则回落顶层默认字段。</para>
            /// <para>本类纯只读，不修改任何数据；可同时服务于 ConfigWindow 导出前取数与 Exporter 直接调用。</para>
            /// </summary>
            public static class DimensionalResolver
            {
                // -------------------------------------------------------
                // 返回值结构：HybridCLR 多字段
                // -------------------------------------------------------

                /// <summary>
                /// HybridCLR 面板维度解析结果（四字段聚合）。
                /// <para>AotMetadataDlls / GameDlls 为深拷贝列表（禁共享引用）；其余两字段为不可变 string。</para>
                /// </summary>
                public sealed class HybridCLRResult
                {
                    /// <summary>
                    /// AOT 元数据 DLL 列表（深拷贝）；对应 ConfigMasterSO.AotMetadataDlls 或匹配 Override 的 AotMetadataDlls。
                    /// </summary>
                    public List<DllMasterAssetEntry> AotMetadataDlls;
                    /// <summary>
                    /// 业务 DLL 列表（深拷贝）；对应 ConfigMasterSO.GameDlls 或匹配 Override 的 GameDlls。
                    /// </summary>
                    public List<DllMasterAssetEntry> GameDlls;
                    /// <summary>
                    /// link.xml 目标路径；对应 ConfigMasterSO.LinkXmlTargetPath 或匹配 Override 的 LinkXmlTargetPath。
                    /// </summary>
                    public string LinkXmlTargetPath;
                    /// <summary>
                    /// 业务入口 Procedure 相对类型名；对应 ConfigMasterSO.GameEntranceProcedureName 或匹配 Override 的值。
                    /// </summary>
                    public string GameEntranceProcedureName;
                }

                // -------------------------------------------------------
                // 返回值结构：YooAsset 两路径
                // -------------------------------------------------------

                /// <summary>
                /// YooAsset 面板维度解析结果（两路径聚合）。
                /// </summary>
                public sealed class YooAssetResult
                {
                    /// <summary>
                    /// YooAssetSettings.asset 项目根相对路径；对应 ConfigMasterSO.YooAssetSettingsPath 或匹配 Override 的值。
                    /// </summary>
                    public string YooAssetSettingsPath;
                    /// <summary>
                    /// BundleCollectorSetting.asset 项目根相对路径；对应 ConfigMasterSO.BundleCollectorSettingPath 或匹配 Override 的值。
                    /// </summary>
                    public string BundleCollectorSettingPath;
                }

                // -------------------------------------------------------
                // 公开取数 API
                // -------------------------------------------------------

                /// <summary>
                /// 按当前坐标解析 Namespace 最终生效值。
                /// <para>全不勾（IsGlobal）→ 直接返回顶层 master.Namespace；否则裁剪坐标到勾选轴后在 NamespaceOverrides 中匹配首个符合条目取 Value，无命中回落 master.Namespace。</para>
                /// </summary>
                /// <param name="master">ConfigMasterSO 实例（可为已落盘的正式数据，也可为工作副本）。</param>
                /// <param name="curP">当前平台。</param>
                /// <param name="curC">当前渠道。</param>
                /// <param name="curM">当前开发模式。</param>
                /// <returns>最终生效的 Namespace 字符串；master 为 null 时返回空字符串。</returns>
                public static string ResolveNamespace(ConfigMasterSO master, PlatformType curP, ChannelType curC, DevelopMode curM)
                {
                    if (master == null) return string.Empty;
                    PanelDimensionMask mask = master.NamespaceMask;
                    if (IsGlobal(mask)) return master.Namespace ?? string.Empty;
                    foreach (NamespaceOverride o in master.NamespaceOverrides)
                    {
                        if (!MatchesMask(mask, o.Platform, o.Channel, o.DevelopMode, curP, curC, curM)) continue;
                        return o.Value ?? string.Empty;
                    }
                    return master.Namespace ?? string.Empty;
                }

#if UNITY_EDITOR
                /// <summary>
                /// 按当前坐标解析 HybridCLR 面板四字段的最终生效值。
                /// <para>全不勾（IsGlobal）→ 直接取顶层各默认字段；否则在 HybridCLROverrides 中匹配首个符合条目，无命中回落顶层字段。</para>
                /// <para>AotMetadataDlls / GameDlls 返回深拷贝列表（禁共享引用）。</para>
                /// </summary>
                /// <param name="master">ConfigMasterSO 实例。</param>
                /// <param name="curP">当前平台。</param>
                /// <param name="curC">当前渠道。</param>
                /// <param name="curM">当前开发模式。</param>
                /// <returns>HybridCLRResult；master 为 null 时各字段返回默认空值。</returns>
                public static HybridCLRResult ResolveHybridCLR(ConfigMasterSO master, PlatformType curP, ChannelType curC, DevelopMode curM)
                {
                    if (master == null)
                    {
                        return new HybridCLRResult
                        {
                            AotMetadataDlls = new List<DllMasterAssetEntry>(),
                            GameDlls = new List<DllMasterAssetEntry>(),
                            LinkXmlTargetPath = string.Empty,
                            GameEntranceProcedureName = string.Empty,
                        };
                    }
                    PanelDimensionMask mask = master.HybridCLRMask;
                    if (!IsGlobal(mask))
                    {
                        foreach (HybridCLROverride o in master.HybridCLROverrides)
                        {
                            if (!MatchesMask(mask, o.Platform, o.Channel, o.DevelopMode, curP, curC, curM)) continue;
                            return new HybridCLRResult
                            {
                                // Override 列表为空时回落顶层字段（与 HybridCLROverride 注释约定一致）
                                AotMetadataDlls = (o.AotMetadataDlls != null && o.AotMetadataDlls.Count > 0) ? CloneDllList(o.AotMetadataDlls) : CloneDllList(master.AotMetadataDlls),
                                GameDlls = (o.GameDlls != null && o.GameDlls.Count > 0) ? CloneDllList(o.GameDlls) : CloneDllList(master.GameDlls),
                                LinkXmlTargetPath = string.IsNullOrEmpty(o.LinkXmlTargetPath) ? (master.LinkXmlTargetPath ?? string.Empty) : o.LinkXmlTargetPath,
                                GameEntranceProcedureName = string.IsNullOrEmpty(o.GameEntranceProcedureName) ? (master.GameEntranceProcedureName ?? string.Empty) : o.GameEntranceProcedureName,
                            };
                        }
                    }
                    // 全不勾或无命中，回落顶层默认字段
                    return new HybridCLRResult
                    {
                        AotMetadataDlls = CloneDllList(master.AotMetadataDlls),
                        GameDlls = CloneDllList(master.GameDlls),
                        LinkXmlTargetPath = master.LinkXmlTargetPath ?? string.Empty,
                        GameEntranceProcedureName = master.GameEntranceProcedureName ?? string.Empty,
                    };
                }

                /// <summary>
                /// 按当前坐标解析 YooAsset 面板两路径字段的最终生效值。
                /// <para>全不勾（IsGlobal）→ 直接取顶层各默认字段；否则在 YooAssetOverrides 中匹配首个符合条目，无命中回落顶层字段。</para>
                /// </summary>
                /// <param name="master">ConfigMasterSO 实例。</param>
                /// <param name="curP">当前平台。</param>
                /// <param name="curC">当前渠道。</param>
                /// <param name="curM">当前开发模式。</param>
                /// <returns>YooAssetResult；master 为 null 时两字段返回空字符串。</returns>
                public static YooAssetResult ResolveYooAsset(ConfigMasterSO master, PlatformType curP, ChannelType curC, DevelopMode curM)
                {
                    if (master == null) return new YooAssetResult { YooAssetSettingsPath = string.Empty, BundleCollectorSettingPath = string.Empty };
                    PanelDimensionMask mask = master.YooAssetMask;
                    if (!IsGlobal(mask))
                    {
                        foreach (YooAssetOverride o in master.YooAssetOverrides)
                        {
                            if (!MatchesMask(mask, o.Platform, o.Channel, o.DevelopMode, curP, curC, curM)) continue;
                            return new YooAssetResult
                            {
                                YooAssetSettingsPath = string.IsNullOrEmpty(o.YooAssetSettingsPath) ? (master.YooAssetSettingsPath ?? string.Empty) : o.YooAssetSettingsPath,
                                BundleCollectorSettingPath = string.IsNullOrEmpty(o.BundleCollectorSettingPath) ? (master.BundleCollectorSettingPath ?? string.Empty) : o.BundleCollectorSettingPath,
                            };
                        }
                    }
                    return new YooAssetResult
                    {
                        YooAssetSettingsPath = master.YooAssetSettingsPath ?? string.Empty,
                        BundleCollectorSettingPath = master.BundleCollectorSettingPath ?? string.Empty,
                    };
                }
#endif

                // -------------------------------------------------------
                // 私有匹配辅助
                // -------------------------------------------------------

                /// <summary>
                /// 判断掩码是否为全不勾（IsGlobal）；全不勾时直接用顶层默认字段，跳过 Override 列表遍历。
                /// </summary>
                /// <param name="mask">要检查的掩码。</param>
                /// <returns>三轴全为 false 时返回 true。</returns>
                private static bool IsGlobal(PanelDimensionMask mask) => !mask.ByPlatform && !mask.ByChannel && !mask.ByDevelopMode;

                /// <summary>
                /// 判断 Override 条目坐标在当前掩码下是否与目标坐标匹配。
                /// <para>勾选轴要求分量严格相等，未勾选轴直接跳过（无论存的是什么值）。</para>
                /// </summary>
                /// <param name="mask">当前掩码。</param>
                /// <param name="entryP">Override 条目的平台分量。</param>
                /// <param name="entryC">Override 条目的渠道分量。</param>
                /// <param name="entryM">Override 条目的开发模式分量。</param>
                /// <param name="targetP">目标坐标平台。</param>
                /// <param name="targetC">目标坐标渠道。</param>
                /// <param name="targetM">目标坐标开发模式。</param>
                /// <returns>勾选轴全部匹配时返回 true。</returns>
                private static bool MatchesMask(PanelDimensionMask mask, PlatformType entryP, ChannelType entryC, DevelopMode entryM, PlatformType targetP, ChannelType targetC, DevelopMode targetM)
                {
                    if (mask.ByPlatform && entryP != targetP) return false;
                    if (mask.ByChannel && entryC != targetC) return false;
                    if (mask.ByDevelopMode && entryM != targetM) return false;
                    return true;
                }

                /// <summary>
                /// 深拷贝 DllMasterAssetEntry 列表；DllMasterAssetEntry 是 struct（三字段均为不可变 string），
                /// new List(source) 逐元素值拷贝即为安全深拷贝。
                /// </summary>
                /// <param name="source">源列表，可为 null。</param>
                /// <returns>独立的新 List；source 为 null 时返回空列表。</returns>
                private static List<DllMasterAssetEntry> CloneDllList(List<DllMasterAssetEntry> source)
                {
                    if (source == null) return new List<DllMasterAssetEntry>();
                    return new List<DllMasterAssetEntry>(source);
                }
            }
        }
    }
}
