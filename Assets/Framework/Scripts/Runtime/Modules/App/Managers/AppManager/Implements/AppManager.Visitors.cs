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
        /// HTTP 管理器，提供版本检查接口调用与 APK 文件下载能力。
        /// </summary>
        private IHttpManager m_HttpManager;

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
