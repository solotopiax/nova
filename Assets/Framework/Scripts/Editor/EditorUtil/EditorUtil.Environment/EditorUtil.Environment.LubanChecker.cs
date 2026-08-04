/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.Environment.LubanChecker.cs
 * author:    taoye
 * created:   2026/4/27
 * descrip:   Luban 运行环境检查器，检测 dotnet-sdk 版本是否落在 [8.0.127, 10.0.203] 建议区间
 ***************************************************************/

using SIO = System.IO;
using NovaFramework.Runtime;
using UnityEditor;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        public static partial class Environment
        {
            /// <summary>
            /// Luban 运行环境检查器，检测 dotnet-sdk 版本是否落在 [8.0.127, 10.0.203] 建议区间。
            /// </summary>
            [InitializeOnLoad]
            internal static class LubanChecker
            {
                /// <summary>
                /// SessionState 缓存键：是否已就绪。
                /// </summary>
                private const string c_SessionKeyReady = "Nova.Luban.EnvCheckReady";

                /// <summary>
                /// SessionState 缓存键：dotnet 路径。
                /// </summary>
                private const string c_SessionKeyDotnetPath = "Nova.Luban.EnvCheckDotnetPath";

                /// <summary>
                /// SessionState 缓存键：dotnet 版本号。
                /// </summary>
                private const string c_SessionKeyDotnetVersion = "Nova.Luban.EnvCheckDotnetVersion";

                /// <summary>
                /// SessionState 缓存键：错误信息。
                /// </summary>
                private const string c_SessionKeyErrorMessage = "Nova.Luban.EnvCheckErrorMessage";

                /// <summary>
                /// SessionState 缓存键：问题类型（int）。
                /// </summary>
                private const string c_SessionKeyIssue = "Nova.Luban.EnvCheckIssue";

                /// <summary>
                /// SessionState 缓存键：dotnet 独立问题类型（int）。
                /// </summary>
                private const string c_SessionKeyDotnetIssue = "Nova.Luban.EnvCheckDotnetIssue";

                /// <summary>
                /// SessionState 缓存键：Luban.dll 是否就绪。
                /// </summary>
                private const string c_SessionKeyLubanDllReady = "Nova.Luban.EnvCheckLubanDllReady";

                /// <summary>
                /// SessionState 缓存结构版本；独立状态字段加入后用于淘汰旧缓存。
                /// </summary>
                private const string c_SessionKeyCacheVersion = "Nova.Luban.EnvCheckCacheVersion";

                /// <summary>
                /// dotnet 建议版本下限（闭区间）。
                /// </summary>
                internal const string c_MinDotnetVersion = "8.0.127";

                /// <summary>
                /// dotnet 建议版本上限（闭区间）。
                /// </summary>
                internal const string c_MaxDotnetVersion = "10.0.203";

                /// <summary>
                /// 环境问题类型。
                /// </summary>
                public enum EnvironmentIssue
                {
                    /// <summary>
                    /// 无问题，环境就绪。
                    /// </summary>
                    None,

                    /// <summary>
                    /// 未找到 dotnet 可执行文件。
                    /// </summary>
                    DotnetNotFound,

                    /// <summary>
                    /// dotnet 版本低于建议下限。
                    /// </summary>
                    DotnetVersionTooLow,

                    /// <summary>
                    /// dotnet 版本高于建议上限。
                    /// </summary>
                    DotnetVersionTooHigh,

                    /// <summary>
                    /// dotnet 无法正常执行（进程失败/超时）。
                    /// </summary>
                    DotnetNotExecutable,

                    /// <summary>
                    /// 未找到 Luban.dll，UPM 包可能未安装。
                    /// </summary>
                    LubanDllNotFound,
                }

                /// <summary>
                /// 环境检查结果（只读值类型）。
                /// </summary>
                public readonly struct EnvironmentCheckResult
                {
                    /// <summary>
                    /// 环境是否就绪。
                    /// </summary>
                    public readonly bool IsReady;

                    /// <summary>
                    /// dotnet 可执行文件路径（未找到时为 null）。
                    /// </summary>
                    public readonly string DotnetPath;

                    /// <summary>
                    /// dotnet 版本号字符串（如 "8.0.100"，失败时为 null）。
                    /// </summary>
                    public readonly string DotnetVersion;

                    /// <summary>
                    /// 错误信息（就绪时为 null）。
                    /// </summary>
                    public readonly string ErrorMessage;

                    /// <summary>
                    /// 环境问题类型。
                    /// </summary>
                    public readonly EnvironmentIssue Issue;

                    /// <summary>
                    /// dotnet 独立检查结果；即使 Luban.dll 同时缺失也不会被覆盖。
                    /// </summary>
                    public readonly EnvironmentIssue DotnetIssue;

                    /// <summary>
                    /// Luban.dll 是否存在。
                    /// </summary>
                    public readonly bool IsLubanDllReady;

                    /// <summary>
                    /// 构造环境检查结果。
                    /// </summary>
                    /// <param name="isReady">是否就绪。</param>
                    /// <param name="dotnetPath">dotnet 路径。</param>
                    /// <param name="dotnetVersion">dotnet 版本。</param>
                    /// <param name="errorMessage">错误信息。</param>
                    /// <param name="issue">问题类型。</param>
                    public EnvironmentCheckResult(bool isReady, string dotnetPath, string dotnetVersion, string errorMessage, EnvironmentIssue issue)
                        : this(
                            isReady,
                            dotnetPath,
                            dotnetVersion,
                            errorMessage,
                            issue,
                            issue == EnvironmentIssue.LubanDllNotFound ? EnvironmentIssue.None : issue,
                            issue != EnvironmentIssue.LubanDllNotFound)
                    {
                    }

                    /// <summary>
                    /// 构造包含 dotnet 与 Luban.dll 独立状态的环境检查结果。
                    /// </summary>
                    public EnvironmentCheckResult(
                        bool isReady,
                        string dotnetPath,
                        string dotnetVersion,
                        string errorMessage,
                        EnvironmentIssue issue,
                        EnvironmentIssue dotnetIssue,
                        bool isLubanDllReady)
                    {
                        IsReady = isReady;
                        DotnetPath = dotnetPath;
                        DotnetVersion = dotnetVersion;
                        ErrorMessage = errorMessage;
                        Issue = issue;
                        DotnetIssue = dotnetIssue;
                        IsLubanDllReady = isLubanDllReady;
                    }
                }

                /// <summary>
                /// 静态构造方法，在编辑器启动后执行静默环境检测。
                /// </summary>
                static LubanChecker()
                {
                    // 延迟到首次 update 再检测，避免 InitializeOnLoad 阶段过早执行影响启动
                    EditorApplication.delayCall += RunSilentCheck;
                }

                /// <summary>
                /// 检查环境，结果缓存到 SessionState；同一会话不重复检查。
                /// </summary>
                /// <returns>环境检查结果。</returns>
                public static EnvironmentCheckResult Check()
                {
                    // 读取缓存
                    bool cached = SessionState.GetBool(c_SessionKeyReady + "_cached", false) &&
                                  SessionState.GetInt(c_SessionKeyCacheVersion, 0) == 2;
                    if (cached)
                    {
                        return ReadFromSession();
                    }

                    return RunCheck();
                }

                /// <summary>
                /// 强制重新检查（忽略缓存）。
                /// </summary>
                /// <returns>最新的环境检查结果。</returns>
                public static EnvironmentCheckResult Recheck()
                {
                    SessionState.SetBool(c_SessionKeyReady + "_cached", false);
                    return RunCheck();
                }

                /// <summary>
                /// 静默检测：结果不就绪时输出 Warning 日志，不弹窗。
                /// </summary>
                private static void RunSilentCheck()
                {
                    EnvironmentCheckResult result = Check();
                    if (!result.IsReady)
                    {
                        Log.Warning(LogTag.Editor, "Luban 环境未就绪：{0}。请通过 Nova/Luban 环境检查 打开引导窗口。", result.ErrorMessage);
                    }
                }

                /// <summary>
                /// 执行完整环境检查并写入 SessionState。
                /// </summary>
                /// <returns>检查结果。</returns>
                private static EnvironmentCheckResult RunCheck()
                {
                    EnvironmentCheckResult result = DoCheck();
                    WriteToSession(result);
                    return result;
                }

                /// <summary>
                /// 实际执行检查逻辑的核心方法。
                /// </summary>
                /// <returns>检查结果。</returns>
                private static EnvironmentCheckResult DoCheck()
                {
                    // Luban.dll 与 dotnet 独立检测，避免 dotnet 版本异常时停留在“待检测”。
                    string dllPath = Luban.CliRunner.GetLubanDllPath();
                    bool isLubanDllReady = dllPath != null && SIO.File.Exists(dllPath);

                    // 步骤 1：检测 dotnet 路径
                    string dotnetPath = Luban.CliRunner.ResolveDotnetPath();
                    if (dotnetPath == null)
                    {
                        return new EnvironmentCheckResult(false, null, null, $"未找到 dotnet 可执行文件，请安装 .NET SDK {c_MinDotnetVersion} ~ {c_MaxDotnetVersion}。", EnvironmentIssue.DotnetNotFound, EnvironmentIssue.DotnetNotFound, isLubanDllReady);
                    }

                    // 步骤 2：执行 dotnet --version 获取版本号
                    ProcessRunner.ProcessResult versionResult = ProcessRunner.RunSync(dotnetPath, "--version");
                    if (versionResult.TimedOut || !versionResult.Success)
                    {
                        string errMsg = versionResult.TimedOut
                            ? $"dotnet --version 执行超时。\n输出：\n{ProcessRunner.FormatOutput(versionResult)}"
                            : $"dotnet --version 执行失败（ExitCode={versionResult.ExitCode}）。\n输出：\n{ProcessRunner.FormatOutput(versionResult)}";
                        return new EnvironmentCheckResult(false, dotnetPath, null, errMsg, EnvironmentIssue.DotnetNotExecutable, EnvironmentIssue.DotnetNotExecutable, isLubanDllReady);
                    }

                    string versionStr = versionResult.Stdout.Trim();

                    // 步骤 3：解析版本并做区间校验
                    System.Version version = ParseVersion(versionStr);
                    if (version == null)
                    {
                        string errMsg = $"dotnet --version 输出无法解析为版本号（输出：{versionStr}）。";
                        return new EnvironmentCheckResult(false, dotnetPath, versionStr, errMsg, EnvironmentIssue.DotnetNotExecutable, EnvironmentIssue.DotnetNotExecutable, isLubanDllReady);
                    }

                    System.Version minVersion = System.Version.Parse(c_MinDotnetVersion);
                    System.Version maxVersion = System.Version.Parse(c_MaxDotnetVersion);
                    EnvironmentIssue dotnetIssue = EnvironmentIssue.None;
                    if (version < minVersion)
                    {
                        dotnetIssue = EnvironmentIssue.DotnetVersionTooLow;
                    }
                    else if (version > maxVersion)
                    {
                        dotnetIssue = EnvironmentIssue.DotnetVersionTooHigh;
                    }

                    return BuildCompletedCheckResult(dotnetPath, versionStr, dotnetIssue, isLubanDllReady);
                }

                /// <summary>
                /// 合并已完成的 dotnet 版本检查与 Luban.dll 检查。
                /// </summary>
                private static EnvironmentCheckResult BuildCompletedCheckResult(
                    string dotnetPath,
                    string versionStr,
                    EnvironmentIssue dotnetIssue,
                    bool isLubanDllReady)
                {
                    if (!isLubanDllReady)
                    {
                        return new EnvironmentCheckResult(false, dotnetPath, versionStr, "未找到 Luban.dll，请确认 com.solotopia.luban UPM 包已安装。", EnvironmentIssue.LubanDllNotFound, dotnetIssue, false);
                    }

                    if (dotnetIssue == EnvironmentIssue.DotnetVersionTooLow || dotnetIssue == EnvironmentIssue.DotnetVersionTooHigh)
                    {
                        string level = dotnetIssue == EnvironmentIssue.DotnetVersionTooLow ? "过低" : "过高";
                        string errMsg = $"dotnet 版本{level}（当前 {versionStr}，建议 {c_MinDotnetVersion} ~ {c_MaxDotnetVersion}）。";
                        return new EnvironmentCheckResult(false, dotnetPath, versionStr, errMsg, dotnetIssue, dotnetIssue, true);
                    }

                    return new EnvironmentCheckResult(true, dotnetPath, versionStr, null, EnvironmentIssue.None, EnvironmentIssue.None, true);
                }

                /// <summary>
                /// 将检查结果写入 SessionState 缓存。
                /// </summary>
                /// <param name="result">检查结果。</param>
                private static void WriteToSession(EnvironmentCheckResult result)
                {
                    SessionState.SetBool(c_SessionKeyReady, result.IsReady);
                    SessionState.SetString(c_SessionKeyDotnetPath, result.DotnetPath ?? "");
                    SessionState.SetString(c_SessionKeyDotnetVersion, result.DotnetVersion ?? "");
                    SessionState.SetString(c_SessionKeyErrorMessage, result.ErrorMessage ?? "");
                    SessionState.SetInt(c_SessionKeyIssue, (int)result.Issue);
                    SessionState.SetInt(c_SessionKeyDotnetIssue, (int)result.DotnetIssue);
                    SessionState.SetBool(c_SessionKeyLubanDllReady, result.IsLubanDllReady);
                    SessionState.SetInt(c_SessionKeyCacheVersion, 2);
                    SessionState.SetBool(c_SessionKeyReady + "_cached", true);
                }

                /// <summary>
                /// 从 SessionState 缓存读取检查结果。
                /// </summary>
                /// <returns>缓存的检查结果。</returns>
                private static EnvironmentCheckResult ReadFromSession()
                {
                    bool isReady = SessionState.GetBool(c_SessionKeyReady, false);
                    string dotnetPath = SessionState.GetString(c_SessionKeyDotnetPath, "");
                    string dotnetVersion = SessionState.GetString(c_SessionKeyDotnetVersion, "");
                    string errorMessage = SessionState.GetString(c_SessionKeyErrorMessage, "");
                    EnvironmentIssue issue = (EnvironmentIssue)SessionState.GetInt(c_SessionKeyIssue, 0);
                    EnvironmentIssue dotnetIssue = (EnvironmentIssue)SessionState.GetInt(c_SessionKeyDotnetIssue, (int)(issue == EnvironmentIssue.LubanDllNotFound ? EnvironmentIssue.None : issue));
                    bool isLubanDllReady = SessionState.GetBool(c_SessionKeyLubanDllReady, issue != EnvironmentIssue.LubanDllNotFound);
                    return new EnvironmentCheckResult(isReady, string.IsNullOrEmpty(dotnetPath) ? null : dotnetPath, string.IsNullOrEmpty(dotnetVersion) ? null : dotnetVersion, string.IsNullOrEmpty(errorMessage) ? null : errorMessage, issue, dotnetIssue, isLubanDllReady);
                }

                /// <summary>
                /// 解析版本字符串为 System.Version，解析失败返回 null。
                /// </summary>
                /// <param name="versionStr">版本字符串（如 "8.0.100" 或 "8.0.100-preview.1"）。</param>
                /// <returns>解析成功的 System.Version，失败时返回 null。</returns>
                private static System.Version ParseVersion(string versionStr)
                {
                    if (string.IsNullOrEmpty(versionStr))
                    {
                        return null;
                    }

                    // 剥离 preview/rc 后缀（"-" 之前的部分才是标准 semver 数字段）
                    int dashIndex = versionStr.IndexOf('-');
                    string cleanStr = dashIndex > 0 ? versionStr.Substring(0, dashIndex) : versionStr;
                    if (System.Version.TryParse(cleanStr, out System.Version result))
                    {
                        return result;
                    }

                    return null;
                }
            }
        }
    }
}
