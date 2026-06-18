/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DebugManagerConfig.cs
 * author:    taoye
 * created:   2026/5/9
 * descrip:   DebugManager 初始化配置。
 ***************************************************************/

using System.Collections.Generic;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// DebugManager 初始化配置，由 DebugComponent 在 Start() 时组装并传入。
    /// </summary>
    public class DebugManagerConfig
    {
        /// <summary>
        /// 磁盘监控配置集合。
        /// </summary>
        public List<DiskCheckingConfig> DiskCheckingConfigs;
    }
}
