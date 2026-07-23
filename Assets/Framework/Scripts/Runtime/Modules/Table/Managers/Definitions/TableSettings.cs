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
    public class TableUnitSetting : DataTableUnitSettingBase
    {
        /// <summary>
        /// 表格模式（列表 / 映射 / 单例），编辑器配置、运行时读取。
        /// </summary>
        [FormerlySerializedAs("ExportMode")]
        public DataTableMode TableMode = DataTableMode.List;
        /// <inheritdoc />
        protected override DataTableMode GetMode() => TableMode;

        /// <summary>
        /// 映射模式的索引字段名（仅 Map 模式使用，默认 "ID"）。
        /// </summary>
        public string IndexField = "ID";
        /// <inheritdoc />
        protected override string GetIndexField() => IndexField;
    }

}
