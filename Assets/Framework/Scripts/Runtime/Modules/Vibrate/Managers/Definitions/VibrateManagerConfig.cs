/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  VibrateManagerConfig.cs
 * author:    taoye
 * created:   2026/4/9
 * descrip:   振动管理器配置
 ***************************************************************/

using System.Collections.Generic;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 振动管理器配置。
    /// </summary>
    public class VibrateManagerConfig
    {
        /// <summary>
        /// Emphasis 振动单元设置列表。
        /// </summary>
        public List<VibrateUnitSetting> EmphasisUnitsSettings;

        /// <summary>
        /// Custom 振动单元设置列表。
        /// </summary>
        public List<VibrateUnitSetting> CustomUnitsSettings;
    }
}
