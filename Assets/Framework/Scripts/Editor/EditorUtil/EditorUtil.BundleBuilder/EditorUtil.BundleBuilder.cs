/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.BundleBuilder.cs
 * author:    taoye
 * created:   2026/5/19
 * descrip:   YooAsset 标准资源与 RawFile 构建薄封装
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
        /// YooAsset 标准资源与 RawFile 构建薄封装。
        /// 仅封装对应构建管线并执行必要输入校验；不处理依赖收集、上传、通知等外围逻辑。
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
                ResolveBundledCopy(
                    target,
                    args.BundledCopyOption,
                    args.BundledCopyParams,
                    out EBundledCopyOption bundledCopyOption,
                    out string bundledCopyParams);

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
                parameters.BundledCopyOption = bundledCopyOption;
                parameters.BundledCopyParams = bundledCopyParams;
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
                FinalizeWebGLBundledCopy(target, args.PackageName, bundledCopyOption);
                Log.Debug(LogTag.Editor, "{0} 构建成功：{1}", c_LogPrefix, result.OutputPackageDirectory);
                return result;
            }

            /// <summary>
            /// 启动一次 YooAsset 原生文件构建，仅处理配置为 PackRawFile 的资源。
            /// 使用 RawFileBuildParameters、RawFileBuildPipeline 与 RawBundle，不改变标准 AssetBundle 构建入口。
            /// </summary>
            /// <param name="args">RawFile 构建参数。</param>
            /// <returns>YooAsset 构建结果。</returns>
            public static BuildResult BuildRawFileBundle(RawFileBuildArgs args)
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
                ResolveBundledCopy(
                    target,
                    args.BundledCopyOption,
                    args.BundledCopyParams,
                    out EBundledCopyOption bundledCopyOption,
                    out string bundledCopyParams);

                RawFileBuildParameters parameters = new RawFileBuildParameters();
                parameters.BuildOutputRoot = BundleBuilderHelper.GetDefaultBuildOutputRoot();
                parameters.BundledFileRoot = BundleBuilderHelper.GetStreamingAssetsRoot();
                parameters.BuildPipeline = nameof(EBuildPipeline.RawFileBuildPipeline);
                parameters.BuildBundleType = (int)EBundleType.RawBundle;
                parameters.BuildTarget = target;
                parameters.PackageName = args.PackageName;
                parameters.PackageVersion = version;
                parameters.VerifyBuildingResult = true;
                parameters.FileNameStyle = args.FileNameStyle;
                parameters.BundledCopyOption = bundledCopyOption;
                parameters.BundledCopyParams = bundledCopyParams;
                parameters.ClearBuildCacheFiles = args.ClearBuildCache;
                parameters.UseAssetDependencyDB = args.UseAssetDependencyDB;
                parameters.BundleEncryptor = CreateInstanceOrNull<IBundleEncryptor>(ResolveClassName(args.BundleEncryptorClassName, typeof(EncryptionNone)));
                parameters.ManifestEncryptor = CreateInstanceOrNull<IManifestEncryptor>(ResolveClassName(args.ManifestEncryptorClassName, typeof(ManifestEncryptorNone)));
                parameters.ManifestDecryptor = CreateInstanceOrNull<IManifestDecryptor>(ResolveClassName(args.ManifestDecryptorClassName, typeof(ManifestDecryptorNone)));
                parameters.IncludePathInHash = args.IncludePathInHash;

                Log.Debug(LogTag.Editor,
                    "{0} 开始 RawFile 构建：package={1}, version={2}, target={3}, clearCache={4}, includePathInHash={5}",
                    c_LogPrefix, args.PackageName, version, target, args.ClearBuildCache, args.IncludePathInHash);

                RawFileBuildPipeline pipeline = new RawFileBuildPipeline();
                BuildResult result = pipeline.Run(parameters, true);
                if (!result.Success)
                {
                    throw new InvalidOperationException(string.Format("{0} RawFile 构建失败：FailedTask={1}, Error={2}", c_LogPrefix, result.FailedTask, result.ErrorInfo));
                }
                FinalizeWebGLBundledCopy(target, args.PackageName, bundledCopyOption);
                Log.Debug(LogTag.Editor, "{0} RawFile 构建成功：{1}", c_LogPrefix, result.OutputPackageDirectory);
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

            /// <summary>
            /// 解析最终首包拷贝参数；WebGL 仅允许可准确描述当前 Package 完整布局的三种清理式选项。
            /// </summary>
            /// <param name="target">目标构建平台。</param>
            /// <param name="requestedOption">调用方请求的拷贝模式。</param>
            /// <param name="requestedParams">调用方请求的 Tag 参数。</param>
            /// <param name="option">最终拷贝模式。</param>
            /// <param name="copyParams">最终 Tag 参数。</param>
            internal static void ResolveBundledCopy(
                BuildTarget target,
                EBundledCopyOption requestedOption,
                string requestedParams,
                out EBundledCopyOption option,
                out string copyParams)
            {
                if (target == BuildTarget.WebGL)
                {
                    if (!IsWebGLBundledCopyOptionSupported(requestedOption))
                    {
                        throw new ArgumentOutOfRangeException(
                            nameof(requestedOption),
                            requestedOption,
                            "WebGL 仅支持 None、ClearAndCopyByTags 或 ClearAndCopyAll。");
                    }

                    option = requestedOption;
                    copyParams = requestedOption == EBundledCopyOption.ClearAndCopyByTags
                        ? requestedParams ?? string.Empty
                        : string.Empty;
                    return;
                }

                option = requestedOption;
                copyParams = requestedParams ?? string.Empty;
            }

            /// <summary>
            /// 判断首包拷贝选项是否可用于 WebGL；禁止不清理旧目录的增量拷贝，避免遗留 Catalog 与 Bundle 污染当前构建。
            /// </summary>
            /// <param name="option">待检查的 YooAsset 首包拷贝选项。</param>
            /// <returns>WebGL 可安全使用时返回 true。</returns>
            internal static bool IsWebGLBundledCopyOptionSupported(EBundledCopyOption option)
            {
                return option == EBundledCopyOption.None
                       || option == EBundledCopyOption.ClearAndCopyByTags
                       || option == EBundledCopyOption.ClearAndCopyAll;
            }

            /// <summary>
            /// 在 WebGL 的 None 构建成功后清理当前 Package 的旧首包目录，确保 Catalog 缺失能够准确表示纯 CDN 布局。
            /// </summary>
            /// <param name="target">本次资源构建平台。</param>
            /// <param name="packageName">YooAsset Package 名称。</param>
            /// <param name="copyOption">已校验的最终首包拷贝选项。</param>
            private static void FinalizeWebGLBundledCopy(
                BuildTarget target,
                string packageName,
                EBundledCopyOption copyOption)
            {
                if (target != BuildTarget.WebGL || copyOption != EBundledCopyOption.None)
                {
                    return;
                }

                string packageRoot = System.IO.Path.Combine(BundleBuilderHelper.GetStreamingAssetsRoot(), packageName);
                EditorFileUtility.DeleteDirectory(packageRoot);
                AssetDatabase.Refresh();
            }
        }
    }
}
