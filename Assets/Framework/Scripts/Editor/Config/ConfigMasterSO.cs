/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ConfigMasterSO.cs
 * author:    taoye
 * created:   2026/4/29
 * descrip:   Nova 全局配置主 SO；聚合公共参数、平台渠道矩阵、启用 SDK 列表与编辑态选中状态
 ***************************************************************/

using System;
using System.Collections.Generic;
using NovaFramework.Runtime;
using UnityEngine;

namespace NovaFramework.Editor
{
    /// <summary>
    /// Nova 全局配置主 SO；聚合公共参数、平台渠道矩阵、启用 SDK 列表与编辑态选中状态。
    /// </summary>
    [CreateAssetMenu(menuName = "Nova/Config Master", fileName = "ConfigMaster")]
    public sealed class ConfigMasterSO : ScriptableObject, ISerializationCallbackReceiver
    {
        /// <summary>
        /// 当前 ConfigMasterSO 序列化结构版本。
        /// </summary>
        public const int CurrentConfigSchemaVersion = 1;

        /// <summary>
        /// 当前资产的配置结构版本；Editor 只接受与当前版本完全一致的资产。
        /// </summary>
        [HideInInspector]
        public int ConfigSchemaVersion = CurrentConfigSchemaVersion;

        /// <summary>
        /// 顶层默认命名空间；NamespaceMask 全不勾时全局统一使用此值，勾选维度后由 NamespaceOverrides 按坐标覆盖，
        /// 最终生效值通过 DimensionalResolver.ResolveNamespace 解析。
        /// </summary>
        [Tooltip("顶层默认命名空间；NamespaceMask 全不勾时全工程共用此值；勾选维度后 NamespaceOverrides 按坐标覆盖，用于业务代码生成与资产路径前缀。")]
        public string Namespace;

        /// <summary>
        /// 启用的 SDK Plugin 类型全名列表；与左树勾选状态对应。
        /// </summary>
        public List<string> EnabledSDKs = new();

        /// <summary>
        /// 已启用的 Kit 配置类型全名白名单；仅白名单内类型在导出时写入 ConfigRuntimeSO。
        /// Kit 配置实例存储在 PlatformChannelEntry.KitConfigsByMode（三维矩阵），由 ConfigWindow 各格独立管理。
        /// </summary>
        public List<string> EnabledKits = new();

        /// <summary>
        /// Custom 本地默认路径键值；导出到 ConfigRuntimeSO。
        /// </summary>
        [Tooltip("Custom 本地默认路径键值。")]
        public CustomConfigData Custom = new();

        // -----------------------------------------------
        // 面板维度掩码
        // -----------------------------------------------

        /// <summary>
        /// 应用配置面板的维度掩码；控制该面板是否按平台/渠道/开发模式分别配置。
        /// </summary>
        public PanelDimensionMask AppConfigsMask = new();

        /// <summary>
        /// 隐私配置面板的维度掩码；与应用配置完全独立。
        /// </summary>
        public PanelDimensionMask PrivacyConfigsMask = new();

        /// <summary>
        /// SDK Plugin 各类型面板的维度掩码列表；每项对应一个 SDK Plugin 配置类型。
        /// TypeName 与 EnabledSDKs 中的元素同口径。
        /// </summary>
        public List<TypedDimensionMask> SDKMasks = new();

        /// <summary>
        /// Kit 各类型面板的维度掩码列表；每项对应一个 Kit 配置类型。
        /// TypeName 与 EnabledKits 中的元素同口径。
        /// </summary>
        public List<TypedDimensionMask> KitMasks = new();

        /// <summary>
        /// Namespace 面板的维度掩码；控制顶层 Namespace 字段是否按维度分别覆盖。
        /// 全不勾时 NamespaceOverrides 列表不参与取数，全局统一使用顶层 Namespace 字段。
        /// </summary>
        public PanelDimensionMask NamespaceMask = new();

        /// <summary>
        /// HybridCLR 面板全部字段（AotMetadataDlls / StartupGameDlls / RunningGameDlls /
        /// LinkXmlTargetPath / GameEntranceProcedureName）共用的维度掩码；全不勾时各字段使用顶层默认值。
        /// </summary>
        public PanelDimensionMask HybridEditorConfigsMask = new();

        /// <summary>
        /// YooAsset 两路径字段（YooAssetSettingsPath / BundleCollectorSettingPath）
        /// 共用的维度掩码；全不勾时两路径使用顶层默认值。
        /// </summary>
        public PanelDimensionMask YooAssetEditorConfigsMask = new();

        /// <summary>
        /// CDN 部署面板维度掩码；全不勾时 CDNEditorConfigs 全局一份，
        /// 勾选后 CDNEditorConfigsOverrides 按坐标覆盖。
        /// </summary>
        public PanelDimensionMask CDNEditorConfigsMask = new();

        /// <summary>
        /// Namespace 维度 Override 列表；列表为空时等同全不勾（全局使用顶层 Namespace 字段）。
        /// DimensionalResolver 按 NamespaceMask 勾选轴从列表中匹配首个符合条目后取 Value，
        /// 无命中回退顶层 Namespace 字段。
        /// </summary>
        public List<NamespaceOverride> NamespaceOverrides = new();

        /// <summary>
        /// CDN 内容部署与缓存清理配置；仅 Editor 期消费，不参与 ConfigRuntimeSO 导出。
        /// </summary>
        public CDNEditorConfigs CDNEditorConfigs = new();

        /// <summary>
        /// CDN 部署维度 Override 列表（仅 Editor 期消费）；列表为空时等同全不勾。
        /// DimensionalResolver 按 CDNEditorConfigsMask 勾选轴从列表中匹配首个符合条目后取 Config，
        /// 无命中回退顶层 CDNEditorConfigs。
        /// </summary>
        public List<CDNEditorConfigsOverride> CDNEditorConfigsOverrides = new();

        /// <summary>
        /// HybridCLR 面板维度 Override 列表（仅 Editor 期消费）；列表为空时等同全不勾。
        /// DimensionalResolver 从列表中匹配首个符合条目后取对应字段值，
        /// 无命中回退顶层 AotMetadataDlls / StartupGameDlls / RunningGameDlls / LinkXmlTargetPath / GameEntranceProcedureName。
        /// </summary>
        public HybridEditorConfigs HybridEditorConfigs = new();

        public List<HybridEditorConfigsOverride> HybridEditorConfigsOverrides = new();

        /// <summary>
        /// YooAsset 路径维度 Override 列表（仅 Editor 期消费）；列表为空时等同全不勾。
        /// DimensionalResolver 从列表中匹配首个符合条目后取路径值，
        /// 无命中回退顶层 YooAssetSettingsPath / BundleCollectorSettingPath。
        /// </summary>
        public YooAssetEditorConfigs YooAssetEditorConfigs = new();

        public List<YooAssetEditorConfigsOverride> YooAssetEditorConfigsOverrides = new();

        /// <summary>
        /// 当前编辑态选中的开发模式；默认 Debug，与原 m_DevelopMode = true 语义一致。
        /// </summary>
        public DevelopMode CurrentDevelopMode = DevelopMode.Debug;

        /// <summary>
        /// 当前编辑期平台；每次访问都实时映射 Unity 的 Active BuildTarget，不受 ConfigMaster 资产内容影响。
        /// </summary>
        public PlatformType CurrentPlatform => EditorUtil.Config.ActivePlatform.Current;

        /// <summary>
        /// 当前编辑态选中的渠道；Inspector 可通过该字段感知切换。
        /// </summary>
        public ChannelType CurrentChannel = ChannelType.Official;

        /// <summary>
        /// 导出目标 ConfigRuntimeSO 资产引用；Pipify Config Step 通过此引用推导目标路径并写入导出结果。
        /// </summary>
        public ConfigRuntimeSO ExportTarget;

        /// <summary>
        /// 序列化形态：完整的 Platform×Channel 矩阵行列表。
        /// </summary>
        [SerializeField]
        private List<PlatformChannelEntry> m_Entries = new();

        /// <summary>
        /// 运行时形态：按平台、渠道二级字典索引；非序列化，由 OnAfterDeserialize 重建。
        /// </summary>
        [NonSerialized]
        private Dictionary<PlatformType, Dictionary<ChannelType, PlatformChannelEntry>> m_Index;

        /// <summary>
        /// 获取指定 SDK Plugin 类型对应的面板维度掩码；
        /// 若 SDKMasks 中不存在该类型的条目，则自动追加默认条目（全不勾）并返回其 Mask，确保永不返回 null。
        /// 仅 Editor 期消费（ConfigWindow 绘制面板 toggle）。
        /// </summary>
        /// <param name="typeName">SDK Plugin 配置类型全名，与 EnabledSDKs 元素同口径。</param>
        /// <returns>对应类型的 PanelDimensionMask 实例，永不为 null。</returns>
        public PanelDimensionMask GetSDKMask(string typeName)
        {
            for (int i = 0; i < SDKMasks.Count; i++)
            {
                if (SDKMasks[i].TypeName == typeName)
                    return SDKMasks[i].Mask;
            }
            var entry = new TypedDimensionMask { TypeName = typeName };
            SDKMasks.Add(entry);
            return entry.Mask;
        }

        /// <summary>
        /// 获取指定 Kit 类型对应的面板维度掩码；
        /// 若 KitMasks 中不存在该类型的条目，则自动追加默认条目（全不勾）并返回其 Mask，确保永不返回 null。
        /// 仅 Editor 期消费（ConfigWindow 绘制面板 toggle）。
        /// </summary>
        /// <param name="typeName">Kit 配置类型全名，与 EnabledKits 元素同口径。</param>
        /// <returns>对应类型的 PanelDimensionMask 实例，永不为 null。</returns>
        public PanelDimensionMask GetKitMask(string typeName)
        {
            for (int i = 0; i < KitMasks.Count; i++)
            {
                if (KitMasks[i].TypeName == typeName)
                    return KitMasks[i].Mask;
            }
            var entry = new TypedDimensionMask { TypeName = typeName };
            KitMasks.Add(entry);
            return entry.Mask;
        }

        /// <summary>
        /// 暴露给 StructureGuard 的可变 Entries 视图；仅编辑期使用。
        /// </summary>
        public List<PlatformChannelEntry> EditorEntries => m_Entries;

        /// <summary>
        /// 获取指定 Platform × Channel × DevelopMode 组合对应的公共配置；
        /// 若矩阵行不存在则先补齐行，再在行内按 DevelopMode 自动追加默认项后返回，确保永不返回 null。
        /// 仅 Editor 期消费（ConfigWindow / StructureGuard），运行时通过 TryGetEntry 读取已有行。
        /// </summary>
        /// <param name="platform">目标平台。</param>
        /// <param name="channel">目标渠道。</param>
        /// <param name="mode">目标开发模式。</param>
        /// <returns>对应组合的 AppConfigs 实例。</returns>
        public AppConfigs GetAppConfigs(PlatformType platform, ChannelType channel, DevelopMode mode)
        {
            EnsureValidCoordinate(platform, channel);
            if (!TryGetEntry(platform, channel, out PlatformChannelEntry entry))
            {
                entry = new PlatformChannelEntry { Platform = platform, Channel = channel };
                EditorAddEntry(entry);
            }
            return entry.GetAppConfigs(mode);
        }

        /// <summary>
        /// 获取指定 Platform × Channel × DevelopMode 组合对应的隐私配置；缺少矩阵行时自动补齐。
        /// </summary>
        /// <param name="platform">目标平台。</param>
        /// <param name="channel">目标渠道。</param>
        /// <param name="mode">目标开发模式。</param>
        /// <returns>对应组合的 PrivacyConfigs 实例，永不为 null。</returns>
        public PrivacyConfigs GetPrivacyConfigs(PlatformType platform, ChannelType channel, DevelopMode mode)
        {
            EnsureValidCoordinate(platform, channel);
            if (!TryGetEntry(platform, channel, out PlatformChannelEntry entry))
            {
                entry = new PlatformChannelEntry { Platform = platform, Channel = channel };
                EditorAddEntry(entry);
            }
            return entry.GetPrivacyConfigs(mode);
        }

        /// <summary>
        /// 尝试获取指定平台渠道的矩阵行；命中返回 true。
        /// </summary>
        /// <param name="platform">目标平台。</param>
        /// <param name="channel">目标渠道。</param>
        /// <param name="entry">命中时输出对应矩阵行，未命中时为 null。</param>
        /// <returns>是否命中。</returns>
        public bool TryGetEntry(PlatformType platform, ChannelType channel, out PlatformChannelEntry entry)
        {
            entry = null;
            if (!IsValidCoordinate(platform, channel)) return false;
            if (m_Index == null)
            {
                RebuildIndex();
            }
            if (m_Index.TryGetValue(platform, out var row) && row.TryGetValue(channel, out entry))
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// 获取矩阵所有行；只读视图。
        /// </summary>
        /// <returns>所有 PlatformChannelEntry 的只读列表。</returns>
        public IReadOnlyList<PlatformChannelEntry> GetAllEntries() => m_Entries;

        /// <summary>
        /// 序列化前钩子；当前无额外操作。
        /// </summary>
        public void OnBeforeSerialize() { }

        /// <summary>
        /// 反序列化后钩子；重建二级字典索引。
        /// </summary>
        public void OnAfterDeserialize()
        {
            RebuildIndex();
        }

        /// <summary>
        /// 供 StructureGuard 编辑态增加矩阵行；添加新行后置空索引，下次访问按需重建。
        /// 仅 Editor 期消费。
        /// </summary>
        /// <param name="entry">要追加的矩阵行。</param>
        public void EditorAddEntry(PlatformChannelEntry entry)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));
            EnsureValidCoordinate(entry.Platform, entry.Channel);
            m_Entries.Add(entry);
            m_Index = null;
        }

        /// <summary>
        /// 供 StructureGuard 编辑态删除指定索引的行；删除后置空索引，下次访问按需重建。
        /// 仅 Editor 期消费。
        /// </summary>
        /// <param name="index">要删除的行在 m_Entries 中的索引。</param>
        public void EditorRemoveEntryAt(int index)
        {
            m_Entries.RemoveAt(index);
            m_Index = null;
        }

        /// <summary>
        /// 重建二级字典索引；供 TryGetEntry 与 OnAfterDeserialize 调用。
        /// </summary>
        private void RebuildIndex()
        {
            m_Index = new Dictionary<PlatformType, Dictionary<ChannelType, PlatformChannelEntry>>();
            for (int i = 0; i < m_Entries.Count; i++)
            {
                PlatformChannelEntry entry = m_Entries[i];
                if (entry == null || !IsValidCoordinate(entry.Platform, entry.Channel)) continue;
                if (!m_Index.TryGetValue(entry.Platform, out var row))
                {
                    row = new Dictionary<ChannelType, PlatformChannelEntry>();
                    m_Index[entry.Platform] = row;
                }
                row[entry.Channel] = entry;
            }
        }

        /// <summary>
        /// 判断平台与渠道是否可作为 Config 的实际矩阵坐标；两个 None 都只保留作内部哨兵。
        /// </summary>
        /// <param name="platform">待检查的平台。</param>
        /// <param name="channel">待检查的渠道。</param>
        /// <returns>两个枚举均已定义且都不是 None 时返回 true。</returns>
        private static bool IsValidCoordinate(PlatformType platform, ChannelType channel)
        {
            return platform != PlatformType.None && Enum.IsDefined(typeof(PlatformType), platform) &&
                   channel != ChannelType.None && Enum.IsDefined(typeof(ChannelType), channel);
        }

        /// <summary>
        /// 拒绝使用 None 或未定义枚举值创建 Config 矩阵数据。
        /// </summary>
        /// <param name="platform">待检查的平台。</param>
        /// <param name="channel">待检查的渠道。</param>
        /// <exception cref="ArgumentOutOfRangeException">平台或渠道不是有效 Config 坐标时抛出。</exception>
        private static void EnsureValidCoordinate(PlatformType platform, ChannelType channel)
        {
            if (platform == PlatformType.None || !Enum.IsDefined(typeof(PlatformType), platform))
                throw new ArgumentOutOfRangeException(nameof(platform), platform, "Config 平台必须是非 None 的已定义 PlatformType。");
            if (channel == ChannelType.None || !Enum.IsDefined(typeof(ChannelType), channel))
                throw new ArgumentOutOfRangeException(nameof(channel), channel, "Config 渠道必须是非 None 的已定义 ChannelType。");
        }
    }
}
