/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  NovaProguardBootstrapper.cs
 * author:    yingzheng
 * created:   2026/4/24
 * descrip:   ProGuard 规则动态重建工具，合并各 SDK 注入规则并覆写输出文件
 ***************************************************************/

using System.Collections.Generic;
using System.IO;
using System.Text;
using NovaFramework.Runtime;

namespace NovaFramework.Editor
{
    /// <summary>
    /// ProGuard 规则动态重建工具。
    /// 仅当 outputPath 文件已存在时才执行写入，全量覆写各 SDK 注入的规则块，输出 UTF-8 无 BOM 编码文件。
    /// </summary>
    public static class NovaProguardBootstrapper
    {
        /// <summary>
        /// 重建 proguard-user.txt 文件。
        /// 文件不存在时跳过；文件存在时全量覆写：文件头注释 + 各 SDK 规则块。
        /// </summary>
        /// <param name="outputPath">目标输出文件的完整磁盘路径。</param>
        /// <param name="sdkRules">SDK 规则列表，每个元素为 (sdkName, rules) 元组；为 null 或空时写出仅含文件头的空白文件。</param>
        public static void Rebuild(string outputPath, IReadOnlyList<(string sdkName, string rules)> sdkRules)
        {
            if (!File.Exists(outputPath))
            {
                Log.Debug(LogTag.Editor, $"[NovaProguardBootstrapper] proguard-user.txt 不存在，跳过生成，路径：{outputPath}");
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine("# [Nova Framework - Auto Generated - Do not edit manually]");

            if (sdkRules != null && sdkRules.Count > 0)
            {
                sb.AppendLine("# === SDK Auto Rules ===");
                foreach (var (sdkName, rules) in sdkRules)
                {
                    sb.AppendLine($"# --- {sdkName} ---");
                    sb.AppendLine(rules);
                    sb.AppendLine();
                }
            }

            File.WriteAllText(outputPath, sb.ToString(), new UTF8Encoding(false));
            Log.Debug(LogTag.Editor, $"[NovaProguardBootstrapper] ProGuard 规则已重建完成，输出路径：{outputPath}");
        }
    }
}
