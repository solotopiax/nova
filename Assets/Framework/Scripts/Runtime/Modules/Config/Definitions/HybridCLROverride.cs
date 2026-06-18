/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  HybridCLROverride.cs
 * author:    taoye
 * created:   2026/6/2
 * descrip:   HybridCLR 面板全部字段的维度 Override 单项容器（仅 Editor 期消费）
 ***************************************************************/

#if UNITY_EDITOR
using System;
using System.Collections.Generic;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// HybridCLR 面板全部字段的维度 Override 单项（仅 Editor 期消费）。
    /// 对应 ConfigMasterSO 的 AotMetadataDlls / GameDlls / LinkXmlTargetPath /
    /// GameEntranceProcedureName 四个字段；当 HybridCLRMask 勾选维度轴后，
    /// 列表中与当前维度匹配的首个条目覆盖上述顶层字段。
    /// 列表为空或无命中时，使用顶层字段值作为全局默认值。
    /// 导出流程与 ConfigRuntimeSO 零交互——导出时由 DimensionProjector / DimensionalResolver
    /// 先解析出最终单值，再走原有 Exporter 路径写入 ConfigRuntimeSO（Runtime 侧无感知）。
    /// </summary>
    [Serializable]
    public sealed class HybridCLROverride
    {
        /// <summary>
        /// 平台类型筛选轴；仅当 HybridCLRMask.ByPlatform == true 时参与匹配，
        /// 不参与匹配时设置为 PlatformType.None 哨兵。
        /// </summary>
        public PlatformType Platform;

        /// <summary>
        /// 渠道类型筛选轴；仅当 HybridCLRMask.ByChannel == true 时参与匹配，
        /// 不参与匹配时设置为 ChannelType.None 哨兵。
        /// </summary>
        public ChannelType Channel;

        /// <summary>
        /// 开发模式筛选轴；仅当 HybridCLRMask.ByDevelopMode == true 时参与匹配；
        /// DevelopMode 枚举无 None 哨兵，不参与匹配时维持默认值 DevelopMode.Debug。
        /// </summary>
        public DevelopMode DevelopMode;

        /// <summary>
        /// AOT 元数据 DLL 列表 Override（编辑期三字段视图）；
        /// 覆盖顶层 ConfigMasterSO.AotMetadataDlls；为空时回退顶层字段。
        /// </summary>
        public List<DllMasterAssetEntry> AotMetadataDlls = new();

        /// <summary>
        /// 业务 DLL 列表 Override（编辑期三字段视图）；
        /// 覆盖顶层 ConfigMasterSO.GameDlls；为空时回退顶层字段。
        /// </summary>
        public List<DllMasterAssetEntry> GameDlls = new();

        /// <summary>
        /// link.xml 目标位置 Override（项目根相对的具体文件路径，含文件名与 .xml 扩展名）；
        /// 覆盖顶层 ConfigMasterSO.LinkXmlTargetPath；为 null 或空时回退顶层字段。
        /// </summary>
        public string LinkXmlTargetPath;

        /// <summary>
        /// 业务入口 Procedure 相对类型名 Override（不含 namespace，如 ProcedurePreload）；
        /// 覆盖顶层 ConfigMasterSO.GameEntranceProcedureName；为 null 或空时回退顶层字段。
        /// </summary>
        public string GameEntranceProcedureName;
    }
}
#endif
