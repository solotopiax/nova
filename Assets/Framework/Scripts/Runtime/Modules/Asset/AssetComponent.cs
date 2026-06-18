/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AssetComponent.cs
 * author:    taoye
 * created:   2025/12/9
 * descrip:   资源组件
 ***************************************************************/

using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 资源组件，对业务侧暴露 IAssetManager 的薄代理。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed partial class AssetComponent : FrameworkComponent
    {
        /// <summary>
        /// 唤醒：反射创建 IAssetManager。
        /// </summary>
        protected override void Awake()
        {
            base.Awake();

            m_AssetManager = Util.TypeCreator.Create<IAssetManager>(m_CurAssetManagerTypeName);
            if (m_AssetManager == null)
            {
                throw new System.InvalidOperationException("AssetManager 无效。");
            }
        }

        /// <summary>
        /// 开始：注入 Inspector 配置。
        /// </summary>
        private void Start()
        {
            m_AssetManager.Initialize(new AssetManagerConfig
            {
                EditorPlayMode = m_EditorPlayMode,
                RuntimePlayMode = m_RuntimePlayMode,
                Packages = m_Packages,
                DefaultPackageName = m_DefaultPackageName,
                AutoCleanupOnSceneUnload = m_AutoCleanupOnSceneUnload,
                EnableHotfix = m_EnableHotfix,
                AutoHotfix = m_AutoHotfix,
                QuitOnFailedOrCancel = m_QuitOnFailedOrCancel,
                MaxDownloadConcurrency = m_MaxDownloadConcurrency,
                RetryDownloadCount = m_RetryDownloadCount,
                CheckTimeout = m_CheckTimeout,
                IdleTimeout = m_IdleTimeout,
                HostServerUrl = ResolveHostServerUrl(),
                HostServerUrlFallback = ResolveHostServerUrlFallback(),
                DecryptorType = m_DecryptorType,
                LaunchHotfixTags = m_LaunchHotfixTags,
                AutoClearUnusedCacheOnHotfix = m_AutoClearUnusedCacheOnHotfix,
            });
        }

        /// <summary>
        /// 根据当前场景快照中的开发模式，解析当前应使用的主机服务器地址。
        /// </summary>
        private string ResolveHostServerUrl()
        {
            return DevelopMode == DevelopMode.Debug
                ? m_HostServerUrlDebug
                : m_HostServerUrlRelease;
        }

        /// <summary>
        /// 根据当前场景快照中的开发模式，解析当前应使用的备用主机服务器地址。
        /// </summary>
        private string ResolveHostServerUrlFallback()
        {
            return DevelopMode == DevelopMode.Debug
                ? m_HostServerUrlFallbackDebug
                : m_HostServerUrlFallbackRelease;
        }

        /// <summary>
        /// 销毁：释放管理器引用。
        /// </summary>
        private void OnDestroy()
        {
            m_AssetManager = null;
        }

        /// <summary>
        /// 启动资源框架：注册包、创建解密器、初始化底层资源系统。
        /// 由 Procedure 编排时调用；包名、URL 模板、解密器类型均已由 Inspector 字段通过 AssetManagerConfig 注入。
        /// </summary>
        /// <param name="ct">取消令牌。</param>
        public UniTask BootstrapAsync(CancellationToken ct = default)
        {
            return m_AssetManager.BootstrapAsync(ct);
        }

        /// <summary>
        /// 加载指定包清单：版本请求 + 清单加载，幂等。
        /// </summary>
        /// <param name="package">包名，null 走默认包。</param>
        /// <param name="ct">取消令牌。</param>
        public UniTask LoadManifestAsync(string package = null, CancellationToken ct = default)
        {
            return m_AssetManager.LoadManifestAsync(package, ct);
        }

        /// <summary>
        /// 检查指定包是否有补丁需要下载。
        /// </summary>
        /// <param name="package">包名，null 走默认包。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>true 表示有补丁需要下载。</returns>
        public UniTask<bool> HasPatchAsync(string package = null, CancellationToken ct = default)
        {
            return m_AssetManager.HasPatchAsync(package, ct);
        }

        /// <summary>
        /// 创建下载器，业务持有后调 RunAsync 触发实际下载。
        /// </summary>
        /// <param name="package">包名，null 走默认包。</param>
        /// <param name="concurrency">并发下载数。</param>
        /// <param name="retry">单文件失败重试次数。</param>
        /// <returns>下载器实例。</returns>
        public IAssetDownloader CreateDownloader(string package = null, int concurrency = 10, int retry = 3)
        {
            return m_AssetManager.CreateDownloader(package, concurrency, retry);
        }

        /// <summary>
        /// 同步加载主资源句柄（泛型），返回中性 IAssetHandle，调用方负责 Release。
        /// </summary>
        /// <typeparam name="T">资源类型。</typeparam>
        /// <param name="location">Asset 地址。</param>
        /// <returns>资源句柄，调用方负责 Release。</returns>
        public IAssetHandle<T> LoadSync<T>(string location) where T : UnityEngine.Object
        {
            return m_AssetManager.LoadSync<T>(location);
        }

        /// <summary>
        /// 异步加载主资源句柄（泛型），正常完成时调用方负责 Release。
        /// </summary>
        /// <typeparam name="T">资源类型。</typeparam>
        /// <param name="location">Asset 地址。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>资源句柄，调用方负责 Release。</returns>
        public UniTask<IAssetHandle<T>> LoadAsync<T>(string location, CancellationToken ct = default) where T : UnityEngine.Object
        {
            return m_AssetManager.LoadAsync<T>(location, ct);
        }

        /// <summary>
        /// 同步加载子资源批量句柄（泛型），整批同生共死，调用方负责 Release。
        /// </summary>
        /// <typeparam name="T">子资源类型。</typeparam>
        /// <param name="location">主资源 Asset 地址。</param>
        /// <returns>子资源批量句柄，调用方负责 Release。</returns>
        public ISubAssetsHandle<T> LoadSubsSync<T>(string location) where T : UnityEngine.Object
        {
            return m_AssetManager.LoadSubsSync<T>(location);
        }

        /// <summary>
        /// 异步加载子资源批量句柄（泛型），整批同生共死，调用方负责 Release。
        /// </summary>
        /// <typeparam name="T">子资源类型。</typeparam>
        /// <param name="location">主资源 Asset 地址。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>子资源批量句柄，调用方负责 Release。</returns>
        public UniTask<ISubAssetsHandle<T>> LoadSubsAsync<T>(string location, CancellationToken ct = default) where T : UnityEngine.Object
        {
            return m_AssetManager.LoadSubsAsync<T>(location, ct);
        }

        /// <summary>
        /// 同步加载全资源批量句柄（泛型），整批同生共死，调用方负责 Release。
        /// </summary>
        /// <typeparam name="T">资源类型。</typeparam>
        /// <param name="location">Asset 地址。</param>
        /// <returns>全资源批量句柄，调用方负责 Release。</returns>
        public IAllAssetsHandle<T> LoadAllSync<T>(string location) where T : UnityEngine.Object
        {
            return m_AssetManager.LoadAllSync<T>(location);
        }

        /// <summary>
        /// 异步加载全资源批量句柄（泛型），整批同生共死，调用方负责 Release。
        /// </summary>
        /// <typeparam name="T">资源类型。</typeparam>
        /// <param name="location">Asset 地址。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>全资源批量句柄，调用方负责 Release。</returns>
        public UniTask<IAllAssetsHandle<T>> LoadAllAsync<T>(string location, CancellationToken ct = default) where T : UnityEngine.Object
        {
            return m_AssetManager.LoadAllAsync<T>(location, ct);
        }

        /// <summary>
        /// 同步加载原始文件句柄（RawFile 通道），调用方负责 Release 归还引用计数。
        /// </summary>
        /// <param name="location">Asset 地址。</param>
        /// <returns>原始文件句柄，调用方负责 Release。</returns>
        public IRawFileHandle LoadRawSync(string location)
        {
            return m_AssetManager.LoadRawSync(location);
        }

        /// <summary>
        /// 异步加载原始文件句柄（RawFile 通道），调用方负责 Release 归还引用计数。
        /// </summary>
        /// <param name="location">Asset 地址。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>原始文件句柄，调用方负责 Release。</returns>
        public UniTask<IRawFileHandle> LoadRawAsync(string location, CancellationToken ct = default)
        {
            return m_AssetManager.LoadRawAsync(location, ct);
        }

        /// <summary>
        /// 同步加载场景并返回场景句柄，调用方负责调用 UnloadAsync 卸载场景并归还引用计数。
        /// </summary>
        /// <param name="location">场景 Asset 地址。</param>
        /// <param name="mode">场景加载模式。</param>
        /// <returns>场景句柄，调用方负责 UnloadAsync。</returns>
        public ISceneHandle LoadSceneSync(string location, LoadSceneMode mode = LoadSceneMode.Single)
        {
            return m_AssetManager.LoadSceneSync(location, mode);
        }

        /// <summary>
        /// 异步加载场景并返回场景句柄，调用方负责调用 UnloadAsync 卸载场景并归还引用计数。
        /// </summary>
        /// <param name="location">场景 Asset 地址。</param>
        /// <param name="mode">场景加载模式。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>场景句柄，调用方负责 UnloadAsync。</returns>
        public UniTask<ISceneHandle> LoadSceneAsync(string location, LoadSceneMode mode = LoadSceneMode.Single, CancellationToken ct = default)
        {
            return m_AssetManager.LoadSceneAsync(location, mode, ct);
        }

        /// <summary>
        /// 预加载单个资源（仅触发 AB + Asset 进缓存，不返回对象）。
        /// </summary>
        /// <param name="location">Asset 地址。</param>
        /// <param name="ct">取消令牌。</param>
        public UniTask PreloadAsync(string location, CancellationToken ct = default)
        {
            return m_AssetManager.PreloadAsync(location, ct);
        }

        /// <summary>
        /// 批量预加载资源。
        /// </summary>
        /// <param name="locations">Asset 地址列表。</param>
        /// <param name="ct">取消令牌。</param>
        public UniTask PreloadAsync(string[] locations, CancellationToken ct = default)
        {
            return m_AssetManager.PreloadAsync(locations, ct);
        }

        /// <summary>
        /// 通过 tag 查询 Asset 地址列表。
        /// </summary>
        /// <param name="tag">tag 名。</param>
        /// <param name="package">包名，null 走默认包。</param>
        /// <returns>命中 Asset 地址数组。</returns>
        public string[] GetLocationsByTag(string tag, string package = null)
        {
            return m_AssetManager.GetLocationsByTag(tag, package);
        }

        /// <summary>
        /// 通过多 tag 求并集查询 Asset 地址列表。
        /// </summary>
        /// <param name="tags">tag 名列表。</param>
        /// <param name="package">包名，null 走默认包。</param>
        /// <returns>命中 Asset 地址数组。</returns>
        public string[] GetLocationsByTag(string[] tags, string package = null)
        {
            return m_AssetManager.GetLocationsByTag(tags, package);
        }

        /// <summary>
        /// 批量回收：扫描所有 RefCount=0 的 Provider 与 BundleLoader 并销毁。
        /// </summary>
        /// <param name="package">包名，null 走默认包。</param>
        /// <param name="ct">取消令牌。</param>
        public UniTask CleanupAsync(string package = null, CancellationToken ct = default)
        {
            return m_AssetManager.CleanupAsync(package, ct);
        }

        /// <summary>
        /// 按 tag 列表创建下载器，业务持有后调 RunAsync 触发实际下载。
        /// tags 为 null 或空数组时等价整包下载（等同 CreateDownloader）。
        /// </summary>
        /// <param name="tags">tag 列表；null 或空表示下载全部资源。</param>
        /// <param name="package">包名，null 走默认包。</param>
        /// <param name="concurrency">并发下载数。</param>
        /// <param name="retry">单文件失败重试次数。</param>
        /// <returns>下载器实例，Scope 为 "all" 或 "tags:a,b,..."。</returns>
        public IAssetDownloader CreateDownloaderByTags(string[] tags, string package = null, int concurrency = 10, int retry = 3)
        {
            return m_AssetManager.CreateDownloaderByTags(tags, package, concurrency, retry);
        }

        /// <summary>
        /// 按 Asset 地址列表创建下载器，下载指定 Asset 地址所属 Bundle 及其依赖。
        /// locations 为空时抛 ArgumentException（整包请用 CreateDownloader）。
        /// </summary>
        /// <param name="locations">Asset 地址列表，不允许为 null 或空。</param>
        /// <param name="package">包名，null 走默认包。</param>
        /// <param name="concurrency">并发下载数。</param>
        /// <param name="retry">单文件失败重试次数。</param>
        /// <returns>下载器实例，Scope 为 "locations:N"。</returns>
        public IAssetDownloader CreateDownloaderByLocations(string[] locations, string package = null, int concurrency = 10, int retry = 3)
        {
            return m_AssetManager.CreateDownloaderByLocations(locations, package, concurrency, retry);
        }

        /// <summary>
        /// 强制刷新指定包的资源清单：清除幂等缓存后重新执行 LoadManifestAsync。
        /// 用于热更下载完成后通知资源系统切换到新版本清单。
        /// </summary>
        /// <param name="package">包名，null 走默认包。</param>
        /// <param name="ct">取消令牌。</param>
        public UniTask RefreshManifestAsync(string package = null, CancellationToken ct = default)
        {
            return m_AssetManager.RefreshManifestAsync(package, ct);
        }

        /// <summary>
        /// 清理磁盘缓存中未被当前清单引用的冗余 Bundle 文件，释放磁盘空间。
        /// 调用前须已完成 LoadManifestAsync（需要清单对比才能判定"未使用"）。
        /// </summary>
        /// <param name="package">包名，null 走默认包。</param>
        /// <param name="ct">取消令牌。</param>
        public UniTask ClearUnusedCacheAsync(string package = null, CancellationToken ct = default)
        {
            return m_AssetManager.ClearUnusedCacheAsync(package, ct);
        }
    }
}
