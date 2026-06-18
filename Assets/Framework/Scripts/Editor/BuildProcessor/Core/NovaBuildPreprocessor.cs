/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  NovaBuildPreprocessor.cs
 * author:    yingzheng
 * created:   2026/4/24
 * descrip:   Nova 构建预处理器，callbackOrder = -1 确保最先执行
 ***************************************************************/

using NovaFramework.Runtime;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace NovaFramework.Editor
{
    /// <summary>
    /// Nova 构建预处理器。
    /// callbackOrder = -1，在所有其他预处理器之前执行。
    /// 创建构建上下文和处理器实例列表，按平台分发到各 SDK 处理器。
    /// </summary>
    public sealed class NovaBuildPreprocessor : IPreprocessBuildWithReport
    {
        /// <summary>
        /// 预处理回调顺序，设为 -1 确保在其他大多数处理器之前执行。
        /// </summary>
        public int callbackOrder => -1;

        /// <summary>
        /// Unity 构建预处理入口。创建上下文，按平台分发到各 SDK 处理器。
        /// </summary>
        /// <param name="report">Unity 构建报告。</param>
        public void OnPreprocessBuild(BuildReport report)
        {
            NovaBuildShared.Context = new NovaBuildContext
            {
                Report = report,
                Target = report.summary.platform,
            };
            NovaBuildShared.Processors = NovaBuildShared.CollectProcessors();

            switch (NovaBuildShared.Context.Target)
            {
                case BuildTarget.Android:
                    PreprocessAndroid(report);
                    break;
                case BuildTarget.iOS:
                    PreprocessiOS(report);
                    break;
                case BuildTarget.WebGL:
                    PreprocessWebGL(report);
                    break;
            }
        }

        /// <summary>
        /// 执行 Android 平台预处理：从模板复制 Manifest，依次调用各处理器，最后保存 Manifest 和 ProGuard 规则。
        /// </summary>
        /// <param name="report">Unity 构建报告。</param>
        private void PreprocessAndroid(BuildReport report)
        {
            var ctx = NovaBuildShared.Context;
            string templateFullPath = Util.SysIO.Path.GetFullPath(NovaBuildShared.c_UnityManifestTemplatePath);
            string manifestFullPath = Util.SysIO.Path.GetFullPath(NovaBuildShared.c_AndroidManifestPath);

            if (!Util.SysIO.File.Exists(templateFullPath))
            {
                Log.Warning(LogTag.Editor, $"[NovaBuildPreprocessor] UnityManifest.xml 模板不存在，路径：{templateFullPath}");
                return;
            }

            Util.SysIO.File.Copy(templateFullPath, manifestFullPath, overwrite: true);
            ctx.AndroidManifest = new CustomAndroidManifest(manifestFullPath);

            NovaBuildShared.Processors.Sort((a, b) => a.PreprocessPriority.CompareTo(b.PreprocessPriority));
            foreach (var processor in NovaBuildShared.Processors)
            {
                Log.Debug(LogTag.Editor, $"[NovaBuildPreprocessor] Android 预处理：{processor.GetType().Name}");
                processor.OnPreprocessBuildOnAndroid(report, ctx);
            }

            NovaManifestBootstrapper.Inject(ctx.AndroidManifest, ctx.ActivityName);

            foreach (var ruleSet in ctx.ManifestRuleSets)
                ctx.AndroidManifest.Apply(ruleSet);

            ctx.AndroidManifest.Save();

            string proguardFullPath = Util.SysIO.Path.GetFullPath(NovaBuildShared.c_ProguardOutputPath);
            NovaProguardBootstrapper.Rebuild(proguardFullPath, ctx.ProguardRules);
        }

        /// <summary>
        /// 执行 iOS 平台预处理：依次调用各处理器。
        /// </summary>
        /// <param name="report">Unity 构建报告。</param>
        private void PreprocessiOS(BuildReport report)
        {
            NovaBuildShared.Processors.Sort((a, b) => a.PreprocessPriority.CompareTo(b.PreprocessPriority));
            foreach (var processor in NovaBuildShared.Processors)
            {
                Log.Debug(LogTag.Editor, $"[NovaBuildPreprocessor] iOS 预处理：{processor.GetType().Name}");
                processor.OnPreprocessBuildOniOS(report, NovaBuildShared.Context);
            }
        }

        /// <summary>
        /// 执行 WebGL 平台预处理：依次调用各处理器。
        /// </summary>
        /// <param name="report">Unity 构建报告。</param>
        private void PreprocessWebGL(BuildReport report)
        {
            NovaBuildShared.Processors.Sort((a, b) => a.PreprocessPriority.CompareTo(b.PreprocessPriority));
            foreach (var processor in NovaBuildShared.Processors)
            {
                Log.Debug(LogTag.Editor, $"[NovaBuildPreprocessor] WebGL 预处理：{processor.GetType().Name}");
                processor.OnPreprocessBuildOnWebGL(report, NovaBuildShared.Context);
            }
        }
    }
}
