/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.Asset.Cache.cs
 * author:    taoye
 * created:   2026/8/6
 * descrip:   Editor 热更资源缓存清理工具
 ***************************************************************/

using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using YooAsset;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        public static partial class Asset
        {
            /// <summary>
            /// Editor 热更资源缓存清理工具。
            /// </summary>
            public static class Cache
            {
                private const string c_FrameworkAssetFolderName = "Asset";

                private static readonly string[] s_ProtectedProjectFolders =
                {
                    "Assets",
                    "Packages",
                    "ProjectSettings",
                    "Library",
                    "UserSettings",
                    ".git"
                };

                /// <summary>
                /// 清空 YooAsset Editor 沙盒和框架自主保存的 version 文件，并显示结果提示。
                /// </summary>
                public static void ClearAllHotfixResources()
                {
                    if (EditorApplication.isPlayingOrWillChangePlaymode)
                    {
                        EditorUtility.DisplayDialog("无法清理", "请退出 Play Mode 后再清理本地热更资源缓存。", "确定");
                        return;
                    }

                    try
                    {
                        string sandboxRoot = GetEditorSandboxRootPath();
                        string frameworkAssetRoot = NovaFramework.Runtime.Path.Persistent.GetFileFullPath(c_FrameworkAssetFolderName);
                        bool sandboxExisted = Directory.Exists(sandboxRoot);
                        int deletedVersionCount = ClearAllAtPaths(sandboxRoot, frameworkAssetRoot);
                        EditorUtility.DisplayDialog(
                            "清理完成",
                            $"YooAsset Editor 沙盒：{(sandboxExisted ? "已清空" : "无需清理")}\n已删除 version 文件：{deletedVersionCount} 个\nDeviceID：已保留",
                            "确定");
                    }
                    catch (Exception exception)
                    {
                        Debug.LogException(exception);
                        EditorUtility.DisplayDialog("清理失败", exception.Message, "确定");
                    }
                }

                /// <summary>
                /// 根据 YooAsset 当前配置动态获取 Editor 沙盒根目录。
                /// </summary>
                /// <returns>Editor 沙盒根目录的绝对路径。</returns>
                /// <exception cref="InvalidOperationException">YooAsset 缓存目录为空或解析到项目根目录时抛出。</exception>
                public static string GetEditorSandboxRootPath()
                {
                    string projectRoot = Path.GetFullPath(Path.GetDirectoryName(Application.dataPath) ?? string.Empty);
                    string yooFolderName = YooAssetConfiguration.GetYooFolderName();
                    return ResolveEditorSandboxRoot(projectRoot, yooFolderName);
                }

                /// <summary>
                /// 根据项目根目录与 YooAsset 文件夹配置解析并校验 Editor 沙盒根目录。
                /// </summary>
                internal static string ResolveEditorSandboxRoot(string projectRoot, string yooFolderName)
                {
                    if (string.IsNullOrWhiteSpace(projectRoot))
                    {
                        throw new InvalidOperationException("无法解析 Unity 项目根目录，已拒绝清理。");
                    }
                    if (string.IsNullOrWhiteSpace(yooFolderName))
                    {
                        throw new InvalidOperationException("YooAsset 的 YooFolderName 为空，沙盒路径会退化为项目根目录，已拒绝清理。");
                    }

                    projectRoot = Path.GetFullPath(projectRoot);
                    string sandboxRoot = Path.GetFullPath(Path.Combine(projectRoot, yooFolderName));
                    if (PathsEqual(sandboxRoot, projectRoot) || !IsChildPath(sandboxRoot, projectRoot))
                    {
                        throw new InvalidOperationException($"YooAsset Editor 沙盒路径不安全，已拒绝清理：{sandboxRoot}");
                    }

                    foreach (string protectedFolder in s_ProtectedProjectFolders)
                    {
                        string protectedRoot = Path.GetFullPath(Path.Combine(projectRoot, protectedFolder));
                        if (PathsEqual(sandboxRoot, protectedRoot) || IsChildPath(sandboxRoot, protectedRoot))
                        {
                            throw new InvalidOperationException($"YooAsset Editor 沙盒路径指向项目关键目录，已拒绝清理：{sandboxRoot}");
                        }
                    }
                    return sandboxRoot;
                }

                /// <summary>
                /// 清空指定沙盒目录，并仅删除框架 Asset 目录第一层的 version 文件。
                /// </summary>
                /// <param name="sandboxRoot">YooAsset Editor 沙盒根目录。</param>
                /// <param name="frameworkAssetRoot">框架自主缓存文件目录。</param>
                /// <returns>删除的 version 文件数量。</returns>
                internal static int ClearAllAtPaths(string sandboxRoot, string frameworkAssetRoot)
                {
                    ValidateDeleteTarget(sandboxRoot);
                    if (Directory.Exists(sandboxRoot))
                    {
                        Directory.Delete(sandboxRoot, true);
                    }

                    if (string.IsNullOrWhiteSpace(frameworkAssetRoot) || !Directory.Exists(frameworkAssetRoot))
                    {
                        return 0;
                    }

                    int deletedVersionCount = 0;
                    foreach (string versionFile in Directory.GetFiles(frameworkAssetRoot, "*.version", SearchOption.TopDirectoryOnly))
                    {
                        File.Delete(versionFile);
                        deletedVersionCount++;
                    }
                    return deletedVersionCount;
                }

                /// <summary>
                /// 校验递归删除目标不是文件系统根目录。
                /// </summary>
                private static void ValidateDeleteTarget(string targetPath)
                {
                    if (string.IsNullOrWhiteSpace(targetPath))
                    {
                        throw new InvalidOperationException("YooAsset Editor 沙盒路径为空，已拒绝清理。");
                    }

                    string fullPath = Path.GetFullPath(targetPath);
                    if (PathsEqual(fullPath, Path.GetPathRoot(fullPath)))
                    {
                        throw new InvalidOperationException($"YooAsset Editor 沙盒路径指向文件系统根目录，已拒绝清理：{fullPath}");
                    }
                }

                /// <summary>
                /// 判断候选路径是否严格位于父目录之下。
                /// </summary>
                private static bool IsChildPath(string candidate, string parent)
                {
                    string normalizedParent = parent.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                        + Path.DirectorySeparatorChar;
                    return candidate.StartsWith(normalizedParent, StringComparison.OrdinalIgnoreCase);
                }

                /// <summary>
                /// 判断两个绝对路径是否相同。
                /// </summary>
                private static bool PathsEqual(string left, string right)
                {
                    return string.Equals(
                        left?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                        right?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                        StringComparison.OrdinalIgnoreCase);
                }
            }
        }
    }
}
