/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EventManagerConfig.cs
 * author:    taoye
 * created:   2026/1/16
 * descrip:   Event管理器配置
 ***************************************************************/

namespace NovaFramework.Runtime
{
    /// <summary>
    /// Event 管理器配置。
    /// </summary>
    public class EventManagerConfig
    {
        /// <summary>
        /// 事件池模式。
        /// </summary>
        public EventPoolMode PoolMode { get; set; } = EventPoolMode.AllowNoHandler | EventPoolMode.AllowMultiHandler;

        /// <summary>
        /// 每帧最大事件分发数量（0 表示无限制）。
        /// </summary>
        public int MaxDispatchPerFrame { get; set; } = 0;
    }
}
