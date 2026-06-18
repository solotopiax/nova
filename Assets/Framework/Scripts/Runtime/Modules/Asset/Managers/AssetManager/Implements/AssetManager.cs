/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AssetManager.cs
 * author:    taoye
 * created:   2026/5/14
 * descrip:   AssetManager 主入口
 ***************************************************************/

using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using YooAsset;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// AssetManager 实现：YooAsset 包裹层。
    /// </summary>
    internal sealed partial class AssetManager : AssetManagerBase
    {
        /// <summary>
        /// 反射创建用无参构造器。
        /// </summary>
        public AssetManager()
        {
        }

        /// <summary>
        /// 轻初始化（Inspector 字段注入），不触达底层资源框架。
        /// </summary>
        /// <param name="config">AssetManager 配置。</param>
        public override void Initialize(AssetManagerConfig config)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            m_Config = config;
            m_Cts = new CancellationTokenSource();
        }

        /// <summary>
        /// 启动资源框架：注册包、创建解密器、初始化底层资源系统。
        /// 由 Procedure 编排时调用；包名、URL 模板、解密器类型均已由 Initialize 注入的 AssetManagerConfig 提供。
        /// </summary>
        /// <param name="ct">取消令牌。</param>
        public override async UniTask BootstrapAsync(CancellationToken ct = default)
        {
            if (m_Config.Packages == null || m_Config.Packages.Count == 0)
            {
                throw new InvalidOperationException("AssetManagerConfig.Packages is empty.");
            }

            m_DefaultPackageName = string.IsNullOrEmpty(m_Config.DefaultPackageName)
                ? m_Config.Packages[0]
                : m_Config.DefaultPackageName;
            m_Decryptor = CreateDecryptor(m_Config.DecryptorType);

            if (YooAssets.IsInitialized == false)
            {
                YooAssets.Initialize();
            }

            for (int i = 0; i < m_Config.Packages.Count; i++)
            {
                string name = m_Config.Packages[i];
                if (YooAssets.ContainsPackage(name) == false)
                {
                    m_Packages[name] = YooAssets.CreatePackage(name);
                }
                else
                {
                    m_Packages[name] = YooAssets.GetPackage(name);
                }
            }

            await UniTask.CompletedTask;
        }

        /// <summary>
        /// 每帧 Tick，由 FrameworkManagersGroup 调度。YooAsset 内部自驱动，此处无额外逻辑。
        /// </summary>
        public override void Update()
        {
            // YooAsset 内部 AsyncOperationSystem 自驱动，AssetManager 无周期任务。
        }

        /// <summary>
        /// 关闭 Manager，取消所有进行中的异步操作并销毁底层资源系统。
        /// </summary>
        public override void Shutdown()
        {
            m_Cts?.Cancel();
            m_Cts?.Dispose();
            m_Cts = null;
            if (YooAssets.IsInitialized)
            {
                YooAssets.Destroy();
            }
            m_ManifestLoadedPackages.Clear();
            m_Packages.Clear();
            m_Decryptor = null;
            m_Config = null;
        }

        /// <summary>
        /// 加载指定包清单：版本请求 + 清单加载，合并 YooAsset 三步初始化，幂等。
        /// </summary>
        /// <param name="package">包名，null 走默认包。</param>
        /// <param name="ct">取消令牌。</param>
        public override async UniTask LoadManifestAsync(string package = null, CancellationToken ct = default)
        {
            string name = ResolvePackageName(package);
            if (m_ManifestLoadedPackages.Contains(name))
            {
                return;
            }

            ResourcePackage pkg = GetPackage(name);
            // 分阶段幂等：YooAsset.InitializePackageAsync 在 _initializeOp != null 时抛 "already initialized"，
            // 故 Initialize 已 Succeeded 时直接跳过本步，继续执行 Version/Manifest，避免上游流程半途失败后重入崩溃。
            if (pkg.InitializeStatus != EOperationStatus.Succeeded)
            {
                InitializePackageOptions options = BuildPlayModeOptions(name);
                var initOp = pkg.InitializePackageAsync(options);
                await UniTask.WaitUntil(() => initOp.IsDone, cancellationToken: ct);
                if (initOp.Status != EOperationStatus.Succeeded)
                {
                    throw new InvalidOperationException($"InitializePackageAsync failed: {initOp.Error}");
                }
            }

            var versionOp = pkg.RequestPackageVersionAsync();
            await UniTask.WaitUntil(() => versionOp.IsDone, cancellationToken: ct);
            if (versionOp.Status != EOperationStatus.Succeeded)
            {
                bool fallbackSucceeded = await TryRecoverManifestAsync(name, versionOp.Error, ct);
                if (fallbackSucceeded)
                {
                    return;
                }

                throw new InvalidOperationException($"RequestPackageVersionAsync failed: {versionOp.Error}");
            }

            var manifestOp = pkg.LoadPackageManifestAsync(new LoadPackageManifestOptions(versionOp.PackageVersion, 60));
            await UniTask.WaitUntil(() => manifestOp.IsDone, cancellationToken: ct);
            if (manifestOp.Status != EOperationStatus.Succeeded)
            {
                bool fallbackSucceeded = await TryRecoverManifestAsync(name, manifestOp.Error, ct);
                if (fallbackSucceeded)
                {
                    return;
                }

                throw new InvalidOperationException($"LoadPackageManifestAsync failed: {manifestOp.Error}");
            }

            SaveLocalCachedVersion(name, pkg.GetPackageVersion());
            m_ManifestLoadedPackages.Add(name);
        }

        /// <summary>
        /// 检查指定包是否有补丁需要下载。
        /// 调用前须已完成 LoadManifestAsync；未加载则内部先触发加载。
        /// </summary>
        /// <param name="package">包名，null 走默认包。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>true 表示有补丁需要下载。</returns>
        public override async UniTask<bool> HasPatchAsync(string package = null, CancellationToken ct = default)
        {
            string name = ResolvePackageName(package);
            if (!m_ManifestLoadedPackages.Contains(name))
            {
                await LoadManifestAsync(name, ct);
            }

            ResourcePackage pkg = GetPackage(name);
            var downloader = pkg.CreateResourceDownloader(new ResourceDownloaderOptions(int.MaxValue, 0));
            await UniTask.Yield(ct);
            return downloader.TotalDownloadCount > 0;
        }

        /// <summary>
        /// 创建下载器，业务持有后调 RunAsync 触发实际下载。
        /// </summary>
        /// <param name="package">包名，null 走默认包。</param>
        /// <param name="concurrency">并发下载数。</param>
        /// <param name="retry">单文件失败重试次数。</param>
        /// <returns>下载器实例。</returns>
        public override IAssetDownloader CreateDownloader(string package = null, int concurrency = 10, int retry = 3)
        {
            string name = ResolvePackageName(package);
            ResourcePackage pkg = GetPackage(name);
            var options = new ResourceDownloaderOptions(concurrency, retry);
            return new AssetDownloader(pkg.CreateResourceDownloader(options), "all");
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
        public override IAssetDownloader CreateDownloaderByTags(string[] tags, string package = null, int concurrency = 10, int retry = 3)
        {
            string name = ResolvePackageName(package);
            ResourcePackage pkg = GetPackage(name);
            if (tags == null || tags.Length == 0)
            {
                var options = new ResourceDownloaderOptions(concurrency, retry);
                return new AssetDownloader(pkg.CreateResourceDownloader(options), "all");
            }
            var tagOptions = new ResourceDownloaderOptions(tags, concurrency, retry);
            return new AssetDownloader(pkg.CreateResourceDownloader(tagOptions), $"tags:{string.Join(',', tags)}");
        }

        /// <summary>
        /// 按 Asset 地址列表创建下载器，下载指定 Asset 地址所属 Bundle 及其依赖。
        /// locations 为 null 或空时抛 ArgumentException（整包请用 CreateDownloader）。
        /// </summary>
        /// <param name="locations">Asset 地址列表，不允许为 null 或空。</param>
        /// <param name="package">包名，null 走默认包。</param>
        /// <param name="concurrency">并发下载数。</param>
        /// <param name="retry">单文件失败重试次数。</param>
        /// <returns>下载器实例，Scope 为 "locations:N"。</returns>
        public override IAssetDownloader CreateDownloaderByLocations(string[] locations, string package = null, int concurrency = 10, int retry = 3)
        {
            if (locations == null || locations.Length == 0)
            {
                throw new System.ArgumentException("locations 不允许为 null 或空，整包下载请用 CreateDownloader。", nameof(locations));
            }
            string name = ResolvePackageName(package);
            ResourcePackage pkg = GetPackage(name);
            var validInfos = new System.Collections.Generic.List<AssetInfo>(locations.Length);
            for (int i = 0; i < locations.Length; i++)
            {
                AssetInfo info = pkg.GetAssetInfo(locations[i]);
                if (!info.IsValid)
                {
                    Log.Warning(LogTag.Asset, Txt.Format("Asset 地址无效，已跳过（不中断其余下载）: {0} — {1}", locations[i], info.Error));
                    continue;
                }
                validInfos.Add(info);
            }
            var options = new BundleDownloaderOptions(validInfos.ToArray(), true, concurrency, retry);
            return new AssetDownloader(pkg.CreateResourceDownloader(options), $"locations:{validInfos.Count}");
        }

        /// <summary>
        /// 强制刷新指定包的资源清单：清除幂等缓存后重新执行 LoadManifestAsync。
        /// 用于热更下载完成后通知资源系统切换到新版本清单。
        /// </summary>
        /// <param name="package">包名，null 走默认包。</param>
        /// <param name="ct">取消令牌。</param>
        public override async UniTask RefreshManifestAsync(string package = null, CancellationToken ct = default)
        {
            string name = ResolvePackageName(package);
            m_ManifestLoadedPackages.Remove(name);
            await LoadManifestAsync(name, ct);
        }

        /// <summary>
        /// 远端版本或清单不可达时的统一恢复编排：① 沿用当前已激活清单 → ② 本地缓存版本离线加载 → ③ 内置首包清单回退。
        /// 任一级成功即返回 true。
        /// </summary>
        /// <param name="name">包名。</param>
        /// <param name="remoteError">远端请求错误。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>true 表示某一级恢复成功。</returns>
        private async UniTask<bool> TryRecoverManifestAsync(string name, string remoteError, CancellationToken ct)
        {
            ResourcePackage pkg = GetPackage(name);
            if (pkg.PackageValid)
            {
                Log.Warning(LogTag.Asset, Txt.Format("远端资源清单请求失败，继续使用当前已激活清单。Package={0}, Error={1}", name, remoteError));
                m_ManifestLoadedPackages.Add(name);
                return true;
            }

            if (await TryFallbackToLocalCachedManifestAsync(name, remoteError, ct))
            {
                return true;
            }

            return await TryFallbackToBuiltinManifestAsync(name, remoteError, ct);
        }

        /// <summary>
        /// HostPlayMode 下远端版本或清单不可达时，回退到本地已记录的缓存版本，离线加载玩家最新缓存清单。
        /// 不销毁资源包、不切换运行模式，直接在当前 Host 包上离线命中沙盒缓存，保留玩家已下载的增量。
        /// </summary>
        /// <param name="name">包名。</param>
        /// <param name="remoteError">远端版本或清单请求错误。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>true 表示已成功用本地缓存版本加载清单。</returns>
        private async UniTask<bool> TryFallbackToLocalCachedManifestAsync(string name, string remoteError, CancellationToken ct)
        {
            if (CanFallbackToBuiltinManifest() == false)
            {
                return false;
            }
            if (TryLoadLocalCachedVersion(name, out string localVersion) == false)
            {
                return false;
            }

            Log.Warning(LogTag.Asset, Txt.Format("远端资源清单请求失败，尝试回退到本地缓存版本清单。Package={0}, LocalVersion={1}, Error={2}", name, localVersion, remoteError));

            ResourcePackage pkg = GetPackage(name);
            var manifestOp = pkg.LoadPackageManifestAsync(new LoadPackageManifestOptions(localVersion, 60));
            await UniTask.WaitUntil(() => manifestOp.IsDone, cancellationToken: ct);
            if (manifestOp.Status != EOperationStatus.Succeeded)
            {
                Log.Warning(LogTag.Asset, Txt.Format("回退本地缓存版本清单失败。Package={0}, LocalVersion={1}, Error={2}", name, localVersion, manifestOp.Error));
                return false;
            }

            m_ManifestLoadedPackages.Add(name);
            Log.Warning(LogTag.Asset, Txt.Format("已回退到本地缓存清单，启动流程将跳过本轮远端热更。Package={0}, Version={1}", name, localVersion));
            return true;
        }

        /// <summary>
        /// HostPlayMode 下远端版本或清单不可达时，回退到随包内置清单。
        /// 用于启动期弱网 / DNS 异常时跳过热更检查，继续使用 Player 内置资源完成 Config 与 DLL 加载。
        /// </summary>
        /// <param name="name">包名。</param>
        /// <param name="remoteError">远端版本或清单请求错误。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>true 表示已成功加载内置清单。</returns>
        private async UniTask<bool> TryFallbackToBuiltinManifestAsync(string name, string remoteError, CancellationToken ct)
        {
            ResourcePackage pkg = GetPackage(name);
            if (!CanFallbackToBuiltinManifest())
            {
                return false;
            }

            Log.Warning(LogTag.Asset,
                "远端资源清单请求失败，尝试回退到内置资源清单。Package={0}, Error={1}",
                name, remoteError);

            var destroyOp = pkg.DestroyPackageAsync();
            await UniTask.WaitUntil(() => destroyOp.IsDone, cancellationToken: ct);
            if (destroyOp.Status != EOperationStatus.Succeeded)
            {
                Log.Warning(LogTag.Asset,
                    "回退内置资源清单失败：销毁当前资源包失败。Package={0}, Error={1}",
                    name, destroyOp.Error);
                return false;
            }

            var initOp = pkg.InitializePackageAsync(BuildOfflineOptions());
            await UniTask.WaitUntil(() => initOp.IsDone, cancellationToken: ct);
            if (initOp.Status != EOperationStatus.Succeeded)
            {
                Log.Warning(LogTag.Asset,
                    "回退内置资源清单失败：OfflinePlayMode 初始化失败。Package={0}, Error={1}",
                    name, initOp.Error);
                return false;
            }

            var builtinVersionOp = pkg.RequestPackageVersionAsync();
            await UniTask.WaitUntil(() => builtinVersionOp.IsDone, cancellationToken: ct);
            if (builtinVersionOp.Status != EOperationStatus.Succeeded)
            {
                Log.Warning(LogTag.Asset,
                    "回退内置资源清单失败：内置版本文件不可用。Package={0}, Error={1}",
                    name, builtinVersionOp.Error);
                return false;
            }

            var builtinManifestOp = pkg.LoadPackageManifestAsync(new LoadPackageManifestOptions(builtinVersionOp.PackageVersion, 60));
            await UniTask.WaitUntil(() => builtinManifestOp.IsDone, cancellationToken: ct);
            if (builtinManifestOp.Status != EOperationStatus.Succeeded)
            {
                Log.Warning(LogTag.Asset,
                    "回退内置资源清单失败：内置清单加载失败。Package={0}, Version={1}, Error={2}",
                    name, builtinVersionOp.PackageVersion, builtinManifestOp.Error);
                return false;
            }

            m_ManifestLoadedPackages.Add(name);
            Log.Warning(LogTag.Asset,
                "已回退到内置资源清单，启动流程将跳过本轮远端热更。Package={0}, Version={1}",
                name, builtinVersionOp.PackageVersion);
            return true;
        }

        /// <summary>
        /// 当前只允许 HostPlayMode 在远端不可达时回退到内置资源。
        /// </summary>
        /// <returns>true 表示可以尝试内置清单回退。</returns>
        private bool CanFallbackToBuiltinManifest()
        {
            AssetPlayMode effectiveMode = Application.isEditor
                ? m_Config.EditorPlayMode
                : m_Config.RuntimePlayMode;
            return effectiveMode == AssetPlayMode.HostPlayMode;
        }

    }
}
