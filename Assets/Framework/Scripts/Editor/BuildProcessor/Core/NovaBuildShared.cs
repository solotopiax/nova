/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  NovaBuildShared.cs
 * author:    yingzheng
 * created:   2026/4/24
 * descrip:   NovaBuildPreprocessor 与 NovaBuildPostprocessor 的跨阶段共享状态
 ***************************************************************/

using System;
using System.Collections.Generic;
using NovaFramework.Runtime;

namespace NovaFramework.Editor
{
    /// <summary>
    /// Pre/Post 两个处理器之间的共享状态容器。
    /// Preprocessor 写入，Postprocessor 读取，保证跨阶段实例字段和上下文一致。
    /// </summary>
    internal static class NovaBuildShared
    {
        /// <summary>
        /// 项目主 AndroidManifest.xml 的 Assets 相对路径，构建时由模板复制生成。
        /// </summary>
        internal const string c_AndroidManifestPath = "Assets/Plugins/Android/AndroidManifest.xml";

        /// <summary>
        /// UnityManifest.xml 模板的 Assets 相对路径，每次构建时作为净底复制到输出路径。
        /// </summary>
        internal const string c_UnityManifestTemplatePath = "Assets/Framework/Scripts/Editor/BuildProcessor/Android/UnityManifest.xml";

        /// <summary>
        /// 构建输出的 proguard-user.txt 完整路径。
        /// </summary>
        internal const string c_ProguardOutputPath = "Assets/Plugins/Android/proguard-user.txt";

        /// <summary>
        /// 当前构建上下文，由 NovaBuildPreprocessor 创建，NovaBuildPostprocessor 读取。
        /// </summary>
        internal static NovaBuildContext Context;

        /// <summary>
        /// 处理器实例列表，由 NovaBuildPreprocessor 创建，Pre/Post 两阶段共享同一批实例。
        /// </summary>
        internal static List<NovaSDKBuildProcessor> Processors;

        /// <summary>
        /// 通过反射发现所有 NovaSDKBuildProcessor 具体子类，并实例化后返回。
        /// </summary>
        /// <returns>所有具体子类的实例列表。</returns>
        internal static List<NovaSDKBuildProcessor> CollectProcessors()
        {
            var result = new List<NovaSDKBuildProcessor>();
            var types = UnityEditor.TypeCache.GetTypesDerivedFrom<NovaSDKBuildProcessor>();
            foreach (var type in types)
            {
                if (type.IsAbstract)
                    continue;
                try
                {
                    result.Add((NovaSDKBuildProcessor)Activator.CreateInstance(type));
                }
                catch (Exception e)
                {
                    Log.Error(LogTag.Editor, $"[NovaBuildShared] 实例化处理器 {type.FullName} 失败：{e.Message}");
                }
            }
            return result;
        }
    }
}
