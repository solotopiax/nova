/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AssetManagerConfig.cs
 * author:    taoye
 * created:   2026/5/14
 * descrip:   AssetManager 配置
 ***************************************************************/

namespace NovaFramework.Runtime
{
    /// <summary>
    /// AssetManager 启动配置。
    /// </summary>
    public sealed class AssetManagerConfig
    {
        /// <summary>
        /// 编辑器下资源加载模式。
        /// 仅在 Application.isEditor 时生效；4 个枚举值均允许选择。
        /// 默认 EditorSimulateMode（直接读 Editor 资源，零网络开销）。
        /// </summary>
        public AssetPlayMode EditorPlayMode = AssetPlayMode.EditorSimulateMode;

        /// <summary>
        /// 终端下资源加载模式。
        /// 在 Player（非 Editor）时生效；不允许 EditorSimulateMode（仅限 OfflinePlayMode/HostPlayMode/WebPlayMode）。
        /// 默认 HostPlayMode（联机热更模式）。
        /// 与 EnableHotfix 双向联动：EnableHotfix=false ⇔ RuntimePlayMode=OfflinePlayMode；EnableHotfix=true ⇔ RuntimePlayMode∈{HostPlayMode, WebPlayMode}。
        /// </summary>
        public AssetPlayMode RuntimePlayMode = AssetPlayMode.HostPlayMode;

        /// <summary>
        /// 需要 CreatePackage 的包名列表，至少包含一个默认包（Inspector 下沉字段）。
        /// </summary>
        public System.Collections.Generic.List<string> Packages;

        /// <summary>
        /// 默认包名；为空时取 Packages[0]。
        /// </summary>
        public string DefaultPackageName;

        /// <summary>
        /// 是否在场景卸载后自动 CleanupAsync 默认包；默认 false，由业务决定时机。
        /// </summary>
        public bool AutoCleanupOnSceneUnload;

        /// <summary>
        /// 热更新功能总开关。
        /// 默认 true，关闭时启动直跳 ProcedureLoadDll，跳过 CheckVersion / Hotfix / AppDownload 三个 Procedure；
        /// 与 RuntimePlayMode 在 Inspector 编辑期双向联动：关闭时 RuntimePlayMode 强制为 OfflinePlayMode；
        /// 开启时 RuntimePlayMode 限制为 HostPlayMode/WebPlayMode（详见 AssetComponentInspector 联动逻辑）。
        /// </summary>
        public bool EnableHotfix = true;

        /// <summary>
        /// 是否启用启动设备白名单；默认关闭。
        /// </summary>
        public bool EnableStartupWhitelist;

        /// <summary>
        /// 当前 DevelopMode 对应的启动白名单文件主 URL。
        /// </summary>
        public string StartupWhitelistUrl;

        /// <summary>
        /// 当前 DevelopMode 对应的启动白名单文件备用 URL。
        /// </summary>
        public string StartupWhitelistUrlFallback;

        /// <summary>
        /// 当前 DevelopMode 对应的白名单版本元数据根主 URL。
        /// </summary>
        public string StartupWhitelistMetadataRootUrl;

        /// <summary>
        /// 当前 DevelopMode 对应的白名单版本元数据根备用 URL。
        /// </summary>
        public string StartupWhitelistMetadataRootUrlFallback;

        /// <summary>
        /// 启动期资源补丁就绪后是否自动开始下载。
        /// </summary>
        public bool AutoHotfix = true;

        /// <summary>
        /// 资源补丁下载失败或取消时是否强制退出应用。
        /// </summary>
        public bool QuitOnFailedOrCancel;

        /// <summary>
        /// 资源补丁下载最大并发数，推荐 3-8。
        /// </summary>
        public int MaxDownloadConcurrency = 5;

        /// <summary>
        /// 每个文件的单个逻辑执行周期内，完整遍历全部有效且去重主备候选的轮数，最小为 1。
        /// </summary>
        public int FallbackRoundCount = 1;

        /// <summary>
        /// 单文件下载重试次数；首次执行不计入该值，每次重试重新执行全部主备轮次，0 表示只执行首次组合。
        /// </summary>
        public int RetryDownloadCount = 3;

        /// <summary>
        /// 新文件建立独立下载计划时，是否优先使用当前进程内最近成功的 Asset 域名；不会删除其他候选。
        /// </summary>
        public bool PreferLastSuccessfulHost = true;

        /// <summary>
        /// 是否启用 Asset UnityWebRequest 链路埋点；仅控制上报，不影响下载执行。
        /// </summary>
        public bool EnableUWRTracks = true;

        /// <summary>
        /// 启动白名单与 .version 的单次物理请求超时秒数；每个主备候选独立使用。
        /// .hash/.bytes Manifest 仍使用现有 60 秒超时，不受此字段影响。
        /// </summary>
        public int CheckTimeout = 5;

        /// <summary>
        /// 单文件字节流入超时秒数（连续无新字节流入时中止下载）。
        /// </summary>
        public int IdleTimeout = 20;

        /// <summary>
        /// 主下载地址 URL。
        /// </summary>
        public string HostServerUrl;

        /// <summary>
        /// 备用下载地址 URL，主地址失败后回退使用。
        /// </summary>
        public string HostServerUrlFallback;

        /// <summary>
        /// Config 导出时同步的渠道快照，用于启动期 URL 占位符解析。
        /// </summary>
        public ChannelType Channel;

        /// <summary>
        /// AssetBundle 解密器类型（Inspector 下沉字段）。
        /// </summary>
        public AssetDecryptorType DecryptorType;

        /// <summary>
        /// 启动期热更按 tag 过滤的 tag 列表。
        /// 非空时 ProcedureCheckVersion 与 ProcedureHotfix 分别按 Tag 判断和下载；
        /// 空列表或 null 表示检查并下载整包（行为与旧逻辑一致）。
        /// </summary>
        public System.Collections.Generic.List<string> LaunchHotfixTags;

        /// <summary>
        /// 热更完成后是否自动执行 ClearUnusedCacheAsync 清理冗余磁盘缓存。
        /// 默认 false，由业务或 Procedure 根据时机决定是否开启。
        /// </summary>
        public bool AutoClearUnusedCacheOnHotfix;
    }
}
