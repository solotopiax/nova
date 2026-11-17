/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.AgentSkills.CliBinding.cs
 * author:    taoye
 * created:   2026/11/16
 * descrip:   Unity CLI 只读连接响应的身份与失败分类
 ***************************************************************/

using System;
using System.IO;
using Newtonsoft.Json.Linq;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        public static partial class AgentSkills
        {
            /// <summary>解析 editor status --result-only 的响应，不从命令清单、缓存或单独的退出码推断连接成功。</summary>
            /// <param name="projectRoot">本轮冻结的当前项目绝对路径。</param>
            /// <param name="windows">仅 Windows 主机比较路径时忽略大小写。</param>
            /// <param name="exitCode">实际 CLI 退出码。</param>
            /// <param name="timedOut">外层进程等待是否超时。</param>
            /// <param name="outputTruncated">输出是否超限或未排空。</param>
            /// <param name="stdout">仅标准输出；错误流不能混入 JSON。</param>
            /// <returns>带失败原因的结果；协议及身份错误停止自动重试，仍须显式重检。</returns>
            private static ToolchainItem InspectHostUnityCliBinding(string projectRoot, bool windows, int exitCode,
                bool timedOut, bool outputTruncated, string stdout)
            {
                if (timedOut)
                    return CreateHostUnityCliBindingResult("partial", "Unity CLI 的只读 Editor 状态查询超时，尚未取得当前项目的连接证据。", true);
                if (outputTruncated)
                    return CreateHostUnityCliBindingResult("blocked", "Unity CLI 输出被截断或未排空，不能把不完整响应当作连接成功。", false);
                if (string.IsNullOrWhiteSpace(stdout))
                    return DescribeHostUnityCliBindingFailure(exitCode, string.Empty);

                JToken response;
                try
                {
                    response = JToken.Parse(stdout, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                }
                catch (Exception)
                {
                    return CreateHostUnityCliBindingResult("blocked", "Unity CLI 未返回可解析的 JSON 对象，当前响应不能用于连接验收。", false);
                }
                if (!(response is JObject document))
                    return CreateHostUnityCliBindingResult("blocked", "Unity CLI 响应结构不受支持：Editor 状态必须是单个 JSON 对象。", false);

                // --result-only 的成功结果直接包含 status/projectPath；CLI 自身失败仍使用 success/errors 信封。
                JToken success = document["success"];
                if (success != null && success.Type != JTokenType.Boolean)
                    return CreateHostUnityCliBindingResult("blocked", "Unity CLI 返回的 success 字段类型不受支持，未确认连接。", false);
                string errorCode = ReadHostUnityCliBindingErrorCode(document);
                bool hasError = document["error"] != null && document["error"].Type != JTokenType.Null
                    || document["errors"] != null && document["errors"].Type != JTokenType.Null
                        && (!(document["errors"] is JArray errors) || errors.Count > 0);
                if (exitCode != 0 || success != null && !(bool)success || hasError)
                    return DescribeHostUnityCliBindingFailure(exitCode, errorCode);

                if (document["status"]?.Type != JTokenType.String || document["projectPath"]?.Type != JTokenType.String
                    || document["compiling"]?.Type != JTokenType.Boolean
                    || document["domainReloadInProgress"] != null && document["domainReloadInProgress"].Type != JTokenType.Boolean)
                    return CreateHostUnityCliBindingResult("blocked", "Unity CLI 响应结构不受支持：缺少 Editor 状态、项目绝对路径或编译状态。命令清单及其成功信封不能代替这些字段。", false);

                string actualProject = (string)document["projectPath"];
                try
                {
                    if (string.IsNullOrWhiteSpace(actualProject) || !Path.IsPathRooted(actualProject)
                        || string.IsNullOrWhiteSpace(projectRoot) || !Path.IsPathRooted(projectRoot))
                        return CreateHostUnityCliBindingResult("blocked", "Unity CLI 未返回有效的项目绝对路径，不能确认目标工程身份。", false);
                    if (!string.Equals(Path.GetFullPath(actualProject).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                            Path.GetFullPath(projectRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                            windows ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
                        return CreateHostUnityCliBindingResult("blocked", "Unity CLI 回应的项目与当前工程不一致，已拒绝将其他工程的连接计为成功。", false);
                }
                catch (Exception)
                {
                    return CreateHostUnityCliBindingResult("blocked", "Unity CLI 返回的项目路径无法规范化，未确认目标工程身份。", false);
                }

                string state = (string)document["status"];
                if ((bool)document["compiling"] || (bool?)document["domainReloadInProgress"] == true)
                    return CreateHostUnityCliBindingResult("partial", "Unity CLI 已回应当前工程，但 Editor 正在编译或重载，等待稳定后重新核验。", true);
                switch (state)
                {
                    case "ready":
                        return CreateHostUnityCliBindingResult("success", "已通过只读 Editor 状态响应确认 Unity CLI 连接当前工程，项目身份一致且 Editor 已就绪。", true);
                    case "blocked_by_dialog":
                        return CreateHostUnityCliBindingResult("partial", "Unity CLI 已识别当前工程，但 Editor 正在等待窗口处理；处理真实窗口后再核验连接。", true);
                    case "compiling":
                    case "reloading":
                    case "busy":
                    case "settling":
                    case "playing":
                    case "paused":
                        return CreateHostUnityCliBindingResult("partial", "Unity CLI 已回应当前工程，但 Editor 尚未处于可继续安装的空闲状态，稍后重新核验。", true);
                    default:
                        return CreateHostUnityCliBindingResult("blocked", "Unity CLI 返回了当前检查器不支持的 Editor 状态，未确认连接就绪。", false);
                }
            }

            /// <summary>只读取固定错误码，不将外部命令原文或可能包含用户信息的 message 写入报告。</summary>
            /// <param name="document">CLI 实际返回的 JSON 对象。</param>
            /// <returns>首个错误码；缺失或类型不符时为空。</returns>
            private static string ReadHostUnityCliBindingErrorCode(JObject document)
            {
                JToken code = document["errors"] is JArray errors && errors.Count > 0 && errors[0] is JObject first
                    ? first["code"] : (document["error"] as JObject)?["code"];
                return code?.Type == JTokenType.String ? (string)code : string.Empty;
            }

            /// <summary>区分未连接、超时、忙碌、错误目标及不可自动修复的命令失败，不把所有错误描述为等待连接。</summary>
            /// <param name="exitCode">真实进程退出码。</param>
            /// <param name="errorCode">已解析的 CLI 错误码，不直接输出原文。</param>
            /// <returns>有界等待或停止无效自动重试的失败结果。</returns>
            private static ToolchainItem DescribeHostUnityCliBindingFailure(int exitCode, string errorCode)
            {
                switch (errorCode)
                {
                    case "EDITOR_NOT_RUNNING":
                    case "EDITOR_UNREACHABLE":
                        return CreateHostUnityCliBindingResult("partial", "Unity CLI 尚未连接到当前工程的 Editor，未取得项目状态回应。", true);
                    case "TIMEOUT":
                    case "READY_TIMEOUT":
                        return CreateHostUnityCliBindingResult("partial", "Unity CLI 的 Editor 状态查询超时，尚未确认当前工程连接。", true);
                    case "AWAITING_INPUT":
                    case "BLOCKED_BY_DIALOG":
                        return CreateHostUnityCliBindingResult("partial", "Unity CLI 正在等待 Editor 窗口处理；需先检查真实窗口，再重新核验。", true);
                    case "EDITOR_NOT_READY":
                    case "EDITOR_BUSY":
                    case "UNAVAILABLE":
                    case "NETWORK_UNREACHABLE":
                        return CreateHostUnityCliBindingResult("partial", "Unity CLI 暂时无法读取当前 Editor 状态，等待连接或 Editor 稳定后重新核验。", true);
                    case "AMBIGUOUS_EDITOR":
                        return CreateHostUnityCliBindingResult("blocked", "Unity CLI 未能唯一定位当前工程，已停止自动重试，不能改用其他工程。", false);
                }
                // 某些命令错误未输出完整信封，只接受 CLI 明确用于可恢复状态的退出码。
                if (string.IsNullOrEmpty(errorCode))
                {
                    if (exitCode == 12)
                        return CreateHostUnityCliBindingResult("partial", "Unity CLI 尚未连接当前工程的 Editor，未取得项目状态回应。", true);
                    if (exitCode == 10 || exitCode == 14)
                        return CreateHostUnityCliBindingResult("partial", "Unity CLI 的 Editor 状态查询超时，尚未确认当前工程连接。", true);
                    if (exitCode == 17 || exitCode == 18 || exitCode == 102)
                        return CreateHostUnityCliBindingResult("partial", "Unity CLI 报告 Editor 忙碌或等待窗口处理，稍后重新核验。", true);
                }
                return CreateHostUnityCliBindingResult("blocked", "Unity CLI 的 Editor 状态命令执行失败或未提供有效结果，未确认连接。", false);
            }

            /// <summary>统一生成连接结果及恢复说明，永久协议错误不会触发没有变化的自动重试。</summary>
            /// <param name="status">success、partial 或 blocked。</param>
            /// <param name="detail">已取得的证据和失败分类。</param>
            /// <param name="canAutomaticallyRetry">只允许可恢复的连接及 Editor 等待自动重试。</param>
            /// <returns>保持原有公开报告字段的连接检查项。</returns>
            private static ToolchainItem CreateHostUnityCliBindingResult(string status, string detail, bool canAutomaticallyRetry)
            {
                string remediation = status == "success" ? string.Empty : canAutomaticallyRetry
                    ? "自动重试会重新读取当前工程的真实状态；首次超时或等待窗口时先检查 Editor 窗口。"
                    : "自动重试已暂停；核对 CLI 版本、响应格式及目标工程后，从 Nova → Check and Setup AI Toolchain 显式重检。";
                return new ToolchainItem("unity-cli-binding", "Unity CLI 与当前项目连接", status, detail, remediation, canAutomaticallyRetry);
            }
        }
    }
}
