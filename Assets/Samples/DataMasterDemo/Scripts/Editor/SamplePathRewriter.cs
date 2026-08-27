/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  SamplePathRewriter.cs
 * author:    taoye
 * created:   2026/5/21
 * descrip:   Sample import 后路径自适应重写器
 *            外部工程通过 Package Manager 导入 Sample 后，sample 内
 *            任何字符串字段如 "Assets/Samples/DataMasterDemo/..." 都会指向
 *            开发工程旧路径。本脚本读取 SamplePathManifest，把所有列入
 *            清单的资产文件中的字符串前缀替换为 import 后的真实路径
 *            （形如 "Assets/Samples/Nova Framework/{version}/DataMasterDemo"）。
 *            通过完成标记减少重复扫描，并在包升级后重新校验待重写路径。
 ***************************************************************/

using System.Collections.Generic;
using System.IO;
using NovaFramework.Runtime;
using UnityEditor;
using UnityEngine;

namespace NovaFramework.Sdk.Datamaster.Samples.Editor
{
    /// <summary>
    /// Sample import 后路径自适应重写器。
    /// 域重载完成后延迟扫描自身所在 Sample 根目录，按 SamplePathManifest 描述的清单
    /// 把所有目标资产中的开发工程路径前缀替换为 import 后的真实路径，完成后写入标记。
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
        /// 旧 Sample 留存的 manifest 无效时只跳过该项，不能阻断新导入 Sample 的路径重写。
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
                assetChanged |= rewrittenCount > 0;

                Log.Debug(LogTag.Editor,
                    $"[SamplePathRewriter] 已重写 {rewrittenCount} 个资产路径前缀: {manifest.DevSampleRoot} -> {sampleRoot}");
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
