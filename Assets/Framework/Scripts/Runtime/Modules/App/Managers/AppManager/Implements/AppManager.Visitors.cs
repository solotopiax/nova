/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AppManager.Visitors.cs
 * author:    taoye
 * created:   2026/5/19
 * descrip:   App 管理器 —— 属性与字段
 ***************************************************************/

namespace NovaFramework.Runtime
{
    /// <summary>
    /// App 管理器。
    /// </summary>
    internal sealed partial class AppManager : AppManagerBase
    {
        /// <summary>
        /// 上次主动放弃推荐更新的 UTC Unix 秒时间戳键。
        /// 该数据必须在 Persist 初始化前可读，因此直接使用启动期 PlatformPlayerPrefs 访问器。
        /// </summary>
        private const string c_RecommendedDownloadDismissedAtKey =
            "Nova.App.RecommendedDownloadDismissedAtUnixSeconds";

        /// <summary>
        /// App 版本检查在当前进程内复用最近成功域名时使用的隔离键。
        /// 该偏好只属于 App 版本规则请求，不能与业务协议或资源下载共用。
        /// </summary>
        private const string c_VersionCheckFallbackScopeKey = "app.version_check";

        /// <summary>
        /// HTTP 管理器，提供版本检查接口调用与 APK 文件下载能力。
        /// </summary>
        private IHttpManager m_HttpManager;

        /// <summary>
        /// App 版本检查最近成功域名的进程内存储。
        /// 整链失败不会清除该偏好，只有新成功、配置不匹配或 Manager 关闭才会改变它。
        /// </summary>
        private readonly HttpFallbackPreferenceStore m_VersionCheckFallbackPreferences =
            new HttpFallbackPreferenceStore();

        /// <summary>
        /// 初始化时注入的配置。
        /// </summary>
        private AppManagerConfig m_Config;

        /// <summary>
        /// 本次检查命中的更新规则。
        /// </summary>
        private AppDownloadRule m_MatchedRule;

        /// <summary>
        /// 强更场景需要跳转的商店地址（仅用于商店跳转）。
        /// </summary>
        private string m_TargetStoreUrl;

        /// <summary>
        /// 强更场景使用的 APK 主下载地址（仅用于 APK 下载）。
        /// </summary>
        private string m_TargetDownloadUrl;

    }
}
