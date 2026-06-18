/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  IDataTableUnitSetting.cs
 * author:    taoye
 * created:   2026/4/16
 * descrip:   数据表单元设置接口
 ***************************************************************/

using System.Collections.Generic;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 数据表单元设置接口，每个数据源文件对应一个单元。
    /// Table / Config 等模块的 UnitSetting 类实现此接口，供 Luban 导出流水线统一消费。
    /// </summary>
    public interface IDataTableUnitSetting
    {
#if UNITY_EDITOR
        /// <summary>
        /// 相对数据源目录的文件路径。
        /// </summary>
        string SourcePath { get; }

        /// <summary>
        /// 导出数据文件的目标路径。
        /// </summary>
        string DatasExportPath { get; }

        /// <summary>
        /// 导出类型定义文件的目标路径。
        /// </summary>
        string ClassesExportPath { get; }

        /// <summary>
        /// Luban __tables__.xml 中 input 属性的路径值。
        /// Table 模块直接返回 SourcePath，Config 模块返回 "_temp/" + 文件名。
        /// </summary>
        string LubanInputPath { get; }
#endif

        /// <summary>
        /// 资源的 Asset 地址。
        /// </summary>
        string AssetLocation { get; }

        /// <summary>
        /// 数据表模式（List / Map / One）。
        /// </summary>
        DataTableMode Mode { get; }

        /// <summary>
        /// 映射模式的索引字段名（仅 Map 模式使用）。
        /// </summary>
        string IndexField { get; }

        /// <summary>
        /// 数据类型短名称列表（不含命名空间，如 "Hero"），一个 JSON 可包含多个类型。
        /// </summary>
        IReadOnlyList<string> DataTypeNames { get; }
    }
}
