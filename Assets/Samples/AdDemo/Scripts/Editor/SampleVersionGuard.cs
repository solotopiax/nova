/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  SampleVersionGuard.cs
 * author:    taoye
 * created:   2026/5/21
 * descrip:   Demo 样例兜底守卫
 *            ① 检测 Samples 根目录下的旧版本 Demo，弹窗询问清理；
 *            ② 询问是否将本 Demo 的 AdDemo 场景设为 Build Settings 启动场景。
 *            不使用 EditorPrefs（项目铁律），每次域重载实际状态检查，状态正确则静默返回。
 ***************************************************************/

using System.Collections.Generic;
using System.IO;
using System.Linq;
using NovaFramework.Runtime;
using UnityEditor;

namespace NovaFramework.Sdk.Ad.Samples.Editor
{
    /// <summary>
    /// Demo 样例兜底守卫。检测旧版本 Demo 并询问清理，提示是否将 AdDemo 设为启动场景。
    /// </summary>
    [InitializeOnLoad]
    internal static class SampleVersionGuard
    {
        /// <summary>
        /// Samples 根目录路径。
        /// </summary>
        private const string c_SamplesRoot = "Assets/Samples/Nova Framework";

        /// <summary>
        /// Demo 子目录名。
        /// </summary>
        private const string c_DemoSubdir = "AdDemo";

        /// <summary>
        /// 启动场景文件名（含扩展名）。
        /// </summary>
        private const string c_AdDemoSceneName = "AdDemo.unity";

        /// <summary>
        /// 静态构造，域重载完成后延迟执行版本检查。
        /// </summary>
        static SampleVersionGuard()
        {
            EditorApplication.delayCall += RunChecks;
        }

        /// <summary>
        /// 定位最新 Demo 版本目录，依次执行旧版本清理与启动场景检查。
        /// </summary>
        private static void RunChecks()
        {
            List<string> versions = ResolveDemoVersions();
            if (versions.Count == 0)
            {
                return;
            }

            string latestVersion = versions[0];
            string selfDemoDir = $"{c_SamplesRoot}/{latestVersion}/{c_DemoSubdir}";

            CheckStaleVersions(latestVersion, versions);
            CheckStartupScene(selfDemoDir);
        }

        /// <summary>
        /// 枚举 Samples 根下所有含 AdDemo 子目录的版本号，按 semver 降序返回。
        /// </summary>
        /// <returns>版本号列表（降序）；无效或无版本时返回空列表。</returns>
        private static List<string> ResolveDemoVersions()
        {
            if (!AssetDatabase.IsValidFolder(c_SamplesRoot))
            {
                return new List<string>();
            }

            return AssetDatabase.GetSubFolders(c_SamplesRoot)
                .Select(path => path.Substring(c_SamplesRoot.Length + 1))
                .Where(ver => AssetDatabase.IsValidFolder($"{c_SamplesRoot}/{ver}/{c_DemoSubdir}"))
                .OrderByDescending(ParseVersionKey)
                .ToList();
        }

        /// <summary>
        /// 把 semver 字符串拆成 (major, minor, patch) 整数组用于排序；非数字段视为 0。
        /// </summary>
        /// <param name="version">版本号字符串。</param>
        /// <returns>用于排序的整数组。</returns>
        private static (int, int, int) ParseVersionKey(string version)
        {
            string[] parts = version.Split('.');
            int Get(int i) => parts.Length > i && int.TryParse(parts[i], out int v) ? v : 0;
            return (Get(0), Get(1), Get(2));
        }

        /// <summary>
        /// 当 Samples 根下存在多个版本时弹窗询问是否清理低版本目录，仅保留最新版本。
        /// </summary>
        /// <param name="latestVersion">本次保留的最新版本号。</param>
        /// <param name="allVersions">已排序的版本列表（降序）。</param>
        private static void CheckStaleVersions(string latestVersion, List<string> allVersions)
        {
            List<string> staleVersions = allVersions.Where(ver => ver != latestVersion).ToList();

            if (staleVersions.Count == 0)
            {
                return;
            }

            string list = string.Join("\n  - ", staleVersions);
            bool clean = EditorUtility.DisplayDialog(
                "Nova Framework Demo",
                $"检测到旧版本 Demo:\n  - {list}\n\n保留最新版本 {latestVersion}，建议删除上述旧版本以避免 asmdef 名称冲突。是否立即删除?",
                "删除",
                "稍后处理");

            if (!clean)
            {
                return;
            }

            foreach (string ver in staleVersions)
            {
                string oldPath = $"{c_SamplesRoot}/{ver}";
                if (!AssetDatabase.DeleteAsset(oldPath))
                {
                    Log.Warning(LogTag.Editor, $"[SampleVersionGuard] 无法删除旧 Demo 目录: {oldPath}");
                }
            }
            AssetDatabase.Refresh();
        }

        /// <summary>
        /// 检查 Build Settings 第一项是否为本 Demo 的 AdDemo 场景，否则弹窗询问设为启动场景。
        /// </summary>
        /// <param name="selfDemoDir">本 Demo 目录路径。</param>
        private static void CheckStartupScene(string selfDemoDir)
        {
            string targetScenePath = $"{selfDemoDir}/{c_AdDemoSceneName}";
            if (!File.Exists(targetScenePath))
            {
                return;
            }

            EditorBuildSettingsScene[] current = EditorBuildSettings.scenes;
            if (current.Length > 0 && current[0].path == targetScenePath && current[0].enabled)
            {
                return;
            }

            bool apply = EditorUtility.DisplayDialog(
                "Nova Framework Demo",
                $"是否将本 Demo 的 AdDemo 场景设为启动场景?\n\n目标: {targetScenePath}\n(会替换 Build Settings 第一项)",
                "设为启动场景",
                "稍后");

            if (!apply)
            {
                return;
            }

            List<EditorBuildSettingsScene> rebuilt = new List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(targetScenePath, true),
            };
            rebuilt.AddRange(current.Where(s => s.path != targetScenePath));
            EditorBuildSettings.scenes = rebuilt.ToArray();
        }
    }
}
