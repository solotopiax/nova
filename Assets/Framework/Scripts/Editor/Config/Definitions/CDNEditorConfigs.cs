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
using UnityEngine;

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
        /// 项目根相对的版本检查本地文件位置；支持 {Platform}、{Channel}、{Package}、{Version} 占位符。
        /// </summary>
        public string VersionCheckLocalFilePath;

        /// <summary>
        /// 拼接在固定 OSS 前缀后的版本检查云端文件位置；支持 {Platform}、{Channel}、{Package}、{Version} 占位符。
        /// </summary>
        public string VersionCheckRemoteFilePath;

        /// <summary>
        /// 项目根相对的本地部署目录；支持 {Platform}、{Channel}、{Package}、{Version} 占位符。
        /// </summary>
        public string LocalDirectory;

        /// <summary>
        /// 拼接在固定 OSS 前缀后的可编辑远端目录后缀；支持 {Platform}、{Channel}、{Package}、{Version} 占位符。
        /// </summary>
        public string RemotePathSuffix;

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
