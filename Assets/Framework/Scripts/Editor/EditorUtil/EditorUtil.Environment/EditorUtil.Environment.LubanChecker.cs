/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.Environment.LubanChecker.cs
 * author:    taoye
 * created:   2026/4/27
 * descrip:   Luban 运行环境检查器，检测 dotnet-sdk 版本是否落在 [8.0.127, 10.0.203] 闭区间
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
            /// Luban 运行环境检查器，检测 dotnet-sdk 版本是否落在 [8.0.127, 10.0.203] 闭区间。
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
                /// dotnet 兼容版本下限（闭区间），低于此版本硬阻断。
                /// </summary>
                internal const string c_MinDotnetVersion = "8.0.127";

                /// <summary>
                /// dotnet 兼容版本上限（闭区间），高于此版本硬阻断。
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
                    /// dotnet 版本低于兼容下限。
                    /// </summary>
                    DotnetVersionTooLow,

                    /// <summary>
                    /// dotnet 版本高于兼容上限，超出 Luban 兼容区间。
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
                    /// 构造环境检查结果。
                    /// </summary>
                    /// <param name="isReady">是否就绪。</param>
                    /// <param name="dotnetPath">dotnet 路径。</param>
                    /// <param name="dotnetVersion">dotnet 版本。</param>
                    /// <param name="errorMessage">错误信息。</param>
                    /// <param name="issue">问题类型。</param>
                    public EnvironmentCheckResult(bool isReady, string dotnetPath, string dotnetVersion, string errorMessage, EnvironmentIssue issue)
                    {
                        IsReady = isReady;
                        DotnetPath = dotnetPath;
                        DotnetVersion = dotnetVersion;
                        ErrorMessage = errorMessage;
                        Issue = issue;
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
                    bool cached = SessionState.GetBool(c_SessionKeyReady + "_cached", false);
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
                    // 步骤 1：检测 dotnet 路径
                    string dotnetPath = Luban.CliRunner.ResolveDotnetPath();
                    if (dotnetPath == null)
                    {
                        return new EnvironmentCheckResult(false, null, null, $"未找到 dotnet 可执行文件，请安装 .NET SDK {c_MinDotnetVersion} ~ {c_MaxDotnetVersion}。", EnvironmentIssue.DotnetNotFound);
                    }

                    // 步骤 2：执行 dotnet --version 获取版本号
                    ProcessRunner.ProcessResult versionResult = ProcessRunner.RunSync(dotnetPath, "--version");
                    if (versionResult.TimedOut || !versionResult.Success)
                    {
                        string errMsg = versionResult.TimedOut
                            ? $"dotnet --version 执行超时。\n输出：\n{ProcessRunner.FormatOutput(versionResult)}"
                            : $"dotnet --version 执行失败（ExitCode={versionResult.ExitCode}）。\n输出：\n{ProcessRunner.FormatOutput(versionResult)}";
                        return new EnvironmentCheckResult(false, dotnetPath, null, errMsg, EnvironmentIssue.DotnetNotExecutable);
                    }

                    string versionStr = versionResult.Stdout.Trim();

                    // 步骤 3：解析版本并做区间校验
                    System.Version version = ParseVersion(versionStr);
                    if (version == null)
                    {
                        string errMsg = $"dotnet --version 输出无法解析为版本号（输出：{versionStr}）。";
                        return new EnvironmentCheckResult(false, dotnetPath, versionStr, errMsg, EnvironmentIssue.DotnetNotExecutable);
                    }

                    System.Version minVersion = System.Version.Parse(c_MinDotnetVersion);
                    System.Version maxVersion = System.Version.Parse(c_MaxDotnetVersion);
                    if (version < minVersion)
                    {
                        string errMsg = $"dotnet 版本过低（当前 {versionStr}，需要 {c_MinDotnetVersion} ~ {c_MaxDotnetVersion}）。";
                        return new EnvironmentCheckResult(false, dotnetPath, versionStr, errMsg, EnvironmentIssue.DotnetVersionTooLow);
                    }

                    if (version > maxVersion)
                    {
                        string errMsg = $"dotnet 版本过高（当前 {versionStr}，需要 {c_MinDotnetVersion} ~ {c_MaxDotnetVersion}）。";
                        return new EnvironmentCheckResult(false, dotnetPath, versionStr, errMsg, EnvironmentIssue.DotnetVersionTooHigh);
                    }

                    // 步骤 4：检测 Luban.dll
                    string dllPath = Luban.CliRunner.GetLubanDllPath();
                    if (dllPath == null || !SIO.File.Exists(dllPath))
                    {
                        return new EnvironmentCheckResult(false, dotnetPath, versionStr, "未找到 Luban.dll，请确认 com.solotopia.luban UPM 包已安装。", EnvironmentIssue.LubanDllNotFound);
                    }

                    return new EnvironmentCheckResult(true, dotnetPath, versionStr, null, EnvironmentIssue.None);
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
                    return new EnvironmentCheckResult(isReady, string.IsNullOrEmpty(dotnetPath) ? null : dotnetPath, string.IsNullOrEmpty(dotnetVersion) ? null : dotnetVersion, string.IsNullOrEmpty(errorMessage) ? null : errorMessage, issue);
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
