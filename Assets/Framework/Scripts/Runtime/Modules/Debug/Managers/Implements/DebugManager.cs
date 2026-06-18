/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DebugManager.cs
 * author:    taoye
 * created:   2026/5/9
 * descrip:   调试 Manager 实现。
 ***************************************************************/

using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 调试 Manager 实现，持有磁盘监控等业务逻辑。
    /// </summary>
    internal sealed partial class DebugManager : DebugManagerBase
    {
        /// <summary>
        /// 初始化 DebugManager：缓存配置、按平台命中档位、写兜底总空间、按 Enabled 启动磁盘检测循环。
        /// </summary>
        /// <param name="config">调试模块配置。</param>
        public override void Initialize(DebugManagerConfig config)
        {
            if (config == null)
            {
                Log.Fatal(LogTag.Debug, "DebugManagerConfig 无效。");
                return;
            }

            m_EventManager = FrameworkManagersGroup.GetManager<IEventManager>();
            if (m_EventManager == null)
            {
                throw new InvalidOperationException("DebugManager 初始化失败：IEventManager 无效。");
            }

            m_DiskCheckingConfigs = config.DiskCheckingConfigs;
            m_CurDiskCheckingConfig = SelectCurrentConfig();
            if (m_CurDiskCheckingConfig == null)
            {
                return;
            }

            if (m_CurDiskCheckingConfig.AvailableSpaces == null
                || m_CurDiskCheckingConfig.AvailableSpaces.Count == 0)
            {
                Log.Warning(LogTag.Debug, "当前平台磁盘检测档位为空，跳过磁盘检测。");
                return;
            }

            if (m_CurDiskCheckingConfig.AvailableSpacesIntervals == null
                || m_CurDiskCheckingConfig.AvailableSpacesIntervals.Count
                   != m_CurDiskCheckingConfig.AvailableSpaces.Count)
            {
                Log.Warning(LogTag.Debug, "AvailableSpacesIntervals 与 AvailableSpaces 长度不一致，跳过磁盘检测。");
                return;
            }

            int lastIndex = m_CurDiskCheckingConfig.AvailableSpaces.Count - 1;
            m_CurDiskCheckingConfig.AvailableSpaces[lastIndex] = CheckDiskTotalSpace();

            if (m_CurDiskCheckingConfig.Enabled)
            {
                m_DiskCheckCts?.Cancel();
                m_DiskCheckCts?.Dispose();
                m_DiskCheckCts = new CancellationTokenSource();
                RunDiskCheckLoopAsync(m_DiskCheckCts.Token).Forget();
            }
        }

        /// <summary>
        /// 每帧更新，磁盘检测改由 UniTask 循环承载，本方法保留空实现。
        /// </summary>
        public override void Update()
        {
        }

        /// <summary>
        /// 关闭 DebugManager：取消磁盘检测循环，清空字段。
        /// </summary>
        public override void Shutdown()
        {
            m_DiskCheckCts?.Cancel();
            m_DiskCheckCts?.Dispose();
            m_DiskCheckCts = null;
            m_CurDiskCheckingConfig = null;
            m_DiskCheckingConfigs = null;
            m_EventManager = null;
        }
    }
}
