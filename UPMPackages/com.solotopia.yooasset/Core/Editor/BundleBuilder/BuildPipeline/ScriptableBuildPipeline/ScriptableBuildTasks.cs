using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEditor.Build.Pipeline;
using UnityEditor.Build.Pipeline.Interfaces;

// modify: local fork - 适配 Unity 6000.4 使用的 SBP 2.6.1 内置资源包构建任务类型。

namespace UnityEditor.Build.Pipeline.Tasks
{
    public static class SBPBuildTasks
    {
        /// <summary>
        /// 创建 YooAsset 的 Scriptable Build Pipeline 标准构建任务序列。
        /// </summary>
        /// <param name="builtInShaderBundleName">内置 Shader 资源包名称。</param>
        /// <param name="monoScriptsBundleName">Mono 脚本资源包名称。</param>
        /// <returns>按执行顺序排列的构建任务集合。</returns>
        public static IList<IBuildTask> Create(string builtInShaderBundleName, string monoScriptsBundleName)
        {
            var buildTasks = new List<IBuildTask>();

            // Setup
            buildTasks.Add(new SwitchToBuildPlatform());
            buildTasks.Add(new RebuildSpriteAtlasCache());

            // Player Scripts
            buildTasks.Add(new BuildPlayerScripts());
            buildTasks.Add(new PostScriptsCallback());

            // Dependency
            buildTasks.Add(new CalculateSceneDependencyData());
#if UNITY_2019_3_OR_NEWER
            buildTasks.Add(new CalculateCustomDependencyData());
#endif
            buildTasks.Add(new CalculateAssetDependencyData());
            buildTasks.Add(new StripUnusedSpriteSources());
            if (string.IsNullOrEmpty(builtInShaderBundleName) == false)
                buildTasks.Add(new CreateBuiltInBundle(builtInShaderBundleName));
            if (string.IsNullOrEmpty(monoScriptsBundleName) == false)
                buildTasks.Add(new CreateMonoScriptBundle(monoScriptsBundleName));
            buildTasks.Add(new PostDependencyCallback());

            // Packing
            buildTasks.Add(new GenerateBundlePacking());
            buildTasks.Add(new UpdateBundleObjectLayout());
            buildTasks.Add(new GenerateBundleCommands());
            buildTasks.Add(new GenerateSubAssetPathMaps());
            buildTasks.Add(new GenerateBundleMaps());
            buildTasks.Add(new PostPackingCallback());

            // Writing
            buildTasks.Add(new WriteSerializedFiles());
            buildTasks.Add(new ArchiveAndCompressBundles());
            buildTasks.Add(new AppendBundleHash());
            buildTasks.Add(new GenerateLinkXml());
            buildTasks.Add(new PostWritingCallback());

            return buildTasks;
        }
    }
}
