using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEditor.Build.Pipeline;
using UnityEditor.Build.Pipeline.Interfaces;

namespace UnityEditor.Build.Pipeline.Tasks
{
    public static class SBPBuildTasks
    {
        /// <summary>
        /// 创建 YooAsset 的 Scriptable Build Pipeline 标准构建任务序列。
        /// </summary>
        /// <param name="builtInShaderBundleName">内置 Shader 资源包名称；为空时跳过对应构建任务。</param>
        /// <param name="monoScriptsBundleName">Mono 脚本资源包名称；为空时跳过对应构建任务。</param>
        /// <returns>按执行顺序排列的构建任务集合。</returns>
        public static IList<IBuildTask> Create(string builtInShaderBundleName, string monoScriptsBundleName)
        {
            var buildTasks = new List<IBuildTask>();

            // 初始化构建环境
            buildTasks.Add(new SwitchToBuildPlatform());
            buildTasks.Add(new RebuildSpriteAtlasCache());

            // 编译 Player 脚本
            buildTasks.Add(new BuildPlayerScripts());
            buildTasks.Add(new PostScriptsCallback());

            // 计算资源依赖
            buildTasks.Add(new CalculateSceneDependencyData());
#if UNITY_2019_3_OR_NEWER
            buildTasks.Add(new CalculateCustomDependencyData());
#endif
            buildTasks.Add(new CalculateAssetDependencyData());
            buildTasks.Add(new StripUnusedSpriteSources());
            if (string.IsNullOrEmpty(builtInShaderBundleName) == false)
            {
#if SBP_2_0_1_OR_NEWER
                buildTasks.Add(new CreateBuiltInBundle(builtInShaderBundleName));
#else
                buildTasks.Add(new CreateBuiltInShadersBundle(builtInShaderBundleName));
#endif
            }
            if (string.IsNullOrEmpty(monoScriptsBundleName) == false)
                buildTasks.Add(new CreateMonoScriptBundle(monoScriptsBundleName));
            buildTasks.Add(new PostDependencyCallback());

            // 生成资源打包布局
            buildTasks.Add(new GenerateBundlePacking());
            buildTasks.Add(new UpdateBundleObjectLayout());
            buildTasks.Add(new GenerateBundleCommands());
            buildTasks.Add(new GenerateSubAssetPathMaps());
            buildTasks.Add(new GenerateBundleMaps());
            buildTasks.Add(new PostPackingCallback());

            // 写入构建产物
            buildTasks.Add(new WriteSerializedFiles());
            buildTasks.Add(new ArchiveAndCompressBundles());
            buildTasks.Add(new AppendBundleHash());
            buildTasks.Add(new GenerateLinkXml());
            buildTasks.Add(new PostWritingCallback());

            return buildTasks;
        }
    }
}
