/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  VibrateUnitSetting.cs
 * author:    taoye
 * created:   2026/4/23
 * descrip:   振动单元设置
 ***************************************************************/

using System;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 振动单元设置，每个数据源文件对应一个单元。
    /// 固定使用 List 模式，不使用索引字段。
    /// </summary>
    [Serializable]
    public class VibrateUnitSetting : DataTableUnitSettingBase
    {
        /// <inheritdoc />
        protected override DataTableMode GetMode() => DataTableMode.List;

        /// <inheritdoc />
        protected override string GetIndexField() => "";
    }
}
