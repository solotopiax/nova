/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.Environment.Python3.cs
 * author:    taoye
 * created:   2026/4/30
 * descrip:   Python3 运行环境多路径探测检查器
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using NovaFramework.Runtime;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        public static partial class Environment
        {
            /// <summary>
            /// Python3 运行环境多路径探测检查器。
            /// 依次执行策略 A（显式候选路径）→ B（PATH python3）→ C（py launcher）→ D（where）→ E（python 降级）。
            /// 结果缓存到 SessionState，同一编辑器会话不重复检测。
            /// </summary>
            public static class Python3Checker
            {
                /// <summary>
                /// 单次命令探测超时（毫秒）。
                /// </summary>
                private const int c_ProbeTimeoutMs = 3000;

                /// <summary>
                /// SessionState 缓存键：是否已执行过检测。
                /// </summary>
                private const string c_SessionKeyCached = "Nova.Python3.EnvCheckCached";

                /// <summary>
                /// SessionState 缓存键：是否可用。
                /// </summary>
                private const string c_SessionKeyAvailable = "Nova.Python3.EnvCheckAvailable";

                /// <summary>
                /// SessionState 缓存键：版本字符串。
                /// </summary>
                private const string c_SessionKeyVersion = "Nova.Python3.EnvCheckVersion";

                /// <summary>
                /// SessionState 缓存键：命中的可执行路径。
                /// </summary>
                private const string c_SessionKeyDetectedPath = "Nova.Python3.EnvCheckDetectedPath";

                /// <summary>
                /// SessionState 缓存键：命中策略名。
                /// </summary>
                private const string c_SessionKeyDetectedVia = "Nova.Python3.EnvCheckDetectedVia";

                /// <summary>
                /// Python 版本号匹配正则：需以 "Python 3." 开头。
                /// </summary>
                private static readonly Regex s_Python3Regex = new Regex(@"^Python 3\.\d+", RegexOptions.Compiled);

                /// <summary>
                /// Python3 检查结果（只读值类型）。
                /// </summary>
                public readonly struct Python3CheckResult
                {
                    /// <summary>
                    /// python3 命令是否可用。
                    /// </summary>
                    public readonly bool IsAvailable;

                    /// <summary>
                    /// 版本字符串（如 "Python 3.11.6"；不可用时为 null）。
                    /// </summary>
                    public readonly string Version;

                    /// <summary>
                    /// 命中的可执行路径（绝对路径或裸命令名；不可用时为 null）。
                    /// </summary>
                    public readonly string DetectedPath;

                    /// <summary>
                    /// 命中策略名（ExplicitPath / PATH / PyLauncher / Where / PythonFallback / NotFound）。
                    /// </summary>
                    public readonly string DetectedVia;

                    /// <summary>
                    /// 构造 Python3 检查结果。
                    /// </summary>
                    /// <param name="isAvailable">是否可用。</param>
                    /// <param name="version">版本字符串。</param>
                    /// <param name="detectedPath">命中路径。</param>
                    /// <param name="detectedVia">命中策略。</param>
                    public Python3CheckResult(bool isAvailable, string version, string detectedPath, string detectedVia)
                    {
                        IsAvailable = isAvailable;
                        Version = version;
                        DetectedPath = detectedPath;
                        DetectedVia = detectedVia;
                    }
                }

                /// <summary>
                /// 检查 python3 环境，结果缓存到 SessionState；同一会话不重复检测。
                /// </summary>
                /// <returns>Python3 检查结果。</returns>
                public static Python3CheckResult Check()
                {
                    bool cached = UnityEditor.SessionState.GetBool(c_SessionKeyCached, false);
                    if (cached)
                    {
                        return ReadFromSession();
                    }

                    return RunCheck();
                }

                /// <summary>
                /// 强制重新检测（忽略 SessionState 缓存）。
                /// </summary>
                /// <returns>最新的 Python3 检查结果。</returns>
                public static Python3CheckResult Recheck()
                {
                    UnityEditor.SessionState.SetBool(c_SessionKeyCached, false);
                    return RunCheck();
                }

                /// <summary>
                /// 执行检测并写入 SessionState 缓存。
                /// </summary>
                /// <returns>检查结果。</returns>
                private static Python3CheckResult RunCheck()
                {
                    Python3CheckResult result = DoCheck();
                    WriteToSession(result);
                    return result;
                }

                /// <summary>
                /// 核心多路径探测逻辑。
                /// 依次执行策略 A/B/C/D/E，第一个成功即返回。
                /// </summary>
                /// <returns>检查结果。</returns>
                private static Python3CheckResult DoCheck()
                {
                    bool isWin = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

                    // 策略 A：显式候选路径
                    foreach (string path in GetExplicitCandidates())
                    {
                        if (!System.IO.File.Exists(path))
                        {
                            continue;
                        }

                        if (TryRunAndParse(path, "--version", out string vA))
                        {
                            Log.Debug(LogTag.Editor, "[Python3Checker] Detected: {0} ({1}) via ExplicitPath", path, vA);
                            return new Python3CheckResult(true, vA, path, "ExplicitPath");
                        }
                    }

                    // 策略 B：PATH 兜底 python3
                    if (TryRunAndParse("python3", "--version", out string vB))
                    {
                        Log.Debug(LogTag.Editor, "[Python3Checker] Detected: python3 ({0}) via PATH", vB);
                        return new Python3CheckResult(true, vB, "python3", "PATH");
                    }

                    // 策略 C：Windows py launcher
                    if (isWin && TryRunAndParse("py", "-3 --version", out string vC))
                    {
                        Log.Debug(LogTag.Editor, "[Python3Checker] Detected: py -3 ({0}) via PyLauncher", vC);
                        return new Python3CheckResult(true, vC, "py -3", "PyLauncher");
                    }

                    // 策略 D：Windows where 命令
                    if (isWin && TryWhere("python3", out string whereResult))
                    {
                        if (TryRunAndParse(whereResult, "--version", out string vD))
                        {
                            Log.Debug(LogTag.Editor, "[Python3Checker] Detected: {0} ({1}) via Where", whereResult, vD);
                            return new Python3CheckResult(true, vD, whereResult, "Where");
                        }
                    }

                    // 策略 E：降级 python，但必须验证是 3.x
                    if (TryRunAndParse("python", "--version", out string vE) && vE.StartsWith("Python 3."))
                    {
                        Log.Debug(LogTag.Editor, "[Python3Checker] Detected: python ({0}) via PythonFallback", vE);
                        return new Python3CheckResult(true, vE, "python", "PythonFallback");
                    }

                    // 全部策略失败
                    string path_ = System.Environment.GetEnvironmentVariable("PATH") ?? "";
                    Log.Debug(LogTag.Editor, "[Python3Checker] Not found after 5 strategies; PATH={0}", path_);
                    return new Python3CheckResult(false, null, null, "NotFound");
                }

                /// <summary>
                /// 按平台返回显式候选可执行路径列表（策略 A）。
                /// macOS: Homebrew（Apple Silicon/Intel）、系统路径、python.org 安装、pyenv。
                /// Windows: 常见安装目录（Python 3.9-3.13）+ AppData 用户路径。
                /// Linux: 系统路径 + pyenv。
                /// </summary>
                /// <returns>候选路径列表（未展开环境变量的路径已展开）。</returns>
                private static IEnumerable<string> GetExplicitCandidates()
                {
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    {
                        return GetMacCandidates();
                    }

                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        return GetWindowsCandidates();
                    }

                    return GetLinuxCandidates();
                }

                /// <summary>
                /// macOS 候选路径列表（5 条）。
                /// </summary>
                /// <returns>macOS 平台候选路径。</returns>
                private static IEnumerable<string> GetMacCandidates()
                {
                    string home = System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile);
                    return new[]
                    {
                        "/opt/homebrew/bin/python3",
                        "/usr/local/bin/python3",
                        "/usr/bin/python3",
                        "/Library/Frameworks/Python.framework/Versions/Current/bin/python3",
                        System.IO.Path.Combine(home, ".pyenv/shims/python3"),
                    };
                }

                /// <summary>
                /// Windows 候选路径列表（涵盖 Python 3.9-3.13 各主流安装位置）。
                /// </summary>
                /// <returns>Windows 平台候选路径（已展开环境变量）。</returns>
                private static IEnumerable<string> GetWindowsCandidates()
                {
                    string[] versions = { "39", "310", "311", "312", "313" };

                    string localAppData = System.Environment.ExpandEnvironmentVariables("%LOCALAPPDATA%");
                    string userProfile = System.Environment.ExpandEnvironmentVariables("%USERPROFILE%");

                    var candidates = new List<string>();
                    foreach (string ver in versions)
                    {
                        candidates.Add($@"C:\Python{ver}\python.exe");
                        candidates.Add($@"C:\Program Files\Python{ver}\python.exe");
                        candidates.Add($@"C:\Program Files (x86)\Python{ver}\python.exe");
                        candidates.Add(System.IO.Path.Combine(localAppData, $@"Programs\Python\Python{ver}\python.exe"));
                        candidates.Add(System.IO.Path.Combine(userProfile, $@"AppData\Local\Programs\Python\Python{ver}\python.exe"));
                    }

                    return candidates;
                }

                /// <summary>
                /// Linux 候选路径列表（3 条）。
                /// </summary>
                /// <returns>Linux 平台候选路径。</returns>
                private static IEnumerable<string> GetLinuxCandidates()
                {
                    string home = System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile);
                    return new[]
                    {
                        "/usr/bin/python3",
                        "/usr/local/bin/python3",
                        System.IO.Path.Combine(home, ".pyenv/shims/python3"),
                    };
                }

                /// <summary>
                /// 尝试执行指定可执行文件并解析 Python 3.x 版本字符串。
                /// 超时 3 秒自动终止，不抛异常。
                /// </summary>
                /// <param name="exe">可执行文件路径或命令名。</param>
                /// <param name="args">命令行参数。</param>
                /// <param name="version">解析到的版本字符串（如 "Python 3.11.6"）；失败时为 null。</param>
                /// <returns>是否成功解析到 Python 3.x 版本。</returns>
                private static bool TryRunAndParse(string exe, string args, out string version)
                {
                    version = null;
                    ProcessRunner.ProcessResult result = ProcessRunner.RunSync(exe, args, c_ProbeTimeoutMs);
                    if (result.TimedOut)
                    {
                        return false;
                    }

                    // python --version 部分旧版本输出到 stderr
                    string output = string.IsNullOrWhiteSpace(result.Stdout) ? result.Stderr : result.Stdout;
                    output = output?.Trim();

                    if (string.IsNullOrEmpty(output))
                    {
                        return false;
                    }

                    // 取第一行（防止多行输出干扰匹配）
                    string firstLine = output.Split('\n')[0].Trim();
                    if (!s_Python3Regex.IsMatch(firstLine))
                    {
                        return false;
                    }

                    version = firstLine;
                    return true;
                }

                /// <summary>
                /// 使用 Windows where 命令查询指定命令的完整路径。
                /// 仅用于策略 D（Windows 专属）。
                /// </summary>
                /// <param name="command">要查询的命令名。</param>
                /// <param name="fullPath">找到的第一个完整路径；失败时为 null。</param>
                /// <returns>是否找到有效路径。</returns>
                private static bool TryWhere(string command, out string fullPath)
                {
                    fullPath = null;
                    ProcessRunner.ProcessResult result = ProcessRunner.RunSync("where", command, c_ProbeTimeoutMs);
                    if (result.TimedOut || result.ExitCode != 0)
                    {
                        return false;
                    }

                    string output = result.Stdout?.Trim();
                    if (string.IsNullOrEmpty(output))
                    {
                        return false;
                    }

                    // where 可能返回多行，取第一个有效路径
                    string[] lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    if (lines.Length == 0)
                    {
                        return false;
                    }

                    fullPath = lines[0].Trim();
                    return !string.IsNullOrEmpty(fullPath);
                }

                /// <summary>
                /// 将检查结果写入 SessionState 缓存（5 个 key）。
                /// </summary>
                /// <param name="result">检查结果。</param>
                private static void WriteToSession(Python3CheckResult result)
                {
                    UnityEditor.SessionState.SetBool(c_SessionKeyAvailable, result.IsAvailable);
                    UnityEditor.SessionState.SetString(c_SessionKeyVersion, result.Version ?? "");
                    UnityEditor.SessionState.SetString(c_SessionKeyDetectedPath, result.DetectedPath ?? "");
                    UnityEditor.SessionState.SetString(c_SessionKeyDetectedVia, result.DetectedVia ?? "");
                    UnityEditor.SessionState.SetBool(c_SessionKeyCached, true);
                }

                /// <summary>
                /// 从 SessionState 缓存读取检查结果。
                /// </summary>
                /// <returns>缓存的检查结果。</returns>
                private static Python3CheckResult ReadFromSession()
                {
                    bool isAvailable = UnityEditor.SessionState.GetBool(c_SessionKeyAvailable, false);
                    string version = UnityEditor.SessionState.GetString(c_SessionKeyVersion, "");
                    string detectedPath = UnityEditor.SessionState.GetString(c_SessionKeyDetectedPath, "");
                    string detectedVia = UnityEditor.SessionState.GetString(c_SessionKeyDetectedVia, "");
                    return new Python3CheckResult(
                        isAvailable,
                        string.IsNullOrEmpty(version) ? null : version,
                        string.IsNullOrEmpty(detectedPath) ? null : detectedPath,
                        string.IsNullOrEmpty(detectedVia) ? null : detectedVia
                    );
                }
            }
        }
    }
}
