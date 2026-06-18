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
    /// YooAsset 远端寻址服务，按主/备地址模板替换占位符 {Platform} {Package} {Version}。
    /// Platform 使用 PlatformType 枚举名（宏判定，不依赖 ConfigRuntimeSO，保证启动期可用）。
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
        /// 平台枚举值；由编译宏决定，不依赖运行时配置。
        /// </summary>
        private readonly PlatformType m_Platform;
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
        private readonly string[] m_Cache;

        /// <summary>
        /// 构造远端寻址服务。
        /// </summary>
        /// <param name="hostServerUrl">主下载地址配置值，默认应为完整 URL 模板。</param>
        /// <param name="hostServerUrlFallback">备用下载地址配置值，可为空。</param>
        /// <param name="package">当前使用的资源包名。</param>
        public AssetRemoteService(string hostServerUrl, string hostServerUrlFallback, string package)
        {
            m_HostServerUrl = hostServerUrl;
            m_HostServerUrlFallback = hostServerUrlFallback;
            m_Platform = ResolvePlatform();
            m_Package = package;
            m_Version = Application.version;
            m_Cache = BuildRemoteUrlCache();
        }

        /// <summary>
        /// 通过编译宏判定当前平台，避免在启动热更前依赖 ConfigRuntimeSO。
        /// </summary>
        /// <returns>当前平台枚举值。</returns>
        private static PlatformType ResolvePlatform()
        {
#if UNITY_ANDROID
            return PlatformType.Android;
#elif UNITY_IOS
            return PlatformType.iOS;
#elif UNITY_WEBGL
            return PlatformType.WebGL;
#else
            return PlatformType.None;
#endif
        }

        /// <summary>
        /// 返回主备 URL 列表，按优先级排列。
        /// </summary>
        /// <param name="fileName">YooAsset 请求的文件名。</param>
        /// <returns>候选 URL 列表，至少 1 项。</returns>
        public IReadOnlyList<string> GetRemoteUrls(string fileName)
        {
            int count = m_Cache.Length;
            string[] urls = new string[count];
            for (int i = 0; i < count; i++)
            {
                urls[i] = $"{m_Cache[i]}/{fileName}";
            }
            return urls;
        }

        /// <summary>
        /// 按运行时上下文替换模板占位符。
        /// </summary>
        /// <param name="template">URL 模板。</param>
        /// <returns>替换后的 URL 前缀。</returns>
        private string ApplyTemplate(string template)
        {
            return template
                .Replace("{Platform}", m_Platform.ToString())
                .Replace("{Package}", m_Package)
                .Replace("{Version}", m_Version);
        }

        /// <summary>
        /// 生成主/备远端前缀缓存。
        /// 直接 URL 优先。
        /// </summary>
        /// <returns>可用远端前缀数组。</returns>
        private string[] BuildRemoteUrlCache()
        {
            List<string> urls = new List<string>(2);

            string primary = ResolveRemoteBaseUrl(m_HostServerUrl);
            if (!string.IsNullOrEmpty(primary))
            {
                urls.Add(primary);
            }

            string fallback = ResolveRemoteBaseUrl(m_HostServerUrlFallback);
            if (!string.IsNullOrEmpty(fallback) && !string.Equals(fallback, primary, StringComparison.OrdinalIgnoreCase))
            {
                urls.Add(fallback);
            }

            if (urls.Count == 0)
            {
                Log.Error(LogTag.Asset, "AssetRemoteService 未解析到任何远端地址。请配置热更 URL。");
            }

            return urls.ToArray();
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
