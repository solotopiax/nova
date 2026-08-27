/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  CDNEditorConfigs.cs
 * author:    Codex
 * created:   2026/7/21
 * descrip:   CDN 内容部署与缓存清理的编辑态配置
 ***************************************************************/

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace NovaFramework.Editor
{
    /// <summary>
    /// CDN 内容部署与缓存清理的编辑态配置；仅随 ConfigMasterSO 保存，不参与 Runtime 导出。
    /// </summary>
    [Serializable]
    public sealed class CDNEditorConfigs
    {
        /// <summary>
        /// 阿里云 OSS 标准地域 Endpoint。
        /// </summary>
        public string Endpoint;

        /// <summary>
        /// 阿里云访问密钥 ID。
        /// </summary>
        public string AccessKeyID;

        /// <summary>
        /// 阿里云访问密钥 Secret；在 ConfigMasterSO 中以明文序列化。
        /// </summary>
        public string AccessKeySecret;

        /// <summary>
        /// OSS 固定远端前缀，格式为 oss://bucket-name/fixed/prefix。
        /// </summary>
        public string PresetOSSPath;

        /// <summary>
        /// 项目根相对的版本检查本地文件位置；支持 {Platform}、{Channel}、{Package}、{Version} 占位符；Editor 部署时 Platform 取 Unity Active BuildTarget 映射值。
        /// </summary>
        public string VersionCheckLocalFilePath;

        /// <summary>
        /// 拼接在固定 OSS 前缀后的版本检查云端文件位置；支持 {Platform}、{Channel}、{Package}、{Version} 占位符；Editor 部署时 Platform 取 Unity Active BuildTarget 映射值。
        /// </summary>
        public string VersionCheckRemoteFilePath;

        /// <summary>
        /// 项目根相对的本地部署目录；支持 {Platform}、{Channel}、{Package}、{Version} 占位符；Editor 部署时 Platform 取 Unity Active BuildTarget 映射值。
        /// </summary>
        public string LocalDirectory;

        /// <summary>
        /// 是否在部署和界面展示时自动关联本地包根目录下最后生成的有效 YooAsset 版本目录。
        /// </summary>
        public bool AutoLinkLatestVersion = true;

        /// <summary>
        /// 拼接在固定 OSS 前缀后的可编辑远端目录后缀；支持 {Platform}、{Channel}、{Package}、{Version} 占位符；Editor 部署时 Platform 取 Unity Active BuildTarget 映射值。
        /// </summary>
        public string RemotePathSuffix;

        /// <summary>
        /// 启动资源校验白名单中的稳定设备 ID；部署时生成 VersionsCheckWhiteList.json 字符串数组。
        /// </summary>
        public List<string> AssetCheckWhitelistDeviceIDs = new();

        /// <summary>
        /// VersionsCheckWhiteList.json 上传到的 OSS 远端文件位置。
        /// </summary>
        [FormerlySerializedAs("AssetCheckWhitelistRemoteDirectory")]
        public string AssetCheckWhitelistRemoteFilePath;

        /// <summary>
        /// 是否自动关联最新完整 YooAsset 版本，并据此解析白名单部署使用的三个版本文件。
        /// </summary>
        public bool AutoLinkLatestAssetCheckVersionFiles = true;

        /// <summary>
        /// 项目根相对的 YooAsset Manifest 二进制版本文件位置（.bytes）。
        /// </summary>
        public string AssetCheckManifestBytesLocalFilePath;

        /// <summary>
        /// 项目根相对的 YooAsset Manifest 哈希版本文件位置（.hash）。
        /// </summary>
        public string AssetCheckManifestHashLocalFilePath;

        /// <summary>
        /// 项目根相对的 YooAsset 包版本文件位置（.version）。
        /// </summary>
        public string AssetCheckPackageVersionLocalFilePath;

        /// <summary>
        /// 三个 YooAsset 版本文件上传到的 OSS 远端目录后缀。
        /// </summary>
        public string AssetCheckVersionRemoteDirectory;

        /// <summary>
        /// Cloudflare Zone ID。
        /// </summary>
        public string ZoneID;

        /// <summary>
        /// 旧版 Cloudflare Zone purge API 完整 URL，仅用于已有 ConfigMasterSO 兼容迁移。
        /// </summary>
        [HideInInspector]
        public string PurgeURL;

        /// <summary>
        /// Cloudflare API Token；在 ConfigMasterSO 中以明文序列化。
        /// </summary>
        public string Token;

        /// <summary>
        /// 英文逗号、分号或换行分隔的待清理缓存 URL。
        /// </summary>
        [TextArea(3, 8)]
        public string CachePaths;
    }
}
