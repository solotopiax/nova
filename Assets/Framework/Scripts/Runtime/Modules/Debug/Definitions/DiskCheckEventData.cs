/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DiskCheckEventData.cs
 * author:    taoye
 * created:   2026/5/18
 * descrip:   磁盘检测事件数据。
 ***************************************************************/

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 磁盘检测事件数据，由 DebugManager 在磁盘检测主循环每次命中档位后发布。
    /// </summary>
    public sealed class DiskCheckEventData : EventData
    {
        /// <summary>
        /// 当前可用磁盘空间（MB）。
        /// </summary>
        public int AvailableSpace { get; private set; }

        /// <summary>
        /// 命中的空间档位阈值（MB）。
        /// </summary>
        public int AvailableSpaceLevel { get; private set; }

        /// <summary>
        /// 从引用池获取并填充事件数据。
        /// </summary>
        /// <param name="availableSpace">当前可用磁盘空间（MB）。</param>
        /// <param name="availableSpaceLevel">命中的空间档位阈值（MB）。</param>
        /// <returns>填充后的事件数据。</returns>
        public static DiskCheckEventData Create(int availableSpace, int availableSpaceLevel)
        {
            DiskCheckEventData e = ReferencePool.Get<DiskCheckEventData>();
            e.AvailableSpace = availableSpace;
            e.AvailableSpaceLevel = availableSpaceLevel;
            return e;
        }

        /// <summary>
        /// 归还引用池时清理字段。
        /// </summary>
        public override void Clear()
        {
            AvailableSpace = 0;
            AvailableSpaceLevel = 0;
        }
    }
}
