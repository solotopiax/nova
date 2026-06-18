/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ISDKPluginConfig.cs
 * author:    taoye
 * created:   2026/4/28
 * descrip:   SDK 插件配置 marker 接口
 ***************************************************************/

namespace NovaFramework.Runtime
{
    /// <summary>
    /// SDK 插件配置接口。
    /// 在 ConfigMasterSO 中静态配置后由 ConfigRuntimeSO 承载；SDKManager 按 Plugin.RequiredConfigType 从 IConfigManager 拉取注入；Plugin 内部通过强转获取。
    /// 实现类必须提供 DisplayName，供 ConfigWindow 左树渲染中文名；右侧字段悬停提示由字段上的 [Tooltip] 特性承载。
    /// </summary>
    public interface ISDKPluginConfig
    {
        /// <summary>
        /// 配置项在 ConfigWindow 左树中展示的中文名称，不可为空。
        /// </summary>
        string DisplayName { get; }
    }
}
