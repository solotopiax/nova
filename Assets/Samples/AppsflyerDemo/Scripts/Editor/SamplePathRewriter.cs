/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  SamplePathRewriter.cs
 * author:    taoye
 * created:   2026/5/21
 * descrip:   Sample import 后路径自适应重写器
 *            外部工程通过 Package Manager 导入 Sample 后，sample 内
 *            任何字符串字段如 "Assets/Samples/AppsflyerDemo/..." 都会指向
 *            开发工程旧路径。本脚本读取 SamplePathManifest，把所有列入
 *            清单的资产文件中的字符串前缀替换为 import 后的真实路径
 *            （形如 "Assets/Samples/Nova Framework/{version}/AppsflyerDemo"）。
 *            先按清单准备生成目录，再通过完成标记减少重复路径扫描。
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using Path = System.IO.Path;
using NovaFramework.Runtime;
using UnityEditor;
using UnityEngine;

namespace NovaFramework.Sdk.Appsflyer.Samples.Editor
{
    /// <summary>
    /// Sample import 后路径自适应重写器。
    /// 域重载完成后延迟扫描自身所在 Sample 根目录，按 SamplePathManifest 描述的清单
    /// 准备清单明确声明的生成目录，再把目标资产的开发态路径前缀替换为真实导入路径。
    /// 目录恢复不受路径重写完成标记限制，也不复制或伪造构建产物。
    /// </summary>
    [InitializeOnLoad]
    internal static class SamplePathRewriter
    {
        /// <summary>
        /// 导入过程中文件尚未全部落盘时的最大延迟重试次数。
        /// </summary>
        private const int c_MaxDelayedRetries = 5;

        private static int s_DelayedRetryCount;

        /// <summary>
        /// 静态构造，域重载完成后延迟执行重写检查。
        /// </summary>
        static SamplePathRewriter()
        {
            EditorApplication.delayCall += RunRewrite;
        }

        /// <summary>
        /// 主入口：遍历当前工程中所有同类 manifest，分别重写其所属 Sample。
        /// 每次检查先准备声明的目录；单个旧清单失效时跳过该项，不阻断其他有效清单。
        /// </summary>
        private static void RunRewrite()
        {
            IReadOnlyList<SamplePathManifest> manifests = LocateManifests();
            bool assetChanged = false;
            bool retryRequired = false;
            foreach (SamplePathManifest manifest in manifests)
            {
                string sampleRoot = LocateSampleRoot(manifest);
                if (string.IsNullOrEmpty(sampleRoot) || sampleRoot == manifest.DevSampleRoot)
                {
                    continue;
                }

                try
                {
                    // 完成标记只负责路径重写，不能跳过目录恢复。
                    PrepareGeneratedDirectories(manifest, sampleRoot);
                    if (manifest.RewriteTargets.Count == 0)
                    {
                        Log.Warning(LogTag.Editor,
                            $"[SamplePathRewriter] 重写清单为空，拒绝写入完成标记: {sampleRoot}");
                        continue;
                    }

                    string markerPath = $"{sampleRoot}/{manifest.RewrittenMarker}";
                    bool hasPendingRewrite = ContainsDevSampleRoot(manifest, sampleRoot, out bool allTargetsAvailable);
                    if (File.Exists(markerPath) && !hasPendingRewrite && allTargetsAvailable)
                    {
                        continue;
                    }

                    int rewrittenCount = RewriteAll(manifest, sampleRoot);
                    assetChanged |= rewrittenCount > 0;
                    hasPendingRewrite = ContainsDevSampleRoot(manifest, sampleRoot, out allTargetsAvailable);
                    if (!allTargetsAvailable || hasPendingRewrite)
                    {
                        retryRequired |= !allTargetsAvailable || hasPendingRewrite;
                        if (allTargetsAvailable || s_DelayedRetryCount >= c_MaxDelayedRetries)
                        {
                            Log.Warning(LogTag.Editor,
                                $"[SamplePathRewriter] Sample 路径尚未全部就绪，未写入完成标记: {sampleRoot}");
                        }
                        continue;
                    }

                    File.WriteAllText(markerPath, $"sampleRoot={sampleRoot}\nrewrittenCount={rewrittenCount}\n");

                    Log.Debug(LogTag.Editor,
                        $"[SamplePathRewriter] 已重写 {rewrittenCount} 个资产路径前缀: {manifest.DevSampleRoot} -> {sampleRoot}");
                }
                catch (Exception exception)
                {
                    Log.Warning(LogTag.Editor,
                        $"[SamplePathRewriter] Sample 导入准备失败: {sampleRoot}。{exception.Message}");
                }
            }

            if (assetChanged)
            {
                AssetDatabase.Refresh();
            }

            if (retryRequired && s_DelayedRetryCount < c_MaxDelayedRetries)
            {
                s_DelayedRetryCount++;
                EditorApplication.delayCall += RunRewrite;
            }
            else
            {
                s_DelayedRetryCount = 0;
            }
        }

        /// <summary>
        /// 只准备清单声明的当前 Sample 生成目录，并确认 AssetDatabase 已识别。
        /// 未声明的源资产目录不做补齐；目录就绪不表示生成文件已经就绪。
        /// </summary>
        /// <param name="manifest">当前类型对应的 Sample 导入清单。</param>
        /// <param name="sampleRoot">由清单资产定位到的真实 Sample 根目录。</param>
        private static void PrepareGeneratedDirectories(SamplePathManifest manifest, string sampleRoot)
        {
            if (manifest.GeneratedDirectories == null || manifest.GeneratedDirectories.Count == 0)
            {
                return;
            }

            // 限定在已导入的 Assets/Samples 内，不能由此创建或切换工程工作区。
            if (!sampleRoot.StartsWith("Assets/Samples/", StringComparison.Ordinal)
                || !Directory.Exists(sampleRoot))
            {
                throw new InvalidOperationException($"生成目录所属 Sample 无效: {sampleRoot}");
            }

            List<string[]> directories = new List<string[]>(manifest.GeneratedDirectories.Count);
            foreach (string relative in manifest.GeneratedDirectories)
            {
                if (string.IsNullOrWhiteSpace(relative) || relative != relative.Trim()
                    || Path.IsPathRooted(relative) || relative.Contains("\\") || relative.Contains(":"))
                {
                    throw new InvalidOperationException($"生成目录必须是 Sample 内的相对目录: {relative}");
                }

                string[] segments = relative.Split('/');
                foreach (string segment in segments)
                {
                    if (string.IsNullOrWhiteSpace(segment) || segment == "." || segment == ".."
                        || segment.StartsWith(".", StringComparison.Ordinal)
                        || segment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                    {
                        throw new InvalidOperationException($"生成目录含无效路径段: {relative}");
                    }
                }
                directories.Add(segments);
            }

            // 不沿符号链接写入 Sample 外部；也检查 Sample 根路径的所有现有父目录。
            string current = string.Empty;
            foreach (string segment in sampleRoot.Split('/'))
            {
                current = string.IsNullOrEmpty(current) ? segment : $"{current}/{segment}";
                RejectLinkedDirectory(current);
            }

            if (!AssetDatabase.IsValidFolder(sampleRoot))
            {
                AssetDatabase.ImportAsset(sampleRoot, ImportAssetOptions.ForceSynchronousImport);
            }
            if (!AssetDatabase.IsValidFolder(sampleRoot))
            {
                throw new IOException($"AssetDatabase 尚未识别 Sample 根目录: {sampleRoot}");
            }

            foreach (string[] segments in directories)
            {
                current = sampleRoot;
                foreach (string segment in segments)
                {
                    string parent = current;
                    current = $"{parent}/{segment}";
                    RejectLinkedDirectory(current);
                    if (File.Exists(current))
                    {
                        throw new IOException($"生成目录被同名文件占用: {current}");
                    }

                    if (!Directory.Exists(current))
                    {
                        if (File.Exists($"{current}.meta"))
                        {
                            // 空目录可能被打包器省略；保留随包文件夹元数据并重新注册。
                            Directory.CreateDirectory(current);
                        }
                        else if (string.IsNullOrEmpty(AssetDatabase.CreateFolder(parent, segment)))
                        {
                            throw new IOException($"无法创建生成目录: {current}");
                        }
                    }

                    if (!AssetDatabase.IsValidFolder(current))
                    {
                        AssetDatabase.ImportAsset(current, ImportAssetOptions.ForceSynchronousImport);
                    }
                    if (!AssetDatabase.IsValidFolder(current))
                    {
                        throw new IOException($"AssetDatabase 尚未识别生成目录: {current}");
                    }
                }
            }
        }

        /// <summary>
        /// 拒绝经符号链接或重解析点准备目录，防止声明路径落到当前 Sample 之外。
        /// </summary>
        /// <param name="path">已存在或即将创建的项目相对目录。</param>
        private static void RejectLinkedDirectory(string path)
        {
            if ((Directory.Exists(path) || File.Exists(path))
                && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            {
                throw new IOException($"生成目录不能经过符号链接: {path}");
            }
        }

        /// <summary>
        /// 查找当前工程中所有 SamplePathManifest 资产。
        /// </summary>
        /// <returns>所有可加载的 manifest。</returns>
        private static IReadOnlyList<SamplePathManifest> LocateManifests()
        {
            string[] guids = AssetDatabase.FindAssets($"t:{nameof(SamplePathManifest)}");
            List<SamplePathManifest> manifests = new List<SamplePathManifest>(guids.Length);
            foreach (string guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                SamplePathManifest manifest = AssetDatabase.LoadAssetAtPath<SamplePathManifest>(assetPath);
                if (manifest != null)
                {
                    manifests.Add(manifest);
                }
            }

            return manifests;
        }

        /// <summary>
        /// 由 manifest 自身资产路径反推所在 Sample 根目录。
        /// </summary>
        /// <param name="manifest">已定位的 manifest 资产。</param>
        /// <returns>Sample 根目录路径；解析失败返回 null。</returns>
        private static string LocateSampleRoot(SamplePathManifest manifest)
        {
            string manifestPath = AssetDatabase.GetAssetPath(manifest);
            if (string.IsNullOrEmpty(manifestPath))
            {
                return null;
            }

            string devRoot = manifest.DevSampleRoot;
            if (string.IsNullOrEmpty(devRoot))
            {
                return null;
            }

            int idx = manifestPath.LastIndexOf($"/{System.IO.Path.GetFileName(devRoot)}/", System.StringComparison.Ordinal);
            if (idx < 0)
            {
                return null;
            }

            return manifestPath.Substring(0, idx + 1 + System.IO.Path.GetFileName(devRoot).Length);
        }

        /// <summary>
        /// 检查清单目标是否仍包含开发态 Sample 根路径，同时确认目标文件已全部导入。
        /// </summary>
        /// <param name="manifest">路径清单。</param>
        /// <param name="sampleRoot">当前 Sample 的真实根目录。</param>
        /// <param name="allTargetsAvailable">所有清单目标都已存在时为 true。</param>
        /// <returns>任一目标仍包含开发态根路径时为 true。</returns>
        private static bool ContainsDevSampleRoot(
            SamplePathManifest manifest,
            string sampleRoot,
            out bool allTargetsAvailable)
        {
            allTargetsAvailable = true;
            bool containsDevSampleRoot = false;
            foreach (string relative in manifest.RewriteTargets)
            {
                string full = $"{sampleRoot}/{relative}";
                if (!File.Exists(full))
                {
                    allTargetsAvailable = false;
                    continue;
                }

                if (File.ReadAllText(full).Contains(manifest.DevSampleRoot))
                {
                    containsDevSampleRoot = true;
                }
            }

            return containsDevSampleRoot;
        }

        /// <summary>
        /// 遍历清单中的目标资产，对每个文件执行字符串前缀替换。
        /// </summary>
        /// <param name="manifest">路径清单。</param>
        /// <param name="sampleRoot">本 Sample 在外部工程中的真实根目录。</param>
        /// <returns>实际重写过的文件数量。</returns>
        private static int RewriteAll(SamplePathManifest manifest, string sampleRoot)
        {
            int count = 0;
            string oldPrefix = manifest.DevSampleRoot;
            string newPrefix = sampleRoot;

            foreach (string relative in manifest.RewriteTargets)
            {
                string full = $"{sampleRoot}/{relative}";
                if (!File.Exists(full))
                {
                    continue;
                }

                string text = File.ReadAllText(full);
                if (!text.Contains(oldPrefix))
                {
                    continue;
                }

                string replaced = text.Replace(oldPrefix, newPrefix);
                if (replaced == text)
                {
                    continue;
                }

                File.WriteAllText(full, replaced);
                count++;
            }

            return count;
        }
    }
}
