/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  HttpManager.Visitors.cs
 * author:    taoye
 * created:   2026/3/9
 * descrip:   HTTP管理器 —— 属性与字段
 ***************************************************************/

namespace NovaFramework.Runtime
{
    /// <summary>
    /// HTTP 管理器。
    /// </summary>
    internal sealed partial class HttpManager : HttpManagerBase
    {
        /// <summary>
        /// HTTP 传输实现，固定使用 UnityWebRequest。
        /// </summary>
        private IUwrHttpTransport m_Transport;

        /// <summary>
        /// 默认网络请求超时时间（秒）。
        /// </summary>
        private float m_RequestTimeout = 60f;

        /// <summary>
        /// 是否启用 UWR 网络链路埋点。
        /// </summary>
        private bool m_EnableUWRTracks = true;

        /// <summary>
        /// 业务请求是否优先使用当前进程内最近成功的域名。
        /// </summary>
        private bool m_PreferLastSuccessfulHost = true;

        /// <summary>
        /// 业务主备候选执行轮数，运行时始终钳制为至少一轮。
        /// </summary>
        private int m_BusinessFallbackRoundCount = 1;

        /// <summary>
        /// 业务请求重试次数；每次重试重新执行全部主备轮次。
        /// </summary>
        private int m_RetryRequestCount = 1;

        /// <summary>
        /// 按 HostKey 隔离且具备并发版本保护的最近成功域名存储。
        /// </summary>
        private readonly HttpFallbackPreferenceStore m_BusinessRoutePreferenceStore =
            new HttpFallbackPreferenceStore();

        /// <summary>
        /// 最近一次观察到的 Unity 网络可达性，用于网络环境切换时清理旧域名偏好。
        /// </summary>
        private UnityEngine.NetworkReachability m_LastNetworkReachability;
    }
}
