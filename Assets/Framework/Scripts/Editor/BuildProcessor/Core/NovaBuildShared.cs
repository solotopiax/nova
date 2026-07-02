/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  NovaBuildShared.cs
 * author:    yingzheng
 * created:   2026/4/24
 * descrip:   NovaBuildPreprocessor 与 NovaBuildPostprocessor 的跨阶段共享状态
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using NovaFramework.Runtime;
using UnityEditor;
using PackageManagerPackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace NovaFramework.Editor
{
    /// <summary>
    /// Pre/Post 两个处理器之间的共享状态容器。
    /// Preprocessor 写入，Postprocessor 读取，保证跨阶段实例字段和上下文一致。
    /// </summary>
    internal static class NovaBuildShared
    {
        /// <summary>
        /// 项目主 AndroidManifest.xml 的 Assets 相对路径，构建时由模板复制生成。
        /// </summary>
        internal const string c_AndroidManifestPath = "Assets/Plugins/Android/AndroidManifest.xml";

        /// <summary>
        /// UnityManifest.xml 模板的开发态 Assets 相对路径，每次构建时作为净底复制到输出路径。
        /// </summary>
        internal const string c_UnityManifestTemplatePath = "Assets/Framework/Scripts/Editor/BuildProcessor/Android/UnityManifest.xml";

        /// <summary>
        /// UnityManifest.xml 模板在 Nova framework 包根下的相对路径。
        /// </summary>
        private const string c_UnityManifestTemplatePackageRelativePath = "Scripts/Editor/BuildProcessor/Android/UnityManifest.xml";

        /// <summary>
        /// 构建输出的 proguard-user.txt 完整路径。
        /// </summary>
        internal const string c_ProguardOutputPath = "Assets/Plugins/Android/proguard-user.txt";

        /// <summary>
        /// 当前构建上下文，由 NovaBuildPreprocessor 创建，NovaBuildPostprocessor 读取。
        /// </summary>
        internal static NovaBuildContext Context;

        /// <summary>
        /// 处理器实例列表，由 NovaBuildPreprocessor 创建，Pre/Post 两阶段共享同一批实例。
        /// </summary>
        internal static List<NovaSDKBuildProcessor> Processors;

        /// <summary>
        /// 解析 UnityManifest.xml 模板路径。开发态优先使用 Assets/Framework，UPM 引用态使用包 resolvedPath。
        /// </summary>
        /// <returns>可被文件系统访问的 UnityManifest.xml 路径；找不到时返回 null。</returns>
        internal static string ResolveUnityManifestTemplatePath()
        {
            if (FilePathExists(c_UnityManifestTemplatePath))
            {
                return c_UnityManifestTemplatePath;
            }

            PackageManagerPackageInfo packageInfo = PackageManagerPackageInfo.FindForAssembly(typeof(NovaBuildShared).Assembly);
            string packageResolvedPath = ResolveFromPackageInfo(packageInfo);
            if (!string.IsNullOrEmpty(packageResolvedPath))
            {
                return packageResolvedPath;
            }

            string[] guids = AssetDatabase.FindAssets("UnityManifest");
            for (int i = 0; i < guids.Length; i++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (!assetPath.EndsWith($"/{c_UnityManifestTemplatePackageRelativePath}", StringComparison.Ordinal))
                {
                    continue;
                }

                string filePath = ResolveFilePathFromAssetPath(assetPath);
                if (FilePathExists(filePath))
                {
                    return filePath;
                }
            }

            return null;
        }

        /// <summary>
        /// 从包信息解析模板物理路径。
        /// </summary>
        /// <param name="packageInfo">当前程序集所属包信息。</param>
        /// <returns>可用物理路径；找不到时返回 null。</returns>
        private static string ResolveFromPackageInfo(PackageManagerPackageInfo packageInfo)
        {
            if (packageInfo == null)
            {
                return null;
            }

            if (!string.IsNullOrEmpty(packageInfo.resolvedPath))
            {
                string resolvedPath = CombineUnityPath(packageInfo.resolvedPath, c_UnityManifestTemplatePackageRelativePath);
                if (FilePathExists(resolvedPath))
                {
                    return resolvedPath;
                }
            }

            if (!string.IsNullOrEmpty(packageInfo.assetPath))
            {
                string assetPath = CombineUnityPath(packageInfo.assetPath, c_UnityManifestTemplatePackageRelativePath);
                if (FilePathExists(assetPath))
                {
                    return assetPath;
                }
            }

            return null;
        }

        /// <summary>
        /// 将 Unity 资产路径转换为可访问的物理路径。
        /// </summary>
        /// <param name="assetPath">Assets/ 或 Packages/ 开头的资产路径。</param>
        /// <returns>可访问的物理路径；无法转换时返回原路径。</returns>
        private static string ResolveFilePathFromAssetPath(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
            {
                return assetPath;
            }

            PackageManagerPackageInfo packageInfo = PackageManagerPackageInfo.FindForAssetPath(assetPath);
            if (packageInfo != null && !string.IsNullOrEmpty(packageInfo.assetPath) && !string.IsNullOrEmpty(packageInfo.resolvedPath)
                && assetPath.StartsWith(packageInfo.assetPath, StringComparison.Ordinal))
            {
                string relativePath = assetPath.Substring(packageInfo.assetPath.Length).TrimStart('/', '\\');
                return CombineUnityPath(packageInfo.resolvedPath, relativePath);
            }

            return assetPath;
        }

        /// <summary>
        /// 判断文件路径是否存在，兼容项目相对路径与绝对路径。
        /// </summary>
        /// <param name="path">待检测路径。</param>
        /// <returns>存在返回 true。</returns>
        private static bool FilePathExists(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            if (File.Exists(path))
            {
                return true;
            }

            string fullPath = System.IO.Path.GetFullPath(path);
            return File.Exists(fullPath);
        }

        /// <summary>
        /// 拼接 Unity 风格路径，统一使用正斜杠。
        /// </summary>
        /// <param name="left">左路径。</param>
        /// <param name="right">右路径。</param>
        /// <returns>拼接后的路径。</returns>
        private static string CombineUnityPath(string left, string right)
        {
            if (string.IsNullOrEmpty(left))
            {
                return right;
            }
            if (string.IsNullOrEmpty(right))
            {
                return left;
            }

            return $"{left.TrimEnd('/', '\\')}/{right.TrimStart('/', '\\')}";
        }

        /// <summary>
        /// 通过反射发现所有 NovaSDKBuildProcessor 具体子类，并实例化后返回。
        /// </summary>
        /// <returns>所有具体子类的实例列表。</returns>
        internal static List<NovaSDKBuildProcessor> CollectProcessors()
        {
            var result = new List<NovaSDKBuildProcessor>();
            var types = TypeCache.GetTypesDerivedFrom<NovaSDKBuildProcessor>();
            foreach (var type in types)
            {
                if (type.IsAbstract)
                    continue;
                try
                {
                    result.Add((NovaSDKBuildProcessor)Activator.CreateInstance(type));
                }
                catch (Exception e)
                {
                    Log.Error(LogTag.Editor, $"[NovaBuildShared] 实例化处理器 {type.FullName} 失败：{e.Message}");
                }
            }
            return result;
        }
    }
}
