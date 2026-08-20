/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  IKitConfig.cs
 * author:    taoye
 * created:   2026/5/31
 * descrip:   Kit 固有配置 marker 接口
 ***************************************************************/

namespace NovaFramework.Runtime
{
    /// <summary>
    /// Kit 固有配置接口。
    /// 配置实例存储在 ConfigMasterSO 的 PlatformChannelEntry.KitConfigsByMode 中，按 DevelopMode 分格；ConfigMasterSO.EnabledKits 仅为 Kit 类型白名单。
    /// Exporter 将当前 Platform×Channel×DevelopMode 坐标的已启用配置导出为单格 ConfigRuntimeSO；Kit Service 方法内通过 Nova.Config.GetKitConfig 按类型拉取并强转使用。
    /// 实现类必须提供 DisplayName，供 ConfigWindow 左树渲染中文名；右侧字段悬停提示由字段上的 [Tooltip] 特性承载。
    /// </summary>
    public interface IKitConfig
    {
        /// <summary>
        /// 配置项在 ConfigWindow 左树中展示的名称，不可为空。
        /// </summary>
        string DisplayName { get; }
    }
}
