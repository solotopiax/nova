/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  HttpSettings.cs
 * author:    taoye
 * created:   2026/3/11
 * descrip:   HTTP 管理器配置
 ***************************************************************/

using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// HTTP 管理器配置，包含 UWR 埋点、业务主备路由与请求超时参数。
    /// </summary>
    [Serializable]
    public class HttpSettings
    {
        /// <summary>
        /// 是否将 UnityWebRequest 网络链路事件转发到 Nova 通用埋点插件；仅控制上报，不影响请求执行。
        /// </summary>
        [FormerlySerializedAs("EnableUwrTelemetry")]
        public bool EnableUWRTracks = true;

        /// <summary>
        /// HostKey + NetCmd 业务请求建立新计划时，是否优先使用当前进程内同 HostKey 最近成功的域名；不会删除其他候选。
        /// </summary>
        public bool PreferLastSuccessfulHost = true;

        /// <summary>
        /// HostKey + NetCmd 业务请求的主备候选轮数，最小为 1。
        /// </summary>
        [Min(1)]
        [FormerlySerializedAs("BusinessRequestRoundCount")]
        public int BusinessFallbackRoundCount = 1;

        /// <summary>
        /// HostKey + NetCmd 业务请求重试次数；首次执行不计入该值，每次重试重新执行全部主备轮次。
        /// </summary>
        [Min(0)]
        public int RetryRequestCount = 1;

        /// <summary>
        /// 兼容旧代码读取 UWR 埋点开关；新代码使用 EnableUWRTracks。
        /// </summary>
        [Obsolete("Use EnableUWRTracks instead.")]
        public bool EnableUwrTelemetry
        {
            get => EnableUWRTracks;
            set => EnableUWRTracks = value;
        }

#if NOVA_LEGACY_BESTHTTP_MIGRATION
        /// <summary>
        /// 仅供已下架的 BestHTTP adapter 在自动卸载前完成一次编译。
        /// 实际值映射到 UWR 埋点开关，不会启用 BestHTTP。
        /// </summary>
        public bool EnableBestHttpTelemetry
        {
            get => EnableUWRTracks;
            set => EnableUWRTracks = value;
        }
#endif

        /// <summary>
        /// 兼容旧代码读写业务候选轮数；新代码使用 BusinessFallbackRoundCount。
        /// </summary>
        [Obsolete("Use BusinessFallbackRoundCount instead.")]
        public int BusinessRequestRoundCount
        {
            get => BusinessFallbackRoundCount;
            set => BusinessFallbackRoundCount = value;
        }

        /// <summary>
        /// HTTP 每次物理请求的超时时间（秒）；不是整条主备链的总超时。
        /// </summary>
        public float RequestTimeout = 60f;
    }
}
