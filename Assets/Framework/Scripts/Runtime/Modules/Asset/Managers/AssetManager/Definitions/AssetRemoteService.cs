/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AssetRemoteService.cs
 * author:    taoye
 * created:   2026/5/14
 * descrip:   YooAsset 远端寻址实现
 ***************************************************************/

using System;
using System.Collections.Generic;
using UnityEngine;
using YooAsset;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// YooAsset 远端寻址服务，按主/备地址模板替换占位符 {Platform} {Channel} {Package} {Version}。
    /// Platform 使用 PlatformType 枚举名（宏判定，不依赖 ConfigRuntimeSO，保证启动期可用）。
    /// Channel 使用 Config 导出时同步到 AssetComponent 的启动期快照。
    /// Version 使用 Application.version（PlayerSettings Bundle Version）。
    /// </summary>
    public sealed class AssetRemoteService : IRemoteService
    {
        /// <summary>
        /// 当前主地址配置值。
        /// </summary>
        private readonly string m_HostServerUrl;

        /// <summary>
        /// 当前备用地址配置值。
        /// </summary>
        private readonly string m_HostServerUrlFallback;

        /// <summary>
        /// 白名单设备使用的版本元数据根主地址。
        /// </summary>
        private readonly string m_MetadataRootUrl;

        /// <summary>
        /// 白名单设备使用的版本元数据根备用地址。
        /// </summary>
        private readonly string m_MetadataRootUrlFallback;

        /// <summary>
        /// 平台枚举值；由编译宏决定，不依赖运行时配置。
        /// </summary>
        private readonly PlatformType m_Platform;
        /// <summary>
        /// Config 导出时写入场景的渠道快照。
        /// </summary>
        private readonly ChannelType m_Channel;
        /// <summary>
        /// 当前使用的资源包名。
        /// </summary>
        private readonly string m_Package;
        /// <summary>
        /// App 版本号（Application.version）。
        /// </summary>
        private readonly string m_Version;
        /// <summary>
        /// 已替换占位符的 URL 前缀缓存。
        /// </summary>
        private readonly string[] m_RemoteBaseUrls;

        /// <summary>
        /// 已替换占位符的白名单版本元数据根地址缓存。
        /// </summary>
        private readonly string[] m_MetadataBaseUrls;

        /// <summary>
        /// 常规与白名单元数据根地址的去重合集，供 BaseUrls 返回。
        /// </summary>
        private readonly string[] m_AllBaseUrls;

        /// <summary>
        /// 已完成平台、包名与版本占位符替换的远端基地址。
        /// </summary>
        public IReadOnlyList<string> BaseUrls => m_AllBaseUrls;

        /// <summary>
        /// 构造远端寻址服务。
        /// </summary>
        /// <param name="hostServerUrl">主下载地址配置值，默认应为完整 URL 模板。</param>
        /// <param name="hostServerUrlFallback">备用下载地址配置值，可为空。</param>
        /// <param name="package">当前使用的资源包名。</param>
        public AssetRemoteService(string hostServerUrl, string hostServerUrlFallback, string package)
            : this(hostServerUrl, hostServerUrlFallback, package, ChannelType.None, null, null)
        {
        }

        /// <summary>
        /// 构造带启动期渠道快照的远端寻址服务。
        /// </summary>
        /// <param name="hostServerUrl">主下载地址配置值。</param>
        /// <param name="hostServerUrlFallback">备用下载地址配置值，可为空。</param>
        /// <param name="package">当前使用的资源包名。</param>
        /// <param name="channel">Config 导出时同步的渠道快照。</param>
        public AssetRemoteService(
            string hostServerUrl,
            string hostServerUrlFallback,
            string package,
            ChannelType channel)
            : this(hostServerUrl, hostServerUrlFallback, package, channel, null, null)
        {
        }

        /// <summary>
        /// 构造可为白名单设备单独指定版本元数据根地址的远端寻址服务。
        /// </summary>
        public AssetRemoteService(
            string hostServerUrl,
            string hostServerUrlFallback,
            string package,
            ChannelType channel,
            string metadataRootUrl,
            string metadataRootUrlFallback)
        {
            m_HostServerUrl = hostServerUrl;
            m_HostServerUrlFallback = hostServerUrlFallback;
            m_MetadataRootUrl = metadataRootUrl;
            m_MetadataRootUrlFallback = metadataRootUrlFallback;
            m_Platform = Util.UrlTemplate.ResolveRuntimePlatform();
            m_Channel = channel;
            m_Package = package;
            m_Version = Application.version;
            m_RemoteBaseUrls = BuildRemoteUrlCache(m_HostServerUrl, m_HostServerUrlFallback);
            m_MetadataBaseUrls = BuildRemoteUrlCache(m_MetadataRootUrl, m_MetadataRootUrlFallback);
            m_AllBaseUrls = MergeBaseUrls(m_RemoteBaseUrls, m_MetadataBaseUrls);
            if (m_RemoteBaseUrls.Length == 0)
            {
                Log.Error(LogTag.Asset, "AssetRemoteService 未解析到任何常规远端地址。请配置热更 URL。");
            }
        }

        /// <summary>
        /// 返回主备 URL 列表，按优先级排列。
        /// </summary>
        /// <param name="fileName">YooAsset 请求的文件名。</param>
        /// <returns>候选 URL 列表，至少 1 项。</returns>
        public IReadOnlyList<string> GetRemoteUrls(string fileName)
        {
            List<string> urls = new List<string>(4);
            if (IsVersionMetadataFile(fileName))
            {
                AppendFileUrls(urls, m_MetadataBaseUrls, fileName);
            }
            AppendFileUrls(urls, m_RemoteBaseUrls, fileName);
            return urls;
        }

        /// <summary>
        /// 按运行时上下文替换模板占位符。
        /// </summary>
        /// <param name="template">URL 模板。</param>
        /// <returns>替换后的 URL 前缀。</returns>
        private string ApplyTemplate(string template)
        {
            return Util.UrlTemplate.Resolve(
                template,
                m_Platform,
                m_Channel,
                m_Package,
                m_Version);
        }

        /// <summary>
        /// 生成主/备远端前缀缓存。
        /// 直接 URL 优先。
        /// </summary>
        /// <returns>可用远端前缀数组。</returns>
        private string[] BuildRemoteUrlCache(string primaryConfiguredUrl, string fallbackConfiguredUrl)
        {
            List<string> urls = new List<string>(2);

            string primary = ResolveRemoteBaseUrl(primaryConfiguredUrl);
            if (!string.IsNullOrEmpty(primary))
            {
                urls.Add(primary);
            }

            string fallback = ResolveRemoteBaseUrl(fallbackConfiguredUrl);
            if (!string.IsNullOrEmpty(fallback) && !string.Equals(fallback, primary, StringComparison.OrdinalIgnoreCase))
            {
                urls.Add(fallback);
            }

            return urls.ToArray();
        }

        /// <summary>
        /// 判断 YooAsset 请求是否属于运行时版本元数据。
        /// 按当前 YooAssetSettings 的 PackageFilePrefix 识别 version、hash 与 bytes 文件。
        /// </summary>
        private bool IsVersionMetadataFile(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                return false;
            }

            string packageVersionFileName = YooAssetConfiguration.GetPackageVersionFileName(m_Package);
            if (string.Equals(fileName, packageVersionFileName, StringComparison.Ordinal))
            {
                return true;
            }

            const string versionExtension = ".version";
            string metadataPrefix = packageVersionFileName.Substring(
                0,
                packageVersionFileName.Length - versionExtension.Length) + "_";
            if (!fileName.StartsWith(metadataPrefix, StringComparison.Ordinal))
            {
                return false;
            }

            string extension = fileName.EndsWith(".hash", StringComparison.Ordinal)
                ? ".hash"
                : fileName.EndsWith(".bytes", StringComparison.Ordinal)
                    ? ".bytes"
                    : null;
            return extension != null && fileName.Length > metadataPrefix.Length + extension.Length;
        }

        /// <summary>
        /// 将一组根地址拼接文件名追加到结果，并按完整 URL 去重。
        /// </summary>
        private static void AppendFileUrls(List<string> result, IReadOnlyList<string> baseUrls, string fileName)
        {
            for (int i = 0; i < baseUrls.Count; i++)
            {
                string url = $"{baseUrls[i]}/{fileName}";
                if (!result.Contains(url))
                {
                    result.Add(url);
                }
            }
        }

        /// <summary>
        /// 合并常规与元数据根地址，保留优先级并忽略大小写去重。
        /// </summary>
        private static string[] MergeBaseUrls(IReadOnlyList<string> regular, IReadOnlyList<string> metadata)
        {
            List<string> result = new List<string>(regular.Count + metadata.Count);
            AppendUniqueBaseUrls(result, regular);
            AppendUniqueBaseUrls(result, metadata);
            return result.ToArray();
        }

        private static void AppendUniqueBaseUrls(List<string> result, IReadOnlyList<string> source)
        {
            for (int i = 0; i < source.Count; i++)
            {
                if (!result.Exists(value => string.Equals(value, source[i], StringComparison.OrdinalIgnoreCase)))
                {
                    result.Add(source[i]);
                }
            }
        }

        /// <summary>
        /// 解析单个远端前缀地址。
        /// </summary>
        /// <param name="configuredValue">业务层配置值，默认应为完整 URL 模板。</param>
        /// <returns>可用远端前缀；解析失败返回 null。</returns>
        private string ResolveRemoteBaseUrl(string configuredValue)
        {
            string resolved = ResolveConfiguredUrl(configuredValue);
            if (!string.IsNullOrWhiteSpace(resolved))
            {
                return NormalizeBaseUrl(ApplyTemplate(resolved));
            }

            return null;
        }

        /// <summary>
        /// 直接按 URL 解析配置值。
        /// </summary>
        /// <param name="configuredValue">业务层配置值。</param>
        /// <returns>解析得到的完整远端前缀 URL；失败返回 null。</returns>
        private static string ResolveConfiguredUrl(string configuredValue)
        {
            if (string.IsNullOrWhiteSpace(configuredValue))
            {
                return null;
            }

            return LooksLikeDirectUrl(configuredValue) ? configuredValue : null;
        }

        /// <summary>
        /// 判断配置值是否已是完整 URL。
        /// </summary>
        /// <param name="value">待判断的配置值。</param>
        /// <returns>是完整 URL 返回 true。</returns>
        private static bool LooksLikeDirectUrl(string value)
        {
            return value.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                   || value.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 统一去除末尾斜杠，避免与文件名拼接时出现双斜杠。
        /// </summary>
        /// <param name="url">远端前缀地址。</param>
        /// <returns>标准化后的远端前缀地址。</returns>
        private static string NormalizeBaseUrl(string url)
        {
            return string.IsNullOrWhiteSpace(url) ? null : url.TrimEnd('/');
        }
    }
}
