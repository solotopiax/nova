/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  CdnDeploymentOverride.cs
 * author:    taoye
 * created:   2026/7/22
 * descrip:   CDN 部署配置的维度 Override 单项容器（仅 Editor 期消费）
 ***************************************************************/

#if UNITY_EDITOR
using System;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// CDN 部署配置的维度 Override 单项（仅 Editor 期消费）。
    /// 对应 ConfigMasterSO 的 CdnDeployment 字段；
    /// 当 CdnMask 勾选维度轴后，列表中与当前维度匹配的首个条目整体覆盖顶层 CdnDeployment。
    /// 列表为空或无命中时，使用顶层 CdnDeployment 作为全局默认值。
    /// Config 为整套快照，切坐标=整套 10 字段一份。
    /// 所有字段均为 Editor-only；导出侧（ConfigRuntimeSO）零改动，Runtime 侧无感知。
    /// </summary>
    [Serializable]
    public sealed class CdnDeploymentOverride
    {
        /// <summary>
        /// 平台类型筛选轴；仅当 CdnMask.ByPlatform == true 时参与匹配，
        /// 不参与匹配时设置为 PlatformType.None 哨兵。
        /// </summary>
        public PlatformType Platform;

        /// <summary>
        /// 渠道类型筛选轴；仅当 CdnMask.ByChannel == true 时参与匹配，
        /// 不参与匹配时设置为 ChannelType.None 哨兵。
        /// </summary>
        public ChannelType Channel;

        /// <summary>
        /// 开发模式筛选轴；仅当 CdnMask.ByDevelopMode == true 时参与匹配；
        /// DevelopMode 枚举无 None 哨兵，不参与匹配时维持默认值 DevelopMode.Debug。
        /// </summary>
        public DevelopMode DevelopMode;

        /// <summary>
        /// CDN 部署配置整套快照；覆盖顶层 ConfigMasterSO.CdnDeployment；
        /// 为 null 时回退顶层字段。整套快照，切坐标=整套 10 字段一份。
        /// 仅 Editor 期消费，由 EditorUtil.Config 相关注入器消费。
        /// </summary>
        public CdnDeploymentConfig Config;
    }
}
#endif
