/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.CDN.AlibabaCloud.cs
 * author:    Codex
 * created:   2026/7/21
 * descrip:   阿里云 OSS 目录部署适配器
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cysharp.Threading.Tasks;
using NovaFramework.Runtime;
using OSS = AlibabaCloud.OSS.V2;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        public static partial class CDN
        {
            /// <summary>
            /// 使用阿里云 OSS SDK 将本地目录顺序部署到配置的 Bucket 与前缀。
            /// </summary>
            /// <param name="config">CDN 编辑态配置。</param>
            /// <param name="projectRoot">Unity 项目根绝对路径。</param>
            /// <param name="onProgress">进度回调，参数依次为完成数、总数和当前本地文件。</param>
            /// <returns>成功上传文件数。</returns>
            internal static async UniTask<int> DeployAsync(
                CDNEditorConfigs config,
                string projectRoot,
                Action<int, int, string> onProgress)
            {
                return await DeployAsync(config, projectRoot, PlatformType.None, onProgress);
            }

            /// <summary>
            /// 使用当前 ConfigWindow 平台解析目录占位符后，将本地目录顺序部署到 OSS。
            /// </summary>
            internal static async UniTask<int> DeployAsync(
                CDNEditorConfigs config,
                string projectRoot,
                PlatformType platform,
                Action<int, int, string> onProgress)
            {
                return await DeployAsync(config, projectRoot, platform, ChannelType.None, onProgress);
            }

            /// <summary>
            /// 使用当前 ConfigWindow 平台与渠道解析目录占位符后，将本地目录顺序部署到 OSS。
            /// </summary>
            internal static async UniTask<int> DeployAsync(
                CDNEditorConfigs config,
                string projectRoot,
                PlatformType platform,
                ChannelType channel,
                Action<int, int, string> onProgress)
            {
                return await DeployAsync(config, projectRoot, platform, channel, false, onProgress);
            }

            /// <summary>
            /// 使用当前平台与渠道，可选先清理本次目标再部署到 OSS。
            /// </summary>
            internal static async UniTask<int> DeployAsync(
                CDNEditorConfigs config,
                string projectRoot,
                PlatformType platform,
                ChannelType channel,
                bool cleanRemoteFilesAndDirectories,
                Action<int, int, string> onProgress)
            {
                return await DeployAsync(
                    config,
                    projectRoot,
                    platform,
                    channel,
                    string.Empty,
                    cleanRemoteFilesAndDirectories,
                    onProgress);
            }

            /// <summary>
            /// 使用显式 YooAsset PackageFilePrefix，可选先清理本次目标再部署到 OSS。
            /// </summary>
            internal static async UniTask<int> DeployAsync(
                CDNEditorConfigs config,
                string projectRoot,
                PlatformType platform,
                ChannelType channel,
                string packageFilePrefix,
                bool cleanRemoteFilesAndDirectories,
                Action<int, int, string> onProgress)
            {
                ValidateOssConfig(config);
                OssLocation location = ParseOssLocation(config.PresetOSSPath);
                string region = ParseRegion(config.Endpoint);
                var sdkConfig = OSS.Configuration.LoadDefault();
                sdkConfig.Endpoint = config.Endpoint.Trim();
                sdkConfig.Region = region;
                sdkConfig.CredentialsProvider = new OSS.Credentials.StaticCredentialsProvider(
                    config.AccessKeyID,
                    config.AccessKeySecret);

                using var client = new OSS.Client(sdkConfig);
                return await DeployAsync(
                    config,
                    projectRoot,
                    platform,
                    channel,
                    ResolveDefaultPackageName(),
                    UnityEngine.Application.version,
                    packageFilePrefix,
                    cleanRemoteFilesAndDirectories,
                    (prefix, token) => ListObjectPageAsync(client, location.Bucket, prefix, token),
                    keys => DeleteObjectsAsync(client, location.Bucket, keys),
                    item => UploadObjectAsync(client, location.Bucket, item),
                    onProgress);
            }

            /// <summary>
            /// 按各自目录上传 VersionsCheckWhiteList.json 与三个 YooAsset 版本文件。
            /// </summary>
            /// <param name="config">当前维度生效的 CDN 编辑配置。</param>
            /// <param name="projectRoot">Unity 项目根绝对路径。</param>
            /// <param name="platform">当前平台。</param>
            /// <param name="channel">当前渠道。</param>
            /// <param name="onProgress">进度回调，参数依次为完成数、总数和当前本地文件。</param>
            /// <returns>成功上传文件数；配置文件位置无效时仅上传三个版本文件。</returns>
            internal static async UniTask<int> DeployAssetCheckWhitelistAsync(
                CDNEditorConfigs config,
                string projectRoot,
                PlatformType platform,
                ChannelType channel,
                Action<int, int, string> onProgress)
            {
                return await DeployAssetCheckWhitelistAsync(
                    config,
                    projectRoot,
                    platform,
                    channel,
                    false,
                    onProgress);
            }

            /// <summary>
            /// 可选先清理白名单文件与版本文件目录，再执行白名单部署。
            /// </summary>
            internal static async UniTask<int> DeployAssetCheckWhitelistAsync(
                CDNEditorConfigs config,
                string projectRoot,
                PlatformType platform,
                ChannelType channel,
                bool cleanRemoteFilesAndDirectories,
                Action<int, int, string> onProgress)
            {
                return await DeployAssetCheckWhitelistAsync(
                    config,
                    projectRoot,
                    platform,
                    channel,
                    string.Empty,
                    cleanRemoteFilesAndDirectories,
                    onProgress);
            }

            /// <summary>
            /// 使用显式 YooAsset PackageFilePrefix 部署白名单版本文件。
            /// </summary>
            internal static async UniTask<int> DeployAssetCheckWhitelistAsync(
                CDNEditorConfigs config,
                string projectRoot,
                PlatformType platform,
                ChannelType channel,
                string packageFilePrefix,
                bool cleanRemoteFilesAndDirectories,
                Action<int, int, string> onProgress)
            {
                ValidateOssConfig(config);
                bool shouldUploadWhitelist = TryResolveAssetCheckWhitelistRemoteFilePath(
                    config.AssetCheckWhitelistRemoteFilePath,
                    platform,
                    channel,
                    ResolveDefaultPackageName(),
                    UnityEngine.Application.version,
                    out _);
                string whitelistFilePath = shouldUploadWhitelist
                    ? CreateAssetCheckWhitelistTempFile(config.AssetCheckWhitelistDeviceIDs)
                    : null;
                try
                {
                    OssLocation location = ParseOssLocation(config.PresetOSSPath);
                    string region = ParseRegion(config.Endpoint);
                    var sdkConfig = OSS.Configuration.LoadDefault();
                    sdkConfig.Endpoint = config.Endpoint.Trim();
                    sdkConfig.Region = region;
                    sdkConfig.CredentialsProvider = new OSS.Credentials.StaticCredentialsProvider(
                        config.AccessKeyID,
                        config.AccessKeySecret);

                    using var client = new OSS.Client(sdkConfig);
                    return await DeployAssetCheckWhitelistAsync(
                        config,
                        projectRoot,
                        whitelistFilePath,
                        platform,
                        channel,
                        ResolveDefaultPackageName(),
                        UnityEngine.Application.version,
                        packageFilePrefix,
                        cleanRemoteFilesAndDirectories,
                        (prefix, token) => ListObjectPageAsync(client, location.Bucket, prefix, token),
                        keys => DeleteObjectsAsync(client, location.Bucket, keys),
                        item => UploadObjectAsync(client, location.Bucket, item),
                        onProgress);
                }
                finally
                {
                    DeleteAssetCheckWhitelistTempFile(whitelistFilePath);
                }
            }

            /// <summary>
            /// 上传单个本地文件，并在请求完成后释放文件流。
            /// </summary>
            /// <param name="client">已配置的 OSS Client。</param>
            /// <param name="bucket">目标 Bucket。</param>
            /// <param name="item">单文件上传计划项。</param>
            private static async UniTask UploadObjectAsync(
                OSS.Client client,
                string bucket,
                OssUploadItem item)
            {
                using FileStream stream = File.Open(item.LocalPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                await client.PutObjectAsync(new OSS.Models.PutObjectRequest
                {
                    Bucket = bucket,
                    Key = item.ObjectKey,
                    Body = stream
                });
            }

            /// <summary>
            /// 分页列举指定目录前缀下的 OSS 对象。
            /// </summary>
            private static async UniTask<OssObjectPage> ListObjectPageAsync(
                OSS.Client client,
                string bucket,
                string prefix,
                string continuationToken)
            {
                OSS.Models.ListObjectsV2Result result = await client.ListObjectsV2Async(
                    new OSS.Models.ListObjectsV2Request
                    {
                        Bucket = bucket,
                        Prefix = prefix,
                        ContinuationToken = continuationToken,
                        MaxKeys = 999,
                    });
                string[] objectKeys = result.Contents?
                    .Select(item => item.Key)
                    .Where(key => !string.IsNullOrEmpty(key))
                    .ToArray() ?? Array.Empty<string>();
                return new OssObjectPage(
                    objectKeys,
                    result.IsTruncated == true ? result.NextContinuationToken : null);
            }

            /// <summary>
            /// 批量删除一组已限制在本次清理计划内的 OSS Object Key。
            /// </summary>
            private static async UniTask DeleteObjectsAsync(
                OSS.Client client,
                string bucket,
                IReadOnlyList<string> objectKeys)
            {
                await client.DeleteMultipleObjectsAsync(new OSS.Models.DeleteMultipleObjectsRequest
                {
                    Bucket = bucket,
                    Quiet = true,
                    Objects = objectKeys.Select(key => new OSS.Models.DeleteObject { Key = key }).ToList(),
                });
            }
        }
    }
}
