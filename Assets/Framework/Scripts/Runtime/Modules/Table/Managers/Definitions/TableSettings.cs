/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  TableSettings.cs
 * author:    taoye
 * created:   2026/2/5
 * descrip:   表格设置
 ***************************************************************/

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 表格设置。
    /// </summary>
    [Serializable]
    public class TableSettings : IDataTableSettings
    {
#if UNITY_EDITOR
        /// <summary>
        /// 数据源目录路径（仅编辑器使用）。
        /// </summary>
        [FormerlySerializedAs("ExcelDirPath")]
        public string SourceDirPath;
        /// <inheritdoc />
        string IDataTableSettings.SourceDirPath => SourceDirPath;
#endif

        /// <summary>
        /// 按 Table 的单元设置（每个 Table 单独指定数据与类型导出位置和 Asset 地址）。
        /// </summary>
        public List<TableUnitSetting> TableUnitsSettings = new List<TableUnitSetting>();
        /// <inheritdoc />
        IReadOnlyList<IDataTableUnitSetting> IDataTableSettings.Units => TableUnitsSettings;
    }
    
    /// <summary>
    /// 单个数据源的单元设置（导出数据位置、导出类型定义位置和 Asset 地址）。
    /// </summary>
    [Serializable]
    public class TableUnitSetting : IDataTableUnitSetting
    {
#if UNITY_EDITOR
        /// <summary>
        /// 相对表格目录的数据源文件相对路径，用于唯一标识该文件。
        /// </summary>
        [FormerlySerializedAs("ExcelPath")]
        public string SourcePath;
        /// <inheritdoc />
        string IDataTableUnitSetting.SourcePath => SourcePath;

        /// <summary>
        /// 该数据源的导出数据位置。
        /// </summary>
        public string DatasExportPath;
        /// <inheritdoc />
        string IDataTableUnitSetting.DatasExportPath => DatasExportPath;

        /// <summary>
        /// 该数据源的导出类型定义位置。
        /// </summary>
        public string ClassesExportPath;
        /// <inheritdoc />
        string IDataTableUnitSetting.ClassesExportPath => ClassesExportPath;

        /// <inheritdoc />
        string IDataTableUnitSetting.LubanInputPath => SourcePath;
#endif

        /// <summary>
        /// 表格模式（列表 / 映射 / 单例），编辑器配置、运行时读取。
        /// </summary>
        [FormerlySerializedAs("ExportMode")]
        public DataTableMode TableMode = DataTableMode.List;
        /// <inheritdoc />
        DataTableMode IDataTableUnitSetting.Mode => TableMode;

        /// <summary>
        /// 映射模式的索引字段名（仅 Map 模式使用，默认 "ID"）。
        /// </summary>
        public string IndexField = "ID";
        /// <inheritdoc />
        string IDataTableUnitSetting.IndexField => IndexField;

        /// <summary>
        /// 表格资源的地址。
        /// </summary>
        public string AssetLocation;
        /// <inheritdoc />
        string IDataTableUnitSetting.AssetLocation => AssetLocation;

        /// <summary>
        /// 表格数据类型短名称列表（不含命名空间，如 "TestAAA"），一个 JSON 可包含多个类型。
        /// </summary>
        public List<string> DataTypeNames = new List<string>();
        /// <inheritdoc />
        IReadOnlyList<string> IDataTableUnitSetting.DataTypeNames => DataTypeNames;
    }

}
