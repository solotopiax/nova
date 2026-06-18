/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ConfigComponent.Visitors.cs
 * author:    taoye
 * created:   2026/1/21
 * descrip:   配置组件-访问器
 ***************************************************************/

using System.Collections.Generic;
using UnityEngine;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 配置组件。
    /// </summary>
    public sealed partial class ConfigComponent : FrameworkComponent
    {
        /// <summary>
        /// 当前配置管理器实现类全名。
        /// </summary>
        [Tooltip("配置管理器实现类全名")]
        [SerializeField]
        private string m_CurManagerTypeName = "NovaFramework.Runtime.ConfigManager";
        public string CurManagerTypeName => m_CurManagerTypeName;

        /// <summary>
        /// ConfigRuntimeSO Asset 地址；由 ConfigManager.Initialize 通过 ConfigManagerConfig 使用。
        /// </summary>
        [Tooltip("ConfigRuntimeSO Asset 地址")]
        [SerializeField]
        private string m_AssetLocation;
        internal string AssetLocation => m_AssetLocation;

        /// <summary>
        /// 配置管理器实例；仅限 ConfigComponent 自身访问，外部一律走本组件的薄封装属性。
        /// </summary>
        private IConfigManager m_ConfigManager;

        /// <summary>
        /// 是否已加载完成；转发 Manager 状态。
        /// </summary>
        public bool IsLoadOver => m_ConfigManager != null && m_ConfigManager.IsLoadOver;

        /// <summary>
        /// 当前运行时的开发模式（Debug / Release）；LoadAsync 完成后可读。
        /// </summary>
        public DevelopMode DevelopMode => m_ConfigManager != null ? m_ConfigManager.DevelopMode : default;

        /// <summary>
        /// 全局公共配置整块；LoadAsync 完成后可读，未加载时返回 null。
        /// </summary>
        public CommonConfig Common => m_ConfigManager?.Common;

        /// <summary>
        /// 当前解析后的 Namespace；LoadAsync 完成后可读。
        /// </summary>
        public string Namespace => m_ConfigManager?.Namespace;

        /// <summary>
        /// 本次加载 ConfigRuntimeSO 时记录的目标平台；由导出侧在 Export 时写入。
        /// </summary>
        public PlatformType Platform => m_ConfigManager != null ? m_ConfigManager.Platform : default;

        /// <summary>
        /// 本次加载 ConfigRuntimeSO 时记录的目标渠道；由导出侧在 Export 时写入。
        /// </summary>
        public ChannelType Channel => m_ConfigManager != null ? m_ConfigManager.Channel : default;

        /// <summary>
        /// 业务入口 Procedure 相对类型名；LoadAsync 完成后可读。
        /// </summary>
        public string GameEntranceProcedureName => m_ConfigManager?.GameEntranceProcedureName ?? string.Empty;

        /// <summary>
        /// AOT 元数据 DLL 列表；LoadAsync 完成后可读，未加载时返回空集合。
        /// </summary>
        public IReadOnlyList<DllAssetEntry> AotMetadataDlls =>
            m_ConfigManager != null ? m_ConfigManager.AotMetadataDlls : System.Array.Empty<DllAssetEntry>();

        /// <summary>
        /// 业务 DLL 列表；LoadAsync 完成后可读，未加载时返回空集合。
        /// </summary>
        public IReadOnlyList<DllAssetEntry> GameDlls =>
            m_ConfigManager != null ? m_ConfigManager.GameDlls : System.Array.Empty<DllAssetEntry>();

        /// <summary>
        /// 当前已加载的所有启用 SDK Plugin 配置只读集合；供 Inspector 面板运行时展示。
        /// 未绑定 Manager 时返回空集合。
        /// </summary>
        public IReadOnlyCollection<ISDKPluginConfig> AllPluginConfigs =>
            m_ConfigManager != null ? m_ConfigManager.GetAllPluginConfigs() : System.Array.Empty<ISDKPluginConfig>();
    }
}
