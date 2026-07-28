/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  TableManagerConfig.cs
 * author:    taoye
 * created:   2026/2/5
 * descrip:   Table 管理器运行时 Binding 配置
 ***************************************************************/

namespace NovaFramework.Runtime
{
    /// <summary>
    /// Table 管理器配置，传递任意数量的生成 Binding。
    /// </summary>
    public sealed class TableManagerConfig
    {
        public System.Collections.Generic.IReadOnlyList<TableRuntimeBindingSetting> Bindings;
    }
}
