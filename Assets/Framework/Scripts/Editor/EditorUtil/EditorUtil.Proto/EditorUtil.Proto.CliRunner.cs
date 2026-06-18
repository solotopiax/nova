/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.Proto.CliRunner.cs
 * author:    taoye
 * created:   2026/4/17
 * descrip:   protoc CLI 外部进程调用器
 ***************************************************************/

using System;
using System.Text;
using System.Text.RegularExpressions;
using NovaFramework.Runtime;
using UnityEditor;
using UnityEngine;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        /// <summary>
        /// Protobuf 编辑器工具集。
        /// </summary>
        public static partial class Proto
        {
            /// <summary>
            /// protoc CLI 外部进程调用器，封装 protoc 命令行调用（Mac + Win 跨平台）。
            /// </summary>
            public static class CliRunner
            {
                /// <summary>
                /// UPM 包名。
                /// </summary>
                private const string c_PackageName = "com.solotopia.luban";

                /// <summary>
                /// protoc 在 UPM 包内的相对路径（Mac）。
                /// </summary>
                private const string c_ProtocRelPathMac = "Core/Tools~/protoc/bin/protoc";

                /// <summary>
                /// protoc 在 UPM 包内的相对路径（Windows）。
                /// </summary>
                private const string c_ProtocRelPathWin = "Core/Tools~/protoc/bin/protoc.exe";

                /// <summary>
                /// protoc include 目录在 UPM 包内的相对路径。
                /// </summary>
                private const string c_ProtocIncludeRelPath = "Core/Tools~/protoc/include";

                /// <summary>
                /// .proto 文件搜索模式。
                /// </summary>
                private const string c_SearchPattern = "*.proto";

                /// <summary>
                /// 获取 protoc 可执行文件完整路径。
                /// </summary>
                /// <returns>protoc 路径，不存在时返回 null。</returns>
                public static string GetProtocPath()
                {
                    string packagePath = Util.SysIO.Path.GetFullPath($"Packages/{c_PackageName}");
                    string relPath = Application.platform == RuntimePlatform.WindowsEditor ? c_ProtocRelPathWin : c_ProtocRelPathMac;
                    string protocPath = Util.SysIO.Path.Combine(packagePath, relPath);
                    if (Util.SysIO.File.Exists(protocPath))
                    {
                        return protocPath;
                    }

                    Log.Error(LogTag.Editor, "未找到 protoc：{0}，请确保 {1} UPM 包已安装。", protocPath, c_PackageName);
                    return null;
                }

                /// <summary>
                /// 获取 protoc include 目录完整路径。
                /// </summary>
                /// <returns>include 目录路径，不存在时返回 null。</returns>
                public static string GetProtocIncludePath()
                {
                    string packagePath = Util.SysIO.Path.GetFullPath($"Packages/{c_PackageName}");
                    string includePath = Util.SysIO.Path.Combine(packagePath, c_ProtocIncludeRelPath);
                    if (Util.SysIO.Directory.Exists(includePath))
                    {
                        return includePath;
                    }

                    return null;
                }

                /// <summary>
                /// 编译单个 .proto 文件为 C# 代码。
                /// </summary>
                /// <param name="protoFilePath">.proto 文件完整路径。</param>
                /// <param name="protoRootDir">.proto 根目录（protoc -I 参数）。</param>
                /// <param name="csharpOutDir">C# 输出目录。</param>
                /// <returns>是否成功。</returns>
                public static bool CompileSingle(string protoFilePath, string protoRootDir, string csharpOutDir)
                {
                    if (string.IsNullOrEmpty(protoFilePath) || !Util.SysIO.File.Exists(protoFilePath))
                    {
                        Log.Error(LogTag.Editor, "Proto 文件不存在：{0}", protoFilePath);
                        return false;
                    }

                    EnsureDirectoryExists(csharpOutDir);
                    FixImportPaths(protoRootDir);

                    StringBuilder args = new StringBuilder();
                    AppendIncludePaths(args, protoRootDir);
                    args.Append($" --csharp_out=\"{csharpOutDir}\"");
                    args.Append($" \"{protoFilePath}\"");

                    bool success = RunProtoc(args.ToString());
                    if (success)
                    {
                        Log.Debug(LogTag.Editor, "Proto 编译成功：{0}", Util.SysIO.Path.GetFileName(protoFilePath));
                    }

                    return success;
                }

                /// <summary>
                /// 编译目录下所有 .proto 文件为 C# 代码。
                /// 逐文件编译，并根据 proto 文件相对于 protoRootDir 的目录结构，
                /// 在 csharpOutDir 下创建对应子目录，保留相对路径层级。
                /// 例如：protoRootDir/common/header.proto → csharpOutDir/common/Header.cs
                /// </summary>
                /// <param name="protoRootDir">.proto 根目录（同时作为 protoc -I 参数）。</param>
                /// <param name="csharpOutDir">C# 输出根目录。</param>
                /// <returns>全部文件编译成功返回 true，任意文件失败立即返回 false。</returns>
                public static bool CompileAll(string protoRootDir, string csharpOutDir)
                {
                    if (string.IsNullOrEmpty(protoRootDir) || !Util.SysIO.Directory.Exists(protoRootDir))
                    {
                        Log.Error(LogTag.Editor, "Proto 根目录不存在：{0}", protoRootDir);
                        return false;
                    }

                    string[] protoFiles = Util.SysIO.Directory.GetFiles(protoRootDir, c_SearchPattern, System.IO.SearchOption.AllDirectories);
                    if (protoFiles.Length == 0)
                    {
                        Log.Warning(LogTag.Editor, "Proto 根目录下无 .proto 文件：{0}", protoRootDir);
                        return true;
                    }

                    FixImportPaths(protoRootDir);
                    EnsureDirectoryExists(csharpOutDir);
                    string rootNorm = protoRootDir.TrimEnd('/', '\\');

                    foreach (string filePath in protoFiles)
                    {
                        string relativePath = Util.SysIO.Path.GetRelativePath(rootNorm, filePath);
                        string relativeDir = Util.SysIO.Path.GetDirectoryName(relativePath);
                        string targetDir = string.IsNullOrEmpty(relativeDir) ? csharpOutDir : Util.SysIO.Path.Combine(csharpOutDir, relativeDir);
                        EnsureDirectoryExists(targetDir);

                        StringBuilder args = new StringBuilder();
                        AppendIncludePaths(args, rootNorm);
                        args.Append($" --csharp_out=\"{targetDir}\"");
                        args.Append($" \"{filePath}\"");

                        if (!RunProtoc(args.ToString()))
                        {
                            return false;
                        }
                    }

                    Log.Debug(LogTag.Editor, "Proto 全量编译成功：{0} 个文件。", protoFiles.Length);
                    return true;
                }

                /// <summary>
                /// 扫描目录获取所有 .proto 文件路径。
                /// </summary>
                /// <param name="directoryPath">目录路径。</param>
                /// <returns>所有 .proto 文件的完整路径数组，目录不存在时返回空数组。</returns>
                public static string[] GetProtoFiles(string directoryPath)
                {
                    if (string.IsNullOrEmpty(directoryPath) || !Util.SysIO.Directory.Exists(directoryPath))
                    {
                        return Array.Empty<string>();
                    }

                    return Util.SysIO.Directory.GetFiles(directoryPath, c_SearchPattern, System.IO.SearchOption.AllDirectories);
                }

                /// <summary>
                /// 扫描 protoRootDir 下所有 .proto 文件，对每条 import 语句做大小写不敏感修正。
                /// 当 import 路径在磁盘上不存在（大小写不匹配）时，按文件名搜索唯一候选并回写文件。
                /// 找到零个或多个同名候选时不修改，仅输出 Warning。
                /// </summary>
                /// <param name="protoRootDir">.proto 根目录。</param>
                private static void FixImportPaths(string protoRootDir)
                {
                    if (string.IsNullOrEmpty(protoRootDir) || !Util.SysIO.Directory.Exists(protoRootDir))
                    {
                        return;
                    }

                    const string c_ImportPattern = @"^(\s*import\s+"")(.+?)(""\s*;\s*)$";
                    Regex importRegex = new Regex(c_ImportPattern, RegexOptions.Multiline);

                    string[] protoFiles = Util.SysIO.Directory.GetFiles(protoRootDir, c_SearchPattern, System.IO.SearchOption.AllDirectories);
                    foreach (string filePath in protoFiles)
                    {
                        string original = Util.SysIO.File.ReadAllTextSync(filePath, Encoding.UTF8);
                        string fixed_ = importRegex.Replace(original, match =>
                        {
                            string importPath = match.Groups[2].Value;
                            string fileName = Util.SysIO.Path.GetFileName(importPath);
                            string[] candidates = Util.SysIO.Directory.GetFiles(protoRootDir, fileName, System.IO.SearchOption.AllDirectories);
                            if (candidates.Length == 0)
                            {
                                Log.Warning(LogTag.Editor, "FixImportPaths：找不到 import 对应文件，跳过修正。文件：{0}，import：{1}", Util.SysIO.Path.GetFileName(filePath), importPath);
                                return match.Value;
                            }

                            if (candidates.Length > 1)
                            {
                                Log.Warning(LogTag.Editor, "FixImportPaths：找到多个同名候选，无法唯一确定，跳过修正。文件：{0}，import：{1}", Util.SysIO.Path.GetFileName(filePath), importPath);
                                return match.Value;
                            }

                            string corrected = Util.SysIO.Path.GetRelativePath(protoRootDir, candidates[0]).Replace('\\', '/');
                            if (string.Equals(importPath, corrected, StringComparison.Ordinal))
                            {
                                return match.Value;
                            }

                            Log.Debug(LogTag.Editor, "FixImportPaths：修正 import 路径。文件：{0}，{1} → {2}", Util.SysIO.Path.GetFileName(filePath), importPath, corrected);
                            return match.Groups[1].Value + corrected + match.Groups[3].Value;
                        });

                        if (!string.Equals(fixed_, original, StringComparison.Ordinal))
                        {
                            Util.SysIO.File.WriteAllTextSync(filePath, fixed_, Encoding.UTF8);
                        }
                    }
                }

                /// <summary>
                /// 执行 Luban → protoc 闭环管线：Luban 从 Excel 生成 .proto → protoc 编译为 C#。
                /// </summary>
                /// <param name="lubanConfPath">luban.conf 文件路径。</param>
                /// <param name="lubanTargetName">Luban target 名称。</param>
                /// <param name="lubanCustomTemplateDirs">Luban 自定义模板目录列表（可为 null）。按优先级排序，Luban 依次查找。</param>
                /// <param name="protoExportDir">Luban 生成 .proto 的输出目录（同时作为 protoc -I 根目录）。</param>
                /// <param name="csharpOutDir">protoc 生成 C# 的输出目录。</param>
                /// <returns>是否成功。</returns>
                public static bool RunLubanProtoPipeline(string lubanConfPath, string lubanTargetName, string[] lubanCustomTemplateDirs, string protoExportDir, string csharpOutDir)
                {
                    if (string.IsNullOrEmpty(lubanConfPath) || !Util.SysIO.File.Exists(lubanConfPath))
                    {
                        Log.Error(LogTag.Editor, "Luban 配置文件不存在：{0}", lubanConfPath);
                        return false;
                    }

                    EnsureDirectoryExists(protoExportDir);
                    EnsureDirectoryExists(csharpOutDir);

                    Log.Debug(LogTag.Editor, "Luban Proto 管线开始：Excel → .proto ...");
                    if (!Luban.CliRunner.RunProtoSchemaGen(lubanConfPath, lubanTargetName, protoExportDir, lubanCustomTemplateDirs))
                    {
                        return false;
                    }

                    Log.Debug(LogTag.Editor, "Luban Proto 管线：.proto → C# ...");
                    if (!CompileAll(protoExportDir, csharpOutDir))
                    {
                        return false;
                    }

                    Log.Debug(LogTag.Editor, "Luban Proto 管线完成。");
                    return true;
                }

                /// <summary>
                /// 拼接 -I include 路径参数（Proto 根目录 + protoc 内置 include）。
                /// </summary>
                /// <param name="args">参数构建器。</param>
                /// <param name="protoRootDir">Proto 根目录。</param>
                private static void AppendIncludePaths(StringBuilder args, string protoRootDir)
                {
                    args.Append($"-I\"{protoRootDir}\"");

                    string includePath = GetProtocIncludePath();
                    if (!string.IsNullOrEmpty(includePath))
                    {
                        args.Append($" -I\"{includePath}\"");
                    }
                }

                /// <summary>
                /// 确保目录存在。
                /// </summary>
                /// <param name="directoryPath">目录路径。</param>
                private static void EnsureDirectoryExists(string directoryPath)
                {
                    if (!string.IsNullOrEmpty(directoryPath))
                    {
                        Util.SysIO.Directory.CreateIfNotExist(directoryPath);
                    }
                }

                /// <summary>
                /// 执行 protoc 命令。
                /// <para>失败时合并 stdout + stderr 输出，附带命令行用于本地复现。</para>
                /// </summary>
                /// <param name="arguments">protoc 参数。</param>
                /// <returns>进程退出码为 0 时返回 true。</returns>
                private static bool RunProtoc(string arguments)
                {
                    string protocPath = GetProtocPath();
                    if (protocPath == null)
                    {
                        return false;
                    }

                    ProcessRunner.ProcessResult result = ProcessRunner.RunSync(protocPath, arguments);

                    if (result.TimedOut)
                    {
                        Log.Error(LogTag.Editor, "protoc 执行超时，进程已终止。命令：{0} {1}\n输出：\n{2}", protocPath, arguments, ProcessRunner.FormatOutput(result));
                        return false;
                    }

                    if (!result.Success)
                    {
                        Log.Error(LogTag.Editor, "protoc 执行失败（ExitCode={0}）。命令：{1} {2}\n输出：\n{3}",
                            result.ExitCode,
                            protocPath,
                            arguments,
                            ProcessRunner.FormatOutput(result));
                        return false;
                    }

                    if (!string.IsNullOrEmpty(result.Stderr))
                    {
                        Log.Warning(LogTag.Editor, "protoc stderr：{0}", result.Stderr);
                    }

                    return true;
                }
            }
        }
    }
}
