/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AppManagerConfig.cs
 * author:    taoye
 * created:   2026/5/14
 * descrip:   AppManager 配置
 ***************************************************************/

namespace NovaFramework.Runtime
{
    /// <summary>
    /// AppManager 配置。
    /// </summary>
    public sealed class AppManagerConfig
    {
        /// <summary>
        /// App 更新功能总开关；默认关闭，需由调用方显式开启。
        /// </summary>
        public bool EnableAppUpdate;

        /// <summary>
        /// CDN 版本检查 JSON 地址。
        /// </summary>
        public string AppDownloadCheckUrl;

        /// <summary>
        /// CDN 版本检查 JSON 备用地址。
        /// </summary>
        public string AppDownloadCheckUrlFallback;

        /// <summary>
        /// 版本检查每次物理请求的超时秒数，默认 5；不是整条主备链的总超时。
        /// </summary>
        public int TimeoutSeconds = 5;

        /// <summary>
        /// 版本检查主备候选的完整执行轮数，默认 1。
        /// 一轮会依次尝试当前有效且去重后的全部候选地址。
        /// </summary>
        public int VersionCheckFallbackRoundCount = 1;

        /// <summary>
        /// 请求重试次数，默认 1；首次执行不计入该值，每次重试重新执行全部主备轮次。
        /// 去重候选数为 C、轮数为 R、重试次数为 K 时，最大物理请求数为 C × R × (K + 1)。
        /// </summary>
        public int RetryRequestCount = 1;

        /// <summary>
        /// 是否在新版本检查链建立计划时，优先使用当前进程内最近一次取得有效版本规则的域名；不会删除其他候选。
        /// </summary>
        public bool PreferLastSuccessfulHost = true;

        /// <summary>
        /// 是否启用 App 版本检查 UnityWebRequest 链路埋点；仅控制上报，不影响请求执行。
        /// </summary>
        public bool EnableUWRTracks = true;

        /// <summary>
        /// 大版本更新路由方式（跳转商店 / 内部下载 APK）。
        /// </summary>
        public AppDownloadRoute DownloadRoute;

        /// <summary>
        /// 主下载地址（用于 APK 下载）。
        /// </summary>
        public string PrimaryDownloadUrl;

        /// <summary>
        /// 备用下载地址（用于 APK 下载）。
        /// </summary>
        public string FallbackDownloadUrl;

        /// <summary>
        /// Android 商店地址（DownloadRoute = Store 时跳转 Google Play / Android 商店）。
        /// </summary>
        public string AndroidStoreUrl;

        /// <summary>
        /// App Store 地址（DownloadRoute = Store 时跳转 iOS App Store）。
        /// </summary>
        public string AppStoreUrl;

        /// <summary>
        /// 是否启用推荐更新规则（本地版本号小于 CDN 配置的推荐更新版本号时触发）。
        /// </summary>
        public bool UseRecommendedDownloadRule;

        /// <summary>
        /// 是否启用强制更新规则（本地版本号小于 CDN 配置的强制更新版本号时触发）。
        /// </summary>
        public bool UseForcedDownloadRule;
    }
}
