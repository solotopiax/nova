/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  HybridEditorConfigsOverride.cs
 * author:    taoye
 * created:   2026/6/2
 * descrip:   HybridCLR 面板全部字段的维度 Override 单项容器（仅 Editor 期消费）
 ***************************************************************/

using System;
using NovaFramework.Runtime;

namespace NovaFramework.Editor
{
    /// <summary>
    /// HybridCLR 编辑配置的维度 Override 单项。
    /// </summary>
    [Serializable]
    public sealed class HybridEditorConfigsOverride : HybridEditorConfigs
    {
        /// <summary>
        /// 平台类型筛选轴；仅当 HybridEditorConfigsMask.ByPlatform == true 时参与匹配，
        /// 不参与匹配时设置为 PlatformType.None 哨兵。
        /// </summary>
        public PlatformType Platform;

        /// <summary>
        /// 渠道类型筛选轴；仅当 HybridEditorConfigsMask.ByChannel == true 时参与匹配，
        /// 不参与匹配时设置为 ChannelType.None 哨兵。
        /// </summary>
        public ChannelType Channel;

        /// <summary>
        /// 开发模式筛选轴；仅当 HybridEditorConfigsMask.ByDevelopMode == true 时参与匹配；
        /// DevelopMode 枚举无 None 哨兵，不参与匹配时维持默认值 DevelopMode.Debug。
        /// </summary>
        public DevelopMode DevelopMode;

        /// <summary>
        /// AOT 元数据 DLL 列表 Override（编辑期三字段视图）；
        /// 覆盖顶层 ConfigMasterSO.AotMetadataDlls；空列表是当前坐标明确配置的有效值。
        /// </summary>

        /// <summary>
        /// 业务 DLL 列表 Override（编辑期三字段视图）；
        /// 覆盖顶层 ConfigMasterSO.GameDlls；空列表是当前坐标明确配置的有效值。
        /// </summary>

        /// <summary>
        /// link.xml 目标位置 Override（项目根相对的具体文件路径，含文件名与 .xml 扩展名）；
        /// 覆盖顶层 ConfigMasterSO.LinkXmlTargetPath；空字符串是当前坐标明确配置的有效值。
        /// </summary>

        /// <summary>
        /// 业务入口 Procedure 相对类型名 Override（不含 namespace，如 ProcedurePreload）；
        /// 覆盖顶层 ConfigMasterSO.GameEntranceProcedureName；空字符串是当前坐标明确配置的有效值。
        /// </summary>
    }
}
