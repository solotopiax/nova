/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.Build.Methods.cs
 * author:    taoye
 * created:   2026/5/10
 * descrip:   EditorUtil.Build 私有方法
 ***************************************************************/

using NovaFramework.Runtime;
using UnityEditor;
using UnityEngine;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        public static partial class Build
        {
            /// <summary>
            /// 组装 BuildPlayerOptions，scenes 置 null 由 Unity 自动使用 Build Settings 中的启用场景列表。
            /// 不显式传 scenes，避免与 EditorBuildSettings 顺序不一致导致 ProducedWithErrors。
            /// </summary>
            /// <param name="target">目标平台。</param>
            /// <param name="outputPath">产物路径。</param>
            /// <param name="developmentBuild">是否开发构建。</param>
            /// <param name="buildMode">打包方式，决定附加哪个互斥 BuildOptions flag。</param>
            /// <returns>BuildPlayerOptions。</returns>
            private static BuildPlayerOptions ResolveScenes(BuildTarget target, string outputPath, bool developmentBuild, BuildMode buildMode)
            {
                BuildOptions options = developmentBuild ? BuildOptions.Development : BuildOptions.None;
                switch (buildMode)
                {
                    case BuildMode.CleanBuild: options |= BuildOptions.CleanBuildCache; break;
                    case BuildMode.ForceSkipDataBuild: options |= BuildOptions.BuildScriptsOnly; break;
                }
                BuildPlayerOptions opts = new BuildPlayerOptions
                {
                    locationPathName = outputPath,
                    target = target,
                    targetGroup = BuildPipeline.GetBuildTargetGroup(target),
                    options = options
                };
                return opts;
            }

            /// <summary>
            /// 将 outputFolder（可能为项目根相对路径或绝对路径）解析为绝对路径。
            /// 复用 Shell.cs 相同策略：System.IO.Path.IsPathRooted 判断，相对路径基于项目根拼接。
            /// </summary>
            /// <param name="outputFolder">原始输出文件夹路径（相对或绝对）。</param>
            /// <returns>绝对路径。</returns>
            private static string ResolveOutputFolder(string outputFolder)
            {
                if (System.IO.Path.IsPathRooted(outputFolder)) return outputFolder;
                string projectRoot = Util.SysIO.Path.GetDirectoryName(Application.dataPath);
                return Util.SysIO.Path.GetFullPath(Util.SysIO.Path.Combine(projectRoot, outputFolder));
            }

            /// <summary>
            /// 根据目标平台和 buildAppBundle 参数解析产物文件后缀。
            /// Android 非工程导出模式：buildAppBundle=true 返回 ".aab"，否则返回 ".apk"。
            /// iOS / WebGL / Android 导出工程（exportAsGoogleAndroidProject）：返回空串（产物为目录）。
            /// </summary>
            /// <param name="target">目标平台。</param>
            /// <param name="buildAppBundle">是否构建 AAB。</param>
            /// <returns>文件后缀（含点号，或空串）。</returns>
            private static string ResolveExtension(BuildTarget target, bool buildAppBundle)
            {
                if (target == BuildTarget.Android && !EditorUserBuildSettings.exportAsGoogleAndroidProject)
                {
                    return buildAppBundle ? ".aab" : ".apk";
                }
                return string.Empty;
            }
        }
    }
}
