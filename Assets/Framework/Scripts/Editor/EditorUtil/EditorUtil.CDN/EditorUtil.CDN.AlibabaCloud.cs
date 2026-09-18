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
using System.IO;
using Cysharp.Threading.Tasks;
using NovaFramework.Runtime;
#if NOVA_ALIBABACLOUD_OSS
using OSS = AlibabaCloud.OSS.V2;
#endif

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        public static partial class CDN
        {
            private const string c_AlibabaCloudOssPackageName = "com.solotopia.alibabacloud.oss";

            /// <summary>
            /// 是否已安装可用于 CDN 部署的 Alibaba Cloud OSS Editor 工具包。
            /// </summary>
            internal static bool IsAlibabaCloudOssAvailable
            {
                get
                {
#if NOVA_ALIBABACLOUD_OSS
                    return true;
#else
                    return false;
#endif
                }
            }

            /// <summary>
            /// 创建 OSS Editor 工具包缺失时的统一错误，供 ConfigWindow 与 Pipify 共用。
            /// </summary>
            private static InvalidOperationException CreateAlibabaCloudOssPackageMissingException()
            {
                return new InvalidOperationException(
                    $"未安装 Alibaba Cloud OSS Editor 工具包（{c_AlibabaCloudOssPackageName}），无法执行 OSS 部署。请在 Unity Package Manager 中安装该工具包后重试。");
            }

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
                return await DeployAsync(
                    config,
                    projectRoot,
                    platform,
                    channel,
                    string.Empty,
                    onProgress);
            }

            /// <summary>
            /// 使用显式 YooAsset PackageFilePrefix 部署到 OSS。
            /// </summary>
            internal static async UniTask<int> DeployAsync(
                CDNEditorConfigs config,
                string projectRoot,
                PlatformType platform,
                ChannelType channel,
                string packageFilePrefix,
                Action<int, int, string> onProgress)
            {
#if NOVA_ALIBABACLOUD_OSS
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
                    item => UploadObjectAsync(client, location.Bucket, item),
                    onProgress);
#else
                return await UniTask.FromException<int>(CreateAlibabaCloudOssPackageMissingException());
#endif
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
                    string.Empty,
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
                Action<int, int, string> onProgress)
            {
#if NOVA_ALIBABACLOUD_OSS
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
                        item => UploadObjectAsync(client, location.Bucket, item),
                        onProgress);
                }
                finally
                {
                    DeleteAssetCheckWhitelistTempFile(whitelistFilePath);
                }
#else
                return await UniTask.FromException<int>(CreateAlibabaCloudOssPackageMissingException());
#endif
            }

#if NOVA_ALIBABACLOUD_OSS
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

#endif
        }
    }
}
