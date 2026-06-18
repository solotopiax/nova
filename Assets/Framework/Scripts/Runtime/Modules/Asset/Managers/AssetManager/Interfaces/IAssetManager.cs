/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  IAssetManager.cs
 * author:    taoye
 * created:   2026/5/14
 * descrip:   资源管理接口
 ***************************************************************/

using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine.SceneManagement;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 资源管理接口，业务侧统一通过 Asset 地址访问资源句柄，
    /// 不感知任何第三方资源框架的内部句柄类型。
    /// 所有加载均返回对应 IHandle 接口，由调用方持有并在适当时机显式 Release。
    /// </summary>
    public interface IAssetManager
    {
        /// <summary>
        /// 轻初始化（Inspector 字段注入），不触达底层资源框架。
        /// </summary>
        /// <param name="config">AssetManager 配置。</param>
        void Initialize(AssetManagerConfig config);

        /// <summary>
        /// 启动资源框架：注册包、创建解密器、初始化底层资源系统。
        /// 由 Procedure 编排时调用；包名、URL 模板、解密器类型均已由 Initialize 注入的 AssetManagerConfig 提供。
        /// </summary>
        /// <param name="ct">取消令牌。</param>
        UniTask BootstrapAsync(CancellationToken ct = default);

        /// <summary>
        /// 加载指定包清单：版本请求 + 清单加载，幂等。
        /// </summary>
        /// <param name="package">包名，null 走默认包。</param>
        /// <param name="ct">取消令牌。</param>
        UniTask LoadManifestAsync(string package = null, CancellationToken ct = default);

        /// <summary>
        /// 检查指定包是否有补丁需要下载。
        /// 调用前须已完成 LoadManifestAsync；未加载则内部先触发加载。
        /// </summary>
        /// <param name="package">包名，null 走默认包。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>true 表示有补丁需要下载。</returns>
        UniTask<bool> HasPatchAsync(string package = null, CancellationToken ct = default);

        /// <summary>
        /// 创建下载器，业务持有后调 RunAsync 触发实际下载。
        /// </summary>
        /// <param name="package">包名，null 走默认包。</param>
        /// <param name="concurrency">并发下载数。</param>
        /// <param name="retry">单文件失败重试次数。</param>
        /// <returns>下载器实例。</returns>
        IAssetDownloader CreateDownloader(string package = null, int concurrency = 10, int retry = 3);

        /// <summary>
        /// 同步加载主资源句柄（泛型），返回中性 IAssetHandle，由调用方自行 Release。
        /// </summary>
        /// <typeparam name="T">资源类型。</typeparam>
        /// <param name="location">Asset 地址。</param>
        /// <returns>资源句柄，调用方负责 Release。</returns>
        IAssetHandle<T> LoadSync<T>(string location) where T : UnityEngine.Object;

        /// <summary>
        /// 异步加载主资源句柄（泛型），取消或异常时内部自动释放 handle，正常完成时由调用方自行 Release。
        /// </summary>
        /// <typeparam name="T">资源类型。</typeparam>
        /// <param name="location">Asset 地址。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>资源句柄，调用方负责 Release。</returns>
        UniTask<IAssetHandle<T>> LoadAsync<T>(string location, CancellationToken ct = default) where T : UnityEngine.Object;

        /// <summary>
        /// 同步加载子资源批量句柄（泛型），返回中性 ISubAssetsHandle，由调用方自行 Release。
        /// 整批同生共死，禁止对单个子资源单独 Release。
        /// </summary>
        /// <typeparam name="T">子资源类型。</typeparam>
        /// <param name="location">主资源 Asset 地址。</param>
        /// <returns>子资源批量句柄，调用方负责 Release。</returns>
        ISubAssetsHandle<T> LoadSubsSync<T>(string location) where T : UnityEngine.Object;

        /// <summary>
        /// 异步加载子资源批量句柄（泛型），取消或异常时内部自动释放 handle，正常完成时由调用方自行 Release。
        /// 整批同生共死，禁止对单个子资源单独 Release。
        /// </summary>
        /// <typeparam name="T">子资源类型。</typeparam>
        /// <param name="location">主资源 Asset 地址。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>子资源批量句柄，调用方负责 Release。</returns>
        UniTask<ISubAssetsHandle<T>> LoadSubsAsync<T>(string location, CancellationToken ct = default) where T : UnityEngine.Object;

        /// <summary>
        /// 同步加载全资源批量句柄（泛型），返回中性 IAllAssetsHandle，由调用方自行 Release。
        /// 整批同生共死，禁止对单个资源单独 Release。
        /// </summary>
        /// <typeparam name="T">资源类型。</typeparam>
        /// <param name="location">Asset 地址。</param>
        /// <returns>全资源批量句柄，调用方负责 Release。</returns>
        IAllAssetsHandle<T> LoadAllSync<T>(string location) where T : UnityEngine.Object;

        /// <summary>
        /// 异步加载全资源批量句柄（泛型），取消或异常时内部自动释放 handle，正常完成时由调用方自行 Release。
        /// 整批同生共死，禁止对单个资源单独 Release。
        /// </summary>
        /// <typeparam name="T">资源类型。</typeparam>
        /// <param name="location">Asset 地址。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>全资源批量句柄，调用方负责 Release。</returns>
        UniTask<IAllAssetsHandle<T>> LoadAllAsync<T>(string location, CancellationToken ct = default) where T : UnityEngine.Object;

        /// <summary>
        /// 同步加载原始文件句柄（RawFile 通道），调用方负责 Release 归还引用计数。
        /// </summary>
        /// <param name="location">Asset 地址。</param>
        /// <returns>原始文件句柄，调用方负责 Release。</returns>
        IRawFileHandle LoadRawSync(string location);

        /// <summary>
        /// 异步加载原始文件句柄（RawFile 通道），取消或异常时内部自动释放，调用方负责 Release 归还引用计数。
        /// </summary>
        /// <param name="location">Asset 地址。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>原始文件句柄，调用方负责 Release。</returns>
        UniTask<IRawFileHandle> LoadRawAsync(string location, CancellationToken ct = default);

        /// <summary>
        /// 同步加载场景并返回场景句柄，调用方负责调用 UnloadAsync 卸载场景并归还引用计数。
        /// </summary>
        /// <param name="location">场景 Asset 地址。</param>
        /// <param name="mode">场景加载模式。</param>
        /// <returns>场景句柄，调用方负责 UnloadAsync。</returns>
        ISceneHandle LoadSceneSync(string location, LoadSceneMode mode = LoadSceneMode.Single);

        /// <summary>
        /// 异步加载场景并返回场景句柄，取消或异常时内部自动卸载，调用方负责调用 UnloadAsync 卸载场景并归还引用计数。
        /// </summary>
        /// <param name="location">场景 Asset 地址。</param>
        /// <param name="mode">场景加载模式。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>场景句柄，调用方负责 UnloadAsync。</returns>
        UniTask<ISceneHandle> LoadSceneAsync(string location, LoadSceneMode mode = LoadSceneMode.Single, CancellationToken ct = default);

        /// <summary>
        /// 预加载单个资源（仅触发 AB + Asset 进缓存，不返回对象）。
        /// </summary>
        /// <param name="location">Asset 地址。</param>
        /// <param name="ct">取消令牌。</param>
        UniTask PreloadAsync(string location, CancellationToken ct = default);

        /// <summary>
        /// 批量预加载资源。
        /// </summary>
        /// <param name="locations">Asset 地址列表。</param>
        /// <param name="ct">取消令牌。</param>
        UniTask PreloadAsync(string[] locations, CancellationToken ct = default);

        /// <summary>
        /// 通过 tag 查询 Asset 地址列表。
        /// </summary>
        /// <param name="tag">tag 名。</param>
        /// <param name="package">包名，null 走默认包。</param>
        /// <returns>命中 Asset 地址数组。</returns>
        string[] GetLocationsByTag(string tag, string package = null);

        /// <summary>
        /// 通过多 tag 求并集查询 Asset 地址列表。
        /// </summary>
        /// <param name="tags">tag 名列表。</param>
        /// <param name="package">包名，null 走默认包。</param>
        /// <returns>命中 Asset 地址数组。</returns>
        string[] GetLocationsByTag(string[] tags, string package = null);

        /// <summary>
        /// 批量回收：扫描所有 RefCount=0 的 Provider 与 BundleLoader 并销毁。
        /// </summary>
        /// <param name="package">包名，null 走默认包。</param>
        /// <param name="ct">取消令牌。</param>
        UniTask CleanupAsync(string package = null, CancellationToken ct = default);

        /// <summary>
        /// 按 tag 列表创建下载器，业务持有后调 RunAsync 触发实际下载。
        /// tags 为 null 或空数组时等价整包下载（等同 CreateDownloader）。
        /// </summary>
        /// <param name="tags">tag 列表；null 或空表示下载全部资源。</param>
        /// <param name="package">包名，null 走默认包。</param>
        /// <param name="concurrency">并发下载数。</param>
        /// <param name="retry">单文件失败重试次数。</param>
        /// <returns>下载器实例，Scope 为 "all" 或 "tags:a,b,..."。</returns>
        IAssetDownloader CreateDownloaderByTags(string[] tags, string package = null, int concurrency = 10, int retry = 3);

        /// <summary>
        /// 按 Asset 地址列表创建下载器，下载指定 Asset 地址所属 Bundle 及其依赖。
        /// locations 为空时抛 ArgumentException（整包请用 CreateDownloader）。
        /// </summary>
        /// <param name="locations">Asset 地址列表，不允许为 null 或空。</param>
        /// <param name="package">包名，null 走默认包。</param>
        /// <param name="concurrency">并发下载数。</param>
        /// <param name="retry">单文件失败重试次数。</param>
        /// <returns>下载器实例，Scope 为 "locations:N"。</returns>
        IAssetDownloader CreateDownloaderByLocations(string[] locations, string package = null, int concurrency = 10, int retry = 3);

        /// <summary>
        /// 强制刷新指定包的资源清单：清除幂等缓存后重新执行 LoadManifestAsync。
        /// 用于热更下载完成后通知资源系统切换到新版本清单。
        /// </summary>
        /// <param name="package">包名，null 走默认包。</param>
        /// <param name="ct">取消令牌。</param>
        UniTask RefreshManifestAsync(string package = null, CancellationToken ct = default);

        /// <summary>
        /// 清理磁盘缓存中未被当前清单引用的冗余 Bundle 文件，释放磁盘空间。
        /// 调用前须已完成 LoadManifestAsync（需要清单对比才能判定"未使用"）。
        /// </summary>
        /// <param name="package">包名，null 走默认包。</param>
        /// <param name="ct">取消令牌。</param>
        UniTask ClearUnusedCacheAsync(string package = null, CancellationToken ct = default);
    }
}
