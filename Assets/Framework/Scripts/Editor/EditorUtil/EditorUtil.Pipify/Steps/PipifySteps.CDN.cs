/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  PipifySteps.CDN.cs
 * author:    Codex
 * created:   2026/7/23
 * descrip:   Pipify 内置 Step 合集 —— CDN 资源部署、白名单部署与缓存清理
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using Cysharp.Threading.Tasks;
using NovaFramework.Runtime;
using UnityEngine;

namespace NovaFramework.Editor
{
    /// <summary>
    /// Pipify 内置 Step 合集（partial）：基于当前 Config 的 CDN 资源部署、白名单部署与缓存清理入口；Platform 统一取 Unity Active BuildTarget。
    /// </summary>
    internal static partial class PipifySteps
    {
        private const string c_CdnDeployDisplayName = "批量部署资源到 CDN";
        private const string c_CdnWhitelistDeployDisplayName = "白名单批量部署到 CDN";
        private const string c_CdnPurgeDisplayName = "批量清除 CDN 缓存";

        /// <summary>
        /// 使用当前激活 Config 的 OSS 凭据、固定前缀和 Unity Active BuildTarget 平台部署 Step 指定目录。
        /// </summary>
        [PipifyStep("cdn.deploy", c_CdnDeployDisplayName, "CDN", ParamsType = typeof(CdnDeployParams))]
        internal static UniTask RunCdnDeploy(PipifyContext ctx, CdnDeployParams parameters)
        {
            ConfigMasterSO master = EditorUtil.Config.WorkspaceActive.Get();
            if (master == null)
            {
                throw new InvalidOperationException("[Pipify] 未找到当前激活的 ConfigMasterSO，无法部署 CDN 资源。");
            }
            EditorUtil.Config.ActivePlatform.RequireCurrent("[Pipify] CDN 资源部署");
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName
                ?? throw new InvalidOperationException("[Pipify] 无法解析 Unity 项目根目录。");
            CDNEditorConfigs config = CreateCdnDeploymentSnapshot(master, parameters);
            string packageFilePrefix = ResolveCdnPackageFilePrefix(master);

            return EditorUtil.CDN.DeployAsync(
                config,
                projectRoot,
                master.CurrentPlatform,
                master.CurrentChannel,
                packageFilePrefix,
                parameters.CleanRemoteFilesAndDirectories,
                (completed, total, _) =>
                {
                    float progress = total > 0 ? completed / (float)total : 0f;
                    if (ctx.Reporter.ReportStep(ctx.CurrentStepIndex, c_CdnDeployDisplayName, progress))
                    {
                        throw new OperationCanceledException(ctx.CancellationToken);
                    }
                });
        }

        /// <summary>
        /// 按 Active BuildTarget 平台及当前 Channel/DevelopMode Resolve CDN 配置，并仅在独立快照中覆盖 Step 的四个路径配置。
        /// </summary>
        internal static CDNEditorConfigs CreateCdnDeploymentSnapshot(
            ConfigMasterSO master,
            CdnDeployParams parameters)
        {
            if (master == null) throw new ArgumentNullException(nameof(master));
            if (parameters == null) throw new ArgumentNullException(nameof(parameters));

            CDNEditorConfigs snapshot = EditorUtil.Config.DimensionalResolver.ResolveCDNEditorConfigs(
                master,
                master.CurrentPlatform,
                master.CurrentChannel,
                master.CurrentDevelopMode);
            snapshot.VersionCheckLocalFilePath = parameters.VersionCheckLocalFilePath ?? string.Empty;
            snapshot.VersionCheckRemoteFilePath = parameters.VersionCheckRemoteFilePath ?? string.Empty;
            snapshot.AutoLinkLatestVersion = parameters.AutoLinkLatestVersion;
            snapshot.LocalDirectory = parameters.LocalDirectory ?? string.Empty;
            snapshot.RemotePathSuffix = parameters.RemoteDirectory ?? string.Empty;
            return snapshot;
        }

        /// <summary>
        /// 使用当前激活 Config 的 OSS 连接配置及 Unity Active BuildTarget 平台，部署白名单配置与三个 YooAsset 版本文件。
        /// </summary>
        [PipifyStep(
            "cdn.whitelist.deploy",
            c_CdnWhitelistDeployDisplayName,
            "CDN",
            ParamsType = typeof(CdnWhitelistDeployParams))]
        internal static UniTask RunCdnWhitelistDeploy(PipifyContext ctx, CdnWhitelistDeployParams parameters)
        {
            ConfigMasterSO master = EditorUtil.Config.WorkspaceActive.Get();
            if (master == null)
            {
                throw new InvalidOperationException("[Pipify] 未找到当前激活的 ConfigMasterSO，无法部署白名单版本文件。");
            }
            EditorUtil.Config.ActivePlatform.RequireCurrent("[Pipify] CDN 白名单部署");
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName
                ?? throw new InvalidOperationException("[Pipify] 无法解析 Unity 项目根目录。");
            CDNEditorConfigs config = CreateCdnWhitelistDeploymentSnapshot(master, parameters);
            string packageFilePrefix = ResolveCdnPackageFilePrefix(master);
            return EditorUtil.CDN.DeployAssetCheckWhitelistAsync(
                config,
                projectRoot,
                master.CurrentPlatform,
                master.CurrentChannel,
                packageFilePrefix,
                parameters.CleanRemoteFilesAndDirectories,
                (completed, total, _) =>
                {
                    float progress = total > 0 ? completed / (float)total : 0f;
                    if (ctx.Reporter.ReportStep(ctx.CurrentStepIndex, c_CdnWhitelistDeployDisplayName, progress))
                    {
                        throw new OperationCanceledException(ctx.CancellationToken);
                    }
                });
        }

        /// <summary>
        /// 按 Active BuildTarget 平台及当前 Channel/DevelopMode Resolve CDN 配置，并仅在独立快照中覆盖白名单设备 ID 与四个路径字段。
        /// </summary>
        internal static CDNEditorConfigs CreateCdnWhitelistDeploymentSnapshot(
            ConfigMasterSO master,
            CdnWhitelistDeployParams parameters)
        {
            if (master == null) throw new ArgumentNullException(nameof(master));
            if (parameters == null) throw new ArgumentNullException(nameof(parameters));

            CDNEditorConfigs snapshot = EditorUtil.Config.DimensionalResolver.ResolveCDNEditorConfigs(
                master,
                master.CurrentPlatform,
                master.CurrentChannel,
                master.CurrentDevelopMode);
            snapshot.AssetCheckWhitelistDeviceIDs = ParseCdnWhitelistDeviceIDs(parameters.DeviceIDs);
            snapshot.AssetCheckWhitelistRemoteFilePath = parameters.WhitelistRemoteFilePath ?? string.Empty;
            snapshot.AutoLinkLatestAssetCheckVersionFiles = parameters.AutoLinkLatestVersion;
            snapshot.AssetCheckManifestBytesLocalFilePath = parameters.ManifestBytesLocalFilePath ?? string.Empty;
            snapshot.AssetCheckManifestHashLocalFilePath = parameters.ManifestHashLocalFilePath ?? string.Empty;
            snapshot.AssetCheckPackageVersionLocalFilePath = parameters.PackageVersionLocalFilePath ?? string.Empty;
            snapshot.AssetCheckVersionRemoteDirectory = parameters.RemoteDirectory ?? string.Empty;
            return snapshot;
        }

        /// <summary>
        /// 从 Unity 当前 Active BuildTarget + 当前 Channel/DevelopMode 对应维度解析 YooAsset 文件前缀；
        /// 包含 {Time} 时复用构建已写入 YooAssetSettings.asset 的实际值。
        /// </summary>
        private static string ResolveCdnPackageFilePrefix(ConfigMasterSO master)
        {
            string packageName = EditorUtil.CDN.ResolveDefaultPackageName();
            return EditorUtil.CDN.ResolvePackageFilePrefix(
                master,
                master.CurrentPlatform,
                master.CurrentChannel,
                master.CurrentDevelopMode,
                packageName,
                Application.version,
                DateTime.Now);
        }

        /// <summary>
        /// 将 Pipify 多行设备 ID 文本解析为有序字符串列表；最终去重由白名单 JSON 生成器统一处理。
        /// </summary>
        private static List<string> ParseCdnWhitelistDeviceIDs(string value)
        {
            string[] lines = (value ?? string.Empty).Split(
                new[] { '\r', '\n' },
                StringSplitOptions.RemoveEmptyEntries);
            var result = new List<string>();
            foreach (string line in lines)
            {
                string deviceID = line.Trim();
                if (!string.IsNullOrEmpty(deviceID)) result.Add(deviceID);
            }
            return result;
        }

        /// <summary>
        /// 使用 Step 指定的 Cloudflare 配置批量清除 CDN 缓存。
        /// </summary>
        [PipifyStep("cdn.purge", c_CdnPurgeDisplayName, "CDN", ParamsType = typeof(CdnPurgeParams))]
        internal static async UniTask RunCdnPurge(PipifyContext ctx, CdnPurgeParams parameters)
        {
            ConfigMasterSO master = EditorUtil.Config.WorkspaceActive.Get();
            if (master == null)
            {
                throw new InvalidOperationException("[Pipify] 未找到当前激活的 ConfigMasterSO，无法清除 CDN 缓存。");
            }
            EditorUtil.Config.ActivePlatform.RequireCurrent("[Pipify] CDN 缓存清理");

            CDNEditorConfigs config = CreateCdnPurgeSnapshot(master, parameters);
            await EditorUtil.CDN.PurgeAsync(
                config,
                (completed, total) =>
                {
                    float progress = total > 0 ? completed / (float)total : 0f;
                    if (ctx.Reporter.ReportStep(ctx.CurrentStepIndex, c_CdnPurgeDisplayName, progress))
                    {
                        throw new OperationCanceledException(ctx.CancellationToken);
                    }
                });
        }

        /// <summary>
        /// Resolve 当前维度 CDN 配置，并仅在独立快照中覆盖 Cloudflare 三个字段。
        /// </summary>
        internal static CDNEditorConfigs CreateCdnPurgeSnapshot(
            ConfigMasterSO master,
            CdnPurgeParams parameters)
        {
            if (master == null) throw new ArgumentNullException(nameof(master));
            if (parameters == null) throw new ArgumentNullException(nameof(parameters));

            CDNEditorConfigs snapshot = EditorUtil.Config.DimensionalResolver.ResolveCDNEditorConfigs(
                master,
                master.CurrentPlatform,
                master.CurrentChannel,
                master.CurrentDevelopMode);
            snapshot.ZoneID = parameters.ZoneID ?? string.Empty;
            snapshot.Token = parameters.Token ?? string.Empty;
            snapshot.CachePaths = parameters.CachePaths ?? string.Empty;
            return snapshot;
        }
    }
}
