/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.CDN.Models.cs
 * author:    Codex
 * created:   2026/7/21
 * descrip:   CDN 编辑器工具的不可变路径模型
 ***************************************************************/

using System.Collections.Generic;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        /// <summary>
        /// CDN 内容部署与缓存清理工具。
        /// </summary>
        public static partial class CDN
        {
            /// <summary>
            /// 已解析的 OSS Bucket 与 Object Key 固定前缀。
            /// </summary>
            internal readonly struct OssLocation
            {
                /// <summary>
                /// 创建 OSS 远端位置。
                /// </summary>
                /// <param name="bucket">OSS Bucket 名称。</param>
                /// <param name="prefix">规范化后的固定 Object Key 前缀。</param>
                internal OssLocation(string bucket, string prefix)
                {
                    Bucket = bucket;
                    Prefix = prefix;
                }

                /// <summary>
                /// OSS Bucket 名称。
                /// </summary>
                internal string Bucket { get; }

                /// <summary>
                /// 规范化后的固定 Object Key 前缀。
                /// </summary>
                internal string Prefix { get; }
            }

            /// <summary>
            /// 单个本地文件对应的 OSS 上传计划项。
            /// </summary>
            internal readonly struct OssUploadItem
            {
                /// <summary>
                /// 创建单文件上传计划项。
                /// </summary>
                /// <param name="localPath">本地文件绝对路径。</param>
                /// <param name="objectKey">目标 OSS Object Key。</param>
                internal OssUploadItem(string localPath, string objectKey)
                {
                    LocalPath = localPath;
                    ObjectKey = objectKey;
                }

                /// <summary>
                /// 本地文件绝对路径。
                /// </summary>
                internal string LocalPath { get; }

                /// <summary>
                /// 目标 OSS Object Key。
                /// </summary>
                internal string ObjectKey { get; }
            }

            /// <summary>
            /// 一次部署允许清理的精确对象与目录前缀集合。
            /// </summary>
            internal sealed class OssCleanupPlan
            {
                internal OssCleanupPlan(
                    IReadOnlyList<string> exactObjectKeys,
                    IReadOnlyList<string> directoryPrefixes)
                {
                    ExactObjectKeys = exactObjectKeys;
                    DirectoryPrefixes = directoryPrefixes;
                }

                internal IReadOnlyList<string> ExactObjectKeys { get; }

                internal IReadOnlyList<string> DirectoryPrefixes { get; }
            }

            /// <summary>
            /// OSS 对象分页结果，供适配器与可注入测试执行器共用。
            /// </summary>
            internal readonly struct OssObjectPage
            {
                internal OssObjectPage(IReadOnlyList<string> objectKeys, string nextContinuationToken)
                {
                    ObjectKeys = objectKeys;
                    NextContinuationToken = nextContinuationToken;
                }

                internal IReadOnlyList<string> ObjectKeys { get; }

                internal string NextContinuationToken { get; }
            }

            /// <summary>
            /// Cloudflare 单批 HTTP 请求结果；业务 success 字段由编排层统一解析。
            /// </summary>
            internal readonly struct CloudflareHttpResult
            {
                /// <summary>
                /// 创建 Cloudflare HTTP 请求结果。
                /// </summary>
                /// <param name="statusCode">HTTP 状态码。</param>
                /// <param name="isSuccessStatusCode">HTTP 状态是否成功。</param>
                /// <param name="body">响应正文。</param>
                internal CloudflareHttpResult(int statusCode, bool isSuccessStatusCode, string body)
                {
                    StatusCode = statusCode;
                    IsSuccessStatusCode = isSuccessStatusCode;
                    Body = body ?? string.Empty;
                }

                /// <summary>
                /// HTTP 状态码。
                /// </summary>
                internal int StatusCode { get; }

                /// <summary>
                /// HTTP 状态是否成功。
                /// </summary>
                internal bool IsSuccessStatusCode { get; }

                /// <summary>
                /// 响应正文。
                /// </summary>
                internal string Body { get; }
            }
        }
    }
}
