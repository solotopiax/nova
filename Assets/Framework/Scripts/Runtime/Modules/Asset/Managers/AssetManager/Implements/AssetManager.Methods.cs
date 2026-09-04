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
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using YooAsset;

namespace NovaFramework.Runtime
{
    internal sealed partial class AssetManager : AssetManagerBase
    {
        [Serializable]
        private sealed class LocalBootableManifestIdentity
        {
            public int SchemaVersion = 2;
            public string PackageVersion;
            public string PackageFilePrefix;
        }

        private readonly struct StartupWhitelistDownloadResult
        {
            public StartupWhitelistDownloadResult(bool succeeded, string body, int statusCode, string error)
            {
                Succeeded = succeeded;
                Body = body;
                StatusCode = statusCode;
                Error = error;
            }

            public bool Succeeded { get; }
            public string Body { get; }
            public int StatusCode { get; }
            public string Error { get; }
        }

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
        /// 获取指定包的版本元数据编排互斥门。
        /// </summary>
        private SemaphoreSlim GetPackageMetadataGate(string package)
        {
            if (m_PackageMetadataGates.TryGetValue(package, out SemaphoreSlim metadataGate) == false)
            {
                metadataGate = new SemaphoreSlim(1, 1);
                m_PackageMetadataGates.Add(package, metadataGate);
            }

            return metadataGate;
        }

        /// <summary>
        /// 判断当前模式是否允许请求远端资源版本。
        /// Host 模式允许；Offline/EditorSimulate 直接返回 null。
        /// </summary>
        private bool CanRequestLatestPackageVersion(string package)
        {
            if (m_Config == null)
            {
                return false;
            }

            AssetPlayMode effectiveMode = Application.isEditor
                ? m_Config.EditorPlayMode
                : m_Config.RuntimePlayMode;
            return effectiveMode == AssetPlayMode.HostPlayMode;
        }

        /// <summary>
        /// 确保包已完成 YooAsset 初始化；版本查询和清单加载共用该前置步骤。
        /// </summary>
        private async UniTask EnsurePackageInitializedAsync(ResourcePackage package, string packageName, CancellationToken ct)
        {
            // YooAsset 在已初始化后重复 InitializePackageAsync 会抛 "already initialized"，
            // 因此仅在未成功初始化时执行，允许启动流程中断后安全重入。
            if (package.InitializeStatus == EOperationStatus.Succeeded)
            {
                return;
            }

            await CheckStartupWhitelistAsync(packageName, ct);
            InitializePackageOptions options = BuildPlayModeOptions(packageName);
            var initOp = package.InitializePackageAsync(options);
            await UniTask.WaitUntil(() => initOp.IsDone, cancellationToken: ct);
            if (initOp.Status != EOperationStatus.Succeeded)
            {
                throw new InvalidOperationException($"InitializePackageAsync failed: {initOp.Error}");
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

                HttpFallbackExecutionPlan fallbackPlan = BuildStartupWhitelistPlan(
                    package, primaryUrl, fallbackUrl);
                HttpFallbackExecutionCursor cursor = fallbackPlan.CreateCursor();
                bool shouldTrack = m_Config.StartupWhitelistEnableUWRTracks
                                   && m_HttpManager is IPhysicalHttpManager;
                string chainId = UwrNetworkTelemetry.CreateChainId();
                string downloadOperationId = UwrNetworkTelemetry.CreateChainId();
                Stopwatch chainStopwatch = Stopwatch.StartNew();
                var firstStep = new HttpFallbackStep(
                    fallbackPlan.Candidates[0], 0, 0, 0, fallbackPlan.CandidateCount, 0L);
                UwrNetworkTelemetry.TrackAssetStart(
                    shouldTrack, chainId, firstStep.Candidate.Url, m_Config.StartupWhitelistCheckTimeout,
                    fallbackPlan, firstStep, downloadOperationId, package, "startup_whitelist");
                List<string> whitelist = null;
                bool hasValidWhitelist = false;
                int attemptsStarted = 0;
                while (cursor.TryBeginNext(out HttpFallbackStep step))
                {
                    string url = step.Candidate.Url;
                    string sourceLabel = string.Equals(url, primaryUrl, StringComparison.OrdinalIgnoreCase)
                        ? "主"
                        : "备用";
                    Stopwatch sendStopwatch = Stopwatch.StartNew();
                    attemptsStarted++;
                    StartupWhitelistDownloadResult result;
                    try
                    {
                        result = await TryDownloadStartupWhitelistAsync(url, sourceLabel, ct);
                    }
                    catch (OperationCanceledException)
                    {
                        cursor.Cancel();
                        UwrNetworkTelemetry.TrackAssetError(
                            shouldTrack, chainId, url, m_Config.StartupWhitelistCheckTimeout, fallbackPlan, step,
                            sendStopwatch.ElapsedMilliseconds, 0L, "Cancelled", "request_aborted_by_client",
                            downloadOperationId, package, "startup_whitelist");
                        UwrNetworkTelemetry.TrackAssetEnd(
                            shouldTrack, chainId, url, m_Config.StartupWhitelistCheckTimeout, fallbackPlan, step,
                            attemptsStarted, sendStopwatch.ElapsedMilliseconds, chainStopwatch.ElapsedMilliseconds,
                            false, 0L, "Cancelled", "request_aborted_by_client",
                            downloadOperationId, package, "startup_whitelist");
                        throw;
                    }
                    if (result.Succeeded && TryParseStartupWhitelist(result.Body, out whitelist))
                    {
                        hasValidWhitelist = true;
                        cursor.CompleteCurrent();
                        m_StartupWhitelistPreferenceStore.MarkSuccess(
                            GetStartupWhitelistPreferenceScope(package),
                            step.Candidate.EndpointId);
                        UwrNetworkTelemetry.TrackAssetEnd(
                            shouldTrack, chainId, url, m_Config.StartupWhitelistCheckTimeout, fallbackPlan, step,
                            attemptsStarted, sendStopwatch.ElapsedMilliseconds, chainStopwatch.ElapsedMilliseconds,
                            true, result.StatusCode, null, null,
                            downloadOperationId, package, "startup_whitelist");
                        break;
                    }
                    string leafErrorCode = result.Succeeded
                        ? "content_verification_failed"
                        : null;
                    UwrNetworkTelemetry.TrackAssetError(
                        shouldTrack, chainId, url, m_Config.StartupWhitelistCheckTimeout, fallbackPlan, step,
                        sendStopwatch.ElapsedMilliseconds, result.StatusCode, result.Error, leafErrorCode,
                        downloadOperationId, package, "startup_whitelist");
                    if (!result.Succeeded
                        && !AssetDownloadUrlPolicy.IsRetryableAssetError(url, result.StatusCode))
                    {
                        cursor.CompleteCurrent();
                        UwrNetworkTelemetry.TrackAssetEnd(
                            shouldTrack, chainId, url, m_Config.StartupWhitelistCheckTimeout, fallbackPlan, step,
                            attemptsStarted, sendStopwatch.ElapsedMilliseconds, chainStopwatch.ElapsedMilliseconds,
                            false, result.StatusCode, result.Error, leafErrorCode,
                            downloadOperationId, package, "startup_whitelist");
                        Log.Warning(LogTag.Asset,
                            "启动白名单收到不可重试响应，停止主备链。Package={0}, URL={1}, HttpCode={2}, Error={3}",
                            package, url, result.StatusCode, result.Error);
                        break;
                    }
                    cursor.RejectCurrent();
                    if (cursor.State == HttpFallbackExecutionState.Exhausted)
                    {
                        UwrNetworkTelemetry.TrackAssetEnd(
                            shouldTrack, chainId, url, m_Config.StartupWhitelistCheckTimeout, fallbackPlan, step,
                            attemptsStarted, sendStopwatch.ElapsedMilliseconds, chainStopwatch.ElapsedMilliseconds,
                            false, result.StatusCode, result.Error, leafErrorCode,
                            downloadOperationId, package, "startup_whitelist");
                    }
                }
                if (!hasValidWhitelist)
                {
                    Log.Debug(LogTag.Asset, "启动白名单文件拉取或内容校验失败：Source=全部候选, Package={0}", package);
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
        /// 校验并解析启动白名单内容；传输成功但内容无效时允许调用方继续尝试备用地址。
        /// </summary>
        private static bool TryParseStartupWhitelist(string body, out List<string> whitelist)
        {
            whitelist = null;
            if (string.IsNullOrWhiteSpace(body))
            {
                return false;
            }

            try
            {
                whitelist = Util.Json.Deserialize<List<string>>(body);
                if (whitelist != null)
                {
                    return true;
                }

                Log.Error(LogTag.Asset, "启动白名单 JSON 解析结果为 null，准备尝试备用地址。");
            }
            catch (Exception exception)
            {
                Log.Error(LogTag.Asset, "启动白名单 JSON 解析失败，准备尝试备用地址。Error={0}", exception.Message);
            }

            return false;
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
            return effectiveMode == AssetPlayMode.HostPlayMode;
        }

        /// <summary>
        /// 下载一个白名单文件地址；失败、空地址或超时返回失败结果，真实外部取消继续向上传播。
        /// </summary>
        private async UniTask<StartupWhitelistDownloadResult> TryDownloadStartupWhitelistAsync(
            string url, string sourceLabel, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(url))
            {
                Log.Debug(LogTag.Asset, "启动白名单文件拉取失败：Source={0}, URL=<empty>, Error=URL 未配置或无效", sourceLabel);
                return new StartupWhitelistDownloadResult(false, null, 0, "URL 未配置或无效");
            }
            if (m_HttpManager == null)
            {
                Log.Debug(LogTag.Asset, "启动白名单文件拉取失败：Source={0}, URL={1}, Error=IHttpManager 不可用", sourceLabel, url);
                return new StartupWhitelistDownloadResult(false, null, 0, "IHttpManager 不可用");
            }

            HttpResponse response = null;
            try
            {
                response = m_HttpManager is IPhysicalHttpManager physicalHttpManager
                    ? await physicalHttpManager.GetPhysicalAsync(url, m_Config.StartupWhitelistCheckTimeout, null, ct)
                    : await m_HttpManager.DownloadTextAsync(url, m_Config.StartupWhitelistCheckTimeout, null, ct);
                if (response == null || !response.IsSuccess || string.IsNullOrWhiteSpace(response.Body))
                {
                    int statusCode = response?.StatusCode ?? 0;
                    string error = response?.Error ?? "Empty response";
                    Log.Debug(LogTag.Asset, "启动白名单文件拉取失败：Source={0}, URL={1}, Error={2}",
                        sourceLabel, url, error);
                    Log.Warning(LogTag.Asset, "{0}启动白名单文件请求失败，准备按未命中或备用地址继续。URL={1}, Error={2}",
                        sourceLabel, url, error);
                    return new StartupWhitelistDownloadResult(false, null, statusCode, error);
                }

                Log.Debug(LogTag.Asset, "启动白名单文件拉取成功：Source={0}, URL={1}", sourceLabel, url);
                return new StartupWhitelistDownloadResult(true, response.Body, response.StatusCode, null);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                Log.Debug(LogTag.Asset, "启动白名单文件拉取失败：Source={0}, URL={1}, Error=Timeout", sourceLabel, url);
                Log.Warning(LogTag.Asset, "{0}启动白名单文件请求超时，准备尝试备用地址。URL={1}", sourceLabel, url);
                return new StartupWhitelistDownloadResult(false, null, 0, "Timeout");
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
                return new StartupWhitelistDownloadResult(false, null, 0, exception.Message);
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
        /// 按 Asset 配置构造启动白名单的完整主备、轮次与重试执行计划。
        /// </summary>
        private HttpFallbackExecutionPlan BuildStartupWhitelistPlan(
            string package, string primaryUrl, string fallbackUrl)
        {
            string scope = GetStartupWhitelistPreferenceScope(package);
            HttpFallbackPreferenceSnapshot preference = m_StartupWhitelistPreferenceStore.Capture(scope);
            var policy = new HttpFallbackPolicy(
                Math.Max(1, m_Config.StartupWhitelistFallbackRoundCount),
                Math.Max(0, m_Config.StartupWhitelistRetryRequestCount),
                m_Config.StartupWhitelistPreferLastSuccessfulHost);
            HttpFallbackExecutionPlan plan = HttpFallbackPlanner.Build(
                new[] { primaryUrl, fallbackUrl }, policy, preference);
            if (preference.HasValue && !PlanContainsEndpoint(plan, preference.EndpointId))
            {
                m_StartupWhitelistPreferenceStore.ClearIfUnchanged(preference);
                plan = HttpFallbackPlanner.Build(new[] { primaryUrl, fallbackUrl }, policy);
            }
            return plan;
        }

        /// <summary>
        /// 判断执行计划是否仍包含最近成功记录指向的候选端点。
        /// </summary>
        private static bool PlanContainsEndpoint(HttpFallbackExecutionPlan plan, string endpointId)
        {
            for (int i = 0; i < plan.CandidateCount; i++)
            {
                if (string.Equals(plan.Candidates[i].EndpointId, endpointId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 生成启动白名单最近成功域名的包级隔离键。
        /// </summary>
        private static string GetStartupWhitelistPreferenceScope(string package)
        {
            return $"asset:whitelist:{package}";
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
        /// 把指定包的逻辑重试次数换算成 YooAsset Bundle 下载器使用的物理重试次数。
        /// </summary>
        private int GetBundlePhysicalRetryCount(string package, int logicalRetryCount)
        {
            AssetRemoteService remote = CreateRemoteService(package);
            int candidateCount = Math.Max(1, remote.GetRemoteUrls("__nova_bundle_retry_probe__.bundle").Count);
            return AssetDownloadUrlPolicy.CalculatePhysicalRetryCount(
                candidateCount,
                m_Config.FallbackRoundCount,
                logicalRetryCount);
        }

        /// <summary>
        /// 获取指定包的远端 URL 轮换策略；文件系统与操作失败收口共享同一实例。
        /// </summary>
        private AssetDownloadUrlPolicy GetOrCreateDownloadUrlPolicy(string package)
        {
            if (!m_DownloadUrlPolicies.TryGetValue(package, out AssetDownloadUrlPolicy policy))
            {
                policy = new AssetDownloadUrlPolicy(
                    m_StartupWhitelistMatchedPackages.Contains(package),
                    m_Config.FallbackRoundCount,
                    m_Config.RetryDownloadCount,
                    m_Config.PreferLastSuccessfulHost,
                    m_Config.EnableUWRTracks,
                    package,
                    m_Config.CheckTimeout,
                    GetBundleRequestTimeout(),
                    m_Config.ManifestRequestTimeout);
                m_DownloadUrlPolicies.Add(package, policy);
            }
            return policy;
        }

        /// <summary>
        /// 按 YooAsset URL 策略轮换候选地址请求版本，确保任意包的主地址失败后都会实际请求备用地址。
        /// </summary>
        private async UniTask<RequestPackageVersionOperation> RequestPackageVersionWithFallbackAsync(
            ResourcePackage package,
            string packageName,
            CancellationToken ct)
        {
            RequestPackageVersionOperation operation = null;
            while (true)
            {
                AssetDownloadUrlPolicy policy = GetOrCreateDownloadUrlPolicy(packageName);
                policy.BeginMetadataRequest();
                operation = package.RequestPackageVersionAsync(
                    new RequestPackageVersionOptions(true, m_Config.CheckTimeout));
                await UniTask.WaitUntil(() => operation.IsDone, cancellationToken: ct);
                bool shouldRetry = policy.CompleteMetadataRequest(
                    operation.Status == EOperationStatus.Succeeded, operation.Error);
                if (operation.Status == EOperationStatus.Succeeded || !shouldRetry)
                {
                    return operation;
                }
            }
        }

        /// <summary>
        /// 按 YooAsset URL 策略轮换候选地址加载清单，确保任意包的主地址失败后都会实际请求备用地址。
        /// </summary>
        private async UniTask<LoadPackageManifestOperation> LoadPackageManifestWithFallbackAsync(
            ResourcePackage package,
            string packageName,
            string packageVersion,
            CancellationToken ct)
        {
            LoadPackageManifestOperation operation = null;
            while (true)
            {
                AssetDownloadUrlPolicy policy = GetOrCreateDownloadUrlPolicy(packageName);
                policy.BeginMetadataRequest();
                operation = package.LoadPackageManifestAsync(
                    new LoadPackageManifestOptions(packageVersion, m_Config.ManifestRequestTimeout));
                await UniTask.WaitUntil(() => operation.IsDone, cancellationToken: ct);
                bool shouldRetry = policy.CompleteMetadataRequest(
                    operation.Status == EOperationStatus.Succeeded, operation.Error);
                if (operation.Status == EOperationStatus.Succeeded || !shouldRetry)
                {
                    return operation;
                }
            }
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
        /// 记录指定包已满足启动下载范围的清单身份，供下次启动远端不可达时离线回退。
        /// 同时保存资源版本与文件名前缀，避免覆盖安装后使用新前缀查找旧清单。
        /// 写失败不抛异常，仅告警——记录失败不得中断启动流程。
        /// </summary>
        /// <param name="name">包名。</param>
        /// <param name="version">当前激活的包裹版本号。</param>
        /// <param name="packageFilePrefix">当前清单文件使用的前缀。</param>
        private static void SaveLocalBootableVersion(string name, string version, string packageFilePrefix)
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
                var identity = new LocalBootableManifestIdentity
                {
                    PackageVersion = version.Trim(),
                    PackageFilePrefix = packageFilePrefix ?? string.Empty,
                };
                File.WriteAllText(filePath, JsonUtility.ToJson(identity), new UTF8Encoding(false));
            }
            catch (Exception ex)
            {
                Log.Warning(LogTag.Asset, Txt.Format("写入本地可启动版本记录失败（不影响启动）。Package={0}, Version={1}, Error={2}", name, version, ex.Message));
            }
        }

        /// <summary>
        /// 读取指定包的本地可启动清单身份。
        /// 兼容旧版纯版本号记录；旧记录的前缀返回 null，由 YooAsset 从缓存文件名中安全解析。
        /// </summary>
        /// <param name="name">包名。</param>
        /// <param name="version">输出读取到的版本号。</param>
        /// <param name="packageFilePrefix">输出清单文件前缀；null 表示旧记录尚未保存前缀。</param>
        /// <returns>true 表示读到有效版本号。</returns>
        private static bool TryLoadLocalBootableVersion(
            string name,
            out string version,
            out string packageFilePrefix)
        {
            version = null;
            packageFilePrefix = null;
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
                string normalized = content.Trim();
                if (normalized.StartsWith("{", StringComparison.Ordinal))
                {
                    LocalBootableManifestIdentity identity = JsonUtility.FromJson<LocalBootableManifestIdentity>(normalized);
                    if (identity == null
                        || identity.SchemaVersion != 2
                        || string.IsNullOrWhiteSpace(identity.PackageVersion))
                    {
                        return false;
                    }

                    version = identity.PackageVersion.Trim();
                    packageFilePrefix = identity.PackageFilePrefix ?? string.Empty;
                    return true;
                }

                version = normalized;
                return true;
            }
            catch (Exception ex)
            {
                Log.Warning(LogTag.Asset, Txt.Format("读取本地可启动版本记录失败。Package={0}, Error={1}", name, ex.Message));
                return false;
            }
        }

        /// <summary>
        /// 将已记录的历史前缀清单映射为当前前缀文件，使 YooAsset 无需修改即可加载覆盖安装前的缓存清单。
        /// 旧版纯版本号记录会从 ManifestFiles 中查找唯一或内容一致的清单文件对。
        /// </summary>
        private static bool TryPrepareLocalBootableManifest(
            string name,
            string version,
            string recordedPrefix,
            out string error)
        {
            error = null;
            try
            {
                string manifestRoot = GetYooAssetManifestFilesRoot(name);
                string currentPrefix = GetCurrentPackageFilePrefix(name);
                string currentHashPath = System.IO.Path.Combine(
                    manifestRoot, BuildPackageMetadataFileName(currentPrefix, name, version, ".hash"));
                string currentManifestPath = System.IO.Path.Combine(
                    manifestRoot, BuildPackageMetadataFileName(currentPrefix, name, version, ".bytes"));

                string sourcePrefix = recordedPrefix;
                if (sourcePrefix == null
                    && TryResolveLegacyManifestPrefix(manifestRoot, name, version, out sourcePrefix, out error) == false)
                {
                    return false;
                }
                if (sourcePrefix.IndexOfAny(System.IO.Path.GetInvalidFileNameChars()) >= 0)
                {
                    error = $"缓存清单前缀包含非法文件名字符：{sourcePrefix}";
                    return false;
                }

                if (string.Equals(sourcePrefix, currentPrefix, StringComparison.Ordinal)
                    && File.Exists(currentHashPath)
                    && File.Exists(currentManifestPath))
                {
                    return true;
                }

                string sourceHashPath = System.IO.Path.Combine(
                    manifestRoot, BuildPackageMetadataFileName(sourcePrefix, name, version, ".hash"));
                string sourceManifestPath = System.IO.Path.Combine(
                    manifestRoot, BuildPackageMetadataFileName(sourcePrefix, name, version, ".bytes"));
                if (File.Exists(sourceHashPath) == false || File.Exists(sourceManifestPath) == false)
                {
                    error = $"缓存清单文件不完整。Prefix={sourcePrefix}, Version={version}";
                    return false;
                }

                Directory.CreateDirectory(manifestRoot);
                CopyFileAtomically(sourceHashPath, currentHashPath);
                CopyFileAtomically(sourceManifestPath, currentManifestPath);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        /// <summary>
        /// 从旧缓存文件名中解析历史前缀；候选内容冲突时拒绝猜测。
        /// </summary>
        private static bool TryResolveLegacyManifestPrefix(
            string manifestRoot,
            string name,
            string version,
            out string packageFilePrefix,
            out string error)
        {
            packageFilePrefix = null;
            error = null;
            if (Directory.Exists(manifestRoot) == false)
            {
                error = $"缓存清单目录不存在：{manifestRoot}";
                return false;
            }

            string noPrefixStem = $"{name}_{version}";
            string prefixedSuffix = $"_{noPrefixStem}";
            var candidates = new List<string>();
            foreach (string hashPath in Directory.GetFiles(manifestRoot, "*.hash"))
            {
                string stem = System.IO.Path.GetFileNameWithoutExtension(hashPath);
                string prefix;
                if (string.Equals(stem, noPrefixStem, StringComparison.Ordinal))
                {
                    prefix = string.Empty;
                }
                else if (stem.EndsWith(prefixedSuffix, StringComparison.Ordinal))
                {
                    prefix = stem.Substring(0, stem.Length - prefixedSuffix.Length);
                }
                else
                {
                    continue;
                }

                string manifestPath = System.IO.Path.Combine(
                    manifestRoot, BuildPackageMetadataFileName(prefix, name, version, ".bytes"));
                if (File.Exists(manifestPath))
                {
                    candidates.Add(prefix);
                }
            }

            if (candidates.Count == 0)
            {
                error = $"未找到 Package={name}, Version={version} 对应的缓存清单文件对。";
                return false;
            }

            string expectedHash = File.ReadAllText(System.IO.Path.Combine(
                manifestRoot, BuildPackageMetadataFileName(candidates[0], name, version, ".hash"))).Trim();
            for (int i = 1; i < candidates.Count; i++)
            {
                string candidateHash = File.ReadAllText(System.IO.Path.Combine(
                    manifestRoot, BuildPackageMetadataFileName(candidates[i], name, version, ".hash"))).Trim();
                if (string.Equals(expectedHash, candidateHash, StringComparison.Ordinal) == false)
                {
                    error = $"找到多个内容不同的历史缓存清单，无法安全判断应使用哪个前缀。Package={name}, Version={version}";
                    return false;
                }
            }

            string currentPrefix = GetCurrentPackageFilePrefix(name);
            packageFilePrefix = candidates.Contains(currentPrefix) ? currentPrefix : candidates[0];
            return true;
        }

        /// <summary>
        /// 获取 YooAsset 默认沙盒中的 ManifestFiles 目录。
        /// 路径规则与当前 YooAsset 1.1.0 本地包保持一致。
        /// </summary>
        private static string GetYooAssetManifestFilesRoot(string name)
        {
#if UNITY_EDITOR
            string cacheRoot = System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(Application.dataPath), "Library");
#elif UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX
            string cacheRoot = Application.dataPath;
#else
            string cacheRoot = Application.persistentDataPath;
#endif
            string yooFolderName = YooAssetConfiguration.GetYooFolderName();
            if (string.IsNullOrEmpty(yooFolderName) == false)
            {
                cacheRoot = System.IO.Path.Combine(cacheRoot, yooFolderName);
            }
            return System.IO.Path.Combine(cacheRoot, name, "ManifestFiles");
        }

        /// <summary>
        /// 从 YooAsset 当前版本文件名反解当前 PackageFilePrefix。
        /// </summary>
        private static string GetCurrentPackageFilePrefix(string name)
        {
            string versionFileName = YooAssetConfiguration.GetPackageVersionFileName(name);
            string noPrefixFileName = $"{name}.version";
            if (string.Equals(versionFileName, noPrefixFileName, StringComparison.Ordinal))
            {
                return string.Empty;
            }

            string suffix = $"_{noPrefixFileName}";
            if (versionFileName.EndsWith(suffix, StringComparison.Ordinal) == false)
            {
                throw new InvalidOperationException($"无法从 YooAsset 版本文件名解析 PackageFilePrefix：{versionFileName}");
            }
            return versionFileName.Substring(0, versionFileName.Length - suffix.Length);
        }

        /// <summary>
        /// 按 YooAsset 当前命名规则构造带显式前缀的 hash 或 bytes 文件名。
        /// </summary>
        private static string BuildPackageMetadataFileName(
            string packageFilePrefix,
            string name,
            string version,
            string extension)
        {
            string stem = string.IsNullOrEmpty(packageFilePrefix)
                ? $"{name}_{version}"
                : $"{packageFilePrefix}_{name}_{version}";
            return stem + extension;
        }

        /// <summary>
        /// 通过同目录临时文件原子覆盖目标文件，避免留下半写入清单。
        /// </summary>
        private static void CopyFileAtomically(string sourcePath, string destinationPath)
        {
            if (string.Equals(sourcePath, destinationPath, StringComparison.Ordinal))
            {
                return;
            }

            string temporaryPath = destinationPath + ".nova.tmp";
            try
            {
                File.Copy(sourcePath, temporaryPath, true);
                if (File.Exists(destinationPath))
                {
                    File.Replace(temporaryPath, destinationPath, null);
                }
                else
                {
                    File.Move(temporaryPath, destinationPath);
                }
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
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
        /// <param name="package">包名，用于 Host 模式构建远端寻址服务。</param>
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
            var buildResult = EditorSimulateBuildInvoker.Build(pkgName, (int)EBundleType.VirtualAssetBundle);
            return new EditorSimulateModeOptions
            {
                EditorFileSystemParameters = FileSystemParameters.CreateDefaultEditorFileSystemParameters(buildResult.PackageRootDirectory),
            };
#else
            throw new PlatformNotSupportedException("EditorSimulateMode 仅支持 Unity Editor 平台。");
#endif
        }

        /// <summary>
        /// 构造离线运行模式初始化参数；WebGL 使用网页服务器文件系统，其他平台使用内置文件系统。
        /// </summary>
        /// <returns>OfflinePlayModeOptions 实例。</returns>
        private InitializePackageOptions BuildOfflineOptions()
        {
#if UNITY_WEBGL
            var serverParams = FileSystemParameters.CreateDefaultWebServerFileSystemParameters();
            // YooAsset 的 Offline 参数槽接受通用 FileSystemParameters，WebGL 用 WebServer 替代不受支持的 Builtin。
            return new OfflinePlayModeOptions
            {
                BuiltinFileSystemParameters = serverParams,
            };
#else
            var builtinParams = FileSystemParameters.CreateDefaultBuiltinFileSystemParameters();
            return new OfflinePlayModeOptions
            {
                BuiltinFileSystemParameters = builtinParams,
            };
#endif
        }

        /// <summary>
        /// 构造联机运行模式初始化参数；WebGL 使用网页服务器与网络文件系统，其他平台使用内置与缓存文件系统。
        /// </summary>
        /// <param name="package">包名，用于构建远端 URL 模板。</param>
        /// <param name="copyBuiltinManifest">是否把当前安装包清单复制到 Sandbox，供内置回退后保持 HostPlayMode。</param>
        /// <returns>HostPlayModeOptions 实例。</returns>
        private InitializePackageOptions BuildHostOptions(string package, bool copyBuiltinManifest = false)
        {
            AssetRemoteService remote = CreateRemoteService(package);
            m_RemoteServices[package] = remote;
#if UNITY_WEBGL
            var serverParams = FileSystemParameters.CreateDefaultWebServerFileSystemParameters();
            var remoteParams = FileSystemParameters.CreateDefaultWebNetworkFileSystemParameters(remote);
            remoteParams.AddParameter(EFileSystemParameter.DownloadUrlPolicy, GetOrCreateDownloadUrlPolicy(package));
            remoteParams.AddParameter(EFileSystemParameter.DownloadRetryPolicy, GetOrCreateDownloadUrlPolicy(package));
            remoteParams.AddParameter(
                EFileSystemParameter.UnityWebRequestCreator,
                (UnityWebRequestCreator)CreateWebGLUnityWebRequest);
            // Web 文件系统不支持 Sandbox 的下载 watchdog，Bundle 改用单次请求总超时。
            // YooAsset 的 Host 两个参数槽接受通用 FileSystemParameters，顺序保持首包优先、网络兜底。
            return new HostPlayModeOptions
            {
                BuiltinFileSystemParameters = serverParams,
                CacheFileSystemParameters = remoteParams,
            };
#else
            var builtinParams = FileSystemParameters.CreateDefaultBuiltinFileSystemParameters();
            var cacheParams = FileSystemParameters.CreateDefaultSandboxFileSystemParameters(remote);
            if (copyBuiltinManifest)
            {
                // 内置清单回退仍保持 HostPlayMode；先把当前安装包清单复制进 Sandbox，供主文件系统本地激活。
                builtinParams.AddParameter(EFileSystemParameter.CopyBuiltinPackageManifest, true);
            }
            cacheParams.AddParameter(EFileSystemParameter.DownloadUrlPolicy, GetOrCreateDownloadUrlPolicy(package));
            cacheParams.AddParameter(EFileSystemParameter.DownloadRetryPolicy, GetOrCreateDownloadUrlPolicy(package));
            cacheParams.AddParameter(EFileSystemParameter.DownloadWatchdogTimeout, m_Config.IdleTimeout);
            return new HostPlayModeOptions
            {
                BuiltinFileSystemParameters = builtinParams,
                CacheFileSystemParameters = cacheParams,
            };
#endif
        }

        /// <summary>
        /// 返回当前平台实际用于 Bundle 请求埋点的超时值。
        /// </summary>
        private int GetBundleRequestTimeout()
        {
#if UNITY_WEBGL
            return Math.Max(1, m_Config.WebGLBundleRequestTimeout);
#else
            return m_Config.IdleTimeout;
#endif
        }

        /// <summary>
        /// 为 WebGL WebNetwork 请求创建带 Bundle 总超时默认值的 UnityWebRequest。
        /// 元数据请求随后会用 CheckTimeout 或 ManifestRequestTimeout 覆盖该默认值。
        /// </summary>
        private UnityWebRequest CreateWebGLUnityWebRequest(string url, string method)
        {
            var request = new UnityWebRequest(url, method);
            request.timeout = Math.Max(1, m_Config.WebGLBundleRequestTimeout);
            return request;
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
