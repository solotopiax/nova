/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  YooAssetOverride.cs
 * author:    taoye
 * created:   2026/6/2
 * descrip:   YooAsset 两路径字段的维度 Override 单项容器（仅 Editor 期消费）
 ***************************************************************/

#if UNITY_EDITOR
using System;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// YooAsset 两路径字段的维度 Override 单项（仅 Editor 期消费）。
    /// 对应 ConfigMasterSO 的 YooAssetSettingsPath / BundleCollectorSettingPath 两个字段；
    /// 当 YooAssetMask 勾选维度轴后，列表中与当前维度匹配的首个条目覆盖顶层路径字段。
    /// 列表为空或无命中时，使用顶层字段值作为全局默认值。
    /// 两字段均为 Editor-only；导出侧（ConfigRuntimeSO）零改动，Runtime 侧无感知。
    /// </summary>
    [Serializable]
    public sealed class YooAssetOverride
    {
        /// <summary>
        /// 平台类型筛选轴；仅当 YooAssetMask.ByPlatform == true 时参与匹配，
        /// 不参与匹配时设置为 PlatformType.None 哨兵。
        /// </summary>
        public PlatformType Platform;

        /// <summary>
        /// 渠道类型筛选轴；仅当 YooAssetMask.ByChannel == true 时参与匹配，
        /// 不参与匹配时设置为 ChannelType.None 哨兵。
        /// </summary>
        public ChannelType Channel;

        /// <summary>
        /// 开发模式筛选轴；仅当 YooAssetMask.ByDevelopMode == true 时参与匹配；
        /// DevelopMode 枚举无 None 哨兵，不参与匹配时维持默认值 DevelopMode.Debug。
        /// </summary>
        public DevelopMode DevelopMode;

        /// <summary>
        /// YooAsset 全局配置文件（YooAssetSettings.asset）的项目根相对路径 Override；
        /// 覆盖顶层 ConfigMasterSO.YooAssetSettingsPath；为 null 或空时回退顶层字段。
        /// 仅 Editor 期消费，由 EditorUtil.Config.YooAssetInjector 注入到 YooAssetConfiguration。
        /// </summary>
        public string YooAssetSettingsPath;

        /// <summary>
        /// YooAsset Bundle 收集器配置（BundleCollectorSetting.asset）的项目根相对路径 Override；
        /// 覆盖顶层 ConfigMasterSO.BundleCollectorSettingPath；为 null 或空时回退顶层字段。
        /// 仅 Editor 期消费，替代 AssetDatabase.FindAssets 全工程扫描。
        /// </summary>
        public string BundleCollectorSettingPath;
    }
}
#endif
