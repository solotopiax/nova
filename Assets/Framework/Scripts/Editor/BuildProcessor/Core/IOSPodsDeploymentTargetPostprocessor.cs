/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  IOSPodsDeploymentTargetPostprocessor.cs
 * author:    taoye
 * created:   2026/9/28
 * descrip:   按 Unity iOS 最低版本修正 CocoaPods target 的部署版本
 ***************************************************************/

using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Callbacks;

namespace NovaFramework.Editor
{
    /// <summary>
    /// 在 EDM4U 生成 Podfile 后、执行 pod install 前写入 Pods 部署版本下限。
    /// </summary>
    internal static class IOSPodsDeploymentTargetPostprocessor
    {
        private const string c_BlockStart = "# Nova Pods deployment target floor begin";
        private const string c_BlockEnd = "# Nova Pods deployment target floor end";

        /// <summary>
        /// 以当前 PlayerSettings 的 iOS 最低版本为下限修正导出工程的 Podfile。
        /// EDM4U 1.2.188 在顺序 40 生成 Podfile、顺序 50 执行 pod install。
        /// </summary>
        /// <param name="target">本轮 Unity 构建目标。</param>
        /// <param name="buildPath">导出的 Xcode 工程目录。</param>
        [PostProcessBuild(45)]
        private static void OnPostProcessBuild(BuildTarget target, string buildPath)
        {
            if (target != BuildTarget.iOS)
                return;

            string podfilePath = Path.Combine(buildPath, "Podfile");
            if (!File.Exists(podfilePath))
                return;

            string configuredVersion = PlayerSettings.iOS.targetOSVersionString;
            if (!Version.TryParse(configuredVersion, out Version minimumVersion))
            {
                throw new InvalidOperationException(
                    $"无法解析 PlayerSettings.iOS.targetOSVersionString：{configuredVersion}");
            }

            string contents = File.ReadAllText(podfilePath);
            int blockStart = contents.IndexOf(c_BlockStart, StringComparison.Ordinal);
            if (blockStart >= 0)
            {
                int blockEnd = contents.IndexOf(c_BlockEnd, blockStart, StringComparison.Ordinal);
                if (blockEnd < 0)
                    throw new InvalidOperationException($"Podfile 中的 Nova 部署版本规则不完整：{podfilePath}");

                contents = contents.Remove(blockStart, blockEnd - blockStart + c_BlockEnd.Length);
            }

            // CocoaPods 不允许同一 Podfile 中存在多个 post_install，避免覆盖第三方已有回调。
            if (System.Text.RegularExpressions.Regex.IsMatch(contents, @"(?m)^\s*post_install\b"))
            {
                throw new InvalidOperationException(
                    $"Podfile 已包含其他 post_install，请先合并 Pods 部署版本规则：{podfilePath}");
            }

            string minimum = minimumVersion.ToString();
            var rule = new StringBuilder();
            rule.AppendLine(c_BlockStart);
            rule.AppendLine("post_install do |installer|");
            rule.AppendLine($"  minimum = Gem::Version.new('{minimum}')");
            rule.AppendLine("  installer.pods_project.targets.each do |pod_target|");
            rule.AppendLine("    pod_target.build_configurations.each do |configuration|");
            rule.AppendLine("      current = configuration.build_settings['IPHONEOS_DEPLOYMENT_TARGET']");
            rule.AppendLine("      if current.nil? || Gem::Version.new(current.to_s) < minimum");
            rule.AppendLine($"        configuration.build_settings['IPHONEOS_DEPLOYMENT_TARGET'] = '{minimum}'");
            rule.AppendLine("      end");
            rule.AppendLine("    end");
            rule.AppendLine("  end");
            rule.AppendLine("end");
            rule.AppendLine(c_BlockEnd);

            File.WriteAllText(podfilePath,
                contents.TrimEnd('\r', '\n') + Environment.NewLine + Environment.NewLine + rule,
                new UTF8Encoding(false));
        }
    }
}
