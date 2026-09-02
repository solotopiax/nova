/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  HttpManagerConfig.cs
 * author:    taoye
 * created:   2026/3/9
 * descrip:   HTTP管理器配置
 ***************************************************************/

namespace NovaFramework.Runtime
{
    /// <summary>
    /// HTTP 管理器配置。
    /// </summary>
    public class HttpManagerConfig
    {
        /// <summary>
        /// 是否将 UnityWebRequest 网络链路事件转发到 Nova 通用埋点插件；仅控制上报，不影响请求执行。
        /// </summary>
        public bool EnableUWRTracks = true;

        /// <summary>
        /// 业务请求建立新计划时，是否优先使用当前进程内同 HostKey 最近成功的域名；不会删除其他候选。
        /// </summary>
        public bool PreferLastSuccessfulHost = true;

        /// <summary>
        /// 业务主备候选的执行轮数，最小为 1。
        /// </summary>
        public int BusinessFallbackRoundCount = 1;

        /// <summary>
        /// 业务请求重试次数；首次执行不计入该值，每次重试重新执行全部主备轮次。
        /// </summary>
        public int RetryRequestCount = 1;

        /// <summary>
        /// 兼容旧初始化代码；新代码使用 EnableUWRTracks。
        /// </summary>
        [System.Obsolete("Use EnableUWRTracks instead.")]
        public bool EnableUwrTelemetry
        {
            get => EnableUWRTracks;
            set => EnableUWRTracks = value;
        }

        /// <summary>
        /// 兼容旧初始化代码；新代码使用 BusinessFallbackRoundCount。
        /// </summary>
        [System.Obsolete("Use BusinessFallbackRoundCount instead.")]
        public int BusinessRequestRoundCount
        {
            get => BusinessFallbackRoundCount;
            set => BusinessFallbackRoundCount = value;
        }

        /// <summary>
        /// 每次物理网络请求的默认超时时间（秒），默认 60 秒；不是整条主备链的总超时。
        /// </summary>
        public float RequestTimeout = 60f;
    }
}
