/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  PipifySteps.Build.cs
 * author:    taoye
 * created:   2026/5/10
 * descrip:   Pipify 内置 Step 合集 —— 打包分组（1 个 Step）
 ***************************************************************/

using System;
using Cysharp.Threading.Tasks;
using NovaFramework.Runtime;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace NovaFramework.Editor
{
    /// <summary>
    /// Pipify 内置 Step 合集（partial）。
    /// 本文件收录打包分组的 1 个原子操作：一键打包 Player。
    /// 方法仅做薄封装，调用 EditorUtil.Build.BuildPackage public API。
    /// </summary>
    internal static partial class PipifySteps
    {
        /// <summary>
        /// Step：按 PackageParams 执行 Unity Player 打包。
        /// 产物文件名由 BuildPackage 按固定格式自动生成，输出到 OutputFolderPath 指定的文件夹。
        /// 打包失败时 BuildPackage 内部抛 InvalidOperationException，Runner 会捕获并中断流水线。
        /// </summary>
        /// <param name="ctx">Runner 下发的运行时上下文。</param>
        /// <param name="p">打包参数。</param>
        /// <returns>完成的 UniTask。</returns>
        [PipifyStep("build.package", "安装包|工程", "构建打包", ParamsType = typeof(PackageParams))]
        internal static UniTask RunPackage(PipifyContext ctx, PackageParams p)
        {
            if (ctx == null) throw new ArgumentNullException(nameof(ctx));
            if (p == null) throw new ArgumentNullException(nameof(p));
            ctx.BuildState.LatestArtifact = null;
            ctx.CancellationToken.ThrowIfCancellationRequested();
            EditorUtil.Config.ActivePlatform.RequireCurrent("[Pipify] Player 构建");
            p.Target = EditorUserBuildSettings.activeBuildTarget;
            ConfigRuntimeSO runtime = EditorUtil.Config.RuntimeProvider.GetCurrent();
            DevelopMode developMode = runtime != null ? runtime.DevelopMode : DevelopMode.Debug;
            if (runtime == null)
            {
                Log.Warning(LogTag.Editor, "[Pipify] 未找到激活 ConfigRuntimeSO，文件名开发模式段降级为 Debug，请先导出 Config。");
            }
            AndroidSigningSettingsSnapshot signingSnapshot = ApplyAndroidSigningSettings(p);
            try
            {
                string packageName = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
                BuildReport report = EditorUtil.Build.BuildPackage(
                    p.Target, p.OutputFolderPath, p.DevelopmentBuild,
                    p.BuildMode, p.BuildAppBundle, p.SplitApplicationBinary, developMode);
                // Build 的公共薄封装仍可能返回 Cancelled/Unknown；流水线只接受明确成功的报告。
                if (report == null || report.summary.result != BuildResult.Succeeded)
                    throw new InvalidOperationException("[Pipify] Player 构建未成功，已停止流水线，不记录上传产物。");
                ctx.CancellationToken.ThrowIfCancellationRequested();
                ctx.BuildState.LatestArtifact = new PipifyBuildArtifact
                {
                    OutputPath = report.summary.outputPath,
                    Target = p.Target,
                    DevelopmentBuild = p.DevelopmentBuild,
                    PackageName = packageName
                };
                return UniTask.CompletedTask;
            }
            finally
            {
                RestoreAndroidSigningSettings(signingSnapshot);
            }
        }

        private static AndroidSigningSettingsSnapshot ApplyAndroidSigningSettings(PackageParams p)
        {
            if (p == null || p.Target != BuildTarget.Android || !p.UseAndroidKeystore)
            {
                return null;
            }

            ValidateAndroidSigningSettings(p);

            AndroidSigningSettingsSnapshot snapshot = new AndroidSigningSettingsSnapshot
            {
                UseCustomKeystore = PlayerSettings.Android.useCustomKeystore,
                KeystoreName = PlayerSettings.Android.keystoreName,
                KeystorePass = PlayerSettings.Android.keystorePass,
                KeyaliasName = PlayerSettings.Android.keyaliasName,
                KeyaliasPass = PlayerSettings.Android.keyaliasPass
            };

            PlayerSettings.Android.useCustomKeystore = true;
            PlayerSettings.Android.keystoreName = p.AndroidKeystorePath;
            PlayerSettings.Android.keystorePass = p.AndroidKeystorePass;
            PlayerSettings.Android.keyaliasName = p.AndroidKeyalias;
            PlayerSettings.Android.keyaliasPass = p.AndroidKeyaliasPass;
            return snapshot;
        }

        private static void RestoreAndroidSigningSettings(AndroidSigningSettingsSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            PlayerSettings.Android.useCustomKeystore = snapshot.UseCustomKeystore;
            PlayerSettings.Android.keystoreName = snapshot.KeystoreName;
            PlayerSettings.Android.keystorePass = snapshot.KeystorePass;
            PlayerSettings.Android.keyaliasName = snapshot.KeyaliasName;
            PlayerSettings.Android.keyaliasPass = snapshot.KeyaliasPass;
        }

        private static void ValidateAndroidSigningSettings(PackageParams p)
        {
            if (string.IsNullOrWhiteSpace(p.AndroidKeystorePath))
            {
                throw new InvalidOperationException("[Pipify] 启用 UseAndroidKeystore 时必须填写 Android keystore 路径。");
            }
            if (string.IsNullOrWhiteSpace(p.AndroidKeystorePass))
            {
                throw new InvalidOperationException("[Pipify] 启用 UseAndroidKeystore 时必须填写 Android keystore 密码。");
            }
            if (string.IsNullOrWhiteSpace(p.AndroidKeyalias))
            {
                throw new InvalidOperationException("[Pipify] 启用 UseAndroidKeystore 时必须填写 Android key alias 名称。");
            }
            if (string.IsNullOrWhiteSpace(p.AndroidKeyaliasPass))
            {
                throw new InvalidOperationException("[Pipify] 启用 UseAndroidKeystore 时必须填写 Android key alias 密码。");
            }
        }

        private sealed class AndroidSigningSettingsSnapshot
        {
            public bool UseCustomKeystore;
            public string KeystoreName;
            public string KeystorePass;
            public string KeyaliasName;
            public string KeyaliasPass;
        }
    }
}
