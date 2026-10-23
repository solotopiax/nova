/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MemoryProtectionLinkerProcessor.cs
 * author:    taoye
 * created:   2026/10/21
 * descrip:   为 Assets 与 UPM 安装态提供内存防护反射 API 的防裁剪声明
 ***************************************************************/

using System;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.UnityLinker;
using UnityEngine;
using IOPath = System.IO.Path;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace NovaFramework.Editor
{
    /// <summary>直接提交 独立 MemoryProtection 包维护的声明，避免 UPM 内的 link.xml 被默认扫描遗漏。</summary>
    internal sealed class MemoryProtectionLinkerProcessor : IUnityLinkerProcessor
    {
        private const string LinkXmlRelativePath = "Nova/Runtime/Actk/link.xml";
        private const string ProcessorScriptSuffix = "/Nova/Editor/MemoryProtectionLinkerProcessor.cs";

        /// <summary>本处理器只提供独立声明，不修改其他构建处理器的执行顺序。</summary>
        public int callbackOrder => 0;

        /// <summary>存在 ACTk 时提交四个反射目标的既有声明；缺少插件时不附加文件。</summary>
        /// <param name="report">当前 Player 构建报告，本处理器不修改其内容。</param>
        /// <param name="data">当前 UnityLinker 上下文，本处理器不创建额外构建产物。</param>
        /// <returns>MemoryProtection 包声明文件的物理绝对路径；未安装 ACTk 时为 null。</returns>
        public string GenerateAdditionalLinkXmlFile(BuildReport report, UnityLinkerBuildPipelineData data)
        {
            if (Type.GetType("CodeStage.AntiCheat.ObscuredTypes.ObscuredLong, ACTk.Runtime", false) == null)
                return null;

            string path = ResolveLinkXmlPath();
            if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path))
                throw new BuildFailedException("内存防护防裁剪声明缺失，请检查 Nova Framework - MemoryProtection 安装内容。");
            return path;
        }

        /// <summary>优先使用 UPM 物理根目录；Assets 开发态按当前脚本的精确类型定位根目录。</summary>
        /// <returns>已有声明的物理绝对路径；无法定位当前开发源时为 null。</returns>
        private static string ResolveLinkXmlPath()
        {
            PackageInfo package = PackageInfo.FindForAssembly(typeof(MemoryProtectionLinkerProcessor).Assembly);
            if (package != null)
            {
                if (string.IsNullOrEmpty(package.resolvedPath)) return null;
                return IOPath.GetFullPath(IOPath.Combine(package.resolvedPath, LinkXmlRelativePath));
            }

            string[] scriptGuids = AssetDatabase.FindAssets("MemoryProtectionLinkerProcessor t:MonoScript", new[] { "Assets" });
            foreach (string guid in scriptGuids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid).Replace('\\', '/');
                if (!assetPath.EndsWith(ProcessorScriptSuffix, StringComparison.Ordinal)) continue;
                MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(assetPath);
                if (script == null || script.GetClass() != typeof(MemoryProtectionLinkerProcessor)) continue;

                string frameworkRoot = assetPath.Substring(0, assetPath.Length - ProcessorScriptSuffix.Length);
                string projectRoot = IOPath.GetDirectoryName(Application.dataPath);
                return IOPath.GetFullPath(IOPath.Combine(projectRoot, frameworkRoot, LinkXmlRelativePath));
            }
            return null;
        }
    }
}
