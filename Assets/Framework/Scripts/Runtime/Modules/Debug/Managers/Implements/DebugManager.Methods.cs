/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DebugManager.Methods.cs
 * author:    taoye
 * created:   2026/5/18
 * descrip:   调试 Manager —— 私有方法（平台选取、磁盘检测循环、磁盘空间查询）。
 ***************************************************************/

using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
#if NOVA_SIMPLEDISKUTILS
using SimpleDiskUtils;
#endif

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 调试 Manager 私有方法集合。
    /// </summary>
    internal sealed partial class DebugManager : DebugManagerBase
    {
        /// <summary>
        /// 按 Application.platform 选取当前平台对应的 DiskCheckingConfig。
        /// 与 Solar 老实现一致：Editor 取 [0]，Android 取 [1]，iOS 取 [2]。
        /// </summary>
        /// <returns>命中的 DiskCheckingConfig；无配置或平台不支持时返回 null。</returns>
        private DiskCheckingConfig SelectCurrentConfig()
        {
            if (m_DiskCheckingConfigs == null || m_DiskCheckingConfigs.Count < 3)
            {
                return null;
            }

            switch (Application.platform)
            {
                case RuntimePlatform.WindowsEditor:
                case RuntimePlatform.OSXEditor:
                    return m_DiskCheckingConfigs[0];
                case RuntimePlatform.Android:
                    return m_DiskCheckingConfigs[1];
                case RuntimePlatform.IPhonePlayer:
                    return m_DiskCheckingConfigs[2];
                default:
                    return null;
            }
        }

        /// <summary>
        /// 磁盘检测主循环：周期检测可用空间，按 AvailableSpaces 命中档位后 Log + Fire DiskCheckEventData，
        /// 间隔由命中档位对应的 AvailableSpacesIntervals 决定。CancellationToken 取消时退出。
        /// </summary>
        /// <param name="token">取消令牌（Shutdown 时触发）。</param>
        private async UniTaskVoid RunDiskCheckLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                int availableSpace = CheckDiskAvailableSpace();
                int curIndex = m_CurDiskCheckingConfig.AvailableSpaces.Count - 1;
                for (int i = 0; i < m_CurDiskCheckingConfig.AvailableSpaces.Count; i++)
                {
                    if (availableSpace <= m_CurDiskCheckingConfig.AvailableSpaces[i])
                    {
                        curIndex = i;
                        break;
                    }
                }

                Log.Debug(LogTag.Debug,
                    $"匹配控制节点[{curIndex + 1}]，当前磁盘大小：{availableSpace} MB <= " +
                    $"{m_CurDiskCheckingConfig.AvailableSpaces[curIndex]} MB，下一次磁盘检测时间间隔为：" +
                    $"{m_CurDiskCheckingConfig.AvailableSpacesIntervals[curIndex]}s");

                m_EventManager.Fire(this,DiskCheckEventData.Create(availableSpace, m_CurDiskCheckingConfig.AvailableSpaces[curIndex]));

                float interval = m_CurDiskCheckingConfig.AvailableSpacesIntervals[curIndex];
                try
                {
                    await UniTask.Delay(TimeSpan.FromSeconds(interval), cancellationToken: token);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }

        /// <summary>
        /// 检查并返回当前可用磁盘空间大小（单位 MB）。包未安装或不支持平台返回 -1。
        /// </summary>
        /// <returns>可用磁盘空间（MB）。</returns>
        private static int CheckDiskAvailableSpace()
        {
#if NOVA_SIMPLEDISKUTILS
#if UNITY_WEBGL
            return -1;
#elif UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
            return DiskUtils.CheckAvailableSpace();
#elif UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            return DiskUtils.CheckAvailableSpace(Application.persistentDataPath.Substring(
                0, Application.persistentDataPath.IndexOf('/') + 1));
#elif UNITY_ANDROID
            return DiskUtils.CheckAvailableSpace(true);
#else
            return -1;
#endif
#else
            return -1;
#endif
        }

        /// <summary>
        /// 检查并返回当前已占用磁盘空间大小（单位 MB）。保留与 Solar 三件套对齐，主循环未消费，留给后续模式扩展。
        /// </summary>
        /// <returns>已占用磁盘空间（MB）。</returns>
        private static int CheckDiskBusySpace()
        {
#if NOVA_SIMPLEDISKUTILS
#if UNITY_WEBGL
            return -1;
#elif UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
            return DiskUtils.CheckBusySpace();
#elif UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            return DiskUtils.CheckBusySpace(Application.persistentDataPath.Substring(
                0, Application.persistentDataPath.IndexOf('/') + 1));
#elif UNITY_ANDROID
            return DiskUtils.CheckBusySpace(true);
#else
            return -1;
#endif
#else
            return -1;
#endif
        }

        /// <summary>
        /// 检查并返回总磁盘空间大小（单位 MB），用于 Initialize 阶段写入 AvailableSpaces[Count-1] 兜底档。
        /// </summary>
        /// <returns>总磁盘空间（MB）。</returns>
        private static int CheckDiskTotalSpace()
        {
#if NOVA_SIMPLEDISKUTILS
#if UNITY_WEBGL
            return -1;
#elif UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
            return DiskUtils.CheckTotalSpace();
#elif UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            return DiskUtils.CheckTotalSpace(Application.persistentDataPath.Substring(
                0, Application.persistentDataPath.IndexOf('/') + 1));
#elif UNITY_ANDROID
            return DiskUtils.CheckTotalSpace(true);
#else
            return -1;
#endif
#else
            return -1;
#endif
        }
    }
}
