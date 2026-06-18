/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  UISettings.cs
 * author:    taoye
 * created:   2026/03/05
 * descrip:   UI 配置设置
 ***************************************************************/

using System;
using System.Collections.Generic;
using UnityEngine.Serialization;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// UI 配置设置，实现 IDataTableSettings 接口，供 Luban 导出流水线统一消费。
    /// </summary>
    [Serializable]
    public class UISettings : IDataTableSettings
    {
#if UNITY_EDITOR
        /// <summary>
        /// 数据源目录路径（仅编辑器使用）。
        /// </summary>
        [FormerlySerializedAs("ExcelDirPath")]
        public string SourceDirPath;
        /// <inheritdoc />
        string IDataTableSettings.SourceDirPath => SourceDirPath;

        /// <summary>
        /// 模板文件路径（仅编辑器使用）。
        /// </summary>
        public string TemplatePath;
#endif

        /// <summary>
        /// 按 UIUnit 的单元设置（每个 UIUnit 单独指定数据与类型导出位置和 AB 路径、资源名称）。
        /// </summary>
        public List<UIUnitSetting> UIUnitsSettings = new List<UIUnitSetting>();
        /// <inheritdoc />
        IReadOnlyList<IDataTableUnitSetting> IDataTableSettings.Units => UIUnitsSettings;
    }

    /// <summary>
    /// 单个 UI 数据源的单元设置（导出数据位置、导出类型定义位置和 AB 路径、资源名称）。
    /// 固定使用 Map 模式，以 Name 为索引字段。
    /// </summary>
    [Serializable]
    public class UIUnitSetting : DataTableUnitSettingBase
    {
        /// <inheritdoc />
        protected override DataTableMode GetMode() => DataTableMode.Map;

        /// <inheritdoc />
        protected override string GetIndexField() => "Name";
    }
}
