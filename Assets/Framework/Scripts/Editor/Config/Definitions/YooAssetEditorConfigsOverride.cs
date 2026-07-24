/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  YooAssetEditorConfigsOverride.cs
 * author:    taoye
 * created:   2026/6/2
 * descrip:   YooAsset 两路径字段的维度 Override 单项容器（仅 Editor 期消费）
 ***************************************************************/

using System;
using NovaFramework.Runtime;

namespace NovaFramework.Editor
{
    /// <summary>
    /// YooAsset 编辑配置的维度 Override 单项。
    /// </summary>
    [Serializable]
    public sealed class YooAssetEditorConfigsOverride : YooAssetEditorConfigs
    {
        /// <summary>
        /// 平台类型筛选轴；仅当 YooAssetEditorConfigsMask.ByPlatform == true 时参与匹配，
        /// 不参与匹配时设置为 PlatformType.None 哨兵。
        /// </summary>
        public PlatformType Platform;

        /// <summary>
        /// 渠道类型筛选轴；仅当 YooAssetEditorConfigsMask.ByChannel == true 时参与匹配，
        /// 不参与匹配时设置为 ChannelType.None 哨兵。
        /// </summary>
        public ChannelType Channel;

        /// <summary>
        /// 开发模式筛选轴；仅当 YooAssetEditorConfigsMask.ByDevelopMode == true 时参与匹配；
        /// DevelopMode 枚举无 None 哨兵，不参与匹配时维持默认值 DevelopMode.Debug。
        /// </summary>
        public DevelopMode DevelopMode;

        /// <summary>
        /// YooAsset 全局配置文件（YooAssetSettings.asset）的项目根相对路径 Override；
        /// 覆盖顶层 ConfigMasterSO.YooAssetSettingsPath；空字符串是当前坐标明确配置的有效值。
        /// 仅 Editor 期消费，由 EditorUtil.Config.YooAssetInjector 注入到 YooAssetConfiguration。
        /// </summary>

        /// <summary>
        /// YooAsset Bundle 收集器配置（BundleCollectorSetting.asset）的项目根相对路径 Override；
        /// 覆盖顶层 ConfigMasterSO.BundleCollectorSettingPath；空字符串是当前坐标明确配置的有效值。
        /// 仅 Editor 期消费，替代 AssetDatabase.FindAssets 全工程扫描。
        /// </summary>
    }
}
