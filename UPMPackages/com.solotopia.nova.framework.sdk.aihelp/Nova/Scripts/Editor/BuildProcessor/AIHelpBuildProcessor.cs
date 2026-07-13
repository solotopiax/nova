/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AIHelpBuildProcessor.cs
 * author:    taoye
 * created:   2026/7/9
 * descrip:   AIHelp 构建期处理器。Android：把 maven 依赖注入导出后的 unityLibrary
 *            build.gradle，并安全合并 androidx/jetifier（非删重写），使 Plugins/Android
 *            零提交。iOS：给 UnityFramework 与主 target 的 OTHER_LDFLAGS 加 -ObjC。
 ***************************************************************/

using System.IO;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Callbacks;
using UnityEngine;
#if UNITY_IOS
using UnityEditor.iOS.Xcode;
#endif

namespace NovaFramework.SDK.AIHelp.Editor
{
    /// <summary>
    /// AIHelp Android 构建期处理器：把 AIHelp 所需 maven 依赖注入 Unity 导出的
    /// unityLibrary/build.gradle，并按需补齐 gradle.properties 的 androidx/jetifier
    /// 标志（仅追加缺失键，不整份删重写）。全程只改导出目录，Assets/Plugins/Android 不落任何文件。
    /// </summary>
    public sealed class AIHelpAndroidBuildProcessor : IPostGenerateGradleAndroidProject
    {
        /// <summary>
        /// 回调顺序，取较大值以尽量在其它处理器之后运行。
        /// </summary>
        public int callbackOrder => 999;

        /// <summary>
        /// 注入块起始标记，配合结束标记实现幂等（重复构建先删旧块再插）。
        /// </summary>
        private const string BlockStart = "// AIHelp Deps Start";

        /// <summary>
        /// 注入块结束标记。
        /// </summary>
        private const string BlockEnd = "// AIHelp Deps End";

        /// <summary>
        /// Unity 导出 Android gradle 工程后回调：注入 maven 依赖并合并 gradle.properties。
        /// </summary>
        /// <param name="path">导出的 unityLibrary 模块目录（或其父级 gradle 工程根，随 Unity 版本而定）。</param>
        public void OnPostGenerateGradleAndroidProject(string path)
        {
            InjectDependencies(path);
            MergeGradleProperties(path);
        }

        /// <summary>
        /// 把 AIHelp maven 依赖幂等注入 build.gradle 的 dependencies{} 块。
        /// </summary>
        /// <param name="modulePath">unityLibrary 模块目录。</param>
        private void InjectDependencies(string modulePath)
        {
            string gradleFile = Path.Combine(modulePath, "build.gradle");
            if (!File.Exists(gradleFile))
            {
                Debug.LogWarning("[AIHelp] 未找到 build.gradle，跳过依赖注入：" + gradleFile);
                return;
            }

            string content = File.ReadAllText(gradleFile);
            content = RemoveExistingBlock(content);

            int depsIndex = content.IndexOf("dependencies {");
            if (depsIndex < 0)
            {
                Debug.LogWarning("[AIHelp] build.gradle 未找到 dependencies{} 块，跳过注入。");
                return;
            }

            int insertAt = content.IndexOf('\n', depsIndex) + 1;
            // multidex/appcompat 版本号与 aar 版本均取自 AIHelp 官方 6.0 SDK 自带的 mainTemplate.gradle 要求，
            // 日后核对来源或升级 AIHelp SDK 版本时应对照官方最新 mainTemplate.gradle 校正。
            string block =
                BlockStart + "\n" +
                "    implementation 'androidx.multidex:multidex:2.0.1'\n" +
                "    implementation 'androidx.appcompat:appcompat:1.0.2'\n" +
                "    implementation 'net.aihelp:android-aihelp-aar:6.0.+'\n" +
                "    " + BlockEnd + "\n";
            content = content.Insert(insertAt, "    " + block);
            File.WriteAllText(gradleFile, content);
            Debug.Log("[AIHelp] 已注入 Android maven 依赖到 " + gradleFile);
        }

        /// <summary>
        /// 移除既有注入块，保证重复构建幂等。
        /// </summary>
        /// <param name="content">build.gradle 全文。</param>
        /// <returns>移除注入块后的文本。</returns>
        private string RemoveExistingBlock(string content)
        {
            int s = content.IndexOf(BlockStart);
            int e = content.IndexOf(BlockEnd);
            if (s >= 0 && e > s)
            {
                int lineEnd = content.IndexOf('\n', e);
                if (lineEnd < 0)
                {
                    lineEnd = content.Length - 1;
                }
                // 连同起始行前的缩进一起删。
                int lineStart = content.LastIndexOf('\n', s);
                if (lineStart < 0)
                {
                    lineStart = 0;
                }
                content = content.Remove(lineStart, lineEnd - lineStart + 1);
            }
            return content;
        }

        /// <summary>
        /// 安全合并 gradle.properties：仅追加缺失的 androidx/jetifier 键，绝不整份删重写。
        /// </summary>
        /// <param name="modulePath">unityLibrary 模块目录（gradle.properties 位于其父级 gradle 工程根）。</param>
        private void MergeGradleProperties(string modulePath)
        {
            string root = Directory.GetParent(modulePath)?.FullName ?? modulePath;
            string propsFile = Path.Combine(root, "gradle.properties");
            var required = new (string Key, string Line)[]
            {
                ("android.useAndroidX", "android.useAndroidX=true"),
                ("android.enableJetifier", "android.enableJetifier=true"),
            };

            string existing = File.Exists(propsFile) ? File.ReadAllText(propsFile) : string.Empty;
            var toAppend = new System.Text.StringBuilder();
            foreach (var (key, line) in required)
            {
                if (existing.IndexOf(key + "=") < 0)
                {
                    toAppend.AppendLine(line);
                }
            }
            if (toAppend.Length > 0)
            {
                File.AppendAllText(propsFile, (existing.EndsWith("\n") || existing.Length == 0 ? string.Empty : "\n") + toAppend);
                Debug.Log("[AIHelp] 已合并 gradle.properties androidx/jetifier 标志。");
            }
        }
    }

    /// <summary>
    /// AIHelp iOS 构建期处理器：给 UnityFramework 与主 target 的 OTHER_LDFLAGS 加 -ObjC，
    /// 确保 AIHelp framework 的 Objective-C category 符号被链接。
    /// </summary>
    public static class AIHelpIOSBuildProcessor
    {
        /// <summary>
        /// iOS 构建后回调：向 Xcode 工程写入 -ObjC 链接标志。
        /// </summary>
        /// <param name="target">构建目标平台。</param>
        /// <param name="pathToBuiltProject">导出的 Xcode 工程目录。</param>
        [PostProcessBuild(999)]
        public static void OnPostProcessBuild(BuildTarget target, string pathToBuiltProject)
        {
#if UNITY_IOS
            if (target != BuildTarget.iOS)
            {
                return;
            }
            string projPath = PBXProject.GetPBXProjectPath(pathToBuiltProject);
            var proj = new PBXProject();
            proj.ReadFromFile(projPath);

            string frameworkGuid = proj.GetUnityFrameworkTargetGuid();
            if (!string.IsNullOrEmpty(frameworkGuid))
            {
                proj.AddBuildProperty(frameworkGuid, "OTHER_LDFLAGS", "-ObjC");
            }
            string mainGuid = proj.GetUnityMainTargetGuid();
            if (!string.IsNullOrEmpty(mainGuid))
            {
                proj.AddBuildProperty(mainGuid, "OTHER_LDFLAGS", "-ObjC");
            }
            proj.WriteToFile(projPath);
            Debug.Log("[AIHelp] 已为 iOS target 追加 -ObjC。");
#endif
        }
    }
}
