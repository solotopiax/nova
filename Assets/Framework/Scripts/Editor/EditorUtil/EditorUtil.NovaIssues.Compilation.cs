/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename: EditorUtil.NovaIssues.Compilation.cs
 * descrip:  从消费端 Unity 编译结果和包导入日志确认 Nova 问题。
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEditor.Compilation;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        internal static partial class NovaIssues
        {
            private const string c_InvalidGuid = "invalid-meta-guid";
            private const string c_CompilerCodePattern = @"[A-Z]{2,8}[0-9]{3,6}";
            private static readonly Regex s_CompilerCode = new($@"(?:\A|\berror[ \t]+)(?<code>{c_CompilerCodePattern}):");
            private static readonly Regex s_CompilerLine = new(
                $@"\A[ \t]*(?<path>.+?)\((?<line>[0-9]+),(?<column>[0-9]+)(?:,[0-9]+,[0-9]+)?\):[ \t]+(?:fatal )?error (?<code>{c_CompilerCodePattern}):");
            private static readonly Regex s_InvalidGuidLine = new(
                @"\AThe \.meta file (?<path>.+?\.meta['""]?) does not have a valid GUID and its corresponding Asset file will be ignored\b");
            private static readonly Regex s_LoggedPackage = new(
                @"\A[ \t]+(?<id>com\.solotopia\.nova\.framework(?:\.[a-z0-9_.-]+)?)@(?<version>[a-zA-Z0-9.+-]+)(?: \(location: (?<root>.+)\))?\z");
            private static readonly Regex s_LoggedProject = new(
                @"^(?:-projectpath[ \t]*\r?\n|Successfully changed project path to:[ \t]*(?:\r?\n)?)(?<path>[^\r\n]+)",
                RegexOptions.Multiline | RegexOptions.IgnoreCase);
            private static readonly StringComparison s_PathComparison =
                Path.DirectorySeparatorChar == '\\' ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            private static readonly HashSet<string> s_QueuedDiagnostics = new(StringComparer.Ordinal);
            private static EditorDiagnosticLog[] s_DiagnosticLogs;
            private static Task<EditorDiagnostic[]> s_DiagnosticScan;
            private static double s_NextDiagnosticScan;

            /// <summary>消费端即使编译失败也能收到逐程序集的编译诊断。</summary>
            private static void OnAssemblyCompilationFinished(string assemblyPath, CompilerMessage[] messages)
            {
                if (messages == null || !messages.Any(message => message.type == CompilerMessageType.Error))
                    return;
                if (!BindFrameworkPackage())
                    return;

                var packages = GetDiagnosticPackages(PackageInfo.GetAllRegisteredPackages());
                string assemblyDefinition = messages.Any(message => message.type == CompilerMessageType.Error &&
                                                                    string.IsNullOrEmpty(message.file))
                    ? CompilationPipeline.GetAssemblyDefinitionFilePathFromAssemblyName(Path.GetFileNameWithoutExtension(assemblyPath))
                    : null;
                foreach (var message in messages)
                    QueueDiagnostic(FromCompilerMessage(message, assemblyDefinition, s_Project, packages));
            }

            /// <summary>后台追踪同工程日志，补齐托管监听就绪前的错误。</summary>
            private static void PollEditorDiagnostics(double now)
            {
                if (s_DiagnosticScan != null)
                {
                    if (!s_DiagnosticScan.IsCompleted)
                        return;
                    if (s_DiagnosticScan.Status == TaskStatus.RanToCompletion)
                    {
                        foreach (var diagnostic in s_DiagnosticScan.Result)
                            QueueDiagnostic(diagnostic);
                    }
                    else
                        _ = s_DiagnosticScan.Exception;
                    s_DiagnosticScan = null;
                }

                if (now < s_NextDiagnosticScan)
                    return;
                s_NextDiagnosticScan = now + 2;
                string log = Application.consoleLogPath;
                if (string.IsNullOrEmpty(log))
                    return;
                s_DiagnosticLogs ??= Path.GetFileName(log) == "Editor.log"
                    ? new[] { new EditorDiagnosticLog(log, s_Project), new EditorDiagnosticLog(Path.Combine(Path.GetDirectoryName(log), "Editor-prev.log"), s_Project) }
                    : new[] { new EditorDiagnosticLog(log, s_Project) };
                s_DiagnosticScan = Task.Run(() => s_DiagnosticLogs.SelectMany(reader => reader.Read()).ToArray());
            }

            /// <summary>同一版本中的同一诊断只入队一次，落盘继续按工作区去重。</summary>
            private static void QueueDiagnostic(EditorDiagnostic diagnostic)
            {
                if (diagnostic == null || !s_QueuedDiagnostics.Add(diagnostic.ObservationId))
                    return;
                Queue(new JObject { ["event"] = "editor-diagnostic", ["id"] = diagnostic.ObservationId, ["issue"] = diagnostic.ToIssue() });
            }

            private static Dictionary<string, DiagnosticPackage> GetDiagnosticPackages(PackageInfo[] packages)
            {
                var result = new Dictionary<string, DiagnosticPackage>(StringComparer.Ordinal);
                foreach (var package in packages ?? Array.Empty<PackageInfo>())
                {
                    if (package != null && IsNovaPackage(package.name))
                        result[package.name] = new DiagnosticPackage(package.version, package.resolvedPath);
                }
                return result;
            }

            private static bool IsNovaPackage(string name) => IsKey(name) &&
                (name == c_Framework || name.StartsWith(c_Framework + ".", StringComparison.Ordinal));

            /// <summary>用当前编译消息的文件归属确认问题，只在无文件时使用程序集定义。</summary>
            internal static EditorDiagnostic FromCompilerMessage(CompilerMessage message, string assemblyDefinition, string project,
                IReadOnlyDictionary<string, DiagnosticPackage> packages)
            {
                if (message.type != CompilerMessageType.Error)
                    return null;
                bool hasFile = !string.IsNullOrEmpty(message.file);
                string path = hasFile ? message.file : assemblyDefinition;
                Match code = s_CompilerCode.Match(message.message ?? string.Empty);
                return CreateDiagnostic(path, code.Success ? code.Groups["code"].Value : "compiler-error",
                    hasFile ? message.line : 0, hasFile ? message.column : 0, project, packages);
            }

            /// <summary>识别 Unity 原始单行诊断，正文、栈和绝对路径均不进入报告。</summary>
            internal static EditorDiagnostic FromEditorLogLine(string line, string project,
                IReadOnlyDictionary<string, DiagnosticPackage> packages)
            {
                Match guid = s_InvalidGuidLine.Match(line);
                if (guid.Success)
                    return CreateDiagnostic(guid.Groups["path"].Value, c_InvalidGuid, 0, 0, project, packages);
                Match compiler = s_CompilerLine.Match(line);
                return compiler.Success
                    ? CreateDiagnostic(compiler.Groups["path"].Value, compiler.Groups["code"].Value,
                        ParsePosition(compiler.Groups["line"].Value), ParsePosition(compiler.Groups["column"].Value), project, packages)
                    : null;
            }

            private static int ParsePosition(string value) =>
                int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int position) ? position : 0;

            private static EditorDiagnostic CreateDiagnostic(string file, string code, int line, int column, string project,
                IReadOnlyDictionary<string, DiagnosticPackage> packages)
            {
                if (string.IsNullOrWhiteSpace(file))
                    return null;
                string path = NormalizePath(file.Trim().Trim('\'', '"'));
                string projectPrefix = NormalizePath(project) + "/";
                string relative = path.StartsWith(projectPrefix, s_PathComparison) ? path.Substring(projectPrefix.Length) :
                                  path.StartsWith("./", StringComparison.Ordinal) ? path.Substring(2) : path;
                string packageName = null;
                string asset = null;
                if (relative.StartsWith("Packages/", StringComparison.Ordinal))
                    SplitPackageAsset(relative.Substring("Packages/".Length), false, out packageName, out asset);
                else if (relative.StartsWith("Library/PackageCache/", StringComparison.Ordinal))
                    SplitPackageAsset(relative.Substring("Library/PackageCache/".Length), true, out packageName, out asset);
                else
                {
                    foreach (var entry in packages)
                    {
                        if (string.IsNullOrEmpty(entry.Value.Root) || !path.StartsWith(entry.Value.Root + "/", s_PathComparison))
                            continue;
                        packageName = entry.Key;
                        asset = path.Substring(entry.Value.Root.Length + 1);
                        break;
                    }
                }
                if (!IsNovaPackage(packageName) || string.IsNullOrEmpty(asset) || asset.Length > 350 ||
                    asset.Split('/').Any(segment => segment is "" or "." or "..") ||
                    asset.Any(char.IsControl) || s_Private.IsMatch(asset))
                    return null;

                return new EditorDiagnostic(code, packageName, asset, Math.Max(0, line), Math.Max(0, column),
                    packages.TryGetValue(c_Framework, out var framework) ? framework.Version : "unknown",
                    packages.TryGetValue(packageName, out var package) ? package.Version : "unknown");
            }

            private static void SplitPackageAsset(string path, bool cached, out string package, out string asset)
            {
                int slash = path.IndexOf('/');
                package = slash > 0 ? path.Substring(0, slash) : null;
                asset = slash > 0 ? path.Substring(slash + 1) : null;
                if (cached && package != null)
                {
                    int separator = package.IndexOf('@');
                    package = separator > 0 ? package.Substring(0, separator) : null;
                }
            }

            private static string NormalizePath(string path) => path.Replace('\\', '/').TrimEnd('/');

            internal readonly struct DiagnosticPackage
            {
                internal DiagnosticPackage(string version, string root)
                {
                    Version = ReadVersion(version);
                    Root = string.IsNullOrEmpty(root) ? null : NormalizePath(root);
                }

                internal string Version { get; }
                internal string Root { get; }
            }

            /// <summary>稳定问题标识和版本化观察编号只依赖已归因的位置及错误代码。</summary>
            internal sealed class EditorDiagnostic
            {
                private readonly string _code;
                private readonly string _location;
                private readonly string _framework;
                private readonly string _version;
                private readonly bool _import;
                private readonly string _issueCode;

                internal EditorDiagnostic(string code, string package, string asset, int line, int column, string framework, string version)
                {
                    _code = code;
                    _location = $"Packages/{package}/{asset}" +
                                (line > 0 ? FormattableString.Invariant($"({line},{column})") : string.Empty);
                    _framework = framework;
                    _version = version;
                    _import = code == c_InvalidGuid;
                    _issueCode = "unity." + code + "." + EventId(_location);
                }

                internal string ObservationId => EventId(_issueCode, _framework, _version);

                /// <summary>保留归因位置和扩展包版本，避免重复发送错误正文和版本字段。</summary>
                internal JObject ToIssue()
                {
                    string title = _import ? "Nova 包资源 GUID 无效" : $"Nova 编译错误 {_code}";
                    string actual = _import ? "Unity 忽略了对应资源。" : $"Unity 编译器报告 {_code}。";
                    return new JObject
                    {
                        ["code"] = _issueCode, ["area"] = _import ? "package-import" : "editor-compilation", ["title"] = title,
                        ["summary"] = _location, ["expected"] = _import ? "Nova 包资源应正常导入。" : "Nova 包代码应正常编译。",
                        ["actual"] = actual, ["source"] = "editor", ["framework"] = _framework,
                        ["evidence"] = new JArray(new JObject
                        {
                            ["source"] = _import ? "unity.package-import" : "unity.compiler",
                            ["detail"] = $"包版本 {_version}。"
                        })
                    };
                }
            }

            /// <summary>分段读取单一工程的原生日志，并按当时登记的版本归因。</summary>
            internal sealed class EditorDiagnosticLog
            {
                private readonly string _path;
                private readonly string _project;
                private readonly byte[] _buffer = new byte[1024 * 1024];
                private readonly Dictionary<string, DiagnosticPackage> _packages = new(StringComparer.Ordinal);
                private long _offset;
                private string _headerIdentity;
                private bool _skipLine;

                internal EditorDiagnosticLog(string path, string project)
                {
                    _path = path;
                    _project = NormalizePath(project);
                }

                internal List<EditorDiagnostic> Read()
                {
                    var captured = new List<EditorDiagnostic>();
                    try
                    {
                        using var stream = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                        int headerLength = stream.Read(_buffer, 0, 8192);
                        string header = Encoding.UTF8.GetString(_buffer, 0, headerLength);
                        Match owner = s_LoggedProject.Match(header);
                        if (!owner.Success || !string.Equals(NormalizePath(owner.Groups["path"].Value.Trim().Trim('"')),
                                _project, s_PathComparison))
                        {
                            _headerIdentity = null;
                            return captured;
                        }

                        string identity = EventId(header.Substring(0, owner.Index + owner.Length));
                        if (_headerIdentity != identity || stream.Length < _offset)
                        {
                            _headerIdentity = identity;
                            _offset = 0;
                            _skipLine = false;
                            _packages.Clear();
                        }
                        stream.Position = _offset;
                        int count = stream.Read(_buffer, 0, _buffer.Length);
                        if (count == 0)
                            return captured;
                        int end = Array.LastIndexOf(_buffer, (byte)'\n', count - 1, count);
                        if (end < 0)
                        {
                            if (count == _buffer.Length)
                            {
                                _offset += count;
                                _skipLine = true;
                            }
                            return captured;
                        }
                        int start = _skipLine ? Array.IndexOf(_buffer, (byte)'\n', 0, end + 1) + 1 : 0;
                        _skipLine = false;
                        foreach (string line in Encoding.UTF8.GetString(_buffer, start, end + 1 - start).Split('\n'))
                        {
                            string text = line.TrimEnd('\r');
                            Match package = s_LoggedPackage.Match(text);
                            if (package.Success)
                            {
                                _packages[package.Groups["id"].Value] =
                                    new DiagnosticPackage(package.Groups["version"].Value, package.Groups["root"].Value);
                                continue;
                            }
                            if (text.StartsWith("The .meta file ", StringComparison.Ordinal) ||
                                (text.Contains("):") && text.Contains("error ")))
                            {
                                var diagnostic = FromEditorLogLine(text, _project, _packages);
                                if (diagnostic != null)
                                    captured.Add(diagnostic);
                            }
                        }
                        _offset += end + 1;
                    }
                    catch (IOException) { }
                    catch (UnauthorizedAccessException) { }
                    return captured;
                }
            }
        }
    }
}
