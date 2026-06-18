/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  SamplePathRewriter.cs
 * author:    taoye
 * created:   2026/5/21
 * descrip:   Sample import 后路径自适应重写器
 *            外部工程通过 Package Manager 导入 Sample 后，sample 内
 *            任何字符串字段如 "Assets/Samples/MainDemo/..." 都会指向
 *            开发工程旧路径。本脚本读取 SamplePathManifest，把所有列入
 *            清单的资产文件中的字符串前缀替换为 import 后的真实路径
 *            （形如 "Assets/Samples/Nova Framework/{version}/MainDemo"）。
 *            通过写入标记文件防重入，保证整套 sample 仅重写一次。
 ***************************************************************/

using System.Collections.Generic;
using System.IO;
using System.Linq;
using NovaFramework.Runtime;
using UnityEditor;
using UnityEngine;

namespace NovaFramework.Samples.Editor
{
    /// <summary>
    /// Sample import 后路径自适应重写器。
    /// 域重载完成后延迟扫描自身所在 Sample 根目录，按 SamplePathManifest 描述的清单
    /// 把所有目标资产中的开发工程路径前缀替换为 import 后的真实路径，写入标记防重入。
    /// </summary>
    [InitializeOnLoad]
    internal static class SamplePathRewriter
    {
        /// <summary>
        /// SamplePathManifest 资产名（不含扩展名）。发版脚本生成时使用同名。
        /// </summary>
        private const string c_ManifestAssetName = "SamplePathManifest";

        /// <summary>
        /// 静态构造，域重载完成后延迟执行重写检查。
        /// </summary>
        static SamplePathRewriter()
        {
            EditorApplication.delayCall += RunRewrite;
        }

        /// <summary>
        /// 主入口：定位 manifest，判断是否已重写，未重写则执行字符串前缀替换并落地标记。
        /// </summary>
        private static void RunRewrite()
        {
            SamplePathManifest manifest = LocateManifest();
            if (manifest == null)
            {
                return;
            }

            string sampleRoot = LocateSampleRoot(manifest);
            if (string.IsNullOrEmpty(sampleRoot))
            {
                return;
            }

            if (sampleRoot == manifest.DevSampleRoot)
            {
                return;
            }

            string markerPath = $"{sampleRoot}/{manifest.RewrittenMarker}";
            if (File.Exists(markerPath))
            {
                return;
            }

            int rewrittenCount = RewriteAll(manifest, sampleRoot);
            File.WriteAllText(markerPath, $"sampleRoot={sampleRoot}\nrewrittenCount={rewrittenCount}\n");
            AssetDatabase.Refresh();

            Log.Debug(LogTag.Editor, $"[SamplePathRewriter] 已重写 {rewrittenCount} 个资产路径前缀: {manifest.DevSampleRoot} -> {sampleRoot}");
        }

        /// <summary>
        /// 查找 SamplePathManifest 资产；通常每个 Sample 根目录下唯一一份。
        /// </summary>
        /// <returns>找到的 manifest；找不到返回 null。</returns>
        private static SamplePathManifest LocateManifest()
        {
            string[] guids = AssetDatabase.FindAssets($"t:{nameof(SamplePathManifest)}");
            return guids
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<SamplePathManifest>)
                .FirstOrDefault(asset => asset != null);
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
