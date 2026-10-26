/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.AgentSkills.ToolchainProvisioning.cs
 * author:    taoye
 * created:   2026/10/26
 * descrip:   自动补齐主机工具与 Unity 官方项目连接包
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        public static partial class AgentSkills
        {
            private const int c_ProvisioningInstallTimeoutMs = 120000;
            private const int c_ProvisioningPluginQueryTimeoutMs = 45000;
            private const int c_ProvisioningOutputLimit = 4 * 1024 * 1024;
            private const string c_ProvisioningUnityPlugin = "unity@openai-curated-remote";
            private const string c_ProvisioningUnityMarketplacePlugin = "unity@unity-agent-plugin";

            /// <summary>冻结主线程数据，让下载、安装、进程等待和文件清理全部在线程池执行。</summary>
            private sealed class HostProvisioningContext
            {
                public string ProjectRoot;
                public string UserDirectory;
                public string CodexHome;
                public string ProcessPath;
                public string LocalApplicationData;
                public string ProgramFiles;
                public string UnityCliHome;
                public string CodexInstallDirectory;
                public string TemporaryDirectory;
                public bool Windows;
                public bool Mac;
                public bool PipelineRegistered;
            }

            /// <summary>记录有界进程结果；原始输出仅供本次解析，不写入 Console 或持久文件。</summary>
            private sealed class HostProvisioningProcessResult
            {
                public int ExitCode = -1;
                public bool TimedOut;
                public bool OutputTruncated;
                public string Stdout = string.Empty;
                public string Stderr = string.Empty;
                public bool Success => ExitCode == 0 && !TimedOut;
            }

            /// <summary>自动安装缺失的主机 CLI、安装并核对 Unity 官方插件，再补齐当前工程的 Pipeline 包声明。</summary>
            /// <remarks>复用可识别的既有 CLI，不升级 Unity Editor；包解析与重载后的注册核验由调用方接力。</remarks>
            /// <param name="cancellationToken">取消时终止本轮安装进程及其子进程，并清理下载脚本。</param>
            /// <returns>各项实际安装和核验结果；命令启动或缓存存在均不算成功。</returns>
            public static async UniTask<IReadOnlyList<ToolchainItem>> ProvisionHostToolsAsync(CancellationToken cancellationToken = default)
            {
                await UniTask.SwitchToMainThread(cancellationToken);
                string userDirectory = System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile);
                string codexHome = System.Environment.GetEnvironmentVariable("CODEX_HOME");
                string projectRoot = ResolveProjectRoot();
                var context = new HostProvisioningContext
                {
                    ProjectRoot = projectRoot,
                    UserDirectory = userDirectory,
                    CodexHome = string.IsNullOrWhiteSpace(codexHome) ? Path.Combine(userDirectory, ".codex") : codexHome,
                    ProcessPath = System.Environment.GetEnvironmentVariable("PATH") ?? string.Empty,
                    LocalApplicationData = System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData),
                    ProgramFiles = System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFiles),
                    UnityCliHome = System.Environment.GetEnvironmentVariable("UNITY_CLI_HOME"),
                    CodexInstallDirectory = System.Environment.GetEnvironmentVariable("CODEX_INSTALL_DIR"),
                    TemporaryDirectory = Path.Combine(projectRoot, "Library", "Nova", "AgentToolchain", "host-" + Guid.NewGuid().ToString("N")),
                    Windows = Application.platform == RuntimePlatform.WindowsEditor,
                    Mac = Application.platform == RuntimePlatform.OSXEditor,
                    PipelineRegistered = (PackageInfo.GetAllRegisteredPackages() ?? Array.Empty<PackageInfo>())
                        .Any(package => package != null && package.name == "com.unity.pipeline"
                            && (package.errors == null || package.errors.Length == 0)),
                };
                return await UniTask.RunOnThreadPool(() => ProvisionHostTools(context, cancellationToken), cancellationToken: cancellationToken);
            }

            /// <summary>在同一后台线程持有跨进程互斥锁，避免多个 Editor 或重载后的新调用重复安装主机工具。</summary>
            /// <param name="context">冻结的主机与工程信息。</param>
            /// <param name="cancellationToken">安装与锁等待的取消信号。</param>
            /// <returns>完成、待注册或自动处理失败的逐项结果。</returns>
            private static IReadOnlyList<ToolchainItem> ProvisionHostTools(HostProvisioningContext context, CancellationToken cancellationToken)
            {
                var items = new List<ToolchainItem>();
                bool ownsMutex = false;
                using var mutex = new Mutex(false, "Nova.AgentToolchain.HostProvisioning.v1");
                try
                {
                    var timer = Stopwatch.StartNew();
                    while (!ownsMutex && timer.ElapsedMilliseconds < c_ProvisioningInstallTimeoutMs)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        try { ownsMutex = mutex.WaitOne(100); }
                        catch (AbandonedMutexException) { ownsMutex = true; }
                    }
                    if (!ownsMutex)
                    {
                        items.Add(new ToolchainItem("unity-cli", "Unity CLI：从命令行操作 Unity", "partial", "其他 Unity 工程仍在自动安装主机工具，本轮等待已到时限。", "自动重试将在主机安装锁释放后继续。"));
                        items.Add(new ToolchainItem("unity-agent-plugin", "Unity 官方 Agent 插件：提供 Unity 开发任务说明", "partial", "等待其他工程的主机安装任务结束，本轮未重复写入插件配置。", "后续自动重试会重新核对安装和启用状态。"));
                        items.Add(new ToolchainItem("unity-pipeline", "Unity Pipeline：让 Unity CLI 连接当前 Editor", "partial", "主机工具安装尚未结束，当前工程接入将在后续自动重试继续。"));
                    }
                    else
                    {
                        string unity = EnsureHostUnityCli(context, items, cancellationToken);
                        EnsureHostUnityPlugin(context, items, cancellationToken);
                        EnsureHostUnityPipeline(context, unity, items, cancellationToken);
                    }
                }
                finally
                {
                    try
                    {
                        if (Directory.Exists(context.TemporaryDirectory)) Directory.Delete(context.TemporaryDirectory, true);
                    }
                    catch (Exception exception)
                    {
                        items.Add(new ToolchainItem("toolchain-cleanup", "安装临时文件清理", "blocked", "自动删除本轮安装脚本失败：" + SanitizeHostProvisioningDiagnostic(exception.Message, context), "后续自动重试将再次清理本轮临时目录。"));
                    }
                    if (ownsMutex) mutex.ReleaseMutex();
                }
                return Array.AsReadOnly(items.ToArray());
            }

            /// <summary>优先验证现有 Unity CLI，仅在无法取得可用命令时运行官方平台安装脚本。</summary>
            /// <param name="context">主机路径和平台。</param>
            /// <param name="items">接收真实版本或失败结果。</param>
            /// <param name="cancellationToken">取消安装和进程等待。</param>
            /// <returns>已经通过版本核验的绝对命令路径；失败时为空。</returns>
            private static string EnsureHostUnityCli(HostProvisioningContext context, List<ToolchainItem> items, CancellationToken cancellationToken)
            {
                const string name = "Unity CLI：从命令行操作 Unity";
                string executable = FindVerifiedHostCli(context, false, cancellationToken, out string version);
                if (executable != null)
                {
                    items.Add(new ToolchainItem("unity-cli", name, "success", $"已复用可正常执行 --version 的 Unity CLI {version}。"));
                    return executable;
                }
                try
                {
                    HostProvisioningProcessResult install = RunHostOfficialInstaller(context, false, cancellationToken);
                    executable = FindVerifiedHostCli(context, false, cancellationToken, out version);
                    if (executable != null)
                    {
                        items.Add(new ToolchainItem("unity-cli", name, "success", $"自动安装后已实际执行 --version，Unity CLI {version} 可用。"));
                        return executable;
                    }
                    items.Add(new ToolchainItem("unity-cli", name, "blocked", "已自动运行 Unity 官方安装程序，但尚未取得可正常返回版本的 Unity CLI。" + DescribeHostProvisioningFailure(install, context), "后续自动重试会重新执行缺项修复。"));
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception exception)
                {
                    items.Add(new ToolchainItem("unity-cli", name, "blocked", "Unity CLI 自动安装失败：" + SanitizeHostProvisioningDiagnostic(exception.Message, context), "后续自动重试会再次从 Unity 官方安装入口补齐。"));
                }
                return null;
            }

            /// <summary>通过官方 Codex 命令自动安装 Unity 插件，并以实际 installed、enabled 和官方来源核验结果。</summary>
            /// <param name="context">宿主路径和当前项目。</param>
            /// <param name="items">接收插件安装结果。</param>
            /// <param name="cancellationToken">取消下载、安装和状态查询。</param>
            private static void EnsureHostUnityPlugin(HostProvisioningContext context, List<ToolchainItem> items, CancellationToken cancellationToken)
            {
                const string name = "Unity 官方 Agent 插件：提供 Unity 开发任务说明";
                try
                {
                    string codex = FindVerifiedHostCli(context, true, cancellationToken, out _);
                    if (codex == null)
                    {
                        HostProvisioningProcessResult installCodex = RunHostOfficialInstaller(context, true, cancellationToken);
                        codex = FindVerifiedHostCli(context, true, cancellationToken, out _);
                        if (codex == null)
                        {
                            items.Add(new ToolchainItem("unity-agent-plugin", name, "blocked", "已自动尝试 OpenAI 官方独立 CLI 安装程序，但未能取得可用的 Codex CLI，插件自动安装未完成。" + DescribeHostProvisioningFailure(installCodex, context), "后续自动重试会重新检查宿主命令并继续安装。"));
                            return;
                        }
                    }
                    HostProvisioningProcessResult list = RunHostCodex(context, codex, new[] { "plugin", "list", "--json" }, c_ProvisioningPluginQueryTimeoutMs, cancellationToken);
                    if (TryConfirmHostUnityPlugin(list, context, out string pluginId))
                    {
                        items.Add(new ToolchainItem("unity-agent-plugin", name, "success", $"当前工程的 Codex 插件状态已确认 {pluginId} 已安装并启用。"));
                        return;
                    }
                    HostProvisioningProcessResult help = RunHostCodex(context, codex, new[] { "plugin", "add", "--help" }, c_ToolchainProbeTimeoutMs, cancellationToken);
                    if (!help.Success || !help.Stdout.Contains("--json"))
                    {
                        // 已有旧宿主不能安装插件时，自动补齐官方独立 CLI，不能反复等待同一缺失能力。
                        RunHostOfficialInstaller(context, true, cancellationToken);
                        codex = FindVerifiedHostCli(context, true, cancellationToken, out _, preferStandalone: true);
                        help = codex == null ? help : RunHostCodex(context, codex, new[] { "plugin", "add", "--help" }, c_ToolchainProbeTimeoutMs, cancellationToken);
                        if (codex == null || !help.Success || !help.Stdout.Contains("--json"))
                        {
                            items.Add(new ToolchainItem("unity-agent-plugin", name, "blocked", "已自动尝试补齐 Codex 独立 CLI，但仍未确认 plugin add --json 安装能力。" + DescribeHostProvisioningFailure(help, context), "后续自动重试会重新核对宿主能力。"));
                            return;
                        }
                    }
                    HostProvisioningProcessResult add = RunHostCodex(context, codex, new[] { "plugin", "add", c_ProvisioningUnityPlugin, "--json" }, c_ProvisioningInstallTimeoutMs, cancellationToken);
                    list = RunHostCodex(context, codex, new[] { "plugin", "list", "--json" }, c_ProvisioningPluginQueryTimeoutMs, cancellationToken);
                    if (TryConfirmHostUnityPlugin(list, context, out pluginId))
                    {
                        items.Add(new ToolchainItem("unity-agent-plugin", name, "success", $"自动安装后已核对 {pluginId} 的 installed 和 enabled 均为 true。"));
                        return;
                    }
                    // 官方远程目录不可用时使用 Unity 自己的市场；两个来源均只通过 Codex 插件管理器安装。
                    HostProvisioningProcessResult marketplace = RunHostCodex(context, codex, new[] { "plugin", "marketplace", "add", "Unity-Technologies/unity-agent-plugin", "--json" }, c_ProvisioningInstallTimeoutMs, cancellationToken);
                    HostProvisioningProcessResult fallback = marketplace.Success
                        ? RunHostCodex(context, codex, new[] { "plugin", "add", c_ProvisioningUnityMarketplacePlugin, "--json" }, c_ProvisioningInstallTimeoutMs, cancellationToken)
                        : marketplace;
                    list = RunHostCodex(context, codex, new[] { "plugin", "list", "--json" }, c_ProvisioningPluginQueryTimeoutMs, cancellationToken);
                    if (TryConfirmHostUnityPlugin(list, context, out pluginId))
                    {
                        items.Add(new ToolchainItem("unity-agent-plugin", name, "success", $"通过 Unity 官方市场自动安装后，已确认 {pluginId} 已安装并启用。"));
                        return;
                    }
                    HostProvisioningProcessResult failure = !fallback.Success ? fallback : !add.Success ? add : list;
                    items.Add(new ToolchainItem("unity-agent-plugin", name, list.Success ? "blocked" : "partial", "已自动尝试 OpenAI 目录和 Unity 官方市场，但尚未确认 Unity 官方插件同时处于已安装、已启用状态。" + DescribeHostProvisioningFailure(failure, context), "后续自动重试会重新核对宿主策略并继续修复；不会把插件缓存当作启用证据。"));
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception exception)
                {
                    items.Add(new ToolchainItem("unity-agent-plugin", name, "blocked", "Unity 官方插件自动安装失败：" + SanitizeHostProvisioningDiagnostic(exception.Message, context), "后续自动重试会从官方入口继续修复。"));
                }
            }

            /// <summary>通过已核验的 Unity CLI 安装当前工程缺失的 Pipeline，不替换 Coplay 或其他依赖。</summary>
            /// <param name="context">包含实际注册状态的工程快照。</param>
            /// <param name="unity">通过版本核验的 CLI；缺失时不执行。</param>
            /// <param name="items">接收安装声明和待重载结果。</param>
            /// <param name="cancellationToken">取消安装进程。</param>
            private static void EnsureHostUnityPipeline(HostProvisioningContext context, string unity, List<ToolchainItem> items, CancellationToken cancellationToken)
            {
                const string name = "Unity Pipeline：让 Unity CLI 连接当前 Editor";
                if (context.PipelineRegistered)
                {
                    items.Add(new ToolchainItem("unity-pipeline", name, "success", "当前 Editor 已实际注册 com.unity.pipeline，未重复安装。"));
                    if (!string.IsNullOrEmpty(unity))
                    {
                        HostProvisioningProcessResult connection = RunHostProvisioningProcess(context, unity,
                            new[] { "command", "--project-path", context.ProjectRoot, "--limit", "1", "--timeout", "10", "--non-interactive", "--format", "json" },
                            15000, cancellationToken);
                        bool connected = false;
                        try
                        {
                            JToken response = JToken.Parse(connection.Stdout);
                            connected = connection.Success && !connection.OutputTruncated && response is JObject document
                                && (bool?)document["success"] == true && document["error"] == null
                                && string.Equals(Path.GetFullPath((string)document["data"]?["target"]?["projectPath"] ?? "")
                                        .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                                    Path.GetFullPath(context.ProjectRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                                    context.Windows ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
                        }
                        catch (Exception) { }
                        items.Add(new ToolchainItem("unity-cli-binding", "Unity CLI 与当前项目连接", connected ? "success" : "partial",
                            connected ? "已限定当前工程路径，通过只读命令清单实际连接 Editor。"
                                : "项目连接包已注册，正在等待 Unity CLI 与本 Editor 建立连接；将自动重试。"));
                    }
                    return;
                }
                if (string.IsNullOrEmpty(unity))
                {
                    items.Add(new ToolchainItem("unity-pipeline", name, "blocked", "Unity CLI 自动安装尚未成功，当前工程的 Pipeline 自动接入暂时无法执行。", "后续自动重试会先修复 Unity CLI，再继续安装当前工程连接包。"));
                    return;
                }
                HostProvisioningProcessResult install = RunHostProvisioningProcess(context, unity,
                    new[] { "pipeline", "install", "--project-path", context.ProjectRoot, "--non-interactive", "--format", "json" },
                    c_ProvisioningInstallTimeoutMs, cancellationToken);
                bool declared = false;
                try
                {
                    JObject manifest = JObject.Parse(File.ReadAllText(Path.Combine(context.ProjectRoot, "Packages", "manifest.json")));
                    declared = !string.IsNullOrWhiteSpace((string)manifest["dependencies"]?["com.unity.pipeline"]);
                }
                catch (Exception) { }
                items.Add(install.Success && declared
                    ? new ToolchainItem("unity-pipeline", name, "partial", "Unity CLI 已完成当前工程的 com.unity.pipeline 安装声明；仍需包解析和脚本重载确认实际注册。", "自动接力将在包解析和重载后核验注册与 Editor 连接。")
                    : new ToolchainItem("unity-pipeline", name, "blocked", "已自动执行当前工程的 Pipeline 安装，但未确认成功写入有效包声明。" + DescribeHostProvisioningFailure(install, context), "后续自动重试会继续完成当前工程接入。"));
            }

            /// <summary>只选择实际执行版本命令成功的 CLI，兼容桌面应用启动时 PATH 不完整的情况。</summary>
            /// <param name="context">用户工具目录与主机类型。</param>
            /// <param name="codex">是否查找 Codex 而非 Unity CLI。</param>
            /// <param name="cancellationToken">停止候选命令探测。</param>
            /// <param name="version">已经核验的版本文本。</param>
            /// <param name="preferStandalone">补装后优先核对独立安装目录，避免继续选择不具备插件能力的旧应用内 CLI。</param>
            /// <returns>可用命令绝对路径；未找到时为空。</returns>
            private static string FindVerifiedHostCli(HostProvisioningContext context, bool codex, CancellationToken cancellationToken, out string version, bool preferStandalone = false)
            {
                string command = codex ? context.Windows ? "codex.exe" : "codex" : context.Windows ? "unity.exe" : "unity";
                var preferred = new List<string>();
                if (codex && preferStandalone)
                {
                    if (!string.IsNullOrWhiteSpace(context.CodexInstallDirectory)) preferred.Add(Path.Combine(context.CodexInstallDirectory, command));
                    preferred.Add(Path.Combine(context.UserDirectory, ".local", "bin", command));
                    preferred.Add(Path.Combine(context.LocalApplicationData, "Programs", "OpenAI", "Codex", "bin", command));
                }
                if (codex && context.Mac)
                {
                    preferred.Add("/Applications/ChatGPT.app/Contents/Resources/codex-cli/CodexCLI.app/Contents/MacOS/codex");
                    preferred.Add("/Applications/Codex.app/Contents/Resources/codex");
                }
                preferred.AddRange(FindToolchainExecutables(context.ProcessPath, command,
                    Path.Combine(context.UserDirectory, ".local", "bin", command),
                    Path.Combine(context.UserDirectory, ".unity", "bin", command),
                    codex ? Path.Combine(context.LocalApplicationData, "Programs", "OpenAI", "Codex", "bin", command) : Path.Combine(context.LocalApplicationData, "Unity", "bin", command),
                    !codex && !string.IsNullOrWhiteSpace(context.UnityCliHome) ? Path.Combine(context.UnityCliHome, "bin", command) : null,
                    codex && !string.IsNullOrWhiteSpace(context.CodexInstallDirectory) ? Path.Combine(context.CodexInstallDirectory, command) : null,
                    !codex && context.Mac ? "/Applications/Unity Hub.app/Contents/Resources/cli/unity" : null,
                    !codex && context.Windows ? Path.Combine(context.ProgramFiles, "Unity Hub", "resources", "cli", command) : null,
                    context.Windows ? null : Path.Combine("/usr/local/bin", command),
                    context.Mac ? Path.Combine("/opt/homebrew/bin", command) : null));
                foreach (string candidate in preferred.Distinct(StringComparer.Ordinal))
                {
                    if (!File.Exists(candidate)) continue;
                    HostProvisioningProcessResult result = RunHostProvisioningProcess(context, candidate, new[] { "--version" }, c_ToolchainProbeTimeoutMs, cancellationToken);
                    if (!result.Success || result.Stdout.Length > 160) continue;
                    Match match = Regex.Match(result.Stdout.Trim(), codex
                        ? @"\A(?:codex(?:-cli)?\s+)v?(?<version>\d+\.\d+\.\d+(?:[-+][0-9A-Za-z.+-]+)?)\z"
                        : @"\A(?:Unity(?: CLI)?\s+)?v?(?<version>\d+\.\d+\.\d+(?:[-+][0-9A-Za-z.+-]+)?)\z", RegexOptions.IgnoreCase);
                    if (!match.Success) continue;
                    version = match.Groups["version"].Value;
                    return candidate;
                }
                version = string.Empty;
                return null;
            }

            /// <summary>下载固定官方入口的安装脚本，再用主机原生解释器非交互执行，禁止 shell 插值。</summary>
            /// <param name="context">平台、用户目录与本轮临时目录。</param>
            /// <param name="codex">选择 OpenAI 独立 CLI 或 Unity CLI 官方安装入口。</param>
            /// <param name="cancellationToken">中止下载或安装进程。</param>
            /// <returns>官方脚本的退出结果；调用方仍须重新验证实际命令。</returns>
            private static HostProvisioningProcessResult RunHostOfficialInstaller(HostProvisioningContext context, bool codex, CancellationToken cancellationToken)
            {
                Directory.CreateDirectory(context.TemporaryDirectory);
                string extension = context.Windows ? ".ps1" : ".sh";
                string script = Path.Combine(context.TemporaryDirectory, (codex ? "codex-install" : "unity-install") + extension);
                string url = (codex ? "https://chatgpt.com/codex/install" : "https://unity.com/install") + extension;
                DownloadHostOfficialInstaller(url, script, cancellationToken);
                if (!context.Windows)
                {
                    return RunHostProvisioningProcess(context, codex ? "/bin/sh" : "/bin/bash", new[] { script }, c_ProvisioningInstallTimeoutMs, cancellationToken);
                }
                string systemDirectory = System.Environment.GetFolderPath(System.Environment.SpecialFolder.System);
                string powershell = Path.Combine(systemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe");
                return RunHostProvisioningProcess(context, powershell,
                    new[] { "-NoLogo", "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", script }, c_ProvisioningInstallTimeoutMs, cancellationToken);
            }

            /// <summary>在后台下载大小受限的 HTTPS 脚本；取消会中断 HTTP 请求，不把网页错误当作脚本执行。</summary>
            /// <param name="url">固定官方 HTTPS 入口。</param>
            /// <param name="destination">Library 下本轮独享的临时脚本路径。</param>
            /// <param name="cancellationToken">取消网络等待。</param>
            private static void DownloadHostOfficialInstaller(string url, string destination, CancellationToken cancellationToken)
            {
                using var downloadCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                downloadCancellation.CancelAfter(30000);
                var request = (HttpWebRequest)WebRequest.Create(url);
                request.Method = "GET";
                request.Timeout = 30000;
                request.ReadWriteTimeout = 30000;
                request.MaximumAutomaticRedirections = 5;
                request.UserAgent = "Nova-AgentToolchain/1.0";
                using (downloadCancellation.Token.Register(request.Abort))
                {
                    try
                    {
                        using var response = (HttpWebResponse)request.GetResponse();
                        if (response.ResponseUri.Scheme != Uri.UriSchemeHttps || response.ContentLength > 2 * 1024 * 1024
                            || (response.ContentType ?? string.Empty).IndexOf("text/html", StringComparison.OrdinalIgnoreCase) >= 0)
                            throw new InvalidDataException("官方安装入口没有返回有效的 HTTPS 安装脚本。");
                        using Stream input = response.GetResponseStream();
                        using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                        byte[] buffer = new byte[16384];
                        int total = 0;
                        int count;
                        while ((count = input.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            if (downloadCancellation.IsCancellationRequested) throw new TimeoutException("下载官方安装脚本超过 30 秒，自动下载已终止。");
                            total += count;
                            if (total > 2 * 1024 * 1024) throw new InvalidDataException("官方安装脚本超过自动下载大小上限。");
                            output.Write(buffer, 0, count);
                        }
                        if (total == 0) throw new InvalidDataException("官方安装入口返回了空脚本。");
                    }
                    catch (WebException)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        if (downloadCancellation.IsCancellationRequested) throw new TimeoutException("下载官方安装脚本超过 30 秒，自动下载已终止。");
                        throw;
                    }
                }
            }

            /// <summary>给 Codex 固定当前工程上下文，避免核对到其他工程的插件启用策略。</summary>
            /// <param name="context">当前工程与主机。</param>
            /// <param name="codex">已核验的 Codex 路径。</param>
            /// <param name="arguments">逐项参数，不包含 shell 表达式。</param>
            /// <param name="timeoutMs">本次命令的最大等待时间。</param>
            /// <param name="cancellationToken">终止命令信号。</param>
            /// <returns>受限的进程结果。</returns>
            private static HostProvisioningProcessResult RunHostCodex(HostProvisioningContext context, string codex, string[] arguments, int timeoutMs, CancellationToken cancellationToken)
            {
                return RunHostProvisioningProcess(context, codex, new[] { "-C", context.ProjectRoot }.Concat(arguments).ToArray(), timeoutMs, cancellationToken);
            }

            /// <summary>只接受实际清单中的已安装、已启用官方 Unity 插件，不以缓存存在代替宿主状态。</summary>
            /// <param name="result">本次 plugin list 的完整进程结果。</param>
            /// <param name="context">当前 Codex 用户目录。</param>
            /// <param name="pluginId">已经核验的插件精确标识。</param>
            /// <returns>当前工程实际启用了可信官方插件时为 true。</returns>
            private static bool TryConfirmHostUnityPlugin(HostProvisioningProcessResult result, HostProvisioningContext context, out string pluginId)
            {
                pluginId = string.Empty;
                if (!result.Success || result.OutputTruncated) return false;
                try
                {
                    List<JObject> entries = ReadToolchainPluginEntries(JObject.Parse(result.Stdout));
                    if (entries == null) return false;
                    foreach (JObject original in entries)
                    {
                        string id = (string)original["pluginId"] ?? (string)original["id"] ?? string.Empty;
                        if (id != c_ProvisioningUnityPlugin && id != c_ProvisioningUnityMarketplacePlugin) continue;
                        if ((bool?)original["installed"] != true || (bool?)original["enabled"] != true) continue;
                        var entry = (JObject)original.DeepClone();
                        // 当前 Codex 的 Git 市场使用 source 字段，兼容只读核验器原有的 url/repo 身份入口。
                        if (entry["marketplaceSource"] is JObject source && source["url"] == null && source["repo"] == null && source["source"] != null)
                            source["repo"] = source["source"].DeepClone();
                        if (!IsOfficialToolchainUnityPlugin(entry, context.CodexHome)) continue;
                        pluginId = id;
                        return true;
                    }
                }
                catch (Exception) { }
                return false;
            }

            /// <summary>执行本工具链专用有界进程，后台排空输出；取消或超时会结束进程树。</summary>
            /// <param name="context">工作目录和主机环境。</param>
            /// <param name="executable">直接执行的程序或平台解释器路径。</param>
            /// <param name="arguments">逐项参数，统一进行标准引号转义。</param>
            /// <param name="timeoutMs">整个命令的最大等待时间。</param>
            /// <param name="cancellationToken">取消信号，同时绑定进程终止。</param>
            /// <returns>退出状态与供内部解析的受限输出。</returns>
            private static HostProvisioningProcessResult RunHostProvisioningProcess(HostProvisioningContext context, string executable, string[] arguments, int timeoutMs, CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var result = new HostProvisioningProcessResult();
                var startInfo = new ProcessStartInfo
                {
                    FileName = executable,
                    Arguments = string.Join(" ", arguments.Select(QuoteToolchainArgument)),
                    WorkingDirectory = context.ProjectRoot,
                    UseShellExecute = false,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8,
                };
                startInfo.EnvironmentVariables["PATH"] = BuildHostProvisioningPath(context, executable);
                startInfo.EnvironmentVariables["CI"] = "1";
                startInfo.EnvironmentVariables["NO_COLOR"] = "1";
                startInfo.EnvironmentVariables["UNITY_NON_INTERACTIVE"] = "1";
                startInfo.EnvironmentVariables["UNITY_NO_CONSENT_PROMPT"] = "1";
                startInfo.EnvironmentVariables["UNITY_NO_PAGER"] = "1";
                startInfo.EnvironmentVariables["UNITY_CLI_CDN_BASE"] = "https://public-cdn.cloud.unity3d.com/hub/prod/cli/";
                startInfo.EnvironmentVariables["CODEX_NON_INTERACTIVE"] = "1";
                try
                {
                    using var process = new Process { StartInfo = startInfo };
                    var stdout = new StringBuilder();
                    var stderr = new StringBuilder();
                    bool stdoutClosed = false;
                    bool stderrClosed = false;
                    object outputGate = new object();
                    process.OutputDataReceived += (_, eventArgs) =>
                    {
                        if (eventArgs.Data == null) { lock (outputGate) { stdoutClosed = true; } }
                        else AppendHostProvisioningOutput(stdout, eventArgs.Data, outputGate, result);
                    };
                    process.ErrorDataReceived += (_, eventArgs) =>
                    {
                        if (eventArgs.Data == null) { lock (outputGate) { stderrClosed = true; } }
                        else AppendHostProvisioningOutput(stderr, eventArgs.Data, outputGate, result);
                    };
                    if (!process.Start()) { result.Stderr = "进程未能启动。"; return result; }
                    try
                    {
                        using (cancellationToken.Register(() => StopHostProvisioningProcessTree(process, context.Windows)))
                        {
                            process.StandardInput.Close();
                            process.BeginOutputReadLine();
                            process.BeginErrorReadLine();
                            var timer = Stopwatch.StartNew();
                            while (!process.WaitForExit(100))
                            {
                                cancellationToken.ThrowIfCancellationRequested();
                                if (timer.ElapsedMilliseconds < timeoutMs) continue;
                                result.TimedOut = true;
                                StopHostProvisioningProcessTree(process, context.Windows);
                                break;
                            }
                            cancellationToken.ThrowIfCancellationRequested();
                            if (process.WaitForExit(2000)) result.ExitCode = process.ExitCode;
                            // 有界排空管道：异常后代继承输出句柄时也不能卡住整个自动接力。
                            var drainTimer = Stopwatch.StartNew();
                            while (drainTimer.ElapsedMilliseconds < 2000)
                            {
                                bool drained;
                                lock (outputGate) { drained = stdoutClosed && stderrClosed; }
                                if (drained) break;
                                cancellationToken.ThrowIfCancellationRequested();
                                Thread.Sleep(10);
                            }
                            lock (outputGate)
                            {
                                if (!stdoutClosed || !stderrClosed) result.OutputTruncated = true;
                                result.Stdout = stdout.ToString();
                                result.Stderr = stderr.ToString();
                            }
                        }
                    }
                    finally
                    {
                        StopHostProvisioningProcessTree(process, context.Windows);
                        // 中止剩余读取，不让异常安装后代持续占用输出回调。
                        try { process.CancelOutputRead(); } catch (Exception) { }
                        try { process.CancelErrorRead(); } catch (Exception) { }
                    }
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception exception) { result.Stderr = exception.Message; }
                return result;
            }

            /// <summary>把常见 GUI 遗漏目录补入本轮子进程 PATH，不修改系统或项目的持久 PATH 配置。</summary>
            /// <param name="context">冻结的现有 PATH 与用户目录。</param>
            /// <param name="executable">当前直接执行的工具路径。</param>
            /// <returns>供本轮命令及其子进程使用的 PATH。</returns>
            private static string BuildHostProvisioningPath(HostProvisioningContext context, string executable)
            {
                var directories = new List<string>
                {
                    Path.GetDirectoryName(executable), Path.Combine(context.UserDirectory, ".local", "bin"),
                    Path.Combine(context.UserDirectory, ".unity", "bin"),
                    Path.Combine(context.LocalApplicationData, "Unity", "bin"),
                    Path.Combine(context.LocalApplicationData, "Programs", "OpenAI", "Codex", "bin"),
                };
                if (!context.Windows) directories.AddRange(new[] { "/usr/local/bin", "/usr/bin", "/bin", "/usr/sbin", "/sbin" });
                if (context.Mac) directories.Add("/opt/homebrew/bin");
                directories.AddRange(context.ProcessPath.Split(Path.PathSeparator));
                return string.Join(Path.PathSeparator.ToString(), directories.Where(path => !string.IsNullOrWhiteSpace(path)).Distinct(StringComparer.Ordinal));
            }

            /// <summary>排空进程每一行输出但限制内存占用，超限后禁止把被截断 JSON 作为成功证据。</summary>
            /// <param name="builder">标准输出或错误缓冲区。</param>
            /// <param name="line">进程返回的一行。</param>
            /// <param name="gate">同步输出回调与结果快照的锁。</param>
            /// <param name="result">记录截断状态。</param>
            private static void AppendHostProvisioningOutput(StringBuilder builder, string line, object gate, HostProvisioningProcessResult result)
            {
                if (line == null) return;
                lock (gate)
                {
                    int remaining = c_ProvisioningOutputLimit - builder.Length;
                    if (remaining <= 0) { result.OutputTruncated = true; return; }
                    if (line.Length >= remaining) { builder.Append(line, 0, remaining); result.OutputTruncated = true; }
                    else builder.AppendLine(line);
                }
            }

            /// <summary>取消或超时时结束安装进程树，兼容 Unity Mono 未提供 Kill(bool) 的运行时。</summary>
            /// <param name="process">本工具启动且尚未释放的根进程。</param>
            /// <param name="windows">是否使用 Windows 进程树终止命令。</param>
            private static void StopHostProvisioningProcessTree(Process process, bool windows)
            {
                try
                {
                    if (process.HasExited) return;
                    var killTree = typeof(Process).GetMethod("Kill", new[] { typeof(bool) });
                    if (killTree != null)
                    {
                        try { killTree.Invoke(process, new object[] { true }); return; }
                        catch (Exception) { }
                    }
                    if (windows)
                    {
                        string systemDirectory = System.Environment.GetFolderPath(System.Environment.SpecialFolder.System);
                        using var killer = Process.Start(new ProcessStartInfo
                        {
                            FileName = Path.Combine(systemDirectory, "taskkill.exe"),
                            Arguments = "/PID " + process.Id.ToString(System.Globalization.CultureInfo.InvariantCulture) + " /T /F",
                            UseShellExecute = false,
                            CreateNoWindow = true,
                        });
                        if (killer != null && !killer.WaitForExit(2000)) killer.Kill();
                    }
                    else
                    {
                        KillHostProvisioningUnixChildren(process.Id);
                    }
                    if (!process.HasExited) process.Kill();
                }
                catch (Exception) { }
            }

            /// <summary>用系统进程父子表结束本轮安装的 Unix 后代，避免安装脚本退出后下载器继续运行。</summary>
            /// <param name="parentId">本工具直接启动的安装根进程编号。</param>
            private static void KillHostProvisioningUnixChildren(int parentId)
            {
                try
                {
                    using var listing = Process.Start(new ProcessStartInfo
                    {
                        FileName = File.Exists("/bin/ps") ? "/bin/ps" : "/usr/bin/ps",
                        Arguments = "-axo pid=,ppid=",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true,
                    });
                    if (listing == null) return;
                    var lines = new List<string>();
                    object gate = new object();
                    listing.OutputDataReceived += (_, eventArgs) => { if (eventArgs.Data != null) lock (gate) { lines.Add(eventArgs.Data); } };
                    listing.BeginOutputReadLine();
                    if (!listing.WaitForExit(1000)) { listing.Kill(); return; }
                    var children = new Dictionary<int, List<int>>();
                    lock (gate)
                    {
                        foreach (string line in lines)
                        {
                            string[] columns = line.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                            if (columns.Length != 2 || !int.TryParse(columns[0], out int pid) || !int.TryParse(columns[1], out int ppid)) continue;
                            if (!children.TryGetValue(ppid, out List<int> group)) children.Add(ppid, group = new List<int>());
                            group.Add(pid);
                        }
                    }
                    var descendants = new List<int> { parentId };
                    for (int index = 0; index < descendants.Count && index < 10000; index++)
                    {
                        if (children.TryGetValue(descendants[index], out List<int> group)) descendants.AddRange(group.Where(pid => !descendants.Contains(pid)));
                    }
                    for (int index = descendants.Count - 1; index > 0; index--)
                    {
                        try { using Process child = Process.GetProcessById(descendants[index]); if (!child.HasExited) child.Kill(); }
                        catch (Exception) { }
                    }
                }
                catch (Exception) { }
            }

            /// <summary>把失败状态转为简短中文原因，不全量转储 stdout、stderr 或任何凭据。</summary>
            /// <param name="result">本轮命令结果。</param>
            /// <param name="context">需要脱敏的用户与工程路径。</param>
            /// <returns>有界的失败原因；成功但未取得证据时明确保留边界。</returns>
            private static string DescribeHostProvisioningFailure(HostProvisioningProcessResult result, HostProvisioningContext context)
            {
                if (result.TimedOut) return " 自动处理已达到时限，已终止本轮命令及其子进程。";
                string detail = result.Stderr;
                try
                {
                    JObject json = JObject.Parse(result.Stdout);
                    detail = (string)json["errors"]?[0]?["message"] ?? (string)json["error"]?["message"] ?? (string)json["message"] ?? detail;
                }
                catch (Exception) { }
                if (string.IsNullOrWhiteSpace(detail)) return result.Success ? " 命令虽已退出，但实际状态仍未满足核验条件。" : $" 自动命令退出码为 {result.ExitCode}，未返回可核验的失败详情。";
                return $" 自动命令退出码为 {result.ExitCode}：" + SanitizeHostProvisioningDiagnostic(detail, context);
            }

            /// <summary>只保留简短诊断，移除终端控制符、个人路径、URL 凭据和常见认证值。</summary>
            /// <param name="value">仅供提炼的错误文本。</param>
            /// <param name="context">本轮用户及工程路径。</param>
            /// <returns>适合项目组查看且长度受限的错误原因。</returns>
            private static string SanitizeHostProvisioningDiagnostic(string value, HostProvisioningContext context)
            {
                string text = value ?? string.Empty;
                if (text.Length > 16000) text = text.Substring(text.Length - 16000);
                text = Regex.Replace(text, @"\x1B\[[0-?]*[ -/]*[@-~]", string.Empty);
                text = Regex.Replace(text, @"[\x00-\x1F\x7F]", " ");
                foreach (string path in new[] { context.TemporaryDirectory, context.ProjectRoot, context.CodexHome, context.UserDirectory })
                    if (!string.IsNullOrWhiteSpace(path)) text = text.Replace(path, "<本地路径>");
                text = Regex.Replace(text, @"(?i)(https?://)[^\s/@]+:[^\s/@]+@", "$1<已隐藏>@");
                text = Regex.Replace(text, @"(?i)(https?://[^\s?]+)\?[^\s]+", "$1?<已隐藏>");
                text = Regex.Replace(text, @"(?i)\b(?:Bearer\s+\S+|(?:access[_-]?token|refresh[_-]?token|api[_-]?key|authorization|password|secret)\s*[:=]\s*[^\s,;]+|sk-[A-Za-z0-9_-]+|gh[pousr]_[A-Za-z0-9_]+)", "<认证信息已隐藏>");
                text = Regex.Replace(text, @"\s+", " ").Trim();
                return text.Length <= 320 ? text : text.Substring(0, 320) + "…";
            }
        }
    }
}
