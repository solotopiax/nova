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
        /// 与 EnableHotfix 双向联动：EnableHotfix=false ⇔ RuntimePlayMode=OfflinePlayMode；EnableHotfix=true ⇔ RuntimePlayMode∈{HostPlayMode, WebPlayMode}。
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
        /// 单文件下载失败自动重试次数，0 表示不重试；默认 3。
        /// </summary>
        [SerializeField]
        private int m_RetryDownloadCount = 3;
        /// <summary>
        /// 单文件下载失败自动重试次数对外只读属性。
        /// </summary>
        public int RetryDownloadCount => m_RetryDownloadCount;

        /// <summary>
        /// 启动期热更按 tag 过滤的 tag 列表。
        /// 非空时 ProcedureHotfix 使用 CreateDownloaderByTags 替代整包下载；
        /// 空列表表示下载整包（行为与旧逻辑一致）。
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
        /// 版本检查空闲超时秒数（连续无新字节流入时中止请求）；默认 5。
        /// </summary>
        [SerializeField]
        private int m_CheckTimeout = 5;

        /// <summary>
        /// 文件下载空闲超时秒数（连续无新字节流入时中止下载）；默认 20。
        /// </summary>
        [SerializeField]
        private int m_IdleTimeout = 20;

        /// <summary>
        /// Debug 开发模式下的主机服务器地址 URL。
        /// </summary>
        [SerializeField]
        private string m_HostServerUrlDebug = "https://xxxx.xxxx.xxxx/{Platform}/{Package}/{Version}";

        /// <summary>
        /// Debug 开发模式下的备用主机服务器地址 URL。
        /// </summary>
        [SerializeField]
        private string m_HostServerUrlFallbackDebug = "https://yyyy.yyyy.yyyy/{Platform}/{Package}/{Version}";

        /// <summary>
        /// Release 开发模式下的主机服务器地址 URL。
        /// </summary>
        [SerializeField]
        private string m_HostServerUrlRelease = "https://xxxx.xxxx.xxxx/{Platform}/{Package}/{Version}";

        /// <summary>
        /// Release 开发模式下的备用主机服务器地址 URL。
        /// </summary>
        [SerializeField]
        private string m_HostServerUrlFallbackRelease = "https://yyyy.yyyy.yyyy/{Platform}/{Package}/{Version}";

        /// <summary>
        /// AssetBundle 解密器类型；默认 None。
        /// </summary>
        [SerializeField]
        private AssetDecryptorType m_DecryptorType = AssetDecryptorType.None;

        /// <summary>
        /// AssetManager 实例。
        /// </summary>
        private IAssetManager m_AssetManager;
    }
}
