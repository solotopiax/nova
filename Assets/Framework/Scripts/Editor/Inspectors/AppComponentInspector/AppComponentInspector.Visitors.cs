/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AppComponentInspector.Visitors.cs
 * author:    taoye
 * created:   2026/5/16
 * descrip:   App 组件编辑器面板定制 —— 属性与字段
 ***************************************************************/

using System.Collections.Generic;
using UnityEditor;

namespace NovaFramework.Editor
{
    internal sealed partial class AppComponentInspector : BaseComponentInspector
    {
        /// <summary>
        /// App 大版本检查 JSON 模板文件名。
        /// </summary>
        private const string c_AppDownloadRulesTemplateFileName = "AppDownloadRulesTemplate.json";

        /// <summary>
        /// 当前 IAppManager 实现类型全名。
        /// </summary>
        private SerializedProperty m_CurManagerTypeName;

        /// <summary>
        /// App 更新功能总开关。
        /// </summary>
        private SerializedProperty m_EnableAppUpdate;

        /// <summary>
        /// Debug 主版本检查地址。
        /// </summary>
        private SerializedProperty m_AppDownloadCheckUrlDebug;

        /// <summary>
        /// Debug 备用版本检查地址。
        /// </summary>
        private SerializedProperty m_AppDownloadCheckUrlFallbackDebug;

        /// <summary>
        /// Release 主版本检查地址。
        /// </summary>
        private SerializedProperty m_AppDownloadCheckUrlRelease;

        /// <summary>
        /// Release 备用版本检查地址。
        /// </summary>
        private SerializedProperty m_AppDownloadCheckUrlFallbackRelease;

        /// <summary>
        /// 版本检查超时秒数。
        /// </summary>
        private SerializedProperty m_TimeoutSeconds;

        /// <summary>
        /// 版本检查主备候选完整执行轮数。
        /// </summary>
        private SerializedProperty m_VersionCheckFallbackRoundCount;

        /// <summary>
        /// 请求重试次数；每次重试重新执行全部主备轮次。
        /// </summary>
        private SerializedProperty m_RetryRequestCount;

        /// <summary>
        /// 是否优先使用最近一次成功域名。
        /// </summary>
        private SerializedProperty m_PreferLastSuccessfulHost;

        /// <summary>
        /// 是否启用 App 版本检查 UWR 链路埋点。
        /// </summary>
        private SerializedProperty m_EnableUWRTracks;

        /// <summary>
        /// 大版本更新路由方式（跳转商店 / 内部下载 APK）。
        /// </summary>
        private SerializedProperty m_DownloadRoute;

        /// <summary>
        /// Android 商店跳转 URL（Google Play / 国内商店落地页）。
        /// </summary>
        private SerializedProperty m_AndroidStoreUrl;

        /// <summary>
        /// iOS App Store 跳转 URL。
        /// </summary>
        private SerializedProperty m_AppStoreUrl;

        /// <summary>
        /// 主下载地址（用于 APK 下载）。
        /// </summary>
        private SerializedProperty m_PrimaryDownloadUrl;

        /// <summary>
        /// 备用下载地址（用于 APK 下载）。
        /// </summary>
        private SerializedProperty m_FallbackDownloadUrl;

        /// <summary>
        /// 是否启用推荐更新规则。
        /// </summary>
        private SerializedProperty m_UseRecommendedDownloadRule;

        /// <summary>
        /// 是否启用强制更新规则。
        /// </summary>
        private SerializedProperty m_UseForcedDownloadRule;

        /// <summary>
        /// IAppManager 所有实现类型名称列表。
        /// </summary>
        private List<string> m_AppManagerTypeNames;
    }
}
