/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  NovaBuildPostprocessor.cs
 * author:    yingzheng
 * created:   2026/4/24
 * descrip:   Nova 构建后处理器，callbackOrder = int.MaxValue 确保最后执行
 ***************************************************************/

using NovaFramework.Runtime;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
#if UNITY_IOS
using UnityEditor.iOS.Xcode;
#endif

namespace NovaFramework.Editor
{
    /// <summary>
    /// Nova 构建后处理器。
    /// callbackOrder = int.MaxValue，在所有其他后处理器（包括 Unity 原生 plist 写入）之后执行。
    /// 从共享状态读取上下文和处理器实例，按平台分发到各 SDK 处理器。
    /// </summary>
    public sealed class NovaBuildPostprocessor : IPostprocessBuildWithReport
    {
        /// <summary>
        /// 后处理回调顺序，设为 int.MaxValue 确保在所有其他后处理器之后执行。
        /// </summary>
        public int callbackOrder => int.MaxValue;

        /// <summary>
        /// Unity 构建后处理入口。从共享状态读取上下文，按平台分发到各 SDK 处理器。
        /// </summary>
        /// <param name="report">Unity 构建报告。</param>
        public void OnPostprocessBuild(BuildReport report)
        {
            if (NovaBuildShared.Context == null)
            {
                Log.Warning(LogTag.Editor, "[NovaBuildPostprocessor] OnPostprocessBuild 被调用但 OnPreprocessBuild 未执行，跳过后处理。");
                return;
            }

            if (NovaBuildShared.Processors == null)
                NovaBuildShared.Processors = NovaBuildShared.CollectProcessors();

            switch (NovaBuildShared.Context.Target)
            {
                case BuildTarget.Android:
                    PostprocessAndroid(report);
                    break;
                case BuildTarget.iOS:
                    PostprocessiOS(report);
                    break;
                case BuildTarget.WebGL:
                    PostprocessWebGL(report);
                    break;
            }
        }

        /// <summary>
        /// 执行 Android 平台后处理：依次调用各处理器。
        /// </summary>
        /// <param name="report">Unity 构建报告。</param>
        private void PostprocessAndroid(BuildReport report)
        {
            NovaBuildShared.Processors.Sort((a, b) => a.PostprocessPriority.CompareTo(b.PostprocessPriority));
            foreach (var processor in NovaBuildShared.Processors)
            {
                Log.Debug(LogTag.Editor, $"[NovaBuildPostprocessor] Android 后处理：{processor.GetType().Name}");
                processor.OnPostprocessBuildOnAndroid(report, NovaBuildShared.Context);
            }
        }

        /// <summary>
        /// 执行 iOS 平台后处理：加载 Xcode 工程、Plist、Entitlements，依次调用各处理器，最后保存所有文件。
        /// </summary>
        /// <param name="report">Unity 构建报告。</param>
        private void PostprocessiOS(BuildReport report)
        {
#if UNITY_IOS
            var ctx = NovaBuildShared.Context;
            string buildPath = report.summary.outputPath;

            string pbxPath = PBXProject.GetPBXProjectPath(buildPath);
            ctx.XProj = new PBXProject();
            ctx.XProj.ReadFromFile(pbxPath);
            ctx.TargetGuid = ctx.XProj.GetUnityMainTargetGuid();

            string plistPath = Util.SysIO.Path.Combine(buildPath, "Info.plist");
            ctx.XPlist = new PlistDocument();
            ctx.XPlist.ReadFromFile(plistPath);
            ctx.XPlistDict = ctx.XPlist.root;

            string entitlementsRelPath = UnityEngine.Application.productName.Replace(" ", "") + ".entitlements";
            ctx.RelativeEntitlementFilePath = entitlementsRelPath;

            string entitlementsFullPath = Util.SysIO.Path.Combine(buildPath, entitlementsRelPath);
            ctx.XEntitlements = new PlistDocument();
            if (Util.SysIO.File.Exists(entitlementsFullPath))
                ctx.XEntitlements.ReadFromFile(entitlementsFullPath);

            NovaBuildShared.Processors.Sort((a, b) => a.PostprocessPriority.CompareTo(b.PostprocessPriority));
            foreach (var processor in NovaBuildShared.Processors)
            {
                Log.Debug(LogTag.Editor, $"[NovaBuildPostprocessor] iOS 后处理：{processor.GetType().Name}");
                processor.OnPostprocessBuildOniOS(report, ctx);
            }

            ctx.XProj.WriteToFile(pbxPath);
            ctx.XPlist.WriteToFile(plistPath);
            if (ctx.XEntitlements.root.values.Count > 0)
                ctx.XEntitlements.WriteToFile(entitlementsFullPath);
#endif
        }

        /// <summary>
        /// 执行 WebGL 平台后处理：依次调用各处理器。
        /// </summary>
        /// <param name="report">Unity 构建报告。</param>
        private void PostprocessWebGL(BuildReport report)
        {
            NovaBuildShared.Processors.Sort((a, b) => a.PostprocessPriority.CompareTo(b.PostprocessPriority));
            foreach (var processor in NovaBuildShared.Processors)
            {
                Log.Debug(LogTag.Editor, $"[NovaBuildPostprocessor] WebGL 后处理：{processor.GetType().Name}");
                processor.OnPostprocessBuildOnWebGL(report, NovaBuildShared.Context);
            }
        }
    }
}
