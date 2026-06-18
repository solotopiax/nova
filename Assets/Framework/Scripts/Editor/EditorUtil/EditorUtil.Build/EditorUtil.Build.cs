/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.Build.cs
 * author:    taoye
 * created:   2026/5/10
 * descrip:   Unity Player 打包封装
 ***************************************************************/

using System;
using System.Text.RegularExpressions;
using NovaFramework.Runtime;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        /// <summary>
        /// Unity Player 打包封装。
        /// 仅做 BuildPipeline.BuildPlayer 的薄封装 + 必要输入校验；不处理签名 / 上传 / 通知等外围逻辑。
        /// </summary>
        public static partial class Build
        {
            /// <summary>
            /// 启动一次 Player 打包。
            /// </summary>
            /// <param name="target">目标平台。</param>
            /// <param name="outputPath">完整产物路径（Android 为 apk 文件路径 / iOS 为工程目录路径）。</param>
            /// <param name="developmentBuild">是否开发构建。</param>
            /// <param name="buildMode">打包方式，对应 Build Profiles 中 Build 按钮的三种触发形态。</param>
            /// <returns>Unity 构建结果。</returns>
            public static BuildReport BuildPlayer(BuildTarget target, string outputPath, bool developmentBuild, BuildMode buildMode)
            {
                if (string.IsNullOrEmpty(outputPath))
                {
                    throw new ArgumentException(string.Format("{0} outputPath 不能为空", c_LogPrefix));
                }
                BuildPlayerOptions opts = ResolveScenes(target, outputPath, developmentBuild, buildMode);
                Log.Debug(LogTag.Editor,
                    "{0} 开始打包：target={1}, output={2}, dev={3}, mode={4}",
                    c_LogPrefix, target, outputPath, developmentBuild, buildMode);
                BuildReport report = BuildPipeline.BuildPlayer(opts);
                if (report.summary.result == BuildResult.Failed)
                {
                    throw new InvalidOperationException(string.Format("{0} 打包失败：{1}", c_LogPrefix, report.summary.result));
                }
                Log.Debug(LogTag.Editor, "{0} 打包完成：result={1}, output={2}", c_LogPrefix, report.summary.result, report.summary.outputPath);
                return report;
            }

            /// <summary>
            /// 按文件夹路径打包：自动生成文件名并处理 Android AAB/APK 相关 Build Settings 临时切换。
            /// 文件名格式：{productName字母数字}_{Debug|Release}_{bundleVersion}_{yyyy_MM_dd_HH_mm}[.apk|.aab]（iOS/工程目录无后缀）。
            /// 文件名中的 Debug/Release 段取自 developMode（ConfigRuntimeSO.DevelopMode，反映工程配置的环境档位），
            /// 与控制 Unity 开发构建选项的 developmentBuild 是两个独立概念。
            /// Android 平台下会临时写入 EditorUserBuildSettings.buildAppBundle 与
            /// PlayerSettings.Android.splitApplicationBinary，在 try/finally 中还原，不污染工程 Build Settings。
            /// </summary>
            /// <param name="target">目标平台。</param>
            /// <param name="outputFolder">输出文件夹路径（遵循项目根相对路径规范；不存在时自动创建）。</param>
            /// <param name="developmentBuild">是否 Unity 开发构建（控制 BuildOptions.Development；与文件名环境段无关）。</param>
            /// <param name="buildMode">打包方式，对应 Build Profiles 中 Build 按钮的三种触发形态。</param>
            /// <param name="buildAppBundle">Android 专用：是否构建 AAB（仅 Android 非工程导出模式生效）。</param>
            /// <param name="splitApplicationBinary">Android 专用：是否拆分应用 Binary（仅 buildAppBundle=true 时生效）。</param>
            /// <param name="developMode">文件名环境段来源（Debug/Release），取自 ConfigRuntimeSO.DevelopMode；与 developmentBuild 独立。</param>
            /// <returns>Unity 构建结果。</returns>
            public static BuildReport BuildPackage(BuildTarget target, string outputFolder,
                bool developmentBuild, BuildMode buildMode, bool buildAppBundle, bool splitApplicationBinary,
                DevelopMode developMode = DevelopMode.Debug)
            {
                if (string.IsNullOrEmpty(outputFolder))
                {
                    throw new ArgumentException(string.Format("{0} outputFolder 不能为空", c_LogPrefix));
                }
                string absoluteFolder = ResolveOutputFolder(outputFolder);
                if (!System.IO.Directory.Exists(absoluteFolder))
                {
                    System.IO.Directory.CreateDirectory(absoluteFolder);
                }
                string ext = ResolveExtension(target, buildAppBundle);
                string fileName = string.Format("{0}_{1}_{2}_{3}{4}",
                    Regex.Replace(PlayerSettings.productName, "[^a-zA-Z0-9]", ""),
                    developMode.ToString(),
                    PlayerSettings.bundleVersion,
                    DateTime.Now.ToString("yyyy_MM_dd_HH_mm"),
                    ext);
                string fullPath = Util.SysIO.Path.Combine(absoluteFolder, fileName);
                Log.Debug(LogTag.Editor, "{0} 输出文件夹：{1}，产物文件名：{2}", c_LogPrefix, absoluteFolder, fileName);
                if (target == BuildTarget.Android)
                {
                    // 快照 Android 相关 Build Settings，try/finally 还原，不污染工程配置
                    bool prevBuildAppBundle = EditorUserBuildSettings.buildAppBundle;
                    bool prevSplitApplicationBinary = PlayerSettings.Android.splitApplicationBinary;
                    try
                    {
                        EditorUserBuildSettings.buildAppBundle = buildAppBundle;
                        PlayerSettings.Android.splitApplicationBinary = buildAppBundle && splitApplicationBinary;
                        return BuildPlayer(target, fullPath, developmentBuild, buildMode);
                    }
                    finally
                    {
                        EditorUserBuildSettings.buildAppBundle = prevBuildAppBundle;
                        PlayerSettings.Android.splitApplicationBinary = prevSplitApplicationBinary;
                    }
                }
                return BuildPlayer(target, fullPath, developmentBuild, buildMode);
            }
        }
    }
}
