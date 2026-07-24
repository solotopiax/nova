/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ConfigManager.Visitors.cs
 * author:    taoye
 * created:   2025/12/5
 * descrip:   配置管理器 —— 字段与属性
 ***************************************************************/

namespace NovaFramework.Runtime
{
    internal sealed partial class ConfigManager : ConfigManagerBase
    {
        /// <summary>
        /// 资源管理器；在 Initialize 中获取并缓存，供 LoadAsync/Shutdown 使用。
        /// </summary>
        private IAssetManager m_AssetManager;

        /// <summary>
        /// ConfigRuntimeSO 的 Asset 地址；由 Component 在 Start 阶段从 Inspector 字段写入。
        /// </summary>
        private string m_AssetLocation;

        /// <summary>
        /// ConfigRuntimeSO 资源句柄；LoadAsync 成功后持有，Shutdown 时 Release 使引用计数归零。
        /// </summary>
        private IAssetHandle<ConfigRuntimeSO> m_ConfigHandle;
        /// <summary>
        /// 加载后持有的 ConfigRuntimeSO 对象引用；直接从 m_ConfigHandle.Asset 读取，Shutdown 时随句柄归零。
        /// </summary>
        private ConfigRuntimeSO m_Runtime;

        /// <summary>
        /// 应用运行时配置；直接读 ConfigRuntimeSO.AppConfigs，LoadAsync 完成后可读。
        /// </summary>
        public override AppConfigs AppConfigs => m_Runtime?.AppConfigs;

        /// <summary>
        /// Namespace；直接读 ConfigRuntimeSO 顶层字段，LoadAsync 完成后可读。
        /// </summary>
        public override string Namespace => m_Runtime?.Namespace ?? string.Empty;

        /// <summary>
        /// HybridCLR 运行时配置；直接读 ConfigRuntimeSO.HybridConfigs。
        /// </summary>
        public override HybridConfigs HybridConfigs => m_Runtime?.HybridConfigs;

        /// <summary>
        /// 业务自定义运行时配置；直接读 ConfigRuntimeSO.CustomConfigs。
        /// </summary>
        public override CustomConfigs CustomConfigs => m_Runtime?.CustomConfigs;

        /// <summary>
        /// 是否已完成加载。
        /// </summary>
        private bool m_IsLoadOver;
        /// <summary>
        /// 是否已完成加载；对外属性。
        /// </summary>
        public override bool IsLoadOver => m_IsLoadOver;

        /// <summary>
        /// 开发模式；从 ConfigRuntimeSO 读取，未加载时返回 DevelopMode.Debug。
        /// </summary>
        public override DevelopMode DevelopMode => m_Runtime == null ? DevelopMode.Debug : m_Runtime.DevelopMode;

        /// <summary>
        /// 目标平台；从 ConfigRuntimeSO 读取，未加载时返回 PlatformType.None。
        /// </summary>
        public override PlatformType Platform => m_Runtime == null ? PlatformType.None : m_Runtime.Platform;

        /// <summary>
        /// 目标渠道；未加载时返回 ChannelType.None。
        /// </summary>
        public override ChannelType Channel => m_Runtime == null ? ChannelType.None : m_Runtime.Channel;
    }
}
