/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.BundleBuilder.cs
 * author:    taoye
 * created:   2026/5/19
 * descrip:   YooAsset 资源包构建薄封装（ScriptableBuildPipeline）
 ***************************************************************/

using System;
using NovaFramework.Runtime;
using UnityEditor;
using YooAsset;
using YooAsset.Editor;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        /// <summary>
        /// YooAsset 资源包构建薄封装。
        /// 仅做 ScriptableBuildPipeline 的薄封装 + 必要输入校验；不处理依赖收集 / 上传 / 通知等外围逻辑。
        /// </summary>
        public static partial class BundleBuilder
        {
            /// <summary>
            /// 启动一次 YooAsset 资源包构建（ScriptableBuildPipeline）。
            /// 内部行为与 BundleBuilderWindow → ScriptableBuildPipelineViewer 完全一致：
            /// 输出根目录 = 项目根 /Bundles，BundledFileRoot = 默认 BuiltinRoot；
            /// EnableSharePackRule = true，VerifyBuildingResult = true。
            /// </summary>
            /// <param name="args">构建参数。</param>
            /// <returns>YooAsset 构建结果。</returns>
            public static BuildResult BuildAssetBundle(AssetBundleBuildArgs args)
            {
                if (args == null)
                {
                    throw new ArgumentNullException(nameof(args));
                }
                if (string.IsNullOrEmpty(args.PackageName))
                {
                    throw new ArgumentException(string.Format("{0} PackageName 不能为空", c_LogPrefix));
                }

                BuildTarget target = args.Target == BuildTarget.NoTarget ? EditorUserBuildSettings.activeBuildTarget : args.Target;
                string version = string.IsNullOrEmpty(args.BuildVersion) ? GetDefaultPackageVersion() : args.BuildVersion;

                ScriptableBuildParameters parameters = new ScriptableBuildParameters();
                parameters.BuildOutputRoot = BundleBuilderHelper.GetDefaultBuildOutputRoot();
                parameters.BundledFileRoot = BundleBuilderHelper.GetStreamingAssetsRoot();
                parameters.BuildPipeline = nameof(EBuildPipeline.ScriptableBuildPipeline);
                parameters.BuildBundleType = (int)EBundleType.AssetBundle;
                parameters.BuildTarget = target;
                parameters.PackageName = args.PackageName;
                parameters.PackageVersion = version;
                parameters.EnableSharePackRule = true;
                parameters.VerifyBuildingResult = true;
                parameters.FileNameStyle = args.FileNameStyle;
                parameters.BundledCopyOption = args.BundledCopyOption;
                parameters.BundledCopyParams = args.BundledCopyParams ?? string.Empty;
                parameters.CompressOption = args.Compression;
                parameters.ClearBuildCacheFiles = args.ClearBuildCache;
                parameters.UseAssetDependencyDB = args.UseAssetDependencyDB;
                parameters.BundleEncryptor = CreateInstanceOrNull<IBundleEncryptor>(ResolveClassName(args.BundleEncryptorClassName, typeof(EncryptionNone)));
                parameters.ManifestEncryptor = CreateInstanceOrNull<IManifestEncryptor>(ResolveClassName(args.ManifestEncryptorClassName, typeof(ManifestEncryptorNone)));
                parameters.ManifestDecryptor = CreateInstanceOrNull<IManifestDecryptor>(ResolveClassName(args.ManifestDecryptorClassName, typeof(ManifestDecryptorNone)));
                parameters.BuiltinShadersBundleName = ResolveBuiltinShaderBundleName(args.PackageName);

                Log.Debug(LogTag.Editor,
                    "{0} 开始构建：package={1}, version={2}, target={3}, compression={4}, clearCache={5}",
                    c_LogPrefix, args.PackageName, version, target, args.Compression, args.ClearBuildCache);

                ScriptableBuildPipeline pipeline = new ScriptableBuildPipeline();
                BuildResult result = pipeline.Run(parameters, true);
                if (!result.Success)
                {
                    throw new InvalidOperationException(string.Format("{0} 构建失败：FailedTask={1}, Error={2}", c_LogPrefix, result.FailedTask, result.ErrorInfo));
                }
                Log.Debug(LogTag.Editor, "{0} 构建成功：{1}", c_LogPrefix, result.OutputPackageDirectory);
                return result;
            }

            /// <summary>
            /// 生成默认包裹版本号（与 BuildPipelineViewerBase.GetDefaultPackageVersion 一致：yyyy-MM-dd-totalMinutes）。
            /// </summary>
            /// <returns>默认包裹版本号。</returns>
            public static string GetDefaultPackageVersion()
            {
                int totalMinutes = DateTime.Now.Hour * 60 + DateTime.Now.Minute;
                return string.Format("{0}-{1}", DateTime.Now.ToString("yyyy-MM-dd"), totalMinutes);
            }
        }
    }
}
