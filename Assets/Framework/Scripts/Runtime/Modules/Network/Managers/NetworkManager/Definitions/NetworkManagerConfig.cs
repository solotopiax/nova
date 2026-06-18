/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  NetworkManagerConfig.cs
 * author:    taoye
 * created:   2026/3/9
 * descrip:   Network管理器配置
 ***************************************************************/

using System.Collections.Generic;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// Network 管理器配置，承载域名表与指令表的 Luban 加载设置及运行时依赖注入。
    /// </summary>
    public class NetworkManagerConfig
    {
        /// <summary>
        /// 域名表单元设置列表。
        /// </summary>
        public List<HostKeyUnitSetting> HostKeyUnitSettings;

        /// <summary>
        /// 指令表单元设置列表。
        /// </summary>
        public List<NetCmdUnitSetting> NetCmdUnitSettings;

    }
}
