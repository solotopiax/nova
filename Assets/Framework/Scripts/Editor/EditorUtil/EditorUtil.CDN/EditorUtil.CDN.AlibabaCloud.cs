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
                    item => UploadObjectAsync(client, location.Bucket, item),
                    onProgress);
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
        }
    }
}
