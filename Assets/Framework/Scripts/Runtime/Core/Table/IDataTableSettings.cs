/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  IDataTableSettings.cs
 * author:    taoye
 * created:   2026/4/16
 * descrip:   数据表设置接口
 ***************************************************************/

using System.Collections.Generic;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 数据表设置接口，包含数据源目录和单元设置列表。
    /// Table / Config 等模块的 Settings 类实现此接口，供 Luban 导出流水线统一消费。
    /// </summary>
    public interface IDataTableSettings
    {
#if UNITY_EDITOR
        /// <summary>
        /// 数据源目录路径（仅编辑器使用）。
        /// </summary>
        string SourceDirPath { get; }
#endif

        /// <summary>
        /// 所有单元设置列表。
        /// </summary>
        IReadOnlyList<IDataTableUnitSetting> Units { get; }
    }
}
