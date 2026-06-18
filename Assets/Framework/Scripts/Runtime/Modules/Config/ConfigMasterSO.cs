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
using UnityEngine;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// Nova 全局配置主 SO；聚合公共参数、平台渠道矩阵、启用 SDK 列表与编辑态选中状态。
    /// </summary>
    [CreateAssetMenu(menuName = "Nova/Config Master", fileName = "ConfigMaster")]
    public sealed class ConfigMasterSO : ScriptableObject, ISerializationCallbackReceiver
    {
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

        // -----------------------------------------------
        // 面板维度掩码（矩阵三类：CommonMask / SDKMasks / KitMasks；顶层三类：NamespaceMask / HybridCLRMask / YooAssetMask）
        // -----------------------------------------------

        /// <summary>
        /// 应用配置面板（Common）的维度掩码；控制该面板是否按平台/渠道/开发模式分别配置。
        /// </summary>
        public PanelDimensionMask CommonMask = new();

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
        /// HybridCLR 面板全部字段（AotMetadataDlls / GameDlls / LinkXmlTargetPath /
        /// GameEntranceProcedureName）共用的维度掩码；全不勾时各字段使用顶层默认值。
        /// </summary>
        public PanelDimensionMask HybridCLRMask = new();

        /// <summary>
        /// YooAsset 两路径字段（YooAssetSettingsPath / BundleCollectorSettingPath）
        /// 共用的维度掩码；全不勾时两路径使用顶层默认值。
        /// </summary>
        public PanelDimensionMask YooAssetMask = new();

        /// <summary>
        /// Namespace 维度 Override 列表；列表为空时等同全不勾（全局使用顶层 Namespace 字段）。
        /// DimensionalResolver 按 NamespaceMask 勾选轴从列表中匹配首个符合条目后取 Value，
        /// 无命中回退顶层 Namespace 字段。
        /// </summary>
        public List<NamespaceOverride> NamespaceOverrides = new();

#if UNITY_EDITOR
        /// <summary>
        /// HybridCLR 面板维度 Override 列表（仅 Editor 期消费）；列表为空时等同全不勾。
        /// DimensionalResolver 从列表中匹配首个符合条目后取对应字段值，
        /// 无命中回退顶层 AotMetadataDlls / GameDlls / LinkXmlTargetPath / GameEntranceProcedureName。
        /// </summary>
        public List<HybridCLROverride> HybridCLROverrides = new();

        /// <summary>
        /// YooAsset 路径维度 Override 列表（仅 Editor 期消费）；列表为空时等同全不勾。
        /// DimensionalResolver 从列表中匹配首个符合条目后取路径值，
        /// 无命中回退顶层 YooAssetSettingsPath / BundleCollectorSettingPath。
        /// </summary>
        public List<YooAssetOverride> YooAssetOverrides = new();
#endif

        /// <summary>
        /// 当前编辑态选中的开发模式；默认 Debug，与原 m_DevelopMode = true 语义一致。
        /// </summary>
        public DevelopMode CurrentDevelopMode = DevelopMode.Debug;

        /// <summary>
        /// 当前编辑态选中的平台；Inspector 可通过该字段感知切换。
        /// </summary>
        public PlatformType CurrentPlatform;

        /// <summary>
        /// 当前编辑态选中的渠道；Inspector 可通过该字段感知切换。
        /// </summary>
        public ChannelType CurrentChannel;

        /// <summary>
        /// 导出目标 ConfigRuntimeSO 资产引用；Pipify Config Step 通过此引用推导目标路径并写入导出结果。
        /// </summary>
        public ConfigRuntimeSO ExportTarget;

        /// <summary>
        /// 业务入口 Procedure 相对类型名（不含 namespace），如 ProcedurePreload；
        /// 由 ProcedureLoadDll 在 DLL 加载后用于注册业务 Procedure 入口。
        /// </summary>
        public string GameEntranceProcedureName;

#if UNITY_EDITOR
        /// <summary>
        /// YooAsset 全局配置文件（YooAssetSettings.asset）的项目根相对路径。
        /// 仅 Editor 期消费；由 ConfigWindow 设置，并由 EditorUtil.Config.YooAssetInjector 注入到 YooAssetConfiguration。
        /// </summary>
        [Tooltip("YooAssetSettings.asset 的项目根相对路径，仅 Editor 期消费")]
        public string YooAssetSettingsPath;

        /// <summary>
        /// YooAsset Bundle 收集器配置（BundleCollectorSetting.asset）的项目根相对路径。
        /// 仅 Editor 期消费；替代 AssetDatabase.FindAssets 全工程扫描。
        /// </summary>
        [Tooltip("BundleCollectorSetting.asset 的项目根相对路径，仅 Editor 期消费")]
        public string BundleCollectorSettingPath;

        /// <summary>
        /// link.xml 目标位置；项目根相对的具体文件路径（含文件名与 .xml 扩展名），
        /// 如 Assets/link.xml；为空时 EditorUtil.HybridCLR 回退默认 Assets/link.xml。
        /// 由 hybridclr.validate_linkxml / hybridclr.generate_linkxml 两个 Step 读取写入。
        /// 仅 Editor 期消费（构建 Step），运行时不读取。
        /// </summary>
        public string LinkXmlTargetPath;

        /// <summary>
        /// AOT 元数据 DLL 列表（编辑期三字段视图）；含源/目标路径与 Asset 地址，
        /// 由 EditorUtil.HybridCLR 拷贝时读取，导出后写入 ConfigRuntimeSO.AotMetadataDlls（单字段）。
        /// 仅 Editor 期消费；运行时从 ConfigRuntimeSO.AotMetadataDlls 读取单字段视图。
        /// </summary>
        public List<DllMasterAssetEntry> AotMetadataDlls = new();

        /// <summary>
        /// 业务 DLL 列表（编辑期三字段视图）；含源/目标路径与 Asset 地址，
        /// 由 EditorUtil.HybridCLR 拷贝时读取，导出后写入 ConfigRuntimeSO.GameDlls（单字段）。
        /// 仅 Editor 期消费；运行时从 ConfigRuntimeSO.GameDlls 读取单字段视图。
        /// </summary>
        public List<DllMasterAssetEntry> GameDlls = new();
#endif

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

#if UNITY_EDITOR
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
        /// <returns>对应组合的 CommonConfig 实例。</returns>
        public CommonConfig GetCommon(PlatformType platform, ChannelType channel, DevelopMode mode)
        {
            if (!TryGetEntry(platform, channel, out PlatformChannelEntry entry))
            {
                entry = new PlatformChannelEntry { Platform = platform, Channel = channel };
                EditorAddEntry(entry);
            }
            return entry.GetCommon(mode);
        }
#endif

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

#if UNITY_EDITOR
        /// <summary>
        /// 供 StructureGuard 编辑态增加矩阵行；添加新行后置空索引，下次访问按需重建。
        /// 仅 Editor 期消费。
        /// </summary>
        /// <param name="entry">要追加的矩阵行。</param>
        public void EditorAddEntry(PlatformChannelEntry entry)
        {
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
#endif

        /// <summary>
        /// 重建二级字典索引；供 TryGetEntry 与 OnAfterDeserialize 调用。
        /// </summary>
        private void RebuildIndex()
        {
            m_Index = new Dictionary<PlatformType, Dictionary<ChannelType, PlatformChannelEntry>>();
            for (int i = 0; i < m_Entries.Count; i++)
            {
                PlatformChannelEntry entry = m_Entries[i];
                if (!m_Index.TryGetValue(entry.Platform, out var row))
                {
                    row = new Dictionary<ChannelType, PlatformChannelEntry>();
                    m_Index[entry.Platform] = row;
                }
                row[entry.Channel] = entry;
            }
        }
    }
}
