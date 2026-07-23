/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  PipifySteps.CDN.cs
 * author:    Codex
 * created:   2026/7/23
 * descrip:   Pipify 内置 Step 合集 —— CDN 资源部署
 ***************************************************************/

using System;
using System.IO;
using Cysharp.Threading.Tasks;
using NovaFramework.Runtime;
using UnityEngine;

namespace NovaFramework.Editor
{
    /// <summary>
    /// Pipify 内置 Step 合集（partial）：基于当前 Config 的 CDN 资源部署入口。
    /// </summary>
    internal static partial class PipifySteps
    {
        private const string c_CdnDeployDisplayName = "批量部署资源到 CDN";

        /// <summary>
        /// 使用当前激活 Config 的 OSS 凭据与固定前缀部署 Step 指定目录。
        /// </summary>
        [PipifyStep("cdn.deploy", c_CdnDeployDisplayName, "CDN", ParamsType = typeof(CdnDeployParams))]
        internal static UniTask RunCdnDeploy(PipifyContext ctx, CdnDeployParams parameters)
        {
            ConfigMasterSO master = EditorUtil.Config.WorkspaceActive.Get();
            if (master == null)
            {
                throw new InvalidOperationException("[Pipify] 未找到当前激活的 ConfigMasterSO，无法部署 CDN 资源。");
            }

            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName
                ?? throw new InvalidOperationException("[Pipify] 无法解析 Unity 项目根目录。");
            CdnDeploymentConfig config = CreateCdnDeploymentSnapshot(master, parameters);

            return EditorUtil.CDN.DeployAsync(
                config,
                projectRoot,
                master.CurrentPlatform,
                master.CurrentChannel,
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
        /// Resolve 当前维度 CDN 配置，并仅在独立快照中覆盖 Step 路径。
        /// </summary>
        internal static CdnDeploymentConfig CreateCdnDeploymentSnapshot(
            ConfigMasterSO master,
            CdnDeployParams parameters)
        {
            if (master == null) throw new ArgumentNullException(nameof(master));
            if (parameters == null) throw new ArgumentNullException(nameof(parameters));

            CdnDeploymentConfig snapshot = EditorUtil.Config.DimensionalResolver.ResolveCdn(
                master,
                master.CurrentPlatform,
                master.CurrentChannel,
                master.CurrentDevelopMode);
            snapshot.LocalDirectory = parameters.LocalDirectory ?? string.Empty;
            snapshot.RemotePathSuffix = parameters.RemoteDirectory ?? string.Empty;
            return snapshot;
        }
    }
}
