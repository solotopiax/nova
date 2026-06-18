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
        /// CDN 版本检查 JSON 地址。
        /// </summary>
        public string AppDownloadCheckUrl;

        /// <summary>
        /// CDN 版本检查 JSON 备用地址。
        /// </summary>
        public string AppDownloadCheckUrlFallback;

        /// <summary>
        /// 版本检查超时秒数，默认 5。
        /// </summary>
        public int TimeoutSeconds = 5;

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
