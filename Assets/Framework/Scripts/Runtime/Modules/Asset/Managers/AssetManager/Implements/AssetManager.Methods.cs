/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AssetManager.Methods.cs
 * author:    taoye
 * created:   2026/5/14
 * descrip:   AssetManager 私有 helper
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using YooAsset;

namespace NovaFramework.Runtime
{
    internal sealed partial class AssetManager : AssetManagerBase
    {
        /// <summary>
        /// 解析包名：null/empty 走默认包。
        /// </summary>
        /// <param name="package">调用方传入的包名，允许 null 或空串。</param>
        /// <returns>实际使用的包名。</returns>
        private string ResolvePackageName(string package)
        {
            return string.IsNullOrEmpty(package) ? m_DefaultPackageName : package;
        }

        /// <summary>
        /// Host/Web 模式初始化前触发 DoH 检测。YooAsset 仍使用原域名，避免破坏 Host/SNI。
        /// </summary>
        private async UniTask PreflightRemoteUrlsAsync(string package, CancellationToken ct)
        {
            AssetPlayMode effectiveMode = Application.isEditor
                ? m_Config.EditorPlayMode
                : m_Config.RuntimePlayMode;
            if (effectiveMode != AssetPlayMode.HostPlayMode && effectiveMode != AssetPlayMode.WebPlayMode)
            {
                return;
            }

            IDoHManager doHManager = FrameworkManagersGroup.GetManager<IDoHManager>();
            if (doHManager == null)
            {
                return;
            }

            AssetRemoteService remote = CreateRemoteService(package);
            for (int i = 0; i < remote.BaseUrls.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    await doHManager.BuildRequestUrlCandidatesAsync(remote.BaseUrls[i], false);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    Log.Warning(LogTag.Asset, "远端资源地址 DoH 预检失败，将继续使用原始域名。URL={0}, Error={1}", remote.BaseUrls[i], exception.Message);
                }
            }
        }

        /// <summary>
        /// 在 YooAsset 包初始化前执行一次可选启动白名单检查。
        /// 任意配置、缓存、网络或协议异常均按未命中降级，不阻断现有热更新链路。
        /// </summary>
        private async UniTask CheckStartupWhitelistAsync(string package, CancellationToken ct)
        {
            if (m_StartupWhitelistCheckedPackages.Contains(package))
            {
                return;
            }

            try
            {
                string effectiveMode = m_Config == null
                    ? "<no config>"
                    : (Application.isEditor ? m_Config.EditorPlayMode : m_Config.RuntimePlayMode).ToString();
                bool canCheck = CanCheckStartupWhitelist();
                Log.Debug(LogTag.Asset, "启动白名单状态：Package={0}, EnableHotfix={1}, EnableStartupWhitelist={2}, EffectiveMode={3}, CanCheck={4}",
                    package,
                    m_Config != null && m_Config.EnableHotfix,
                    m_Config != null && m_Config.EnableStartupWhitelist,
                    effectiveMode,
                    canCheck);
                if (!canCheck)
                {
                    return;
                }
                if (!TryLoadAssetCheckDeviceId(out string deviceId))
                {
                    Log.Debug(LogTag.Asset, "启动白名单跳过：本地尚无稳定 DeviceID 缓存。");
                    return;
                }

                string primaryUrl = ResolveStartupUrl(m_Config.StartupWhitelistUrl, package);
                string fallbackUrl = ResolveStartupUrl(m_Config.StartupWhitelistUrlFallback, package);
                string metadataRoot = ResolveStartupUrl(m_Config.StartupWhitelistMetadataRootUrl, package);
                string metadataRootFallback = ResolveStartupUrl(m_Config.StartupWhitelistMetadataRootUrlFallback, package);
                if (string.IsNullOrEmpty(primaryUrl) && string.IsNullOrEmpty(fallbackUrl))
                {
                    Log.Debug(LogTag.Asset, "启动白名单跳过：当前 DevelopMode 未配置有效白名单文件 URL。");
                    return;
                }
                if (string.IsNullOrEmpty(metadataRoot) && string.IsNullOrEmpty(metadataRootFallback))
                {
                    Log.Debug(LogTag.Asset, "启动白名单跳过：当前 DevelopMode 未配置有效白名单版本元数据根 URL。");
                    return;
                }

                string body = await TryDownloadStartupWhitelistAsync(primaryUrl, "主", ct);
                if (string.IsNullOrWhiteSpace(body)
                    && !string.Equals(fallbackUrl, primaryUrl, StringComparison.OrdinalIgnoreCase))
                {
                    body = await TryDownloadStartupWhitelistAsync(fallbackUrl, "备用", ct);
                }
                if (string.IsNullOrWhiteSpace(body))
                {
                    Log.Debug(LogTag.Asset, "启动白名单文件拉取失败：Source=全部候选, Package={0}", package);
                    return;
                }

                List<string> whitelist;
                try
                {
                    whitelist = Util.Json.Deserialize<List<string>>(body);
                }
                catch (Exception exception)
                {
                    Log.Error(LogTag.Asset, "启动白名单 JSON 解析失败，按未命中继续启动。Error={0}", exception.Message);
                    return;
                }

                if (whitelist == null)
                {
                    Log.Error(LogTag.Asset, "启动白名单 JSON 解析结果为 null，按未命中继续启动。");
                    return;
                }

                for (int i = 0; i < whitelist.Count; i++)
                {
                    if (string.Equals(whitelist[i]?.Trim(), deviceId, StringComparison.Ordinal))
                    {
                        m_StartupWhitelistMatchedPackages.Add(package);
                        Log.Debug(LogTag.Asset, "启动白名单命中：Package={0}, DeviceID={1}，仅切换 YooAsset 版本元数据地址。", package, deviceId);
                        return;
                    }
                }

                Log.Debug(LogTag.Asset, "启动白名单未命中，继续使用常规资源地址。Package={0}", package);
            }
            finally
            {
                m_StartupWhitelistCheckedPackages.Add(package);
            }
        }

        /// <summary>
        /// 判断当前配置是否允许执行启动白名单检查。
        /// </summary>
        private bool CanCheckStartupWhitelist()
        {
            if (m_Config == null || !m_Config.EnableHotfix || !m_Config.EnableStartupWhitelist)
            {
                return false;
            }

            AssetPlayMode effectiveMode = Application.isEditor
                ? m_Config.EditorPlayMode
                : m_Config.RuntimePlayMode;
            return effectiveMode == AssetPlayMode.HostPlayMode || effectiveMode == AssetPlayMode.WebPlayMode;
        }

        /// <summary>
        /// 下载一个白名单文件地址；失败、空地址或超时返回 null，真实外部取消继续向上传播。
        /// </summary>
        private async UniTask<string> TryDownloadStartupWhitelistAsync(string url, string sourceLabel, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(url))
            {
                Log.Debug(LogTag.Asset, "启动白名单文件拉取失败：Source={0}, URL=<empty>, Error=URL 未配置或无效", sourceLabel);
                return null;
            }
            if (m_HttpManager == null)
            {
                Log.Debug(LogTag.Asset, "启动白名单文件拉取失败：Source={0}, URL={1}, Error=IHttpManager 不可用", sourceLabel, url);
                return null;
            }

            HttpResponse response = null;
            try
            {
                response = await m_HttpManager.DownloadTextAsync(url, m_Config.CheckTimeout, null, ct);
                if (response == null || !response.IsSuccess || string.IsNullOrWhiteSpace(response.Body))
                {
                    Log.Debug(LogTag.Asset, "启动白名单文件拉取失败：Source={0}, URL={1}, Error={2}",
                        sourceLabel, url, response?.Error ?? "Empty response");
                    Log.Warning(LogTag.Asset, "{0}启动白名单文件请求失败，准备按未命中或备用地址继续。URL={1}, Error={2}",
                        sourceLabel, url, response?.Error ?? "Empty response");
                    return null;
                }

                Log.Debug(LogTag.Asset, "启动白名单文件拉取成功：Source={0}, URL={1}", sourceLabel, url);
                return response.Body;
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                Log.Debug(LogTag.Asset, "启动白名单文件拉取失败：Source={0}, URL={1}, Error=Timeout", sourceLabel, url);
                Log.Warning(LogTag.Asset, "{0}启动白名单文件请求超时，准备尝试备用地址。URL={1}", sourceLabel, url);
                return null;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                Log.Debug(LogTag.Asset, "启动白名单文件拉取失败：Source={0}, URL={1}, Error={2}",
                    sourceLabel, url, exception.Message);
                Log.Warning(LogTag.Asset, "{0}启动白名单文件请求异常，准备按未命中或备用地址继续。URL={1}, Error={2}",
                    sourceLabel, url, exception.Message);
                return null;
            }
            finally
            {
                if (response != null)
                {
                    ReferencePool.Put(response);
                }
            }
        }

        /// <summary>
        /// 解析启动期 URL 模板，仅接受 http/https 完整地址。
        /// </summary>
        private string ResolveStartupUrl(string template, string package)
        {
            if (string.IsNullOrWhiteSpace(template))
            {
                return null;
            }

            string resolved = Util.UrlTemplate.Resolve(
                template,
                Util.UrlTemplate.ResolveRuntimePlatform(),
                m_Config.Channel,
                package,
                Application.version);
            return Uri.TryCreate(resolved, UriKind.Absolute, out Uri uri)
                   && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
                ? resolved.TrimEnd('/')
                : null;
        }

        /// <summary>
        /// 按当前包的白名单命中状态创建 YooAsset 远端服务。
        /// </summary>
        private AssetRemoteService CreateRemoteService(string package)
        {
            bool matched = m_StartupWhitelistMatchedPackages.Contains(package);
            return new AssetRemoteService(
                m_Config.HostServerUrl,
                m_Config.HostServerUrlFallback,
                package,
                m_Config.Channel,
                matched ? m_Config.StartupWhitelistMetadataRootUrl : null,
                matched ? m_Config.StartupWhitelistMetadataRootUrlFallback : null);
        }

        /// <summary>
        /// 获取启动白名单设备 ID 缓存文件绝对路径。
        /// </summary>
        private static string GetAssetCheckDeviceIdFilePath()
        {
            return Path.Persistent.GetFileFullPath("Asset/asset-check-device-id.dat");
        }

        /// <summary>
        /// 读取明文 DeviceID；缺失、空白或异常均返回 false。
        /// </summary>
        private static bool TryLoadAssetCheckDeviceId(out string deviceId)
        {
            return TryLoadAssetCheckDeviceIdFromPath(GetAssetCheckDeviceIdFilePath(), out deviceId);
        }

        /// <summary>
        /// 从指定路径读取明文 DeviceID；供运行时固定路径与隔离测试共同复用。
        /// </summary>
        private static bool TryLoadAssetCheckDeviceIdFromPath(string path, out string deviceId)
        {
            deviceId = null;
            try
            {
                if (!File.Exists(path))
                {
                    return false;
                }

                string content = File.ReadAllText(path, Encoding.UTF8).Trim();
                if (string.IsNullOrEmpty(content))
                {
                    return false;
                }

                deviceId = content;
                return true;
            }
            catch (Exception exception)
            {
                Log.Warning(LogTag.Asset, "读取启动白名单 DeviceID 缓存失败，按无缓存继续。Error={0}", exception.Message);
                return false;
            }
        }

        /// <summary>
        /// 使用同目录临时文件原子写入明文 DeviceID；失败不向调用方抛出。
        /// </summary>
        private static void WriteAssetCheckDeviceId(string deviceId)
        {
            WriteAssetCheckDeviceIdToPath(GetAssetCheckDeviceIdFilePath(), deviceId);
        }

        /// <summary>
        /// 使用指定路径的同目录临时文件原子写入明文 DeviceID。
        /// </summary>
        private static void WriteAssetCheckDeviceIdToPath(string path, string deviceId)
        {
            string normalized = deviceId?.Trim();
            if (string.IsNullOrEmpty(normalized))
            {
                return;
            }

            string temporaryPath = path + ".tmp";
            try
            {
                string directory = System.IO.Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(temporaryPath, normalized, new UTF8Encoding(false));
                if (File.Exists(path))
                {
                    File.Replace(temporaryPath, path, null);
                }
                else
                {
                    File.Move(temporaryPath, path);
                }
            }
            catch (Exception exception)
            {
                Log.Warning(LogTag.Asset, "写入启动白名单 DeviceID 缓存失败，不影响启动。Error={0}", exception.Message);
                try
                {
                    if (File.Exists(temporaryPath))
                    {
                        File.Delete(temporaryPath);
                    }
                }
                catch
                {
                    // 清理临时文件失败不覆盖原始错误。
                }
            }
        }

        /// <summary>
        /// 获取一次白名单版本元数据请求应覆盖的候选地址数量。
        /// </summary>
        private static int GetMetadataRequestAttemptCount(AssetRemoteService remote, string package)
        {
            return Math.Max(1, remote.GetRemoteUrls($"{package}.version").Count);
        }

        /// <summary>
        /// 获取指定包的远端 URL 轮换策略；文件系统与操作失败收口共享同一实例。
        /// </summary>
        private AssetDownloadUrlPolicy GetOrCreateDownloadUrlPolicy(string package)
        {
            if (!m_DownloadUrlPolicies.TryGetValue(package, out AssetDownloadUrlPolicy policy))
            {
                policy = new AssetDownloadUrlPolicy(m_StartupWhitelistMatchedPackages.Contains(package));
                m_DownloadUrlPolicies.Add(package, policy);
            }
            return policy;
        }

        /// <summary>
        /// 白名单命中时按 YooAsset URL 策略轮换候选地址请求版本；未命中时保持单次请求。
        /// </summary>
        private async UniTask<RequestPackageVersionOperation> RequestPackageVersionWithFallbackAsync(
            ResourcePackage package,
            string packageName,
            CancellationToken ct)
        {
            int attempts = m_StartupWhitelistMatchedPackages.Contains(packageName)
                ? GetMetadataRequestAttemptCount(CreateRemoteService(packageName), packageName)
                : 1;
            RequestPackageVersionOperation operation = null;
            for (int i = 0; i < attempts; i++)
            {
                AssetDownloadUrlPolicy policy = GetOrCreateDownloadUrlPolicy(packageName);
                long failureGeneration = policy.FailureGeneration;
                policy.BeginMetadataRequest();
                operation = package.RequestPackageVersionAsync();
                await UniTask.WaitUntil(() => operation.IsDone, cancellationToken: ct);
                policy.CompleteMetadataRequest(operation.Status == EOperationStatus.Succeeded, operation.Error);
                if (operation.Status == EOperationStatus.Succeeded)
                {
                    return operation;
                }
                policy.AdvanceAfterOperationFailure(failureGeneration);
            }
            return operation;
        }

        /// <summary>
        /// 白名单命中时按 YooAsset URL 策略轮换候选地址加载清单；未命中时保持单次请求。
        /// </summary>
        private async UniTask<LoadPackageManifestOperation> LoadPackageManifestWithFallbackAsync(
            ResourcePackage package,
            string packageName,
            string packageVersion,
            CancellationToken ct)
        {
            int attempts = m_StartupWhitelistMatchedPackages.Contains(packageName)
                ? GetMetadataRequestAttemptCount(CreateRemoteService(packageName), packageName)
                : 1;
            LoadPackageManifestOperation operation = null;
            for (int i = 0; i < attempts; i++)
            {
                AssetDownloadUrlPolicy policy = GetOrCreateDownloadUrlPolicy(packageName);
                long failureGeneration = policy.FailureGeneration;
                policy.BeginMetadataRequest();
                operation = package.LoadPackageManifestAsync(new LoadPackageManifestOptions(packageVersion, 60));
                await UniTask.WaitUntil(() => operation.IsDone, cancellationToken: ct);
                policy.CompleteMetadataRequest(operation.Status == EOperationStatus.Succeeded, operation.Error);
                if (operation.Status == EOperationStatus.Succeeded)
                {
                    return operation;
                }
                policy.AdvanceAfterOperationFailure(failureGeneration);
            }
            return operation;
        }

        /// <summary>
        /// 取已注册的 YooAsset 包，未注册则抛异常。
        /// </summary>
        /// <param name="name">包名。</param>
        /// <returns>对应的 ResourcePackage 实例。</returns>
        private ResourcePackage GetPackage(string name)
        {
            if (m_Packages.TryGetValue(name, out var pkg) == false)
            {
                throw new InvalidOperationException($"Package '{name}' is not registered.");
            }
            return pkg;
        }

        /// <summary>
        /// 获取指定包「本地可启动版本记录文件」的绝对路径。
        /// </summary>
        /// <param name="name">包名。</param>
        /// <returns>persistentDataPath 下的记录文件绝对路径。</returns>
        private static string GetLocalBootableVersionFilePath(string name)
        {
            return Path.Persistent.GetFileFullPath($"Asset/{name}.version");
        }

        /// <summary>
        /// 记录指定包已满足启动下载范围的版本号，供下次启动远端不可达时离线回退。
        /// 写失败不抛异常，仅告警——记录失败不得中断启动流程。
        /// </summary>
        /// <param name="name">包名。</param>
        /// <param name="version">当前激活的包裹版本号。</param>
        private static void SaveLocalBootableVersion(string name, string version)
        {
            if (string.IsNullOrEmpty(version))
            {
                return;
            }
            try
            {
                string filePath = GetLocalBootableVersionFilePath(name);
                string dir = System.IO.Path.GetDirectoryName(filePath);
                if (string.IsNullOrEmpty(dir) == false && Directory.Exists(dir) == false)
                {
                    Directory.CreateDirectory(dir);
                }
                File.WriteAllText(filePath, version.Trim(), new UTF8Encoding(false));
            }
            catch (Exception ex)
            {
                Log.Warning(LogTag.Asset, Txt.Format("写入本地可启动版本记录失败（不影响启动）。Package={0}, Version={1}, Error={2}", name, version, ex.Message));
            }
        }

        /// <summary>
        /// 读取指定包的本地可启动版本记录。文件缺失、为空或读取异常均返回 false。
        /// </summary>
        /// <param name="name">包名。</param>
        /// <param name="version">输出读取到的版本号。</param>
        /// <returns>true 表示读到有效版本号。</returns>
        private static bool TryLoadLocalBootableVersion(string name, out string version)
        {
            version = null;
            try
            {
                string filePath = GetLocalBootableVersionFilePath(name);
                if (File.Exists(filePath) == false)
                {
                    return false;
                }
                string content = File.ReadAllText(filePath);
                if (string.IsNullOrWhiteSpace(content))
                {
                    return false;
                }
                version = content.Trim();
                return true;
            }
            catch (Exception ex)
            {
                Log.Warning(LogTag.Asset, Txt.Format("读取本地可启动版本记录失败。Package={0}, Error={1}", name, ex.Message));
                return false;
            }
        }

        /// <summary>
        /// 按当前启动下载策略检查激活 Manifest 的启动范围是否全部在本地可用。
        /// 空 LaunchHotfixTags 检查整包；非空时只检查对应 Tag 范围。
        /// </summary>
        private bool IsLaunchScopeReady(ResourcePackage package)
        {
            List<string> tags = m_Config?.LaunchHotfixTags;
            ResourceDownloaderOperation downloader = tags == null || tags.Count == 0
                ? package.CreateResourceDownloader(new ResourceDownloaderOptions(int.MaxValue, 0))
                : package.CreateResourceDownloader(new ResourceDownloaderOptions(tags.ToArray(), int.MaxValue, 0));
            return downloader.TotalDownloadCount <= 0;
        }

        /// <summary>
        /// 按 Inspector 配置的 EditorPlayMode / RuntimePlayMode 构造 YooAsset 初始化参数。
        /// 编辑器下使用 EditorPlayMode，非编辑器下使用 RuntimePlayMode，运行时不再二次覆盖。
        /// </summary>
        /// <param name="package">包名，用于 Host/Web 模式构建远端寻址服务。</param>
        /// <returns>对应运行模式的 InitializePackageOptions 实例。</returns>
        private InitializePackageOptions BuildPlayModeOptions(string package)
        {
            AssetPlayMode effectiveMode = Application.isEditor
                ? m_Config.EditorPlayMode
                : m_Config.RuntimePlayMode;

            switch (effectiveMode)
            {
                case AssetPlayMode.EditorSimulateMode:
                    return BuildEditorSimulateOptions(package);
                case AssetPlayMode.OfflinePlayMode:
                    return BuildOfflineOptions();
                case AssetPlayMode.HostPlayMode:
                    return BuildHostOptions(package);
                case AssetPlayMode.WebPlayMode:
                    return BuildWebOptions(package);
                default:
                    throw new InvalidOperationException($"Unsupported play mode: {effectiveMode}");
            }
        }

        /// <summary>
        /// 构造编辑器模拟模式初始化参数。
        /// 调用 EditorSimulateBuildInvoker.Build 执行模拟构建，将返回的
        /// PackageRootDirectory 注入 CreateDefaultEditorFileSystemParameters，
        /// 避免 EditorFileSystem package root is null or empty 异常。
        /// </summary>
        /// <param name="package">包名，传入 null/空串时走默认包名。</param>
        /// <returns>EditorSimulateModeOptions 实例。</returns>
        private InitializePackageOptions BuildEditorSimulateOptions(string package)
        {
#if UNITY_EDITOR
            string pkgName = ResolvePackageName(package);
            var buildResult = EditorSimulateBuildInvoker.Build(pkgName, (int)EBundleType.VirtualBundle);
            return new EditorSimulateModeOptions
            {
                EditorFileSystemParameters = FileSystemParameters.CreateDefaultEditorFileSystemParameters(buildResult.PackageRootDirectory),
            };
#else
            throw new PlatformNotSupportedException("EditorSimulateMode 仅支持 Unity Editor 平台。");
#endif
        }

        /// <summary>
        /// 构造离线运行模式初始化参数。
        /// </summary>
        /// <returns>OfflinePlayModeOptions 实例。</returns>
        private InitializePackageOptions BuildOfflineOptions()
        {
            return new OfflinePlayModeOptions
            {
                BuiltinFileSystemParameters = FileSystemParameters.CreateDefaultBuiltinFileSystemParameters(),
            };
        }

        /// <summary>
        /// 构造联机运行模式初始化参数，含解密器注入。
        /// </summary>
        /// <param name="package">包名，用于构建远端 URL 模板。</param>
        /// <returns>HostPlayModeOptions 实例。</returns>
        private InitializePackageOptions BuildHostOptions(string package)
        {
            AssetRemoteService remote = CreateRemoteService(package);
            var cacheParams = FileSystemParameters.CreateDefaultSandboxFileSystemParameters(remote);
            cacheParams.AddParameter(EFileSystemParameter.DownloadUrlPolicy, GetOrCreateDownloadUrlPolicy(package));
            ApplyDecryptor(cacheParams);
            return new HostPlayModeOptions
            {
                BuiltinFileSystemParameters = FileSystemParameters.CreateDefaultBuiltinFileSystemParameters(),
                CacheFileSystemParameters = cacheParams,
            };
        }

        /// <summary>
        /// 构造 WebGL 运行模式初始化参数。
        /// </summary>
        /// <param name="package">包名，用于构建远端 URL 模板。</param>
        /// <returns>WebPlayModeOptions 实例。</returns>
        private InitializePackageOptions BuildWebOptions(string package)
        {
            AssetRemoteService remote = CreateRemoteService(package);
            var remoteParams = FileSystemParameters.CreateDefaultWebRemoteFileSystemParameters(remote);
            remoteParams.AddParameter(EFileSystemParameter.DownloadUrlPolicy, GetOrCreateDownloadUrlPolicy(package));
            return new WebPlayModeOptions
            {
                WebServerFileSystemParameters = FileSystemParameters.CreateDefaultWebServerFileSystemParameters(),
                WebRemoteFileSystemParameters = remoteParams,
            };
        }

        /// <summary>
        /// 按解密器类型构造解密器实例。
        /// </summary>
        /// <param name="type">配置指定的解密器类型枚举。</param>
        /// <returns>解密器实例；None 时返回 null。</returns>
        private object CreateDecryptor(AssetDecryptorType type)
        {
            switch (type)
            {
                case AssetDecryptorType.None:
                    return null;
                case AssetDecryptorType.OffsetBundleDecryptor:
                    return new OffsetBundleDecryptor();
                default:
                    throw new InvalidOperationException($"Unsupported decryptor type: {type}");
            }
        }

        /// <summary>
        /// 把解密器注入沙盒文件系统参数。
        /// IBundleMemoryDecryptor 走备用解密通道；
        /// IBundleDecryptor（如 OffsetBundleDecryptor）走标准解密通道。
        /// </summary>
        /// <param name="parameters">沙盒文件系统参数，将被就地修改。</param>
        private void ApplyDecryptor(FileSystemParameters parameters)
        {
            if (m_Decryptor == null)
            {
                return;
            }
            if (m_Decryptor is IBundleMemoryDecryptor)
            {
                parameters.AddParameter(EFileSystemParameter.AssetbundleFallbackDecryptor, m_Decryptor);
            }
            else if (m_Decryptor is IBundleDecryptor)
            {
                parameters.AddParameter(EFileSystemParameter.AssetbundleDecryptor, m_Decryptor);
            }
        }

        /// <summary>
        /// Editor 下加载真实 AssetBundle 时，将 bundle 内 shader 引用重绑到当前 Editor 可用的同名 shader。
        /// 这只服务非 EditorSimulateMode 的编辑器预览，Player 运行时保持 AssetBundle 原始 shader 引用。
        /// </summary>
        /// <param name="asset">刚加载完成的主资源。</param>
        private void RepairLoadedAssetShadersForEditor(UnityEngine.Object asset)
        {
#if UNITY_EDITOR
            if (m_Config == null || m_Config.EditorPlayMode == AssetPlayMode.EditorSimulateMode || asset == null)
            {
                return;
            }

            RepairObjectShadersForEditor(asset);
#endif
        }

        /// <summary>
        /// Editor 下批量修正刚加载完成的资源集合 shader 引用。
        /// </summary>
        /// <param name="assets">刚加载完成的资源集合。</param>
        private void RepairLoadedAssetShadersForEditor(IReadOnlyList<UnityEngine.Object> assets)
        {
#if UNITY_EDITOR
            if (m_Config == null || m_Config.EditorPlayMode == AssetPlayMode.EditorSimulateMode || assets == null)
            {
                return;
            }

            for (int i = 0; i < assets.Count; i++)
            {
                RepairObjectShadersForEditor(assets[i]);
            }
#endif
        }

#if UNITY_EDITOR
        /// <summary>
        /// 对常见资源类型做局部 shader 重绑，避免跨平台 bundle shader 在 Editor HostPlayMode 下渲染为洋红色。
        /// </summary>
        /// <param name="asset">待修正资源。</param>
        private void RepairObjectShadersForEditor(UnityEngine.Object asset)
        {
            switch (asset)
            {
                case null:
                    return;
                case Material material:
                    RepairMaterialShaderForEditor(material);
                    return;
                case TMPro.TMP_FontAsset fontAsset:
                    RepairMaterialShaderForEditor(fontAsset.material);
                    return;
                case GameObject gameObject:
                    RepairGameObjectShadersForEditor(gameObject);
                    return;
            }
        }

        /// <summary>
        /// 修正 GameObject 资源内部 Renderer 与 TMP FontAsset 的 shader 引用。
        /// </summary>
        /// <param name="gameObject">待修正的 GameObject 资源。</param>
        private void RepairGameObjectShadersForEditor(GameObject gameObject)
        {
            Renderer[] renderers = gameObject.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Material[] materials = renderers[i].sharedMaterials;
                for (int j = 0; j < materials.Length; j++)
                {
                    RepairMaterialShaderForEditor(materials[j]);
                }
            }

            TMPro.TMP_Text[] tmpTexts = gameObject.GetComponentsInChildren<TMPro.TMP_Text>(true);
            for (int i = 0; i < tmpTexts.Length; i++)
            {
                RepairMaterialShaderForEditor(tmpTexts[i].fontSharedMaterial);

                if (tmpTexts[i].font != null)
                {
                    RepairMaterialShaderForEditor(tmpTexts[i].font.material);
                }
            }
        }

        /// <summary>
        /// 将 AssetBundle 反序列化出的 shader 对象替换为当前 Editor 进程中的同名 shader。
        /// </summary>
        /// <param name="material">待修正材质。</param>
        private void RepairMaterialShaderForEditor(Material material)
        {
            if (material == null || material.shader == null)
            {
                return;
            }

            Shader editorShader = Shader.Find(material.shader.name);
            if (editorShader != null && !ReferenceEquals(editorShader, material.shader))
            {
                material.shader = editorShader;
            }
        }
#endif
    }
}
