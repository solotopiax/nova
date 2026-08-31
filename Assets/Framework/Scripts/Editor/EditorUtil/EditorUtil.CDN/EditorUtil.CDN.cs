/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.CDN.cs
 * author:    Codex
 * created:   2026/7/21
 * descrip:   CDN 部署路径解析与请求计划构建
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Cysharp.Threading.Tasks;
using NovaFramework.Runtime;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using IOPath = System.IO.Path;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        public static partial class CDN
        {
            private const int c_CloudflareBatchSize = 100;
            private const string c_AssetCheckWhitelistFileName = "VersionsCheckWhiteList.json";
            private const string c_CloudflarePurgeUrlPrefix = "https://api.cloudflare.com/client/v4/zones/";
            private const string c_CloudflarePurgeUrlSuffix = "/purge_cache";

            private static readonly Regex s_OssEndpointRegex = new(
                @"^oss-(?<region>[a-z0-9-]+)\.aliyuncs\.com$",
                RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

            private static readonly Regex s_CloudflareZoneIdRegex = new(
                @"^[A-Za-z0-9_-]+$",
                RegexOptions.CultureInvariant);

            /// <summary>
            /// Cloudflare purge API 响应的最小解析模型。
            /// </summary>
            [Serializable]
            private sealed class CloudflarePurgeResponse
            {
                public bool success;
            }

            /// <summary>
            /// 解析 oss://bucket/prefix 远端位置，并在发起请求前拒绝非法格式。
            /// </summary>
            /// <param name="value">待解析的 OSS URI。</param>
            /// <returns>Bucket 与规范化固定前缀。</returns>
            /// <exception cref="ArgumentException">URI 为空、Scheme 错误、Bucket 缺失或包含查询参数时抛出。</exception>
            internal static OssLocation ParseOssLocation(string value)
            {
                if (string.IsNullOrWhiteSpace(value) ||
                    !Uri.TryCreate(value.Trim(), UriKind.Absolute, out Uri uri) ||
                    !string.Equals(uri.Scheme, "oss", StringComparison.OrdinalIgnoreCase) ||
                    string.IsNullOrWhiteSpace(uri.Host) ||
                    !string.IsNullOrEmpty(uri.Query) ||
                    !string.IsNullOrEmpty(uri.Fragment))
                {
                    throw new ArgumentException("PresetOSSPath 必须使用 oss://bucket-name/fixed/prefix 格式。", nameof(value));
                }

                string prefix = NormalizeObjectKeyPart(uri.AbsolutePath);
                return new OssLocation(uri.Host, prefix);
            }

            /// <summary>
            /// 从标准阿里云 OSS 地域 Endpoint 推导 V4 签名所需 Region。
            /// </summary>
            /// <param name="endpoint">带或不带 HTTPS Scheme 的标准 OSS Endpoint。</param>
            /// <returns>地域标识，例如 cn-hangzhou。</returns>
            /// <exception cref="ArgumentException">Endpoint 不是标准地域域名时抛出。</exception>
            internal static string ParseRegion(string endpoint)
            {
                string candidate = endpoint?.Trim();
                if (string.IsNullOrEmpty(candidate))
                {
                    throw new ArgumentException("Endpoint 不能为空。", nameof(endpoint));
                }

                if (!candidate.Contains("://", StringComparison.Ordinal))
                {
                    candidate = "https://" + candidate;
                }

                if (!Uri.TryCreate(candidate, UriKind.Absolute, out Uri uri))
                {
                    throw new ArgumentException("Endpoint 不是有效 URL。", nameof(endpoint));
                }

                Match match = s_OssEndpointRegex.Match(uri.Host);
                if (!match.Success)
                {
                    throw new ArgumentException("Endpoint 必须是标准阿里云 OSS 地域域名。", nameof(endpoint));
                }

                return match.Groups["region"].Value.ToLowerInvariant();
            }

            /// <summary>
            /// 将固定前缀、可编辑后缀和本地相对路径拼为规范化 OSS Object Key。
            /// </summary>
            /// <param name="prefix">PresetOSSPath 中的固定前缀。</param>
            /// <param name="suffix">用户填写的远端目录后缀。</param>
            /// <param name="relativePath">本地文件相对部署目录的路径。</param>
            /// <returns>使用正斜杠且不含首尾分隔符的 Object Key。</returns>
            /// <exception cref="ArgumentException">相对文件路径为空时抛出。</exception>
            internal static string CombineObjectKey(string prefix, string suffix, string relativePath)
            {
                string filePart = NormalizeObjectKeyPart(relativePath);
                if (string.IsNullOrEmpty(filePart))
                {
                    throw new ArgumentException("本地文件相对路径不能为空。", nameof(relativePath));
                }

                return string.Join("/", new[]
                    {
                        NormalizeObjectKeyPart(prefix),
                        NormalizeObjectKeyPart(suffix),
                        filePart
                    }
                    .Where(part => !string.IsNullOrEmpty(part)));
            }

            /// <summary>
            /// 从已校验的资源上传计划派生本次允许清理的精确对象。
            /// 不清理整个目录，避免同目录下其他 PackageFilePrefix 分支被误删。
            /// </summary>
            internal static OssCleanupPlan BuildCleanupPlan(
                CDNEditorConfigs config,
                IReadOnlyList<OssUploadItem> uploadPlan,
                PlatformType platform,
                ChannelType channel,
                string package,
                string version)
            {
                return BuildCleanupPlanCore(
                    config,
                    uploadPlan);
            }

            /// <summary>
            /// 从已校验的白名单上传计划派生本次允许清理的精确对象。
            /// 不清理整个目录，避免同目录下其他 PackageFilePrefix 分支被误删。
            /// </summary>
            internal static OssCleanupPlan BuildAssetCheckWhitelistCleanupPlan(
                CDNEditorConfigs config,
                IReadOnlyList<OssUploadItem> uploadPlan,
                PlatformType platform,
                ChannelType channel,
                string package,
                string version)
            {
                return BuildCleanupPlanCore(
                    config,
                    uploadPlan);
            }

            private static OssCleanupPlan BuildCleanupPlanCore(
                CDNEditorConfigs config,
                IReadOnlyList<OssUploadItem> uploadPlan)
            {
                if (config == null) throw new ArgumentNullException(nameof(config));
                if (uploadPlan == null) throw new ArgumentNullException(nameof(uploadPlan));
                ParseOssLocation(config.PresetOSSPath);

                var exactObjectKeys = new List<string>();
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (OssUploadItem item in uploadPlan)
                {
                    if (!string.IsNullOrEmpty(item.ObjectKey) && seen.Add(item.ObjectKey))
                    {
                        exactObjectKeys.Add(item.ObjectKey);
                    }
                }

                return new OssCleanupPlan(exactObjectKeys, Array.Empty<string>());
            }

            /// <summary>
            /// 按 Asset 主机服务器 URL 的同一规则替换 CDN 路径占位符。
            /// </summary>
            internal static string ResolvePathPlaceholders(
                string template,
                PlatformType platform,
                string package,
                string version)
            {
                return ResolvePathPlaceholders(template, platform, ChannelType.None, package, version);
            }

            /// <summary>
            /// 按当前配置渠道替换 CDN 路径占位符。
            /// </summary>
            internal static string ResolvePathPlaceholders(
                string template,
                PlatformType platform,
                ChannelType channel,
                string package,
                string version)
            {
                if (template == null) return null;
                return template
                    .Replace("{Platform}", platform.ToString())
                    .Replace("{Channel}", channel.ToString())
                    .Replace("{Package}", package ?? string.Empty)
                    .Replace("{Version}", version ?? string.Empty);
            }

            /// <summary>
            /// 使用当前编辑器上下文解析 CDN 路径占位符。
            /// </summary>
            internal static string ResolveEditorPathPlaceholders(string template, PlatformType platform)
            {
                return ResolveEditorPathPlaceholders(template, platform, ChannelType.None);
            }

            /// <summary>
            /// 使用当前 ConfigWindow 平台与渠道解析 CDN 路径占位符。
            /// </summary>
            internal static string ResolveEditorPathPlaceholders(
                string template,
                PlatformType platform,
                ChannelType channel)
            {
                return ResolvePathPlaceholders(
                    template,
                    platform,
                    channel,
                    ResolveDefaultPackageName(),
                    Application.version);
            }

            /// <summary>
            /// 从 canonical Nova.prefab 的 AssetComponent 读取默认资源包名；空值回退包列表首项。
            /// </summary>
            internal static string ResolveDefaultPackageName()
            {
                return EditorUtil.Placeholder.ResolveDefaultPackageName();
            }

            /// <summary>
            /// 递归枚举本地目录并构建稳定排序的 OSS 上传计划。
            /// </summary>
            /// <param name="config">CDN 编辑态配置。</param>
            /// <param name="projectRoot">Unity 项目根绝对路径。</param>
            /// <returns>按相对路径升序排列的上传计划。</returns>
            /// <exception cref="ArgumentException">配置、项目根、本地目录或文件集合无效时抛出。</exception>
            internal static IReadOnlyList<OssUploadItem> BuildUploadPlan(CDNEditorConfigs config, string projectRoot)
            {
                return BuildUploadPlan(
                    config,
                    projectRoot,
                    PlatformType.None,
                    ChannelType.None,
                    string.Empty,
                    Application.version);
            }

            /// <summary>
            /// 使用指定的平台、资源包与版本上下文构建上传计划。
            /// </summary>
            internal static IReadOnlyList<OssUploadItem> BuildUploadPlan(
                CDNEditorConfigs config,
                string projectRoot,
                PlatformType platform,
                string package,
                string version)
            {
                return BuildUploadPlan(
                    config,
                    projectRoot,
                    platform,
                    ChannelType.None,
                    package,
                    version);
            }

            /// <summary>
            /// 使用指定的平台、渠道、资源包与版本上下文构建上传计划。
            /// </summary>
            internal static IReadOnlyList<OssUploadItem> BuildUploadPlan(
                CDNEditorConfigs config,
                string projectRoot,
                PlatformType platform,
                ChannelType channel,
                string package,
                string version)
            {
                return BuildUploadPlan(
                    config,
                    projectRoot,
                    platform,
                    channel,
                    package,
                    version,
                    string.Empty);
            }

            /// <summary>
            /// 使用显式 YooAsset PackageFilePrefix 构建上传计划。
            /// </summary>
            internal static IReadOnlyList<OssUploadItem> BuildUploadPlan(
                CDNEditorConfigs config,
                string projectRoot,
                PlatformType platform,
                ChannelType channel,
                string package,
                string version,
                string packageFilePrefix)
            {
                if (config == null) throw new ArgumentNullException(nameof(config));
                if (string.IsNullOrWhiteSpace(projectRoot))
                    throw new ArgumentException("项目根目录不能为空。", nameof(projectRoot));
                if (string.IsNullOrWhiteSpace(config.LocalDirectory))
                    throw new ArgumentException("本地目录不能为空。", nameof(config));

                string normalizedRoot = IOPath.GetFullPath(projectRoot);
                string localDirectory = ResolvePathPlaceholders(
                    config.LocalDirectory,
                    platform,
                    channel,
                    package,
                    version);
                if (config.AutoLinkLatestVersion)
                {
                    if (!TryResolveLatestPackageDirectory(
                            localDirectory,
                            normalizedRoot,
                            package,
                            packageFilePrefix,
                            out string latestDirectory,
                            out string resolveError))
                    {
                        throw new ArgumentException(resolveError, nameof(config));
                    }
                    localDirectory = latestDirectory;
                }
                string remotePathSuffix = ResolvePathPlaceholders(
                    config.RemotePathSuffix,
                    platform,
                    channel,
                    package,
                    version);
                string localRoot = IOPath.GetFullPath(IOPath.Combine(normalizedRoot, localDirectory));
                if (!IsPathInsideRoot(localRoot, normalizedRoot))
                    throw new ArgumentException($"本地目录必须位于 Unity 项目根目录内：{localRoot}", nameof(config));
                if (!Directory.Exists(localRoot))
                    throw new ArgumentException($"本地目录不存在：{localRoot}", nameof(config));
                if (TryFindReparsePointInTree(localRoot, normalizedRoot, out string reparsePoint))
                    throw new ArgumentException($"本地部署目录不允许包含符号链接或 junction：{reparsePoint}", nameof(config));

                OssLocation location = ParseOssLocation(config.PresetOSSPath);
                string[] files;
                if (!TryGetYooAssetRuntimeUploadFiles(
                        localRoot,
                        package,
                        packageFilePrefix,
                        out files,
                        out string yooAssetError))
                {
                    if (!string.IsNullOrEmpty(yooAssetError))
                        throw new ArgumentException(yooAssetError, nameof(config));
                    files = Directory.GetFiles(localRoot, "*", SearchOption.AllDirectories);
                }
                if (files.Length == 0)
                    throw new ArgumentException($"本地目录中没有可部署文件：{localRoot}", nameof(config));

                var plan = files
                    .Select(file => new
                    {
                        LocalPath = IOPath.GetFullPath(file),
                        RelativePath = IOPath.GetRelativePath(localRoot, file).Replace('\\', '/')
                    })
                    .OrderBy(item => item.RelativePath, StringComparer.Ordinal)
                    .Select(item => new OssUploadItem(
                        item.LocalPath,
                        CombineObjectKey(location.Prefix, remotePathSuffix, item.RelativePath)))
                    .ToList();

                if (!string.IsNullOrWhiteSpace(config.VersionCheckLocalFilePath) &&
                    !string.IsNullOrWhiteSpace(config.VersionCheckRemoteFilePath))
                {
                    string versionCheckLocalPath = ResolvePathPlaceholders(
                        config.VersionCheckLocalFilePath,
                        platform,
                        channel,
                        package,
                        version);
                    string versionCheckRemotePath = ResolvePathPlaceholders(
                        config.VersionCheckRemoteFilePath,
                        platform,
                        channel,
                        package,
                        version);
                    string versionCheckFullPath = IOPath.GetFullPath(
                        IOPath.Combine(normalizedRoot, versionCheckLocalPath));
                    if (!IsPathInsideRoot(versionCheckFullPath, normalizedRoot))
                        throw new ArgumentException($"版本检查本地文件必须位于 Unity 项目根目录内：{versionCheckFullPath}", nameof(config));
                    if (!File.Exists(versionCheckFullPath))
                        throw new ArgumentException($"版本检查本地文件不存在：{versionCheckFullPath}", nameof(config));
                    if (TryFindReparsePointInPath(versionCheckFullPath, normalizedRoot, out string versionCheckLink))
                        throw new ArgumentException($"版本检查本地文件不允许包含符号链接或 junction：{versionCheckLink}", nameof(config));

                    plan.Insert(0, new OssUploadItem(
                        versionCheckFullPath,
                        CombineObjectKey(location.Prefix, string.Empty, versionCheckRemotePath)));
                }

                return plan;
            }

            /// <summary>
            /// 构建启动白名单配置和三个 YooAsset 版本文件的独立上传计划。
            /// </summary>
            /// <param name="config">当前维度生效的 CDN 编辑配置。</param>
            /// <param name="projectRoot">Unity 项目根绝对路径。</param>
            /// <param name="generatedWhitelistFilePath">已生成的白名单 JSON 临时文件绝对路径。</param>
            /// <param name="platform">当前平台。</param>
            /// <param name="channel">当前渠道。</param>
            /// <param name="package">YooAsset 默认资源包名。</param>
            /// <param name="version">应用版本。</param>
            /// <returns>三个版本文件，以及配置目录有效时追加的白名单配置上传项。</returns>
            internal static IReadOnlyList<OssUploadItem> BuildAssetCheckWhitelistUploadPlan(
                CDNEditorConfigs config,
                string projectRoot,
                string generatedWhitelistFilePath,
                PlatformType platform,
                ChannelType channel,
                string package,
                string version)
            {
                return BuildAssetCheckWhitelistUploadPlan(
                    config,
                    projectRoot,
                    generatedWhitelistFilePath,
                    platform,
                    channel,
                    package,
                    version,
                    string.Empty);
            }

            /// <summary>
            /// 使用显式 YooAsset PackageFilePrefix 构建白名单版本文件上传计划。
            /// </summary>
            internal static IReadOnlyList<OssUploadItem> BuildAssetCheckWhitelistUploadPlan(
                CDNEditorConfigs config,
                string projectRoot,
                string generatedWhitelistFilePath,
                PlatformType platform,
                ChannelType channel,
                string package,
                string version,
                string packageFilePrefix)
            {
                if (config == null) throw new ArgumentNullException(nameof(config));
                if (string.IsNullOrWhiteSpace(projectRoot))
                    throw new ArgumentException("项目根目录不能为空。", nameof(projectRoot));
                string normalizedRoot = IOPath.GetFullPath(projectRoot);
                string versionRemoteDirectory = ResolvePathPlaceholders(
                    config.AssetCheckVersionRemoteDirectory,
                    platform,
                    channel,
                    package,
                    version);
                OssLocation location = ParseOssLocation(config.PresetOSSPath);
                var plan = new List<OssUploadItem>();
                if (TryResolveAssetCheckWhitelistRemoteFilePath(
                        config.AssetCheckWhitelistRemoteFilePath,
                        platform,
                        channel,
                        package,
                        version,
                        out string whitelistRemoteFilePath))
                {
                    if (string.IsNullOrWhiteSpace(generatedWhitelistFilePath) || !File.Exists(generatedWhitelistFilePath))
                        throw new ArgumentException($"白名单配置临时文件不存在：{generatedWhitelistFilePath}", nameof(generatedWhitelistFilePath));
                    plan.Add(new OssUploadItem(
                        IOPath.GetFullPath(generatedWhitelistFilePath),
                        CombineObjectKey(location.Prefix, string.Empty, whitelistRemoteFilePath)));
                }

                string manifestBytesLocalFilePath = config.AssetCheckManifestBytesLocalFilePath;
                string manifestHashLocalFilePath = config.AssetCheckManifestHashLocalFilePath;
                string packageVersionLocalFilePath = config.AssetCheckPackageVersionLocalFilePath;
                if (config.AutoLinkLatestAssetCheckVersionFiles)
                {
                    string configuredBytesPath = ResolvePathPlaceholders(
                        manifestBytesLocalFilePath,
                        platform,
                        channel,
                        package,
                        version);
                    if (!TryResolveLatestAssetCheckVersionFiles(
                            configuredBytesPath,
                            normalizedRoot,
                            package,
                            packageFilePrefix,
                            out manifestBytesLocalFilePath,
                            out manifestHashLocalFilePath,
                            out packageVersionLocalFilePath,
                            out string resolveError))
                    {
                        throw new ArgumentException(resolveError, nameof(config));
                    }
                }

                AddAssetCheckVersionFile(
                    plan,
                    normalizedRoot,
                    location.Prefix,
                    versionRemoteDirectory,
                    manifestBytesLocalFilePath,
                    ".bytes",
                    platform,
                    channel,
                    package,
                    version);
                AddAssetCheckVersionFile(
                    plan,
                    normalizedRoot,
                    location.Prefix,
                    versionRemoteDirectory,
                    manifestHashLocalFilePath,
                    ".hash",
                    platform,
                    channel,
                    package,
                    version);
                AddAssetCheckVersionFile(
                    plan,
                    normalizedRoot,
                    location.Prefix,
                    versionRemoteDirectory,
                    packageVersionLocalFilePath,
                    ".version",
                    platform,
                    channel,
                    package,
                    version);
                return plan;
            }

            /// <summary>
            /// 解析配置文件远端文件位置；空值、非 JSON 文件、绝对 URI、查询片段或父级路径均视为不可上传。
            /// </summary>
            private static bool TryResolveAssetCheckWhitelistRemoteFilePath(
                string template,
                PlatformType platform,
                ChannelType channel,
                string package,
                string version,
                out string remoteFilePath)
            {
                remoteFilePath = ResolvePathPlaceholders(template, platform, channel, package, version)?.Trim();
                if (string.IsNullOrEmpty(remoteFilePath) ||
                    Uri.TryCreate(remoteFilePath, UriKind.Absolute, out _) ||
                    remoteFilePath.IndexOfAny(new[] { '?', '#' }) >= 0 ||
                    !string.Equals(IOPath.GetExtension(remoteFilePath), ".json", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                string[] segments = remoteFilePath
                    .Replace('\\', '/')
                    .Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                return segments.Length > 0 && segments.All(segment => segment != "." && segment != "..");
            }

            /// <summary>
            /// 将设备 ID 列表清理为空白去除、空项过滤和首次出现顺序去重后的 JSON 字符串数组。
            /// </summary>
            /// <param name="deviceIDs">界面配置的原始设备 ID 列表。</param>
            /// <returns>可直接写入 VersionsCheckWhiteList.json 的 JSON。</returns>
            internal static string SerializeAssetCheckWhitelist(IReadOnlyList<string> deviceIDs)
            {
                if (deviceIDs == null) throw new ArgumentNullException(nameof(deviceIDs));
                var normalized = new List<string>();
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (string raw in deviceIDs)
                {
                    string value = raw?.Trim();
                    if (string.IsNullOrEmpty(value) || !seen.Add(value)) continue;
                    normalized.Add(value);
                }

                if (normalized.Count == 0)
                    throw new ArgumentException("白名单设备 ID 至少需要配置一项。", nameof(deviceIDs));
                return JsonConvert.SerializeObject(normalized, Formatting.Indented);
            }

            /// <summary>
            /// 创建仅供本次上传使用的白名单 JSON 临时文件。
            /// </summary>
            /// <param name="deviceIDs">界面配置的设备 ID 列表。</param>
            /// <returns>白名单临时文件绝对路径。</returns>
            internal static string CreateAssetCheckWhitelistTempFile(IReadOnlyList<string> deviceIDs)
            {
                string json = SerializeAssetCheckWhitelist(deviceIDs);
                string directory = IOPath.Combine(
                    IOPath.GetTempPath(),
                    "Nova",
                    "AssetCheckWhitelist",
                    Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(directory);
                string path = IOPath.Combine(directory, c_AssetCheckWhitelistFileName);
                File.WriteAllText(path, json, new UTF8Encoding(false));
                return path;
            }

            /// <summary>
            /// 删除白名单临时文件所在的本次部署目录；清理失败不遮蔽原部署结果。
            /// </summary>
            /// <param name="filePath">白名单临时文件绝对路径。</param>
            internal static void DeleteAssetCheckWhitelistTempFile(string filePath)
            {
                if (string.IsNullOrWhiteSpace(filePath)) return;
                try
                {
                    string directory = IOPath.GetDirectoryName(filePath);
                    if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
                        Directory.Delete(directory, true);
                }
                catch (Exception exception)
                {
                    Debug.LogWarning($"[CDN] 清理白名单部署临时目录失败：{exception.Message}");
                }
            }

            /// <summary>
            /// 解析英文逗号、分号或换行分隔的 Cloudflare 缓存 URL，并按首次出现顺序去重。
            /// </summary>
            /// <param name="value">缓存 URL 文本。</param>
            /// <returns>已校验的绝对 HTTP/HTTPS URL。</returns>
            /// <exception cref="ArgumentException">列表为空或包含非法 URL 时抛出。</exception>
            internal static IReadOnlyList<string> ParseCacheUrls(string value)
            {
                string[] parts = (value ?? string.Empty).Split(
                    new[] { ',', ';', '\r', '\n' },
                    StringSplitOptions.RemoveEmptyEntries);
                var result = new List<string>();
                var seen = new HashSet<string>(StringComparer.Ordinal);

                foreach (string part in parts)
                {
                    string url = part.Trim();
                    if (url.Length == 0) continue;
                    if (!Uri.TryCreate(url, UriKind.Absolute, out Uri uri) ||
                        (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                    {
                        throw new ArgumentException($"缓存路径不是有效 HTTP/HTTPS URL：{url}", nameof(value));
                    }

                    if (seen.Add(url)) result.Add(url);
                }

                if (result.Count == 0)
                    throw new ArgumentException("缓存路径不能为空。", nameof(value));
                return result;
            }

            /// <summary>
            /// 使用 Zone ID 构造 Cloudflare 官方缓存清理 API 地址。
            /// </summary>
            internal static string BuildCloudflarePurgeUrl(string zoneId)
            {
                string normalized = zoneId?.Trim();
                if (string.IsNullOrEmpty(normalized) || !s_CloudflareZoneIdRegex.IsMatch(normalized))
                    throw new ArgumentException("Cloudflare Zone ID 格式无效。", nameof(zoneId));
                return c_CloudflarePurgeUrlPrefix + normalized + c_CloudflarePurgeUrlSuffix;
            }

            /// <summary>
            /// 从旧版完整 PurgeURL 中提取 Zone ID，仅接受 Cloudflare 官方 API 地址形态。
            /// </summary>
            internal static string ExtractCloudflareZoneId(string purgeUrl)
            {
                string value = purgeUrl?.Trim();
                if (!Uri.TryCreate(value, UriKind.Absolute, out Uri uri) ||
                    uri.Scheme != Uri.UriSchemeHttps ||
                    !string.Equals(uri.Host, "api.cloudflare.com", StringComparison.OrdinalIgnoreCase) ||
                    !string.IsNullOrEmpty(uri.Query) ||
                    !string.IsNullOrEmpty(uri.Fragment) ||
                    !uri.AbsolutePath.StartsWith("/client/v4/zones/", StringComparison.Ordinal) ||
                    !uri.AbsolutePath.EndsWith(c_CloudflarePurgeUrlSuffix, StringComparison.Ordinal))
                {
                    throw new ArgumentException("Cloudflare Zone ID 不能为空，且旧 PurgeURL 无法迁移。", nameof(purgeUrl));
                }

                int start = "/client/v4/zones/".Length;
                int length = uri.AbsolutePath.Length - start - c_CloudflarePurgeUrlSuffix.Length;
                if (length <= 0)
                    throw new ArgumentException("旧 PurgeURL 中缺少 Cloudflare Zone ID。", nameof(purgeUrl));
                return ValidateAndNormalizeCloudflareZoneId(uri.AbsolutePath.Substring(start, length));
            }

            /// <summary>
            /// 将缓存 URL 按 Cloudflare 单请求上限拆成有序批次。
            /// </summary>
            /// <param name="urls">已校验的缓存 URL。</param>
            /// <param name="batchSize">每批最大条数，默认一百。</param>
            /// <returns>保持原顺序的 URL 批次。</returns>
            /// <exception cref="ArgumentOutOfRangeException">批大小小于一时抛出。</exception>
            internal static IReadOnlyList<IReadOnlyList<string>> BatchCacheUrls(
                IReadOnlyList<string> urls,
                int batchSize = c_CloudflareBatchSize)
            {
                if (batchSize < 1) throw new ArgumentOutOfRangeException(nameof(batchSize));
                if (urls == null) throw new ArgumentNullException(nameof(urls));

                var batches = new List<IReadOnlyList<string>>();
                for (int start = 0; start < urls.Count; start += batchSize)
                {
                    int count = Math.Min(batchSize, urls.Count - start);
                    var batch = new string[count];
                    for (int index = 0; index < count; index++)
                    {
                        batch[index] = urls[start + index];
                    }
                    batches.Add(batch);
                }
                return batches;
            }

            /// <summary>
            /// 使用注入的单文件执行器顺序部署本地目录，供实际 OSS 适配器与测试共用。
            /// </summary>
            /// <param name="config">CDN 编辑态配置。</param>
            /// <param name="projectRoot">Unity 项目根绝对路径。</param>
            /// <param name="uploadAsync">单文件上传执行器。</param>
            /// <param name="onProgress">进度回调，参数依次为完成数、总数和当前本地文件。</param>
            /// <returns>成功上传文件数。</returns>
            internal static async UniTask<int> DeployAsync(
                CDNEditorConfigs config,
                string projectRoot,
                Func<OssUploadItem, UniTask> uploadAsync,
                Action<int, int, string> onProgress)
            {
                return await DeployAsync(
                    config,
                    projectRoot,
                    PlatformType.None,
                    ChannelType.None,
                    string.Empty,
                    Application.version,
                    uploadAsync,
                    onProgress);
            }

            /// <summary>
            /// 使用指定路径占位符上下文顺序部署本地目录。
            /// </summary>
            internal static async UniTask<int> DeployAsync(
                CDNEditorConfigs config,
                string projectRoot,
                PlatformType platform,
                string package,
                string version,
                Func<OssUploadItem, UniTask> uploadAsync,
                Action<int, int, string> onProgress)
            {
                return await DeployAsync(
                    config,
                    projectRoot,
                    platform,
                    ChannelType.None,
                    package,
                    version,
                    uploadAsync,
                    onProgress);
            }

            /// <summary>
            /// 使用指定路径占位符上下文顺序部署本地目录。
            /// </summary>
            internal static async UniTask<int> DeployAsync(
                CDNEditorConfigs config,
                string projectRoot,
                PlatformType platform,
                ChannelType channel,
                string package,
                string version,
                Func<OssUploadItem, UniTask> uploadAsync,
                Action<int, int, string> onProgress)
            {
                return await DeployAsync(
                    config,
                    projectRoot,
                    platform,
                    channel,
                    package,
                    version,
                    false,
                    null,
                    null,
                    uploadAsync,
                    onProgress);
            }

            /// <summary>
            /// 可选先清理本次资源部署目标，再按上传计划顺序上传。
            /// </summary>
            internal static async UniTask<int> DeployAsync(
                CDNEditorConfigs config,
                string projectRoot,
                PlatformType platform,
                ChannelType channel,
                string package,
                string version,
                bool cleanRemoteFilesAndDirectories,
                Func<string, string, UniTask<OssObjectPage>> listObjectsAsync,
                Func<IReadOnlyList<string>, UniTask> deleteObjectsAsync,
                Func<OssUploadItem, UniTask> uploadAsync,
                Action<int, int, string> onProgress)
            {
                return await DeployAsync(
                    config,
                    projectRoot,
                    platform,
                    channel,
                    package,
                    version,
                    string.Empty,
                    cleanRemoteFilesAndDirectories,
                    listObjectsAsync,
                    deleteObjectsAsync,
                    uploadAsync,
                    onProgress);
            }

            /// <summary>
            /// 使用显式 YooAsset PackageFilePrefix 部署资源。
            /// </summary>
            internal static async UniTask<int> DeployAsync(
                CDNEditorConfigs config,
                string projectRoot,
                PlatformType platform,
                ChannelType channel,
                string package,
                string version,
                string packageFilePrefix,
                bool cleanRemoteFilesAndDirectories,
                Func<string, string, UniTask<OssObjectPage>> listObjectsAsync,
                Func<IReadOnlyList<string>, UniTask> deleteObjectsAsync,
                Func<OssUploadItem, UniTask> uploadAsync,
                Action<int, int, string> onProgress)
            {
                ValidateOssConfig(config);
                if (uploadAsync == null) throw new ArgumentNullException(nameof(uploadAsync));

                IReadOnlyList<OssUploadItem> plan = BuildUploadPlan(
                    config,
                    projectRoot,
                    platform,
                    channel,
                    package,
                    version,
                    packageFilePrefix);
                if (cleanRemoteFilesAndDirectories)
                {
                    OssCleanupPlan cleanupPlan = BuildCleanupPlan(
                        config,
                        plan,
                        platform,
                        channel,
                        package,
                        version);
                    try
                    {
                        await CleanRemoteAsync(cleanupPlan, listObjectsAsync, deleteObjectsAsync);
                    }
                    catch (Exception exception)
                    {
                        string detail = RedactSecrets(exception.Message, config.AccessKeySecret, config.Token);
                        throw new InvalidOperationException($"清理云端文件和目录失败：{detail}");
                    }
                }
                onProgress?.Invoke(0, plan.Count, plan[0].LocalPath);

                for (int index = 0; index < plan.Count; index++)
                {
                    OssUploadItem item = plan[index];
                    try
                    {
                        await uploadAsync(item);
                    }
                    catch (Exception exception)
                    {
                        string detail = RedactSecrets(exception.Message, config.AccessKeySecret, config.Token);
                        throw new InvalidOperationException(
                            $"上传失败：{item.LocalPath} -> {item.ObjectKey}。{detail}");
                    }

                    onProgress?.Invoke(index + 1, plan.Count, item.LocalPath);
                }

                return plan.Count;
            }

            /// <summary>
            /// 使用注入的单文件执行器顺序上传白名单配置及三个 YooAsset 版本文件。
            /// </summary>
            internal static async UniTask<int> DeployAssetCheckWhitelistAsync(
                CDNEditorConfigs config,
                string projectRoot,
                string generatedWhitelistFilePath,
                PlatformType platform,
                ChannelType channel,
                string package,
                string version,
                Func<OssUploadItem, UniTask> uploadAsync,
                Action<int, int, string> onProgress)
            {
                return await DeployAssetCheckWhitelistAsync(
                    config,
                    projectRoot,
                    generatedWhitelistFilePath,
                    platform,
                    channel,
                    package,
                    version,
                    false,
                    null,
                    null,
                    uploadAsync,
                    onProgress);
            }

            /// <summary>
            /// 可选先清理本次白名单部署目标，再按上传计划顺序上传。
            /// </summary>
            internal static async UniTask<int> DeployAssetCheckWhitelistAsync(
                CDNEditorConfigs config,
                string projectRoot,
                string generatedWhitelistFilePath,
                PlatformType platform,
                ChannelType channel,
                string package,
                string version,
                bool cleanRemoteFilesAndDirectories,
                Func<string, string, UniTask<OssObjectPage>> listObjectsAsync,
                Func<IReadOnlyList<string>, UniTask> deleteObjectsAsync,
                Func<OssUploadItem, UniTask> uploadAsync,
                Action<int, int, string> onProgress)
            {
                return await DeployAssetCheckWhitelistAsync(
                    config,
                    projectRoot,
                    generatedWhitelistFilePath,
                    platform,
                    channel,
                    package,
                    version,
                    string.Empty,
                    cleanRemoteFilesAndDirectories,
                    listObjectsAsync,
                    deleteObjectsAsync,
                    uploadAsync,
                    onProgress);
            }

            /// <summary>
            /// 使用显式 YooAsset PackageFilePrefix 部署白名单版本文件。
            /// </summary>
            internal static async UniTask<int> DeployAssetCheckWhitelistAsync(
                CDNEditorConfigs config,
                string projectRoot,
                string generatedWhitelistFilePath,
                PlatformType platform,
                ChannelType channel,
                string package,
                string version,
                string packageFilePrefix,
                bool cleanRemoteFilesAndDirectories,
                Func<string, string, UniTask<OssObjectPage>> listObjectsAsync,
                Func<IReadOnlyList<string>, UniTask> deleteObjectsAsync,
                Func<OssUploadItem, UniTask> uploadAsync,
                Action<int, int, string> onProgress)
            {
                ValidateOssConfig(config);
                if (uploadAsync == null) throw new ArgumentNullException(nameof(uploadAsync));

                IReadOnlyList<OssUploadItem> plan = BuildAssetCheckWhitelistUploadPlan(
                    config,
                    projectRoot,
                    generatedWhitelistFilePath,
                    platform,
                    channel,
                    package,
                    version,
                    packageFilePrefix);
                if (plan.Count == 0) return 0;
                if (cleanRemoteFilesAndDirectories)
                {
                    OssCleanupPlan cleanupPlan = BuildAssetCheckWhitelistCleanupPlan(
                        config,
                        plan,
                        platform,
                        channel,
                        package,
                        version);
                    try
                    {
                        await CleanRemoteAsync(cleanupPlan, listObjectsAsync, deleteObjectsAsync);
                    }
                    catch (Exception exception)
                    {
                        string detail = RedactSecrets(exception.Message, config.AccessKeySecret, config.Token);
                        throw new InvalidOperationException($"清理云端文件和目录失败：{detail}");
                    }
                }
                onProgress?.Invoke(0, plan.Count, plan[0].LocalPath);

                for (int index = 0; index < plan.Count; index++)
                {
                    OssUploadItem item = plan[index];
                    try
                    {
                        await uploadAsync(item);
                    }
                    catch (Exception exception)
                    {
                        string detail = RedactSecrets(exception.Message, config.AccessKeySecret, config.Token);
                        throw new InvalidOperationException(
                            $"白名单部署失败：{item.LocalPath} -> {item.ObjectKey}。{detail}");
                    }

                    onProgress?.Invoke(index + 1, plan.Count, item.LocalPath);
                }

                return plan.Count;
            }

            /// <summary>
            /// 列举目录前缀下的对象，与精确对象合并去重后分批删除。
            /// </summary>
            internal static async UniTask<int> CleanRemoteAsync(
                OssCleanupPlan cleanupPlan,
                Func<string, string, UniTask<OssObjectPage>> listObjectsAsync,
                Func<IReadOnlyList<string>, UniTask> deleteObjectsAsync)
            {
                if (cleanupPlan == null) throw new ArgumentNullException(nameof(cleanupPlan));
                if (listObjectsAsync == null) throw new ArgumentNullException(nameof(listObjectsAsync));
                if (deleteObjectsAsync == null) throw new ArgumentNullException(nameof(deleteObjectsAsync));

                var objectKeys = new List<string>();
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (string exactObjectKey in cleanupPlan.ExactObjectKeys)
                {
                    if (!string.IsNullOrEmpty(exactObjectKey) && seen.Add(exactObjectKey))
                    {
                        objectKeys.Add(exactObjectKey);
                    }
                }

                foreach (string directoryPrefix in cleanupPlan.DirectoryPrefixes)
                {
                    string continuationToken = null;
                    do
                    {
                        OssObjectPage page = await listObjectsAsync(directoryPrefix, continuationToken);
                        if (page.ObjectKeys != null)
                        {
                            foreach (string objectKey in page.ObjectKeys)
                            {
                                if (!string.IsNullOrEmpty(objectKey) &&
                                    objectKey.StartsWith(directoryPrefix, StringComparison.Ordinal) &&
                                    seen.Add(objectKey))
                                {
                                    objectKeys.Add(objectKey);
                                }
                            }
                        }
                        continuationToken = page.NextContinuationToken;
                    } while (!string.IsNullOrEmpty(continuationToken));
                }

                const int deleteBatchSize = 1000;
                for (int start = 0; start < objectKeys.Count; start += deleteBatchSize)
                {
                    int count = Math.Min(deleteBatchSize, objectKeys.Count - start);
                    await deleteObjectsAsync(objectKeys.GetRange(start, count));
                }
                return objectKeys.Count;
            }

            /// <summary>
            /// 使用注入的批请求执行器顺序清理 Cloudflare 缓存，供实际 HTTP 适配器与测试共用。
            /// </summary>
            /// <param name="config">CDN 编辑态配置。</param>
            /// <param name="sendAsync">单批 Cloudflare 请求执行器。</param>
            /// <param name="onProgress">进度回调，参数依次为完成批数和总批数。</param>
            /// <returns>成功清理 URL 数量。</returns>
            internal static async UniTask<int> PurgeAsync(
                CDNEditorConfigs config,
                Func<string, string, IReadOnlyList<string>, UniTask<CloudflareHttpResult>> sendAsync,
                Action<int, int> onProgress)
            {
                ValidateCloudflareConfig(config);
                if (sendAsync == null) throw new ArgumentNullException(nameof(sendAsync));

                IReadOnlyList<string> urls = ParseCacheUrls(config.CachePaths);
                IReadOnlyList<IReadOnlyList<string>> batches = BatchCacheUrls(urls);
                string purgeUrl = ResolveCloudflarePurgeUrl(config);
                onProgress?.Invoke(0, batches.Count);

                for (int index = 0; index < batches.Count; index++)
                {
                    CloudflareHttpResult result;
                    try
                    {
                        result = await sendAsync(purgeUrl, config.Token, batches[index]);
                    }
                    catch (Exception exception)
                    {
                        string detail = RedactSecrets(exception.Message, config.AccessKeySecret, config.Token);
                        throw new InvalidOperationException($"Cloudflare 第 {index + 1} 批请求失败。{detail}");
                    }

                    if (!result.IsSuccessStatusCode || !IsCloudflareSuccess(result.Body))
                    {
                        string excerpt = CreateResponseExcerpt(result.Body, config.Token);
                        throw new InvalidOperationException(
                            $"Cloudflare 第 {index + 1} 批清理失败，HTTP {result.StatusCode}。{excerpt}");
                    }

                    onProgress?.Invoke(index + 1, batches.Count);
                }

                return urls.Count;
            }

            /// <summary>
            /// 校验 OSS 连接与凭据字段，确保所有静态错误在首个请求前暴露。
            /// </summary>
            /// <param name="config">CDN 编辑态配置。</param>
            private static void ValidateOssConfig(CDNEditorConfigs config)
            {
                if (config == null) throw new ArgumentNullException(nameof(config));
                ParseRegion(config.Endpoint);
                ParseOssLocation(config.PresetOSSPath);
                if (string.IsNullOrWhiteSpace(config.AccessKeyID))
                    throw new ArgumentException("AccessKeyID 不能为空。", nameof(config));
                if (string.IsNullOrWhiteSpace(config.AccessKeySecret))
                    throw new ArgumentException("AccessKeySecret 不能为空。", nameof(config));
            }

            /// <summary>
            /// 校验并追加一个项目内 YooAsset 版本文件到白名单上传计划。
            /// </summary>
            private static void AddAssetCheckVersionFile(
                ICollection<OssUploadItem> plan,
                string projectRoot,
                string remotePrefix,
                string remoteDirectory,
                string localPathTemplate,
                string expectedExtension,
                PlatformType platform,
                ChannelType channel,
                string package,
                string version)
            {
                string relativePath = ResolvePathPlaceholders(
                    localPathTemplate,
                    platform,
                    channel,
                    package,
                    version);
                if (string.IsNullOrWhiteSpace(relativePath))
                    throw new ArgumentException($"版本文件({expectedExtension})本地文件位置不能为空。", nameof(localPathTemplate));

                string fullPath = IOPath.GetFullPath(IOPath.Combine(projectRoot, relativePath));
                if (!IsPathInsideRoot(fullPath, projectRoot))
                    throw new ArgumentException($"版本文件({expectedExtension})必须位于 Unity 项目根目录内：{fullPath}", nameof(localPathTemplate));
                if (!File.Exists(fullPath))
                    throw new ArgumentException($"版本文件({expectedExtension})不存在：{fullPath}", nameof(localPathTemplate));
                if (TryFindReparsePointInPath(fullPath, projectRoot, out string versionFileLink))
                    throw new ArgumentException($"版本文件({expectedExtension})不允许包含符号链接或 junction：{versionFileLink}", nameof(localPathTemplate));
                if (!string.Equals(IOPath.GetExtension(fullPath), expectedExtension, StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException($"版本文件扩展名必须为 {expectedExtension}：{fullPath}", nameof(localPathTemplate));

                plan.Add(new OssUploadItem(
                    fullPath,
                    CombineObjectKey(remotePrefix, remoteDirectory, IOPath.GetFileName(fullPath))));
            }

            /// <summary>
            /// 校验 Cloudflare Zone ID（兼容旧 PurgeURL）与 Token，确保所有静态错误在首个请求前暴露。
            /// </summary>
            /// <param name="config">CDN 编辑态配置。</param>
            private static void ValidateCloudflareConfig(CDNEditorConfigs config)
            {
                if (config == null) throw new ArgumentNullException(nameof(config));
                ResolveCloudflarePurgeUrl(config);
                if (string.IsNullOrWhiteSpace(config.Token))
                    throw new ArgumentException("Cloudflare API Token 不能为空。", nameof(config));
            }

            private static string ResolveCloudflarePurgeUrl(CDNEditorConfigs config)
            {
                if (config == null) throw new ArgumentNullException(nameof(config));
                string zoneId = string.IsNullOrWhiteSpace(config.ZoneID)
                    ? ExtractCloudflareZoneId(config.PurgeURL)
                    : ValidateAndNormalizeCloudflareZoneId(config.ZoneID);
                return BuildCloudflarePurgeUrl(zoneId);
            }

            private static string ValidateAndNormalizeCloudflareZoneId(string zoneId)
            {
                string normalized = zoneId?.Trim();
                if (string.IsNullOrEmpty(normalized) || !s_CloudflareZoneIdRegex.IsMatch(normalized))
                    throw new ArgumentException("Cloudflare Zone ID 格式无效。", nameof(zoneId));
                return normalized;
            }

            /// <summary>
            /// 解析 Cloudflare 响应中的 success 字段；空正文或非法 JSON 均按失败处理。
            /// </summary>
            /// <param name="body">响应正文。</param>
            /// <returns>Cloudflare 是否明确返回成功。</returns>
            private static bool IsCloudflareSuccess(string body)
            {
                if (string.IsNullOrWhiteSpace(body)) return false;
                try
                {
                    CloudflarePurgeResponse response = JsonUtility.FromJson<CloudflarePurgeResponse>(body);
                    return response != null && response.success;
                }
                catch (ArgumentException)
                {
                    return false;
                }
            }

            /// <summary>
            /// 截断并脱敏外部响应，避免对话框和日志泄露 Token。
            /// </summary>
            /// <param name="body">响应正文。</param>
            /// <param name="token">需要脱敏的 Token。</param>
            /// <returns>最多 1024 字符的响应摘要。</returns>
            private static string CreateResponseExcerpt(string body, string token)
            {
                string excerpt = RedactSecrets(body, token);
                return excerpt.Length <= 1024 ? excerpt : excerpt.Substring(0, 1024);
            }

            /// <summary>
            /// 从错误文本中移除所有非空秘密值。
            /// </summary>
            /// <param name="text">原始错误文本。</param>
            /// <param name="secrets">需要替换的秘密值。</param>
            /// <returns>脱敏后的文本。</returns>
            private static string RedactSecrets(string text, params string[] secrets)
            {
                string result = text ?? string.Empty;
                foreach (string secret in secrets)
                {
                    if (!string.IsNullOrEmpty(secret))
                        result = result.Replace(secret, "***", StringComparison.Ordinal);
                }
                return result;
            }

            /// <summary>
            /// 规范化 Object Key 单段，移除首尾分隔符并合并重复分隔符。
            /// </summary>
            /// <param name="value">待规范化文本。</param>
            /// <returns>使用正斜杠的规范化文本。</returns>
            private static string NormalizeObjectKeyPart(string value)
            {
                if (string.IsNullOrWhiteSpace(value)) return string.Empty;
                return string.Join("/", value
                    .Replace('\\', '/')
                    .Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries));
            }

            /// <summary>
            /// 判断目标绝对路径是否位于指定根目录内或等于根目录。
            /// </summary>
            /// <param name="path">目标绝对路径。</param>
            /// <param name="root">根目录绝对路径。</param>
            /// <returns>路径是否处于根目录边界内。</returns>
            private static bool IsPathInsideRoot(string path, string root)
            {
                string relative = IOPath.GetRelativePath(root, path);
                return relative == "." ||
                       (!relative.Equals("..", StringComparison.Ordinal) &&
                        !relative.StartsWith(".." + IOPath.DirectorySeparatorChar, StringComparison.Ordinal) &&
                        !IOPath.IsPathRooted(relative));
            }
        }
    }
}
