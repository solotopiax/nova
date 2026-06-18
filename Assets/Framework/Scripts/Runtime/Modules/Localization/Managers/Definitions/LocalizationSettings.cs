/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  LocalizationSettings.cs
 * author:    taoye
 * created:   2026/4/19
 * descrip:   本地化设置
 ***************************************************************/

using System;
using System.Collections.Generic;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 本地化设置，包含文本和字体两组数据源配置。
    /// 文本数据使用 Map 模式（按语言聚合），字体数据使用 List 模式。
    /// </summary>
    [Serializable]
    public class LocalizationSettings : IDataTableSettings
    {
#if UNITY_EDITOR
        /// <summary>
        /// 文本数据源目录路径（仅编辑器使用）。
        /// </summary>
        public string TextSourceDirPath;

        /// <summary>
        /// 字体数据源目录路径（仅编辑器使用）。
        /// </summary>
        public string FontSourceDirPath;

        /// <inheritdoc />
        string IDataTableSettings.SourceDirPath => TextSourceDirPath;
#endif

        /// <inheritdoc />
        IReadOnlyList<IDataTableUnitSetting> IDataTableSettings.Units => TextUnitsSettings;

        /// <summary>
        /// 文本数据单元设置列表（每个 Excel 文件对应一个单元）。
        /// </summary>
        public List<LocalizationTextUnitSetting> TextUnitsSettings = new List<LocalizationTextUnitSetting>();

        /// <summary>
        /// 字体数据单元设置列表（每个 Excel 文件对应一个单元）。
        /// </summary>
        public List<LocalizationFontUnitSetting> FontUnitsSettings = new List<LocalizationFontUnitSetting>();
    }

    /// <summary>
    /// 本地化文本数据单元设置，每个数据源文件对应一个单元。
    /// 固定使用 Map 模式，索引字段为 "Name"。
    /// LubanInputPath 使用 "_temp/" 前缀，不同于默认的 SourcePath 直接返回。
    /// </summary>
    [Serializable]
    public class LocalizationTextUnitSetting : DataTableUnitSettingBase
    {
#if UNITY_EDITOR
        /// <inheritdoc />
        protected override string GetLubanInputPath() => "_temp/" + System.IO.Path.GetFileName(SourcePath);
#endif

        /// <inheritdoc />
        protected override DataTableMode GetMode() => DataTableMode.Map;

        /// <inheritdoc />
        protected override string GetIndexField() => "Name";
    }

    /// <summary>
    /// 本地化字体数据单元设置，每个数据源文件对应一个单元。
    /// 固定使用 List 模式，不使用索引字段。
    /// </summary>
    [Serializable]
    public class LocalizationFontUnitSetting : DataTableUnitSettingBase
    {
        /// <inheritdoc />
        protected override DataTableMode GetMode() => DataTableMode.List;

        /// <inheritdoc />
        protected override string GetIndexField() => string.Empty;
    }
}
