/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.AgentSkills.ToolchainValidation.cs
 * author:    taoye
 * created:   2026/10/26
 * descrip:   安装、升级和重检共用的 AI 工具链自动安装调度
 ***************************************************************/

using System;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using Log = NovaFramework.Runtime.Log;
using LogTag = NovaFramework.Runtime.LogTag;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        public static partial class AgentSkills
        {
            /// <summary>NovaSpark 与手动重试共用的固定入口，不依赖安装脚本引用 Framework 程序集。</summary>
            public const string ToolchainValidationMenuPath = "Nova/Check and Setup AI Toolchain";

            /// <summary>供独立 NovaSpark 查询的会话结果；pending/running/partial/blocked 均未完成。</summary>
            public const string ToolchainStateSessionKey = "Nova.AgentSkills.Toolchain.State.v1";

            /// <summary>仅立即排队或正在运行的安装阶段为 true；延时重试和人工等待不占用自动窗口门。</summary>
            internal const string ToolchainActiveSessionKey = "Nova.AgentSkills.Toolchain.Active.v1";
            /// <summary>当前安装调度阶段；独立引导脚本可只读该键，无需引用 Framework 程序集。</summary>
            internal const string ToolchainPhaseSessionKey = "Nova.AgentSkills.Toolchain.Phase.v1";
            /// <summary>当前阶段的中文等待原因；只用于观察，不授予继续执行或确认的权限。</summary>
            internal const string ToolchainWaitingReasonSessionKey = "Nova.AgentSkills.Toolchain.WaitingReason.v1";

            /// <summary>仅供当前会话去重，不能替代当前 Agent 的工具调用验收。</summary>
            private const string c_ToolchainCheckedIdentityKey = "Nova.AgentSkills.Toolchain.CheckedIdentity.v1";
            private const string c_ToolchainPendingKey = "Nova.AgentSkills.Toolchain.Pending.v1";
            private const string c_ToolchainLastMessageKey = "Nova.AgentSkills.Toolchain.LastMessage.v1";
            // 会话内跨重载保留不可恢复错误；值同时冻结目标工程与 Framework 来源/版本。
            private const string c_ToolchainRetryPausedIdentityKey = "Nova.AgentSkills.Toolchain.RetryPausedIdentity.v1";
            private static bool s_ToolchainValidationQueued;
            private static bool s_ForceToolchainValidation;
            private static bool s_ToolchainStopping;
            private static double s_ToolchainNextAttempt;
            private static int s_ToolchainRetryCount;
            private static CancellationTokenSource s_ToolchainCancellation;

            /// <summary>自动安装、绑定或后验检查仍在进行。</summary>
            public static bool IsToolchainSetupRunning { get; private set; }

            /// <summary>当前域最近一次实际结果；域重载后自动重新取得证据。</summary>
            public static ToolchainReport LastToolchainReport { get; private set; }

            /// <summary>自动窗口只等待有限的当前安装阶段，不等待整个 partial 重试队列。</summary>
            internal static bool IsToolchainInstallationActive => SessionState.GetBool(ToolchainActiveSessionKey, false);

            /// <summary>安排自动补齐、绑定和复核，立即显示等待状态，并确保本次结果不被相同日志去重。</summary>
            // 使用末尾独立分组，Unity 按优先级间隔自动在上方显示分隔线。
            [MenuItem(ToolchainValidationMenuPath, false, 20000)]
            public static void SetupAndCheckToolchain()
            {
                EditorApplication.ExecuteMenuItem("Window/General/Console");
                if (Application.isBatchMode || s_ToolchainStopping)
                {
                    Log.Warning(LogTag.Editor, "[Nova AI 工具链] 当前 Editor 正在重载、退出或以无界面模式运行，暂不能从菜单启动；未完成的安装会在下次交互式 Editor 就绪后接续。");
                    return;
                }

                // 自动重试仍按内容去重；明确点击菜单时，必须重新显示本次实际结果。
                SessionState.EraseString(c_ToolchainLastMessageKey);
                RequestToolchainSetup();
                string progress = IsToolchainSetupRunning
                    ? "当前检查正在进行，已追加一次完整重检请求；完成后会显示结果。"
                    : EditorApplication.isCompiling || EditorApplication.isUpdating
                        ? "已收到安装和检查请求，等待 Unity 编译、导入结束后自动开始。"
                        : EditorApplication.isPlayingOrWillChangePlaymode
                            ? "已收到安装和检查请求，等待退出运行模式后自动开始。"
                            : "已收到安装和检查请求，正在准备自动补齐、绑定和完整性验证；完成后会显示结果。";
                Log.Debug(LogTag.Editor, "[Nova AI 工具链] " + progress);
            }

            /// <summary>
            /// 编译与包解析稳定后自动安装缺失工具、同步 Skills 并绑定本项目。
            /// 未完成请求跨域重载接力；可恢复失败自动重试，响应协议或项目身份错误等待显式重检。
            /// </summary>
            /// <param name="force">为 true 时绕过当前会话相同 Framework 身份的成功记录。</param>
            public static void RequestToolchainSetup(bool force = true)
            {
                if (Application.isBatchMode || s_ToolchainStopping) return;
                s_ToolchainValidationQueued = true;
                s_ForceToolchainValidation |= force;
                if (force)
                {
                    s_ToolchainNextAttempt = 0;
                    s_ToolchainRetryCount = 0;
                    SessionState.SetBool(c_ToolchainPendingKey, true);
                    SessionState.SetString(ToolchainStateSessionKey, "pending");
                }
                AssemblyReloadEvents.beforeAssemblyReload -= CancelToolchainSetup;
                AssemblyReloadEvents.beforeAssemblyReload += CancelToolchainSetup;
                EditorApplication.quitting -= CancelToolchainSetup;
                EditorApplication.quitting += CancelToolchainSetup;
                EditorApplication.update -= TryStartToolchainValidation;
                EditorApplication.update += TryStartToolchainValidation;
                RefreshToolchainSchedulingPhase();
            }

            /// <summary>只在 Editor 空闲且重试间隔到达后启动，避免并发安装及包解析竞争。</summary>
            private static void TryStartToolchainValidation()
            {
                RefreshToolchainSchedulingPhase();
                if (!s_ToolchainValidationQueued)
                {
                    EditorApplication.update -= TryStartToolchainValidation;
                    return;
                }
                if (s_ToolchainStopping || IsToolchainSetupRunning || EditorApplication.isCompiling
                    || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode
                    || ImportantUpdates.IsPresentationShowing || EditorApplication.timeSinceStartup < s_ToolchainNextAttempt) return;

                string identity;
                try { identity = ResolveToolchainValidationIdentity(); }
                catch (Exception)
                {
                    SessionState.SetBool(c_ToolchainPendingKey, true);
                    SessionState.SetString(ToolchainStateSessionKey, "partial");
                    LogToolchainMessage("[Nova AI 工具链] 正在等待 Framework 包身份稳定，稍后自动重试。", false);
                    QueueToolchainRetry();
                    return;
                }
                bool explicitlyRequested = s_ForceToolchainValidation;
                bool force = explicitlyRequested || SessionState.GetBool(c_ToolchainPendingKey, false);
                s_ToolchainValidationQueued = false;
                s_ForceToolchainValidation = false;
                EditorApplication.update -= TryStartToolchainValidation;
                string retryIdentity = BuildToolchainRetryPauseIdentity(ResolveProjectRoot(), identity);
                if (!explicitlyRequested && SessionState.GetString(c_ToolchainRetryPausedIdentityKey, string.Empty) == retryIdentity)
                {
                    SetToolchainSchedulingPhase("blocked", "自动重试已暂停，等待处理报告中的原因后显式重检。", false);
                    return;
                }
                if (!force && SessionState.GetString(c_ToolchainCheckedIdentityKey, string.Empty) == identity)
                {
                    SetToolchainSchedulingPhase("complete", "本次会话已通过当前 Framework 身份的检查。", false);
                    return;
                }

                SessionState.EraseString(c_ToolchainRetryPausedIdentityKey);
                SessionState.SetBool(c_ToolchainPendingKey, true);
                SessionState.SetString(ToolchainStateSessionKey, "running");
                IsToolchainSetupRunning = true;
                SetToolchainSchedulingPhase("running", "正在安装、绑定并核验当前项目的 AI 工具链。", true);
                s_ToolchainCancellation = new CancellationTokenSource();
                RunToolchainValidationAsync(identity, s_ToolchainCancellation.Token).Forget();
            }

            /// <summary>只有本轮实际补齐与绑定通过才标记成功；升级期间的旧结果会被丢弃。</summary>
            private static async UniTaskVoid RunToolchainValidationAsync(string identity, CancellationToken cancellationToken)
            {
                try
                {
                    ToolchainReport report = await EnsureToolchainAsync(cancellationToken);
                    await UniTask.SwitchToMainThread(cancellationToken);
                    if (EditorApplication.isCompiling || EditorApplication.isUpdating
                        || !string.Equals(identity, ResolveToolchainValidationIdentity(), StringComparison.Ordinal))
                    {
                        RequestToolchainSetup();
                        return;
                    }
                    LastToolchainReport = report;
                    bool complete = report.Status == "success";
                    if (!complete && !report.CanAutomaticallyRetry)
                    {
                        SessionState.SetString(c_ToolchainRetryPausedIdentityKey, BuildToolchainRetryPauseIdentity(ResolveProjectRoot(), identity));
                        // 运行中的显式重检请求仍有效；普通重载和自动请求不能循环执行同一协议错误。
                        if (!s_ForceToolchainValidation)
                        {
                            s_ToolchainValidationQueued = false;
                            EditorApplication.update -= TryStartToolchainValidation;
                        }
                    }
                    SessionState.SetString(ToolchainStateSessionKey,
                        s_ToolchainValidationQueued ? "pending" : report.Status);
                    if (complete)
                    {
                        SessionState.SetString(c_ToolchainCheckedIdentityKey, identity);
                        if (!s_ToolchainValidationQueued) SessionState.EraseBool(c_ToolchainPendingKey);
                        s_ToolchainRetryCount = 0;
                        SetToolchainSchedulingPhase("complete", "自动安装、绑定与本地复核已完成。", false);
                    }
                    else if (report.CanAutomaticallyRetry) QueueToolchainRetry();
                    else SetToolchainSchedulingPhase("blocked", "自动重试已暂停；未完成状态保留，等待处理报告中的原因后显式重检。", false);
                    LogToolchainMessage("[Nova AI 工具链] " + report.FormatMessage()
                        + (complete ? "\n自动安装、绑定与本地复核已完成。"
                            : report.CanAutomaticallyRetry
                                ? "\n缺失项继续自动安装与绑定，未完成状态将自动重试；不会把补装工作交给项目组。"
                                : "\n自动重试已暂停，未完成状态与引导接力均保留；处理所报原因后可从 Nova 菜单显式重检。"), complete);
                }
                catch (OperationCanceledException)
                {
                    // 重载/退出中断安装时保留接力状态，下一域会自动恢复。
                    SessionState.SetString(ToolchainStateSessionKey, "pending");
                    SetToolchainSchedulingPhase("waiting-editor", "本轮被重载或退出中断，保留接力状态。", false);
                }
                catch (Exception)
                {
                    SessionState.SetString(ToolchainStateSessionKey, "partial");
                    QueueToolchainRetry();
                    LogToolchainMessage("[Nova AI 工具链] 自动安装或绑定尚未完成，将自动重试；未记录为通过。", false);
                }
                finally
                {
                    IsToolchainSetupRunning = false;
                    s_ToolchainCancellation?.Dispose();
                    s_ToolchainCancellation = null;
                    if (s_ToolchainValidationQueued) RefreshToolchainSchedulingPhase();
                }
            }

            /// <summary>自动重试仅补齐尚缺项，已安装的工具不反复升级。</summary>
            private static void QueueToolchainRetry()
            {
                if (s_ToolchainStopping) return;
                int delay = s_ToolchainRetryCount == 0 ? 30 : s_ToolchainRetryCount == 1 ? 120 : 300;
                s_ToolchainRetryCount++;
                s_ToolchainNextAttempt = EditorApplication.timeSinceStartup + delay;
                s_ToolchainValidationQueued = true;
                SessionState.SetBool(c_ToolchainPendingKey, true);
                EditorApplication.update -= TryStartToolchainValidation;
                EditorApplication.update += TryStartToolchainValidation;
                SetToolchainSchedulingPhase("waiting-retry", $"本轮尚未完成，等待 {delay} 秒后重试；当前没有正在运行的安装。", false);
            }

            /// <summary>同样的失败只显示一次，持续重试不刷屏，不输出外部命令或账户原文。</summary>
            private static void LogToolchainMessage(string message, bool success)
            {
                if (SessionState.GetString(c_ToolchainLastMessageKey, string.Empty) == message) return;
                SessionState.SetString(c_ToolchainLastMessageKey, message);
                if (success) Log.Debug(LogTag.Editor, message); else Log.Warning(LogTag.Editor, message);
            }

            /// <summary>退出或域重载前终止尚未结束的安装进程，保留待处理标记。</summary>
            private static void CancelToolchainSetup()
            {
                s_ToolchainStopping = true;
                s_ToolchainCancellation?.Cancel();
                EditorApplication.update -= TryStartToolchainValidation;
                SetToolchainSchedulingPhase("waiting-editor", "等待脚本重载或下次交互式 Editor 接续安装。", false);
            }

            /// <summary>按真实运行、排队和退避状态更新观察键，重要声明人工等待与安装活跃阶段分别表达。</summary>
            private static void RefreshToolchainSchedulingPhase()
            {
                if (s_ToolchainStopping)
                    SetToolchainSchedulingPhase("waiting-editor", "等待脚本重载或下次交互式 Editor 接续安装。", false);
                else if (IsToolchainSetupRunning)
                    SetToolchainSchedulingPhase("running", "正在安装、绑定并核验当前项目的 AI 工具链。", true);
                else if (!s_ToolchainValidationQueued)
                    SessionState.SetBool(ToolchainActiveSessionKey, false);
                else if (ImportantUpdates.IsPresentationShowing)
                    SetToolchainSchedulingPhase("waiting-important-updates", "等待当前重要升级说明的人工确认，尚未开始新一轮安装。", false);
                else if (EditorApplication.timeSinceStartup < s_ToolchainNextAttempt)
                    SetToolchainSchedulingPhase("waiting-retry", "等待自动重试间隔；当前没有正在运行的安装。", false);
                else if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
                    SetToolchainSchedulingPhase("waiting-editor", "安装请求已排队，等待 Unity 编译、导入或运行模式切换结束。", true);
                else
                    SetToolchainSchedulingPhase("queued", "本轮安装请求已排队，等待 Editor 调度。", true);
            }

            /// <summary>只写会话内观察状态，不改最终验收状态、成功身份或恢复标记。</summary>
            /// <param name="phase">queued、running、waiting-editor、waiting-retry、waiting-important-updates、blocked 或 complete。</param>
            /// <param name="reason">本阶段的中文说明。</param>
            /// <param name="active">当前是否有立即排队或正在运行的安装。</param>
            private static void SetToolchainSchedulingPhase(string phase, string reason, bool active)
            {
                SessionState.SetBool(ToolchainActiveSessionKey, active);
                SessionState.SetString(ToolchainPhaseSessionKey, phase);
                SessionState.SetString(ToolchainWaitingReasonSessionKey, reason);
            }

            /// <summary>把暂停范围同时绑定当前工程和 Framework 身份，避免共享包来源影响其他工程。</summary>
            /// <param name="projectRoot">当前实际工程绝对路径。</param>
            /// <param name="frameworkIdentity">已经解析的 Framework 来源及版本。</param>
            /// <returns>仅用于会话内自动重试控制的复合身份，不代表安装通过。</returns>
            private static string BuildToolchainRetryPauseIdentity(string projectRoot, string frameworkIdentity)
            {
                return Path.GetFullPath(projectRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + "|" + frameworkIdentity;
            }

            /// <summary>按真实来源与版本识别手动升级，不依赖安装时的旧 manifest 或缓存目录。</summary>
            private static string ResolveToolchainValidationIdentity()
            {
                string agentsRoot = ResolveAgentsRoot();
                string packageJson = Path.Combine(Path.GetDirectoryName(agentsRoot) ?? string.Empty, "package.json");
                JObject package = JObject.Parse(File.ReadAllText(packageJson));
                string version = (string)package["version"];
                if ((string)package["name"] != c_FrameworkPackageName || string.IsNullOrWhiteSpace(version))
                    throw new InvalidOperationException("Framework 包身份不完整。");
                return agentsRoot + "|" + version;
            }
        }
    }
}
