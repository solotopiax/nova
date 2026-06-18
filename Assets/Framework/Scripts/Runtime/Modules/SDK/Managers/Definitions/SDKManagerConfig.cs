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
    /// 携带 Inspector 序列化的插件条目列表，Manager 据此反射实例化并建立索引。
    /// </summary>
    public sealed class SDKManagerConfig
    {
        /// <summary>
        /// Inspector 序列化的插件条目列表，来自 SDKComponent.m_PluginEntries。
        /// Manager.Initialize 按此列表进行反射实例化，跳过 Enabled==false 与 IsMissing==true 的条目。
        /// </summary>
        public IReadOnlyList<SDKPluginEntry> PluginEntries;
    }
}
