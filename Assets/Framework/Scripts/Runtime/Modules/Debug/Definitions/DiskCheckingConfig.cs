/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DiskCheckingConfig.cs
 * author:    taoye
 * created:   2025/3/17
 * descrip:   磁盘检测配置
 ***************************************************************/

using System;
using System.Collections.Generic;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 磁盘检测配置，按平台配置磁盘空间档位和检测间隔。
    /// </summary>
    [Serializable]
    public class DiskCheckingConfig
    {
        /// <summary>
        /// 构造磁盘检测配置。
        /// </summary>
        /// <param name="platformName">平台名称。</param>
        public DiskCheckingConfig(string platformName)
        {
            PlatformName = platformName;
            Enabled = false;
            AvailableSpaces = new List<int>();
            AvailableSpacesIntervals = new List<float>();
        }

        /// <summary>
        /// 是否开启磁盘检测。
        /// </summary>
        public bool Enabled;

        /// <summary>
        /// 平台名称。
        /// </summary>
        public string PlatformName;

        /// <summary>
        /// 磁盘检测剩余空间的不同档位集合。
        /// </summary>
        public List<int> AvailableSpaces;

        /// <summary>
        /// 磁盘检测剩余空间的不同档位的时间间隔集合。
        /// </summary>
        public List<float> AvailableSpacesIntervals;
    }
}
