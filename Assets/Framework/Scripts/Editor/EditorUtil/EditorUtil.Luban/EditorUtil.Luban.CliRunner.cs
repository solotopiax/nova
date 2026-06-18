/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.Luban.CliRunner.cs
 * author:    taoye
 * created:   2026/4/16
 * descrip:   Luban CLI 外部进程调用器
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Text;
using SysEnv = System.Environment;
using NovaFramework.Runtime;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        /// <summary>
        /// Luban 导出工具集。
        /// </summary>
        public static partial class Luban
        {
            /// <summary>
            /// Luban CLI 外部进程调用器，封装 dotnet Luban.dll 命令行调用。
            /// </summary>
            public static class CliRunner
            {
                /// <summary>
                /// UPM 包名。
                /// </summary>
                private const string c_PackageName = "com.solotopia.luban";

                /// <summary>
                /// Luban.dll 在 UPM 包内的相对路径。
                /// </summary>
                private const string c_LubanDllRelPath = "Core/Tools~/Luban/Luban.dll";

                /// <summary>
                /// 获取 Luban.dll 完整路径。
                /// </summary>
                /// <returns>Luban.dll 路径，不存在时返回 null。</returns>
                public static string GetLubanDllPath()
                {
                    string packagePath = Util.SysIO.Path.GetFullPath($"Packages/{c_PackageName}");
                    string dllPath = Util.SysIO.Path.Combine(packagePath, c_LubanDllRelPath);
                    if (Util.SysIO.File.Exists(dllPath))
                    {
                        return dllPath;
                    }

                    Log.Error(LogTag.Editor, "未找到 Luban.dll：{0}，请确保 {1} UPM 包已安装。", dllPath, c_PackageName);
                    return null;
                }

                /// <summary>
                /// 运行代码生成。
                /// </summary>
                /// <param name="confPath">luban.conf 文件路径。</param>
                /// <param name="targetName">target 名称（如 "table"）。</param>
                /// <param name="outputCodeDir">生成代码输出目录。</param>
                /// <param name="customTemplateDirs">自定义模板目录列表（可为 null，使用内置模板）。按优先级排序，Luban 依次查找。</param>
                /// <returns>是否成功。</returns>
                public static bool RunCodeGen(string confPath, string targetName, string outputCodeDir, string[] customTemplateDirs)
                {
                    StringBuilder args = new StringBuilder();
                    args.Append($"--conf \"{confPath}\"");
                    args.Append($" -t {targetName}");
                    args.Append(" -c cs-newtonsoft-json");
                    args.Append($" -x outputCodeDir=\"{outputCodeDir}\"");
                    if (customTemplateDirs != null)
                    {
                        foreach (string dir in customTemplateDirs)
                        {
                            if (!string.IsNullOrEmpty(dir))
                            {
                                args.Append($" --customTemplateDir \"{dir}\"");
                            }
                        }
                    }

                    return RunLuban(args.ToString());
                }

                /// <summary>
                /// 运行数据导出。
                /// </summary>
                /// <param name="confPath">luban.conf 文件路径。</param>
                /// <param name="targetName">target 名称。</param>
                /// <param name="outputDataDir">导出数据输出目录。</param>
                /// <returns>是否成功。</returns>
                public static bool RunDataExport(string confPath, string targetName, string outputDataDir)
                {
                    StringBuilder args = new StringBuilder();
                    args.Append($"--conf \"{confPath}\"");
                    args.Append($" -t {targetName}");
                    args.Append(" -d json");
                    args.Append($" -x outputDataDir=\"{outputDataDir}\"");

                    return RunLuban(args.ToString());
                }

                /// <summary>
                /// 同时运行代码生成与数据导出。
                /// </summary>
                /// <param name="confPath">luban.conf 文件路径。</param>
                /// <param name="targetName">target 名称。</param>
                /// <param name="outputCodeDir">生成代码输出目录。</param>
                /// <param name="outputDataDir">导出数据输出目录。</param>
                /// <param name="customTemplateDirs">自定义模板目录列表（可为 null，使用内置模板）。按优先级排序，Luban 依次查找。</param>
                /// <returns>是否成功。</returns>
                public static bool RunAll(string confPath, string targetName, string outputCodeDir, string outputDataDir, string[] customTemplateDirs)
                {
                    StringBuilder args = new StringBuilder();
                    args.Append($"--conf \"{confPath}\"");
                    args.Append($" -t {targetName}");
                    args.Append(" -c cs-newtonsoft-json");
                    args.Append(" -d json");
                    args.Append($" -x outputCodeDir=\"{outputCodeDir}\"");
                    args.Append($" -x outputDataDir=\"{outputDataDir}\"");
                    if (customTemplateDirs != null)
                    {
                        foreach (string dir in customTemplateDirs)
                        {
                            if (!string.IsNullOrEmpty(dir))
                            {
                                args.Append($" --customTemplateDir \"{dir}\"");
                            }
                        }
                    }

                    return RunLuban(args.ToString());
                }

                /// <summary>
                /// 运行 Protobuf3 Schema 生成：调用 Luban 以 `-c protobuf3` 从 Excel 表生成 .proto 文件。
                /// </summary>
                /// <param name="confPath">luban.conf 文件路径。</param>
                /// <param name="targetName">target 名称。</param>
                /// <param name="outputCodeDir">生成 .proto 文件的输出目录。</param>
                /// <param name="customTemplateDirs">自定义模板目录列表（可为 null，使用内置模板）。按优先级排序，Luban 依次查找。</param>
                /// <returns>是否成功。</returns>
                public static bool RunProtoSchemaGen(string confPath, string targetName, string outputCodeDir, string[] customTemplateDirs)
                {
                    StringBuilder args = new StringBuilder();
                    args.Append($"--conf \"{confPath}\"");
                    args.Append($" -t {targetName}");
                    args.Append(" -c protobuf3");
                    args.Append($" -x outputCodeDir=\"{outputCodeDir}\"");
                    if (customTemplateDirs != null)
                    {
                        foreach (string dir in customTemplateDirs)
                        {
                            if (!string.IsNullOrEmpty(dir))
                            {
                                args.Append($" --customTemplateDir \"{dir}\"");
                            }
                        }
                    }

                    return RunLuban(args.ToString());
                }

                /// <summary>
                /// 扫描输出目录中的 .cs 文件，返回文件名到相对路径的映射。
                /// </summary>
                /// <param name="outputCodeDir">代码输出目录。</param>
                /// <param name="relevantFileNames">仅包含这些文件名（为 null 时返回全部）。</param>
                /// <returns>文件名 → 相对路径的字典。</returns>
                internal static Dictionary<string, string> GetGeneratedCodeFiles(string outputCodeDir, HashSet<string> relevantFileNames = null)
                {
                    Dictionary<string, string> result = new Dictionary<string, string>();
                    if (string.IsNullOrEmpty(outputCodeDir) || !Util.SysIO.Directory.Exists(outputCodeDir))
                    {
                        return result;
                    }

                    string projectPath = Util.SysIO.Path.GetFullPath(".") + Util.SysIO.Path.DirectorySeparatorChar;
                    string[] csFiles = Util.SysIO.Directory.GetFiles(outputCodeDir, "*.cs", System.IO.SearchOption.TopDirectoryOnly);
                    foreach (string fullPath in csFiles)
                    {
                        string fileName = Util.SysIO.Path.GetFileName(fullPath);
                        if (relevantFileNames != null && !relevantFileNames.Contains(fileName))
                        {
                            continue;
                        }

                        string relativePath = fullPath.StartsWith(projectPath) ? fullPath.Substring(projectPath.Length) : fullPath;
                        result[fileName] = relativePath;
                    }

                    return result;
                }

                /// <summary>
                /// 当前平台的 dotnet 可执行文件名。
                /// </summary>
                private static readonly string s_DotnetFileName = UnityEngine.Application.platform == UnityEngine.RuntimePlatform.WindowsEditor ? "dotnet.exe" : "dotnet";

                /// <summary>
                /// dotnet 可执行文件的常见安装路径（Unity 进程不继承 shell PATH，需手动探测）。
                /// </summary>
                private static readonly string[] s_DotnetSearchPaths = BuildDotnetSearchPaths();

                /// <summary>
                /// 按当前平台构建 dotnet 探测路径列表。
                /// </summary>
                /// <returns>平台对应的 dotnet 候选路径数组。</returns>
                private static string[] BuildDotnetSearchPaths()
                {
                    if (UnityEngine.Application.platform == UnityEngine.RuntimePlatform.WindowsEditor)
                    {
                        return new[]
                        {
                            Util.SysIO.Path.Combine(SysEnv.GetFolderPath(SysEnv.SpecialFolder.ProgramFiles), "dotnet", "dotnet.exe"),
                            Util.SysIO.Path.Combine(SysEnv.GetFolderPath(SysEnv.SpecialFolder.UserProfile), ".dotnet", "dotnet.exe"),
                        };
                    }

                    return new[]
                    {
                        "/usr/local/share/dotnet/dotnet",
                        "/usr/local/bin/dotnet",
                        "/opt/homebrew/bin/dotnet",
                        Util.SysIO.Path.Combine(SysEnv.GetFolderPath(SysEnv.SpecialFolder.UserProfile), ".dotnet", "dotnet"),
                    };
                }

                /// <summary>
                /// 获取 dotnet 可执行文件路径：优先 PATH 查找，失败则逐一探测常见安装位置。
                /// </summary>
                /// <returns>dotnet 可执行文件完整路径，未找到时返回 null。</returns>
                internal static string ResolveDotnetPath()
                {
                    string pathEnv = SysEnv.GetEnvironmentVariable("PATH") ?? "";
                    char separator = Util.SysIO.Path.PathSeparator;
                    foreach (string dir in pathEnv.Split(separator, StringSplitOptions.RemoveEmptyEntries))
                    {
                        string candidate = Util.SysIO.Path.Combine(dir, s_DotnetFileName);
                        if (Util.SysIO.File.Exists(candidate))
                        {
                            return candidate;
                        }
                    }

                    foreach (string candidate in s_DotnetSearchPaths)
                    {
                        if (Util.SysIO.File.Exists(candidate))
                        {
                            return candidate;
                        }
                    }

                    return null;
                }

                /// <summary>
                /// 执行 Luban CLI 命令。
                /// <para>失败时合并 stdout + stderr 输出真实原因（Luban 多数错误打在 stdout）。</para>
                /// </summary>
                /// <param name="arguments">Luban 参数（不含 dotnet 和 dll 路径）。</param>
                /// <returns>进程退出码为 0 时返回 true。</returns>
                private static bool RunLuban(string arguments)
                {
                    string dllPath = GetLubanDllPath();
                    if (dllPath == null)
                    {
                        return false;
                    }

                    string dotnetPath = ResolveDotnetPath();
                    if (dotnetPath == null)
                    {
                        Log.Error(LogTag.Editor, "未找到 dotnet，请确保已安装 .NET SDK。");
                        return false;
                    }

                    string fullArgs = $"\"{dllPath}\" {arguments}";
                    ProcessRunner.ProcessResult result = ProcessRunner.RunSync(dotnetPath, fullArgs);

                    if (result.TimedOut)
                    {
                        Log.Error(LogTag.Editor, "Luban 执行超时，进程已终止。命令：{0} {1}\n输出：\n{2}", dotnetPath, fullArgs, ProcessRunner.FormatOutput(result));
                        return false;
                    }

                    if (!result.Success)
                    {
                        string summary = ExtractFailureSummary(result.Stdout, result.Stderr);
                        Log.Error(LogTag.Editor, "Luban 执行失败（ExitCode={0}）：{1}\n命令：{2} {3}\n输出：\n{4}",
                            result.ExitCode,
                            string.IsNullOrEmpty(summary) ? "(详见下方输出)" : summary,
                            dotnetPath,
                            fullArgs,
                            ProcessRunner.FormatOutput(result));
                        return false;
                    }

                    if (!string.IsNullOrEmpty(result.Stderr))
                    {
                        Log.Warning(LogTag.Editor, "Luban stderr：{0}", result.Stderr);
                    }

                    return true;
                }

                /// <summary>
                /// 从 Luban 输出中抽取最具诊断价值的一段摘要。
                /// <para>Luban 日志格式形如 `2026/05/21 11:14:58.155|ERROR|===> "..."`，直接定位 ERROR/run failed 行即可。</para>
                /// </summary>
                /// <param name="stdout">标准输出。</param>
                /// <param name="stderr">标准错误。</param>
                /// <returns>失败摘要，未匹配到关键行时返回 null。</returns>
                private static string ExtractFailureSummary(string stdout, string stderr)
                {
                    StringBuilder sb = new StringBuilder();
                    AppendSignificantLines(sb, stdout);
                    AppendSignificantLines(sb, stderr);
                    return sb.Length == 0 ? null : sb.ToString().TrimEnd();
                }

                /// <summary>
                /// 扫描输出文本，将命中 ERROR/FATAL/run failed 等关键行追加到 builder。
                /// </summary>
                /// <param name="sb">摘要构建器。</param>
                /// <param name="text">待扫描文本。</param>
                private static void AppendSignificantLines(StringBuilder sb, string text)
                {
                    if (string.IsNullOrEmpty(text))
                    {
                        return;
                    }

                    foreach (string raw in text.Split('\n'))
                    {
                        string line = raw.Trim('\r', ' ', '\t');
                        if (line.Length == 0)
                        {
                            continue;
                        }
                        if (line.IndexOf("|ERROR|", StringComparison.OrdinalIgnoreCase) < 0
                            && line.IndexOf("|FATAL|", StringComparison.OrdinalIgnoreCase) < 0
                            && line.IndexOf("run failed", StringComparison.OrdinalIgnoreCase) < 0
                            && line.IndexOf("Exception", StringComparison.Ordinal) < 0)
                        {
                            continue;
                        }
                        if (sb.Length > 0)
                        {
                            sb.Append('\n');
                        }
                        sb.Append(line);
                    }
                }
            }
        }
    }
}
