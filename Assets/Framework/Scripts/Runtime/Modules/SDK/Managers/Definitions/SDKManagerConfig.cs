/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  SDKManagerConfig.cs
 * author:    taoye
 * created:   2026/3/16
 * descrip:   SDK 管理器构造配置 DTO
 ***************************************************************/

using System.Collections.Generic;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// SDK 管理器构造配置 DTO，由 SDKComponent.Start() 构造后传入 ISDKManager.Initialize。
    /// 携带 Inspector 序列化的插件条目列表。运行时启用和排序不依赖该列表。
    /// </summary>
    public sealed class SDKManagerConfig
    {
        /// <summary>
        /// Inspector 序列化的插件条目列表，来自 SDKComponent.m_PluginEntries。
        /// Manager.Initialize 不按此列表实例化插件；运行时启用统一来自 ConfigMaster.EnabledSDKs，排序使用 ISDKPlugin.Priority。
        /// </summary>
        public IReadOnlyList<SDKPluginEntry> PluginEntries;
    }
}
