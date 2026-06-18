/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  NovaSDKBuildProcessor.cs
 * author:    yingzheng
 * created:   2026/4/23
 * descrip:   Nova SDK 构建处理器基类，所有 UPM SDK 包的 BuildProcessor 继承此类
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using NovaFramework.Runtime;
using UnityEditor.Build.Reporting;
#if UNITY_IOS
using UnityEditor.iOS.Xcode.Extensions;
#endif

namespace NovaFramework.Editor
{
    /// <summary>
    /// Nova SDK 构建处理器基类。
    /// 所有 UPM SDK 包的 BuildProcessor 继承此类，并按需 override 对应平台的预处理或后处理方法。
    /// NovaBuildProcessor 通过反射发现所有具体子类，按优先级依次调用。
    /// </summary>
    public abstract class NovaSDKBuildProcessor
    {
        /// <summary>
        /// 预处理优先级，值越小优先级越高，默认 0。
        /// </summary>
        public virtual int PreprocessPriority => 0;

        /// <summary>
        /// 后处理优先级，值越小优先级越高，默认 0。
        /// </summary>
        public virtual int PostprocessPriority => 0;

        /// <summary>
        /// Android 平台预处理回调，在 Unity 开始构建 Android 包之前调用。
        /// </summary>
        /// <param name="report">Unity 构建报告。</param>
        /// <param name="context">Nova 构建上下文，包含 AndroidManifest 等共享资源。</param>
        public virtual void OnPreprocessBuildOnAndroid(BuildReport report, NovaBuildContext context) { }

        /// <summary>
        /// iOS 平台预处理回调，在 Unity 开始构建 iOS 包之前调用。
        /// </summary>
        /// <param name="report">Unity 构建报告。</param>
        /// <param name="context">Nova 构建上下文。</param>
        public virtual void OnPreprocessBuildOniOS(BuildReport report, NovaBuildContext context) { }

        /// <summary>
        /// WebGL 平台预处理回调，在 Unity 开始构建 WebGL 包之前调用。
        /// </summary>
        /// <param name="report">Unity 构建报告。</param>
        /// <param name="context">Nova 构建上下文。</param>
        public virtual void OnPreprocessBuildOnWebGL(BuildReport report, NovaBuildContext context) { }

        /// <summary>
        /// Android 平台后处理回调，在 Unity 完成 Android 构建之后调用。
        /// </summary>
        /// <param name="report">Unity 构建报告。</param>
        /// <param name="context">Nova 构建上下文。</param>
        public virtual void OnPostprocessBuildOnAndroid(BuildReport report, NovaBuildContext context) { }

        /// <summary>
        /// WebGL 平台后处理回调，在 Unity 完成 WebGL 构建之后调用。
        /// </summary>
        /// <param name="report">Unity 构建报告。</param>
        /// <param name="context">Nova 构建上下文。</param>
        public virtual void OnPostprocessBuildOnWebGL(BuildReport report, NovaBuildContext context) { }

        /// <summary>
        /// 通过 ConfigRuntimeSO.GetSDKPluginConfig<T>() 查找并返回指定类型的 SDK 配置实例。
        /// 内部通过 EditorUtil.Config.RuntimeProvider.GetCurrent() 获取 ConfigRuntimeSO。
        /// 未找到 ConfigRuntimeSO 或未配置目标类型时返回 null。
        /// </summary>
        /// <typeparam name="T">目标 SDK 配置类型，须实现 ISDKPluginConfig。</typeparam>
        /// <returns>找到的 SDK 配置实例；未找到时返回 null。</returns>
        protected static T GetSDKConfig<T>() where T : class, ISDKPluginConfig
        {
            ConfigRuntimeSO runtimeSO = EditorUtil.Config.RuntimeProvider.GetCurrent();
            if (runtimeSO == null)
            {
                Log.Warning(LogTag.Editor, $"[NovaSDKBuildProcessor] 未找到 ConfigRuntimeSO，跳过构建注入。请先在 ConfigWindow 导出配置。");
                return null;
            }

            T config = runtimeSO.GetSDKPluginConfig<T>();
            if (config == null)
            {
                Log.Warning(LogTag.Editor, $"[NovaSDKBuildProcessor] ConfigRuntimeSO 中未找到 {typeof(T).Name}，跳过构建注入。");
                return null;
            }
            return config;
        }

#if UNITY_IOS
        /// <summary>
        /// 子类 override 此钩子返回需要 Embed 到 Unity-iPhone target 的 xcframework 名单（含 .xcframework 后缀）。
        /// 返回 null 或空数组表示本 SDK 不需要 Embed，OnPostprocessBuildOniOS 默认实现会跳过 Embed 流程。
        /// </summary>
        /// <returns>xcframework 名称数组；无需 Embed 时返回 null。</returns>
        protected virtual string[] GetEmbedXcframeworkNames() => null;

        /// <summary>
        /// iOS 后处理默认实现：当 GetEmbedXcframeworkNames 返回非空名单时，
        /// 调用 EmbedDynamicLibraries 把命中的 xcframework 添加到 Xcode 工程并标记 Embed and Sign。
        /// 子类如需追加额外后处理，可 override 后先调 base.OnPostprocessBuildOniOS(...)，再写自定义逻辑。
        /// </summary>
        /// <param name="report">Unity 构建报告。</param>
        /// <param name="context">Nova 构建上下文。</param>
        public virtual void OnPostprocessBuildOniOS(BuildReport report, NovaBuildContext context)
        {
            string[] names = GetEmbedXcframeworkNames();
            if (names == null || names.Length == 0) return;
            EmbedDynamicLibraries(context, report.summary.outputPath, names);
        }

        /// <summary>
        /// 在 Xcode 工程的 Pods 目录中递归搜索每个 xcframework，命中后通过 PBXProject 添加文件
        /// 并标记 Embed and Sign，未命中则 Warning 跳过。所有 SDK 子类共享此实现。
        /// </summary>
        /// <param name="context">Nova 构建上下文，提供 XProj 与 TargetGuid。</param>
        /// <param name="outputPath">Xcode 工程根目录（report.summary.outputPath）。</param>
        /// <param name="xcframeworkNames">需要 Embed 的 xcframework 名称列表。</param>
        protected static void EmbedDynamicLibraries(NovaBuildContext context, string outputPath, IReadOnlyList<string> xcframeworkNames)
        {
            string podsDirectory = Util.SysIO.Path.Combine(outputPath, "Pods");
            if (!Directory.Exists(podsDirectory))
            {
                Log.Warning(LogTag.Editor, $"[NovaSDKBuildProcessor] Pods 目录不存在，跳过 xcframework Embed：{podsDirectory}");
                return;
            }

            foreach (string xcframeworkName in xcframeworkNames)
            {
                string[] hits = Directory.GetDirectories(podsDirectory, xcframeworkName, SearchOption.AllDirectories);
                if (hits.Length == 0)
                {
                    Log.Warning(LogTag.Editor, $"[NovaSDKBuildProcessor] 未在 Pods 中找到 {xcframeworkName}，跳过此项 Embed。");
                    continue;
                }

                string absolutePath = hits[0];
                int podsIndex = absolutePath.LastIndexOf("Pods", StringComparison.Ordinal);
                string relativePath = absolutePath.Substring(podsIndex);

                string fileGuid = context.XProj.AddFile(relativePath, relativePath);
                context.XProj.AddFileToEmbedFrameworks(context.TargetGuid, fileGuid);

                Log.Debug(LogTag.Editor, $"[NovaSDKBuildProcessor] 已 Embed xcframework：{relativePath}");
            }
        }
#endif
    }
}
