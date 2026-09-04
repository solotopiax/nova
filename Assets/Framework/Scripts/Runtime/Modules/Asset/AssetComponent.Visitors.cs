/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AssetComponent.Visitors.cs
 * author:    taoye
 * created:   2020/12/16
 * descrip:   Asset组件-访问器
 ***************************************************************/
using System.Collections.Generic;
using UnityEngine;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 资源组件。
    /// </summary>
    public sealed partial class AssetComponent : FrameworkComponent
    {
        /// <summary>
        /// 当前 AssetManager类型名称。
        /// </summary>
        [Tooltip("AssetManager的实现类全名")]
        [SerializeField]
        private string m_CurAssetManagerTypeName = "NovaFramework.Runtime.AssetManager";
        public string CurAssetManagerTypeName => m_CurAssetManagerTypeName;

        /// <summary>
        /// 编辑器下资源加载模式。
        /// 仅在 Application.isEditor 时生效；默认 EditorSimulateMode（直接读 Editor 资源，零网络开销）。
        /// </summary>
        [SerializeField]
        private AssetPlayMode m_EditorPlayMode = AssetPlayMode.EditorSimulateMode;

        /// <summary>
        /// 终端下资源加载模式。
        /// 在 Player（非 Editor）时生效；不允许 EditorSimulateMode。默认 HostPlayMode（联机热更模式）。
        /// 与 EnableHotfix 双向联动：EnableHotfix=false ⇔ RuntimePlayMode=OfflinePlayMode；EnableHotfix=true ⇔ RuntimePlayMode=HostPlayMode。
        /// </summary>
        [SerializeField]
        private AssetPlayMode m_RuntimePlayMode = AssetPlayMode.HostPlayMode;

        /// <summary>
        /// 需要 CreatePackage 的包名列表，至少包含一个默认包。
        /// </summary>
        [SerializeField]
        private System.Collections.Generic.List<string> m_Packages = new System.Collections.Generic.List<string> { "Default" };

        /// <summary>
        /// 默认包名；为空时取 m_Packages[0]。
        /// </summary>
        [SerializeField]
        private string m_DefaultPackageName;

        /// <summary>
        /// 是否在场景卸载后自动 CleanupAsync 默认包；默认 false，由业务决定时机。
        /// </summary>
        [SerializeField]
        private bool m_AutoCleanupOnSceneUnload;

        /// <summary>
        /// 热更新功能总开关；默认 true，关闭时启动直跳 ProcedureLoadDll，跳过 CheckVersion / Hotfix / AppDownload 三个 Procedure。
        /// 与 RuntimePlayMode 在 Inspector 编辑期双向联动。
        /// </summary>
        [SerializeField]
        private bool m_EnableHotfix = true;
        /// <summary>
        /// 热更新功能总开关对外只读属性。
        /// </summary>
        public bool EnableHotfix => m_EnableHotfix;

        /// <summary>
        /// 是否启用启动设备白名单；默认关闭。
        /// 仅在 EnableHotfix=true 且有效资源模式为 HostPlayMode 时生效。
        /// </summary>
        [SerializeField]
        private bool m_EnableStartupWhitelist;

        /// <summary>
        /// Debug 开发模式下的启动白名单文件 URL。
        /// </summary>
        [SerializeField]
        private string m_StartupWhitelistUrlDebug;

        /// <summary>
        /// Debug 开发模式下的启动白名单文件备用 URL。
        /// </summary>
        [SerializeField]
        private string m_StartupWhitelistUrlFallbackDebug;

        /// <summary>
        /// Release 开发模式下的启动白名单文件 URL。
        /// </summary>
        [SerializeField]
        private string m_StartupWhitelistUrlRelease;

        /// <summary>
        /// Release 开发模式下的启动白名单文件备用 URL。
        /// </summary>
        [SerializeField]
        private string m_StartupWhitelistUrlFallbackRelease;

        /// <summary>
        /// Debug 开发模式下白名单设备使用的版本元数据根 URL。
        /// </summary>
        [SerializeField]
        private string m_StartupWhitelistMetadataRootUrlDebug;

        /// <summary>
        /// Debug 开发模式下白名单设备使用的版本元数据备用根 URL。
        /// </summary>
        [SerializeField]
        private string m_StartupWhitelistMetadataRootUrlFallbackDebug;

        /// <summary>
        /// Release 开发模式下白名单设备使用的版本元数据根 URL。
        /// </summary>
        [SerializeField]
        private string m_StartupWhitelistMetadataRootUrlRelease;

        /// <summary>
        /// Release 开发模式下白名单设备使用的版本元数据备用根 URL。
        /// </summary>
        [SerializeField]
        private string m_StartupWhitelistMetadataRootUrlFallbackRelease;

        /// <summary>
        /// 启动白名单文件请求每个重试周期内的主备完整轮数；默认 1。
        /// </summary>
        [SerializeField, Min(1)]
        private int m_StartupWhitelistFallbackRoundCount = 1;

        /// <summary>
        /// 启动白名单文件全部轮次失败后的重试次数；默认 1。
        /// </summary>
        [SerializeField, Min(0)]
        private int m_StartupWhitelistRetryRequestCount = 1;

        /// <summary>
        /// 启动白名单文件的新请求是否优先使用当前进程内最近成功的域名；默认开启。
        /// </summary>
        [SerializeField]
        private bool m_StartupWhitelistPreferLastSuccessfulHost = true;

        /// <summary>
        /// 是否启用启动白名单文件 UWR 请求链埋点；默认开启，仅控制上报。
        /// </summary>
        [SerializeField]
        private bool m_StartupWhitelistEnableUWRTracks = true;

        /// <summary>
        /// 启动白名单文件单次物理请求超时秒数；主备候选分别计时，默认 5。
        /// </summary>
        [SerializeField]
        private int m_StartupWhitelistCheckTimeout = 5;

        /// <summary>
        /// 启动期资源补丁就绪后是否自动开始下载；默认 true。
        /// </summary>
        [SerializeField]
        private bool m_AutoHotfix = true;

        /// <summary>
        /// 资源补丁下载失败或取消时是否强制退出应用；默认 false。
        /// </summary>
        [SerializeField]
        private bool m_QuitOnFailedOrCancel;
        /// <summary>
        /// 下载失败或取消时是否强制退出应用对外只读属性。
        /// </summary>
        public bool QuitOnFailedOrCancel => m_QuitOnFailedOrCancel;

        /// <summary>
        /// 资源补丁下载最大并发数，推荐 3-8；默认 5。
        /// </summary>
        [SerializeField]
        private int m_MaxDownloadConcurrency = 5;
        /// <summary>
        /// 下载最大并发数对外只读属性。
        /// </summary>
        public int MaxDownloadConcurrency => m_MaxDownloadConcurrency;

        /// <summary>
        /// 每个文件的单个逻辑执行周期内，完整遍历全部有效且去重主备候选的轮数；默认 1。
        /// </summary>
        [SerializeField, Min(1)]
        private int m_FallbackRoundCount = 1;
        /// <summary>
        /// Asset 主备候选完整轮数。
        /// </summary>
        public int FallbackRoundCount => Mathf.Max(1, m_FallbackRoundCount);

        /// <summary>
        /// 单文件下载重试次数；首次执行不计入该值，每次重试重新执行全部主备轮次，默认 3。
        /// </summary>
        [SerializeField, Min(0)]
        private int m_RetryDownloadCount = 3;
        /// <summary>
        /// 单文件下载重试次数。
        /// </summary>
        public int RetryDownloadCount => Mathf.Max(0, m_RetryDownloadCount);

        /// <summary>
        /// 新文件建立独立下载计划时，是否优先使用当前进程内最近成功的 Asset 域名；不会删除其他候选。
        /// </summary>
        [SerializeField]
        private bool m_PreferLastSuccessfulHost = true;
        /// <summary>
        /// 获取最近成功域名优先开关。
        /// </summary>
        public bool PreferLastSuccessfulHost => m_PreferLastSuccessfulHost;

        /// <summary>
        /// 是否启用 Asset UnityWebRequest 链路埋点；仅控制上报，不影响下载执行。
        /// </summary>
        [SerializeField]
        private bool m_EnableUWRTracks = true;
        /// <summary>
        /// 获取 Asset UnityWebRequest 链路埋点开关。
        /// </summary>
        public bool EnableUWRTracks => m_EnableUWRTracks;

        /// <summary>
        /// 启动期热更按 tag 过滤的 tag 列表。
        /// 非空时 ProcedureCheckVersion 与 ProcedureHotfix 分别按 Tag 判断和下载；
        /// 空列表表示检查并下载整包（行为与旧逻辑一致）。
        /// </summary>
        [SerializeField]
        private List<string> m_LaunchHotfixTags;
        /// <summary>
        /// 启动期热更 tag 列表对外只读属性。
        /// </summary>
        public List<string> LaunchHotfixTags => m_LaunchHotfixTags;

        /// <summary>
        /// 热更完成后是否自动执行 ClearUnusedCacheAsync 清理冗余磁盘缓存；默认 false。
        /// </summary>
        [SerializeField]
        private bool m_AutoClearUnusedCacheOnHotfix;
        /// <summary>
        /// 热更后自动清理冗余缓存对外只读属性。
        /// </summary>
        public bool AutoClearUnusedCacheOnHotfix => m_AutoClearUnusedCacheOnHotfix;

        /// <summary>
        /// .version 的单次物理请求超时秒数；每个主备候选独立使用，默认 5。
        /// 主备轮次、下载重试次数、最近成功域名优先和 UWR 埋点仍使用 Asset 公共配置。
        /// </summary>
        [SerializeField]
        private int m_CheckTimeout = 5;

        /// <summary>
        /// .hash/.bytes Manifest 的单次物理请求总超时秒数；每个主备候选独立使用，默认 60。
        /// 主备轮次、下载重试次数、最近成功域名优先和 UWR 埋点仍使用 Asset 公共配置。
        /// </summary>
        [SerializeField]
        private int m_ManifestRequestTimeout = 60;

        /// <summary>
        /// WebGL 远端 Bundle 单次物理请求的总超时秒数；默认 300。
        /// 非 WebGL 平台不使用该字段。
        /// </summary>
        [SerializeField]
        private int m_WebGLBundleRequestTimeout = 300;

        /// <summary>
        /// 单文件字节流入超时秒数（连续无新字节流入时中止下载）；默认 20。
        /// WebGL 不支持可靠的字节流入看门狗，不使用该字段。
        /// </summary>
        [SerializeField]
        private int m_IdleTimeout = 20;

        /// <summary>
        /// Debug 开发模式下的主机服务器地址 URL。
        /// </summary>
        [SerializeField]
        private string m_HostServerUrlDebug = "https://mergewonder-test.oss-cn-beijing.aliyuncs.com/Nova/{Platform}/{Channel}/{Package}/{Version}";

        /// <summary>
        /// Debug 开发模式下的备用主机服务器地址 URL。
        /// </summary>
        [SerializeField]
        private string m_HostServerUrlFallbackDebug = "https://mergewonder-test.oss-cn-beijing.aliyuncs.com/Nova/{Platform}/{Channel}/{Package}/{Version}";

        /// <summary>
        /// Release 开发模式下的主机服务器地址 URL。
        /// </summary>
        [SerializeField]
        private string m_HostServerUrlRelease = "https://mergewonder-test.oss-cn-beijing.aliyuncs.com/Nova/{Platform}/{Channel}/{Package}/{Version}";

        /// <summary>
        /// Release 开发模式下的备用主机服务器地址 URL。
        /// </summary>
        [SerializeField]
        private string m_HostServerUrlFallbackRelease = "https://mergewonder-test.oss-cn-beijing.aliyuncs.com/Nova/{Platform}/{Channel}/{Package}/{Version}";

        /// <summary>
        /// Config 导出时同步的渠道快照，供资源系统启动前解析远端 URL。
        /// </summary>
        [SerializeField, HideInInspector]
        private ChannelType m_Channel;

        /// <summary>
        /// AssetManager 实例。
        /// </summary>
        private IAssetManager m_AssetManager;
    }
}
