/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  NovaBestHttpTelemetrySink.cs
 * author:    taoye
 * created:   2026/8/3
 * descrip:   BestHTTP 遥测到 Nova 通用埋点插件的桥接器
 ***************************************************************/

#if NOVA_BEST_HTTP

using System;
using System.Collections.Generic;

using Best.HTTP.Telemetry;

using NovaFramework.Runtime;

namespace NovaFramework.BestHTTP.Runtime
{
    /// <summary>
    /// 将 Best HTTP 的后端无关遥测事件转发给 Nova 当前可用的全部通用埋点插件。
    /// </summary>
    internal sealed class NovaBestHttpTelemetrySink : IBestHttpTelemetrySink
    {
        internal const int MaxPendingEvents = 128;

        private readonly Func<bool> m_IsEnabled;
        private readonly Func<bool> m_IsReady;
        private readonly Func<IReadOnlyList<ITrackPlugin>> m_GetTrackPlugins;
        private readonly Queue<BestHttpTelemetryEvent> m_PendingEvents = new Queue<BestHttpTelemetryEvent>();
        private readonly object m_Gate = new object();

        /// <summary>
        /// 创建 Nova 遥测接收器，并注入开关、就绪状态和插件查询入口以隔离生命周期依赖。
        /// </summary>
        /// <param name="isEnabled">返回当前 Network 面板开关状态。</param>
        /// <param name="isReady">返回 Nova SDK 是否已初始化。</param>
        /// <param name="getTrackPlugins">返回所有当前可用的通用埋点插件。</param>
        internal NovaBestHttpTelemetrySink(
            Func<bool> isEnabled,
            Func<bool> isReady,
            Func<IReadOnlyList<ITrackPlugin>> getTrackPlugins)
        {
            m_IsEnabled = isEnabled ?? throw new ArgumentNullException(nameof(isEnabled));
            m_IsReady = isReady ?? throw new ArgumentNullException(nameof(isReady));
            m_GetTrackPlugins = getTrackPlugins ?? throw new ArgumentNullException(nameof(getTrackPlugins));
        }

        /// <summary>
        /// 获取尚未派发的启动期事件数量，仅供测试与诊断使用。
        /// </summary>
        internal int PendingCount
        {
            get
            {
                lock (m_Gate)
                    return m_PendingEvents.Count;
            }
        }

        /// <inheritdoc />
        public void Track(BestHttpTelemetryEvent telemetryEvent)
        {
            if (telemetryEvent == null)
                return;

            if (!IsEnabled())
            {
                ClearPending();
                return;
            }

            if (!IsReady())
            {
                EnqueueBounded(telemetryEvent);
                return;
            }

            FlushPendingIfReady();
            Dispatch(telemetryEvent);
        }

        /// <summary>
        /// SDK 就绪后按原始顺序派发启动阶段缓存的事件。
        /// </summary>
        internal void FlushPendingIfReady()
        {
            if (!IsEnabled())
            {
                ClearPending();
                return;
            }
            if (!IsReady())
                return;

            while (true)
            {
                BestHttpTelemetryEvent telemetryEvent;
                lock (m_Gate)
                {
                    if (m_PendingEvents.Count == 0)
                        break;
                    telemetryEvent = m_PendingEvents.Dequeue();
                }
                Dispatch(telemetryEvent);
            }
        }

        /// <summary>
        /// 清空尚未派发的启动期事件；关闭开关或子系统重置时调用。
        /// </summary>
        internal void ClearPending()
        {
            lock (m_Gate)
                m_PendingEvents.Clear();
        }

        /// <summary>
        /// 安全读取埋点开关，读取异常时按关闭处理，避免影响网络请求。
        /// </summary>
        /// <returns>开关开启时返回 true。</returns>
        private bool IsEnabled()
        {
            try
            {
                return m_IsEnabled();
            }
            catch (Exception exception)
            {
                Log.Warning(LogTag.SDK, "读取 BestHTTP 网络埋点开关失败，已跳过本次事件：{0}", exception);
                return false;
            }
        }

        /// <summary>
        /// 安全读取 SDK 就绪状态，读取异常时按未就绪处理。
        /// </summary>
        /// <returns>SDK 已初始化时返回 true。</returns>
        private bool IsReady()
        {
            try
            {
                return m_IsReady();
            }
            catch (Exception exception)
            {
                Log.Warning(LogTag.SDK, "读取 Nova SDK 初始化状态失败，BestHTTP 事件暂存：{0}", exception);
                return false;
            }
        }

        /// <summary>
        /// 将启动期事件放入固定容量队列；满载时淘汰最旧事件以限制内存占用。
        /// </summary>
        /// <param name="telemetryEvent">待缓存的不可变 BestHTTP 事件。</param>
        private void EnqueueBounded(BestHttpTelemetryEvent telemetryEvent)
        {
            lock (m_Gate)
            {
                if (m_PendingEvents.Count >= MaxPendingEvents)
                    m_PendingEvents.Dequeue();
                m_PendingEvents.Enqueue(telemetryEvent);
            }
        }

        /// <summary>
        /// 将单个事件扇出到全部可用埋点插件，并隔离查询与单插件上报异常。
        /// </summary>
        /// <param name="telemetryEvent">待派发的不可变 BestHTTP 事件。</param>
        private void Dispatch(BestHttpTelemetryEvent telemetryEvent)
        {
            IReadOnlyList<ITrackPlugin> trackPlugins;
            try
            {
                trackPlugins = m_GetTrackPlugins();
            }
            catch (Exception exception)
            {
                Log.Warning(LogTag.SDK, "获取 Nova 通用埋点插件失败，已跳过 BestHTTP 事件：{0}", exception);
                return;
            }

            if (trackPlugins == null)
                return;

            for (int i = 0; i < trackPlugins.Count; i++)
            {
                ITrackPlugin trackPlugin = trackPlugins[i];
                if (trackPlugin == null)
                    continue;

                try
                {
                    trackPlugin.TrackEvent(telemetryEvent.Name, CopyProperties(telemetryEvent));
                }
                catch (Exception exception)
                {
                    Log.Warning(LogTag.SDK, "BestHTTP 单个埋点插件上报异常（已隔离）：{0}", exception);
                }
            }
        }

        /// <summary>
        /// 为每个埋点插件复制独立属性字典，避免插件修改污染后续接收方。
        /// </summary>
        /// <param name="telemetryEvent">属性来源事件。</param>
        /// <returns>可由单个插件独立消费的属性字典。</returns>
        private static Dictionary<string, object> CopyProperties(BestHttpTelemetryEvent telemetryEvent)
        {
            var properties = new Dictionary<string, object>(telemetryEvent.Properties.Count);
            foreach (KeyValuePair<string, object> pair in telemetryEvent.Properties)
                properties.Add(pair.Key, pair.Value);
            return properties;
        }
    }
}

#endif
