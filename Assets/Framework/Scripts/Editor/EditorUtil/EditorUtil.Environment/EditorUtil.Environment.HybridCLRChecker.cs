/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.Environment.HybridCLRChecker.cs
 * author:    taoye
 * created:   2026/5/9
 * descrip:   HybridCLR 安装状态检查器（包是否安装 + Installer 是否已跑过）
 ***************************************************************/

using System.IO;
using UnityEditor;
using UnityEngine;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        public static partial class Environment
        {
            /// <summary>
            /// HybridCLR 安装状态检查器。
            /// 双条件：①包是否安装（package.json 存在）；②Installer 是否已跑过（libil2cpp/hybridclr 目录存在）。
            /// 结果缓存到 SessionState，同一编辑器会话不重复检测。
            /// </summary>
            public static class HybridCLRChecker
            {
                /// <summary>
                /// SessionState 缓存键：是否已执行过检测。
                /// </summary>
                private const string c_SessionKeyCached = "Nova.HybridCLR.EnvCheckCached";

                /// <summary>
                /// SessionState 缓存键：是否就绪。
                /// </summary>
                private const string c_SessionKeyReady = "Nova.HybridCLR.EnvCheckReady";

                /// <summary>
                /// SessionState 缓存键：问题类型（int）。
                /// </summary>
                private const string c_SessionKeyIssue = "Nova.HybridCLR.EnvCheckIssue";

                /// <summary>
                /// SessionState 缓存键：包版本号（从 package.json 解析）。
                /// </summary>
                private const string c_SessionKeyVersion = "Nova.HybridCLR.EnvCheckVersion";

                /// <summary>
                /// SessionState 缓存键：可读错误描述。
                /// </summary>
                private const string c_SessionKeyError = "Nova.HybridCLR.EnvCheckError";

                /// <summary>
                /// HybridCLR 环境问题类型。
                /// </summary>
                public enum HybridCLRIssue
                {
                    /// <summary>
                    /// 无问题，环境就绪。
                    /// </summary>
                    None,

                    /// <summary>
                    /// HybridCLR 包（com.solotopia.hybridclr）未安装。
                    /// </summary>
                    PackageNotFound,

                    /// <summary>
                    /// 包已安装，但尚未运行 Installer（libil2cpp 热补丁目录不存在）。
                    /// </summary>
                    InstallerNotRun,
                }

                /// <summary>
                /// 用于反序列化 package.json 版本号字段。
                /// </summary>
                [System.Serializable]
                private class PackageJsonDto
                {
                    /// <summary>
                    /// package.json 中的 version 字段。
                    /// </summary>
                    public string version;
                }

                /// <summary>
                /// HybridCLR 环境检查结果（只读值类型）。
                /// </summary>
                public readonly struct HybridCLRCheckResult
                {
                    /// <summary>
                    /// 环境是否就绪（包已安装且 Installer 已跑过）。
                    /// </summary>
                    public readonly bool IsReady;

                    /// <summary>
                    /// 从 package.json 解析到的包版本号；解析失败或包未安装时为 null。
                    /// </summary>
                    public readonly string PackageVersion;

                    /// <summary>
                    /// 环境问题类型；就绪时为 None。
                    /// </summary>
                    public readonly HybridCLRIssue Issue;

                    /// <summary>
                    /// 可读错误描述；就绪时为 null。
                    /// </summary>
                    public readonly string ErrorMessage;

                    /// <summary>
                    /// 构造 HybridCLR 环境检查结果。
                    /// </summary>
                    /// <param name="isReady">是否就绪。</param>
                    /// <param name="packageVersion">包版本号。</param>
                    /// <param name="issue">问题类型。</param>
                    /// <param name="errorMessage">可读错误描述。</param>
                    public HybridCLRCheckResult(bool isReady, string packageVersion, HybridCLRIssue issue, string errorMessage)
                    {
                        IsReady = isReady;
                        PackageVersion = packageVersion;
                        Issue = issue;
                        ErrorMessage = errorMessage;
                    }
                }

                /// <summary>
                /// 检查 HybridCLR 环境，结果缓存到 SessionState；同一会话不重复检测。
                /// </summary>
                /// <returns>HybridCLR 环境检查结果。</returns>
                public static HybridCLRCheckResult Check()
                {
                    bool cached = SessionState.GetBool(c_SessionKeyCached, false);
                    if (cached)
                    {
                        return ReadFromSession();
                    }

                    return RunCheck();
                }

                /// <summary>
                /// 强制重新检测（忽略 SessionState 缓存）。
                /// </summary>
                /// <returns>最新的 HybridCLR 环境检查结果。</returns>
                public static HybridCLRCheckResult Recheck()
                {
                    SessionState.SetBool(c_SessionKeyCached, false);
                    return RunCheck();
                }

                /// <summary>
                /// 执行检测并写入 SessionState 缓存。
                /// </summary>
                /// <returns>检查结果。</returns>
                private static HybridCLRCheckResult RunCheck()
                {
                    HybridCLRCheckResult result = DoCheck();
                    WriteToSession(result);
                    return result;
                }

                /// <summary>
                /// 核心检测逻辑：先验证包是否安装，再验证 Installer 是否已跑过。
                /// </summary>
                /// <returns>检查结果。</returns>
                private static HybridCLRCheckResult DoCheck()
                {
                    // 步骤 1：检查 package.json 是否存在
                    string packageJsonPath = Path.Combine(Application.dataPath, "..", "Packages", "com.solotopia.hybridclr", "package.json");
                    packageJsonPath = Path.GetFullPath(packageJsonPath);

                    if (!File.Exists(packageJsonPath))
                    {
                        return new HybridCLRCheckResult(false, null, HybridCLRIssue.PackageNotFound, "HybridCLR 包未安装。");
                    }

                    // 步骤 2：解析版本号（失败不阻断 Ready 判定）
                    string packageVersion = TryParsePackageVersion(packageJsonPath);

                    // 步骤 3：检查 Installer 是否已跑过（libil2cpp/hybridclr 热补丁目录存在）
                    string hybridclrDataDir = Path.Combine(Application.dataPath, "..", "HybridCLRData", $"LocalIl2CppData-{Application.platform}", "il2cpp", "libil2cpp", "hybridclr");
                    hybridclrDataDir = Path.GetFullPath(hybridclrDataDir);

                    if (!Directory.Exists(hybridclrDataDir))
                    {
                        return new HybridCLRCheckResult(false, packageVersion, HybridCLRIssue.InstallerNotRun, "HybridCLR Installer 尚未运行。");
                    }

                    return new HybridCLRCheckResult(true, packageVersion, HybridCLRIssue.None, null);
                }

                /// <summary>
                /// 尝试从 package.json 解析 version 字段；失败时返回 null，不抛异常。
                /// </summary>
                /// <param name="packageJsonPath">package.json 的完整路径。</param>
                /// <returns>版本号字符串；解析失败时为 null。</returns>
                private static string TryParsePackageVersion(string packageJsonPath)
                {
                    try
                    {
                        string json = File.ReadAllText(packageJsonPath);
                        PackageJsonDto dto = JsonUtility.FromJson<PackageJsonDto>(json);
                        return string.IsNullOrEmpty(dto?.version) ? null : dto.version;
                    }
                    catch
                    {
                        return null;
                    }
                }

                /// <summary>
                /// 将检查结果写入 SessionState 缓存（5 个 key）。
                /// </summary>
                /// <param name="result">检查结果。</param>
                private static void WriteToSession(HybridCLRCheckResult result)
                {
                    SessionState.SetBool(c_SessionKeyReady, result.IsReady);
                    SessionState.SetInt(c_SessionKeyIssue, (int)result.Issue);
                    SessionState.SetString(c_SessionKeyVersion, result.PackageVersion ?? "");
                    SessionState.SetString(c_SessionKeyError, result.ErrorMessage ?? "");
                    SessionState.SetBool(c_SessionKeyCached, true);
                }

                /// <summary>
                /// 从 SessionState 缓存读取检查结果。
                /// </summary>
                /// <returns>缓存的检查结果。</returns>
                private static HybridCLRCheckResult ReadFromSession()
                {
                    bool isReady = SessionState.GetBool(c_SessionKeyReady, false);
                    HybridCLRIssue issue = (HybridCLRIssue)SessionState.GetInt(c_SessionKeyIssue, 0);
                    string version = SessionState.GetString(c_SessionKeyVersion, "");
                    string error = SessionState.GetString(c_SessionKeyError, "");
                    return new HybridCLRCheckResult(
                        isReady,
                        string.IsNullOrEmpty(version) ? null : version,
                        issue,
                        string.IsNullOrEmpty(error) ? null : error
                    );
                }
            }
        }
    }
}
