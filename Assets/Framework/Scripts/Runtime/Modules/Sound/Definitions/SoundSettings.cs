/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  SoundSettings.cs
 * author:    taoye
 * created:   2026/4/9
 * descrip:   声音设置
 ***************************************************************/

using System;
using System.Collections.Generic;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 声音设置。
    /// </summary>
    [Serializable]
    public class SoundSettings : IDataTableSettings
    {
#if UNITY_EDITOR
        /// <summary>
        /// 数据源目录路径（仅编辑器使用）。
        /// </summary>
        public string SourceDirPath;
        /// <inheritdoc />
        string IDataTableSettings.SourceDirPath => SourceDirPath;

        /// <summary>
        /// 模板文件路径（仅编辑器使用）。
        /// </summary>
        public string TemplatePath;
#endif

        /// <summary>
        /// 按声音单元的设置列表（每个单元独立指定导出路径和运行时 Asset 地址）。
        /// </summary>
        public List<SoundUnitSetting> SoundUnitsSettings = new List<SoundUnitSetting>();
        /// <inheritdoc />
        IReadOnlyList<IDataTableUnitSetting> IDataTableSettings.Units => SoundUnitsSettings;
    }

    /// <summary>
    /// 单个声音数据源的单元设置（导出数据位置、导出类型定义位置和运行时 Asset 地址）。
    /// 固定使用 Map 模式，索引字段为 "Name"。
    /// </summary>
    [Serializable]
    public class SoundUnitSetting : DataTableUnitSettingBase
    {
        /// <inheritdoc />
        protected override DataTableMode GetMode() => DataTableMode.Map;

        /// <inheritdoc />
        protected override string GetIndexField() => "Name";
    }
}
