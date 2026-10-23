/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MaxIOSPodOwnershipPostprocessor.cs
 * author:    taoye
 * created:   2026/10/13
 * descrip:   将 MAX iOS 原生依赖统一交给 CocoaPods，保留其他 SDK 的 SPM 配置
 ***************************************************************/

#if UNITY_IOS && NOVA_APPLOVIN_MAX
using System;
using System.IO;
using System.Linq;
using NovaFramework.Editor;
using NovaFramework.Runtime;
using UnityEditor;
using UnityEditor.Callbacks;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;
using Path = System.IO.Path;

namespace NovaFramework.SDK.MaxAdPlugin.Editor
{
    /// <summary>
    /// 在 EDM4U 生成 SPM 引用和 Podfile 后，将两个 MAX adapter 的原生依赖恢复为 Pods。
    /// 不依赖 MaxAdChannelConfig 是否配置；原厂依赖在包安装后就会进入生成工程。
    /// </summary>
    internal static class MaxIOSPodOwnershipPostprocessor
    {
        private const string c_PackageName = "com.solotopia.nova.framework.sdk.max";
        private const string c_ResultMarker = "NOVA_MAX_IOS_POD_OWNERSHIP:";
        private const int c_TimeoutMs = 120000;

        /// <summary>
        /// 顺序 44 接管两项 MAX 原生依赖；EDM4U 的 SPM/Podfile/install 顺序分别为 35/40/50，Nova 部署下限为 45。
        /// 固定脚本只在当前 pod 子进程的 Ruby 启动阶段加载，完成后直接退出，不运行 pod CLI 或再次安装 Pods。
        /// 工具、包身份、官方 XML 或工程结构不符合已审契约时令构建失败；普通回写异常由脚本恢复原始两文件。
        /// 120 秒超时、强制终止或系统中断无法保证脚本恢复，必须重新导出工程，不复用可能中断的回写结果。
        /// </summary>
        /// <param name="target">本轮 Unity 构建目标；非 iOS 不执行。</param>
        /// <param name="buildPath">Unity 已生成的 Xcode 工程目录，只允许脚本修改其中的两项 MAX 引用和 Pod 声明。</param>
        [PostProcessBuild(44)]
        private static void OnPostProcessBuild(BuildTarget target, string buildPath)
        {
            if (target != BuildTarget.iOS)
                return;

            PackageInfo owner = PackageInfo.FindForAssembly(typeof(MaxIOSPodOwnershipPostprocessor).Assembly);
            if (owner == null || owner.name != c_PackageName || string.IsNullOrEmpty(owner.resolvedPath))
                throw new InvalidOperationException("MAX iOS 依赖归属修复无法解析自身 UPM 包身份。");

            string scriptPath = Path.Combine(owner.resolvedPath, "Nova", "Scripts", "Editor", "BuildProcessor", "MaxIOSPodOwnership.rb");
            if (!File.Exists(scriptPath))
                throw new FileNotFoundException("MAX iOS 依赖归属脚本缺失，请重新安装完整的 Nova MAX 包。", scriptPath);

            PackageInfo[] packages = PackageInfo.GetAllRegisteredPackages();
            string lineXml = FindDependencyXml(packages, "com.applovin.mediation.adapters.line.ios", "3010102.0.0");
            string unityAdsXml = FindDependencyXml(packages, "com.applovin.mediation.adapters.unityads.ios", "4200001.0.0");

            // 使用有效 pod 启动器自己的 Ruby 与 gem 环境，不固定 Homebrew 路径、不修改全局环境。
            // 相对 require 使包安装路径含空格时也可加载；RUBYOPT 只覆盖本次子进程并由脚本在 CLI 前退出。
            string command = "set -eu\n" +
                "podExecutable=$(command -v pod) || { echo 'MAX iOS 依赖归属修复需要可执行的 CocoaPods。' >&2; exit 1; }\n" +
                "cd " + QuoteShellArgument(Path.GetDirectoryName(Path.GetFullPath(scriptPath))) + "\n" +
                "exec env RUBYOPT=-r./MaxIOSPodOwnership.rb " +
                "NOVA_MAX_IOS_EXPORT_ROOT=" + QuoteShellArgument(Path.GetFullPath(buildPath)) + " " +
                "NOVA_MAX_IOS_LINE_XML=" + QuoteShellArgument(lineXml) + " " +
                "NOVA_MAX_IOS_UNITYADS_XML=" + QuoteShellArgument(unityAdsXml) + " " +
                "\"$podExecutable\" --version";

            EditorUtil.ProcessRunner.ProcessResult result = EditorUtil.ProcessRunner.RunSync(
                "/bin/bash", "-lc " + QuoteProcessArgument(command), c_TimeoutMs);
            if (!result.Success || !result.Stdout.Contains(c_ResultMarker))
            {
                throw new InvalidOperationException(
                    $"MAX iOS 依赖归属修复失败（exit={result.ExitCode}, timeout={result.TimedOut}），请修复直接错误后重新导出：\n{result.Stderr}\n{result.Stdout}");
            }

            Log.Debug(LogTag.Editor, "[MaxIOSPodOwnershipPostprocessor] " + result.Stdout.Trim());
        }

        /// <summary>
        /// 从已注册 UPM 包定位已审版本的官方 Dependencies.xml；不读取或修改 vendor 缓存内容以外的文件。
        /// 包缺失、版本漂移或 XML 缺失均明确失败，避免按未知上游版本继续注入旧的原生依赖。
        /// </summary>
        /// <param name="packages">Unity 当前已注册的 UPM 包身份列表。</param>
        /// <param name="packageName">固定的 LINE 或 Unity Ads iOS 官方 adapter 包名。</param>
        /// <param name="version">本轮已审查并由 Nova MAX package.json 固定的 UPM 版本。</param>
        /// <returns>该官方包 Editor/Dependencies.xml 的绝对路径。</returns>
        private static string FindDependencyXml(PackageInfo[] packages, string packageName, string version)
        {
            PackageInfo package = packages.SingleOrDefault(item => item.name == packageName);
            if (package == null || package.version != version || string.IsNullOrEmpty(package.resolvedPath))
                throw new InvalidOperationException($"MAX iOS 依赖归属修复要求 {packageName}@{version}，实际版本缺失或不一致。");

            string xmlPath = Path.GetFullPath(Path.Combine(package.resolvedPath, "Editor", "Dependencies.xml"));
            if (!File.Exists(xmlPath))
                throw new FileNotFoundException("官方 MAX iOS adapter 的 Dependencies.xml 缺失。", xmlPath);
            return xmlPath;
        }

        /// <summary>
        /// 将固定路径作为单个 bash 字面参数传递，保留空格、单引号和美元符号，禁止路径内容成为 shell 代码。
        /// </summary>
        /// <param name="value">由已解析 UPM 包或 Unity 构建目录提供的路径。</param>
        /// <returns>完整单引号 shell 参数。</returns>
        private static string QuoteShellArgument(string value) => "'" + value.Replace("'", "'\"'\"'") + "'";

        /// <summary>
        /// 将固定 bash 脚本作为 ProcessStartInfo.Arguments 的一个双引号参数传递，不让参数解析拆开脚本。
        /// </summary>
        /// <param name="value">只包含固定命令与已经 shell 转义的路径的完整脚本。</param>
        /// <returns>供 bash -lc 使用的单个进程参数。</returns>
        private static string QuoteProcessArgument(string value) => "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    }
}
#endif
