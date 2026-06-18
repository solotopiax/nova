/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DebugManager.Visitors.cs
 * author:    taoye
 * created:   2026/5/9
 * descrip:   调试 Manager —— 字段与属性。
 ***************************************************************/

using System.Collections.Generic;
using System.Threading;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 调试 Manager 实现。
    /// </summary>
    internal sealed partial class DebugManager : DebugManagerBase
    {
        /// <summary>
        /// 磁盘监控配置集合，由 Initialize 从 config 中传入。
        /// </summary>
        private List<DiskCheckingConfig> m_DiskCheckingConfigs;

        /// <summary>
        /// 当前平台命中的 DiskCheckingConfig（Initialize 时按 Application.platform 选定）。
        /// </summary>
        private DiskCheckingConfig m_CurDiskCheckingConfig;

        /// <summary>
        /// 事件管理器引用，用于 Fire DiskCheckEventData。
        /// </summary>
        private IEventManager m_EventManager;

        /// <summary>
        /// 磁盘检测循环取消令牌，Shutdown 时取消并清空。
        /// </summary>
        private CancellationTokenSource m_DiskCheckCts;
    }
}
