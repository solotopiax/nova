/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AppComponent.Visitors.cs
 * author:    taoye
 * created:   2026/5/19
 * descrip:   App 组件 —— 属性与字段
 ***************************************************************/

using UnityEngine;
namespace NovaFramework.Runtime
{
    /// <summary>
    /// App 组件。
    /// </summary>
    public sealed partial class AppComponent : FrameworkComponent
    {
        /// <summary>
        /// 当前 IAppManager 实现类型全名（Inspector 下拉选择）。
        /// </summary>
        [Tooltip("AppManager 实现类全名")]
        [SerializeField]
        private string m_CurManagerTypeName = "NovaFramework.Runtime.AppManager";
        public string CurManagerTypeName => m_CurManagerTypeName;

        /// <summary>
        /// Debug 开发模式下的主版本检查地址。
        /// </summary>
        [SerializeField]
        private string m_AppDownloadCheckUrlDebug;
        public string AppDownloadCheckUrl => ResolvePrimaryCheckUrl();

        /// <summary>
        /// Debug 开发模式下的备用版本检查地址。
        /// </summary>
        [SerializeField]
        private string m_AppDownloadCheckUrlFallbackDebug;
        public string AppDownloadCheckUrlFallback => ResolveFallbackCheckUrl();

        /// <summary>
        /// Release 开发模式下的主版本检查地址。
        /// </summary>
        [SerializeField]
        private string m_AppDownloadCheckUrlRelease;

        /// <summary>
        /// Release 开发模式下的备用版本检查地址。
        /// </summary>
        [SerializeField]
        private string m_AppDownloadCheckUrlFallbackRelease;

        /// <summary>
        /// 版本检查超时秒数，默认 5。
        /// </summary>
        [SerializeField]
        private int m_TimeoutSeconds = 5;
        public int TimeoutSeconds => m_TimeoutSeconds;

        /// <summary>
        /// 大版本更新路由方式（跳转商店 / 内部下载 APK）。
        /// </summary>
        [SerializeField]
        private AppDownloadRoute m_DownloadRoute;
        public AppDownloadRoute DownloadRoute => m_DownloadRoute;

        /// <summary>
        /// Android 商店地址（DownloadRoute = Store 时跳转 Google Play / Android 商店）。
        /// </summary>
        [SerializeField]
        private string m_AndroidStoreUrl;
        public string AndroidStoreUrl => m_AndroidStoreUrl;

        /// <summary>
        /// App Store 地址（DownloadRoute = Store 时跳转 iOS App Store）。
        /// </summary>
        [SerializeField]
        private string m_AppStoreUrl;
        public string AppStoreUrl => m_AppStoreUrl;

        /// <summary>
        /// 主下载地址（用于 APK 下载）。
        /// </summary>
        [SerializeField]
        private string m_PrimaryDownloadUrl;
        public string PrimaryDownloadUrl => m_PrimaryDownloadUrl;

        /// <summary>
        /// 备用下载地址（用于 APK 下载）。
        /// </summary>
        [SerializeField]
        private string m_FallbackDownloadUrl;
        public string FallbackDownloadUrl => m_FallbackDownloadUrl;

        /// <summary>
        /// 是否启用推荐更新规则（本地版本号小于 CDN 配置的推荐更新版本号时触发）。
        /// </summary>
        [SerializeField]
        private bool m_UseRecommendedDownloadRule;
        public bool UseRecommendedDownloadRule => m_UseRecommendedDownloadRule;

        /// <summary>
        /// 是否启用强制更新规则（本地版本号小于 CDN 配置的强制更新版本号时触发）。
        /// </summary>
        [SerializeField]
        private bool m_UseForcedDownloadRule;
        public bool UseForcedDownloadRule => m_UseForcedDownloadRule;

        /// <summary>
        /// AppManager 实例，由 Awake 通过 Util.TypeCreator 反射创建。
        /// </summary>
        private IAppManager m_AppManager;

        /// <summary>
        /// 命中的更新规则。
        /// </summary>
        public AppDownloadRule MatchedRule => m_AppManager.MatchedRule;

        /// <summary>
        /// 强更场景需要跳转的商店地址（按平台从 AppStoreUrl / AndroidStoreUrl 解析）。
        /// </summary>
        public string TargetStoreUrl => m_AppManager.TargetStoreUrl;

        /// <summary>
        /// 强更场景使用的 APK 主下载地址（仅在 DownloadRoute=Apk 时写入）。
        /// </summary>
        public string TargetDownloadUrl => m_AppManager.TargetDownloadUrl;
    }
}
