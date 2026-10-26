/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  NovaToolchainBindingUnityMcp.cs
 * author:    taoye
 * created:   2026/10/26
 * descrip:   复用 Coplay 服务能力完成当前项目的自动连接与 Codex 绑定
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using MCPForUnity.Editor.Dependencies;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Services;
using MCPForUnity.Editor.Services.Server;
using MCPForUnity.Editor.Services.Transport;
using MCPForUnity.Editor.Services.Transport.Transports;
using Newtonsoft.Json.Linq;
using NovaFramework.Mcp.Editor;
using UnityEditor;
using UnityEngine;
using BindingResult = NovaFramework.Mcp.Editor.NovaToolchainBinding.BindingResult;

namespace NovaFramework.Mcp.UnityMcp.Editor
{
    /// <summary>明确注册 Coplay 绑定实现；初始化本身不安装、不启动进程，也不修改客户端配置。</summary>
    [InitializeOnLoad]
    internal static partial class NovaToolchainBindingUnityMcp
    {
        private const string LaunchKeyPrefix = "Nova.Mcp.Binding.ServerLaunch.v1.";
        private const string RuntimeInstallKey = "Nova.Mcp.Binding.UvInstallStarted.v1";
        private static Process s_LaunchProcess;
        private static string s_LaunchKey;

        /// <summary>注册固定入口；重载只释放本域进程句柄，不停止已经提供服务的进程。</summary>
        static NovaToolchainBindingUnityMcp()
        {
            NovaToolchainBinding.RegisterProvider("com.coplaydev.unity-mcp", EnsureAsync);
            AssemblyReloadEvents.beforeAssemblyReload += ReleaseLaunchHandle;
        }

        /// <summary>准备 uv、复用或启动本机服务，并只绑定当前项目的 Codex 配置。</summary>
        /// <param name="projectRoot">已由中立层验证的当前项目根。</param>
        /// <param name="cancellationToken">调用方中断信号。</param>
        /// <returns>本地连接、网关和绑定均通过时为 success；不据此推断当前聊天已热加载工具。</returns>
        private static async UniTask<BindingResult> EnsureAsync(string projectRoot, CancellationToken cancellationToken)
        {
            await UniTask.SwitchToMainThread(cancellationToken);
            bool useHttp = EditorConfigurationCache.Instance.UseHttpTransport;
            TransportMode mode = useHttp ? TransportMode.Http : TransportMode.Stdio;
            string endpoint = useHttp ? HttpEndpointUtility.GetMcpRpcUrl() : null;
            string codexHome = System.Environment.GetEnvironmentVariable("CODEX_HOME");
            string home = System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile);
            codexHome = string.IsNullOrWhiteSpace(codexHome) ? Path.Combine(home, ".codex") : codexHome;
            bool remote = useHttp && HttpEndpointUtility.IsRemoteScope();
            bool connected = MCPServiceLocator.TransportManager.GetState(mode).IsConnected
                && await MCPServiceLocator.TransportManager.VerifyAsync(mode).AsUniTask().AttachExternalCancellation(cancellationToken);
            string runtime = null;
            string[] stdioArguments = null;

            if (!connected && remote)
                return new BindingResult(projectRoot, "blocked", "已有远程 MCP 配置尚未连通；自动绑定保留其地址和认证，不改成本机端口，也不覆盖凭据，程序稍后重试。");

            if (!connected || !useHttp)
            {
                var runtimeResult = await EnsureRuntimeAsync(cancellationToken);
                runtime = runtimeResult.Path;
                if (string.IsNullOrEmpty(runtime))
                    return new BindingResult(projectRoot, runtimeResult.Pending ? "partial" : "blocked", runtimeResult.Pending
                        ? "uv 服务运行时的自动安装仍在本机会话中进行；程序将等待后重试，不并发启动另一份安装器。"
                        : "Coplay 所需的 uv 服务运行时未能自动安装并验证，可能受网络或系统权限限制；程序稍后自动重试。");
                await UniTask.SwitchToMainThread(cancellationToken);
                if (!MatchesFrozenTransport(useHttp, endpoint))
                    return new BindingResult(projectRoot, "partial", "MCP 传输或地址在准备期间发生变化，程序将重新读取后自动绑定，未继续按旧地址启动服务。");
                var sourceArguments = new List<string>(AssetPathUtility.GetUvxDevFlagsList());
                sourceArguments.AddRange(AssetPathUtility.GetBetaServerFromArgsList());
                sourceArguments.Add("mcp-for-unity");
                if (Path.GetFileNameWithoutExtension(runtime).Equals("uv", StringComparison.OrdinalIgnoreCase)) sourceArguments.InsertRange(0, new[] { "tool", "run" });

                if (useHttp)
                {
                    string baseUrl = HttpEndpointUtility.GetLocalBaseUrl();
                    if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out Uri localUri) || localUri.Scheme != "http"
                        || !HttpEndpointUtility.IsLoopbackHost(localUri.Host) || !string.IsNullOrEmpty(localUri.UserInfo))
                        return new BindingResult(projectRoot, "blocked", "当前 MCP 地址不符合本机 loopback 服务条件；程序保留已有地址与凭据，没有占用新端口或修改远程配置。");
                    if (!await CanConnectAsync(localUri, cancellationToken))
                    {
                        sourceArguments.AddRange(new[] { "--transport", "http", "--http-url", baseUrl, "--project-scoped-tools" });
                        bool launched = await EnsureServerProcessAsync(projectRoot, localUri, runtime, sourceArguments, cancellationToken);
                        if (!launched) return new BindingResult(projectRoot, "blocked", "本机 MCP 服务进程未能自动启动；未停止占用端口的其他进程，程序稍后重试。");
                    }

                    // 首次 uvx 可能正在下载固定版本服务，短时间未监听不等于安装失败，不重复启动。
                    bool reachable = false;
                    for (int attempt = 0; attempt < 20; attempt++)
                    {
                        if (await CanConnectAsync(localUri, cancellationToken)) { reachable = true; break; }
                        await UniTask.Delay(TimeSpan.FromMilliseconds(500), ignoreTimeScale: true, cancellationToken: cancellationToken);
                    }
                    if (!reachable) return new BindingResult(projectRoot, "partial", "MCP 服务已进入后台准备，首次启动可能仍在下载运行依赖；程序将继续自动等待和重试，不需要项目组手工启动。");
                }
                else
                {
                    stdioArguments = sourceArguments.Concat(new[] { "--transport", "stdio" }).ToArray();
                }

                if (!connected)
                {
                    // 直接启动所需 transport，不使用会先停止另一 transport 的 Bridge.StartAsync。
                    bool started = await MCPServiceLocator.TransportManager.StartAsync(mode).AsUniTask()
                        .AttachExternalCancellation(cancellationToken).Timeout(TimeSpan.FromSeconds(20), DelayType.Realtime);
                    connected = started && await MCPServiceLocator.TransportManager.VerifyAsync(mode).AsUniTask().AttachExternalCancellation(cancellationToken);
                }
            }

            await UniTask.SwitchToMainThread(cancellationToken);
            if (!MatchesFrozenTransport(useHttp, endpoint))
                return new BindingResult(projectRoot, "partial", "MCP 传输配置已变化，旧连接结果已丢弃，程序将重新核对绑定。");
            if (!connected || !MCPServiceLocator.TransportManager.GetState(mode).IsConnected)
                return new BindingResult(projectRoot, "blocked", "当前 Unity Editor 尚未通过 Coplay 的实际连接核验；端口和其他项目服务保持不变，程序稍后自动重试。");

            MCPServiceLocator.ToolDiscovery.InvalidateCache();
            if (MCPServiceLocator.ToolDiscovery.GetToolMetadata("nova_project_action") == null)
                return new BindingResult(projectRoot, "blocked", "Coplay 已连接当前 Editor，但 Nova MCP 工具尚未完成注册；等待包编译后自动重试。");
            if (!MCPServiceLocator.ToolDiscovery.IsToolEnabled("nova_project_action")) MCPServiceLocator.ToolDiscovery.SetToolEnabled("nova_project_action", true);
            if (useHttp && MCPServiceLocator.TransportManager.GetClient(mode) is WebSocketTransportClient client)
                await client.ReregisterToolsAsync().AsUniTask().AttachExternalCancellation(cancellationToken);
            NovaProjectActionGatewayResponse description = await NovaProjectActionGateway.HandleCommand(new JObject { ["operation"] = "describe" })
                .AsUniTask().AttachExternalCancellation(cancellationToken);
            if (!description.success) return new BindingResult(projectRoot, "blocked", "当前 Editor 已连接，但 Nova 工具的实际 describe 尚未通过；未把包安装或端口监听当作完整可用。");
            if (!MatchesFrozenTransport(useHttp, endpoint) || (useHttp && !MatchesCurrentHttpConnection(endpoint)))
                return new BindingResult(projectRoot, "partial", "Provider 的实际连接和当前地址尚未一致，程序保留已有服务，稍后自动重新核验；未写入未经确认的绑定。");

            string configuration = await UniTask.RunOnThreadPool(() => EnsureCodexConfiguration(projectRoot, codexHome, endpoint, runtime, stdioArguments, cancellationToken), cancellationToken: cancellationToken);
            if (!MatchesFrozenTransport(useHttp, endpoint) || (useHttp && !MatchesCurrentHttpConnection(endpoint)))
                return new BindingResult(projectRoot, "partial", "配置已保存，但 Provider 在核验期间发生变化，程序将重新核对；本轮不记录为部署成功。");
            return new BindingResult(projectRoot, "success", "Coplay 与当前 Unity Editor 的连接及 Nova describe 已验证。" + configuration
                + (useHttp ? "" : "stdio 服务将由宿主读取配置时自动启动。")
                + "当前聊天是否已热加载新工具无法由安装包确认，等待宿主自动加载；程序不会执行 AI 任务或消耗模型额度。");
        }

        /// <summary>拒绝沿用异步等待前的旧传输选项，避免把新连接与旧地址混作同一次成功。</summary>
        /// <param name="useHttp">开始时冻结的传输选择。</param>
        /// <param name="endpoint">开始时冻结的 HTTP MCP 地址。</param>
        /// <returns>当前选项仍与冻结值一致时为 true。</returns>
        private static bool MatchesFrozenTransport(bool useHttp, string endpoint)
        {
            return EditorConfigurationCache.Instance.UseHttpTransport == useHttp
                && (!useHttp || string.Equals(HttpEndpointUtility.GetMcpRpcUrl(), endpoint, StringComparison.Ordinal));
        }

        /// <summary>核对实际 WebSocket 端口、协议和路径；成功仍须前置 VerifyAsync 与 Nova describe。</summary>
        /// <param name="endpoint">已经冻结的 HTTP MCP 地址。</param>
        /// <returns>当前实际注册连接与目标地址一致时为 true。</returns>
        private static bool MatchesCurrentHttpConnection(string endpoint)
        {
            return MatchesHttpConnection(MCPServiceLocator.TransportManager.GetClient(TransportMode.Http)?.State, endpoint);
        }

        /// <summary>比较已连接快照的精确地址；Coplay 10.3 的 StartAsync 可能在注册回调后把 SessionId 覆写成 pending，不能用它否定有效连接。</summary>
        /// <param name="state">当前客户端状态；命令和网关验证由调用方分别完成。</param>
        /// <param name="endpoint">本轮冻结的 MCP RPC 地址。</param>
        /// <returns>当前连接与本轮目标的协议、端口及路径一致时为 true。</returns>
        private static bool MatchesHttpConnection(TransportState state, string endpoint)
        {
            if (state == null || !state.IsConnected
                || !Uri.TryCreate(state.Details, UriKind.Absolute, out Uri actual) || !Uri.TryCreate(endpoint, UriKind.Absolute, out Uri expected)) return false;
            string expectedPath = expected.AbsolutePath.EndsWith("/mcp", StringComparison.Ordinal)
                ? expected.AbsolutePath.Substring(0, expected.AbsolutePath.Length - 4) + "/hub/plugin" : "";
            return actual.Port == expected.Port && actual.Scheme == (expected.Scheme == "https" ? "wss" : "ws")
                && (string.Equals(actual.Host, expected.Host, StringComparison.OrdinalIgnoreCase) || (actual.IsLoopback && expected.IsLoopback))
                && string.Equals(actual.AbsolutePath, expectedPath, StringComparison.Ordinal);
        }

        /// <summary>在线程池验证或自动安装 uv；不会在主线程等待外部命令。</summary>
        /// <param name="cancellationToken">中断信号；原厂安装器自身最多运行 120 秒。</param>
        /// <returns>已执行版本核验的运行时路径；Pending 表示已有安装接力仍在期限内。</returns>
        private static async UniTask<(string Path, bool Pending)> EnsureRuntimeAsync(CancellationToken cancellationToken)
        {
            string home = System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile);
            string path = System.Environment.GetEnvironmentVariable("PATH") ?? "";
            string localAppData = System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData);
            string pathOverride = EditorConfigurationCache.Instance.UvxPathOverride;
            string runtime = await UniTask.RunOnThreadPool(() => FindRuntime(pathOverride, path, home, localAppData), cancellationToken: cancellationToken);
            if (runtime != null) { SessionState.EraseString(RuntimeInstallKey); return (runtime, false); }
            if (!UvInstaller.IsSupported) return (null, false);
            if (long.TryParse(SessionState.GetString(RuntimeInstallKey, ""), NumberStyles.Integer, CultureInfo.InvariantCulture, out long started)
                && DateTime.UtcNow.Ticks - started < TimeSpan.FromMinutes(3).Ticks) return (null, true);
            SessionState.SetString(RuntimeInstallKey, DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture));
            try
            {
                // 原厂安装器不接受 CancellationToken；中断不保证终止其进程，接力租期避免重载后并发重装。
                UvInstaller.UvInstallResult install = await UniTask.RunOnThreadPool(() => UvInstaller.Run(120000), cancellationToken: cancellationToken);
                if (!install.Success) return (null, false);
                runtime = await UniTask.RunOnThreadPool(() => FindRuntime(pathOverride, path, home, localAppData), cancellationToken: cancellationToken);
                return (runtime, false);
            }
            finally
            {
                SessionState.EraseString(RuntimeInstallKey);
            }
        }

        /// <summary>只检查有限已知位置，并用原厂版本解析器验证真实命令；不输出安装器原始信息。</summary>
        /// <param name="pathOverride">已冻结的原厂路径覆盖。</param>
        /// <param name="path">已冻结的 PATH。</param>
        /// <param name="home">用户目录。</param>
        /// <param name="localAppData">Windows 用户程序目录。</param>
        /// <returns>首个可验证的运行时路径。</returns>
        private static string FindRuntime(string pathOverride, string path, string home, string localAppData)
        {
            var candidates = new List<string>();
            if (!string.IsNullOrWhiteSpace(pathOverride)) candidates.Add(pathOverride);
            var directories = path.Split(Path.PathSeparator).Where(value => !string.IsNullOrWhiteSpace(value)).Concat(new[]
            {
                Path.Combine(home, ".local", "bin"), Path.Combine(home, ".cargo", "bin"),
                "/opt/homebrew/bin", "/usr/local/bin", Path.Combine(localAppData, "Programs", "uv"),
            });
            foreach (string directory in directories)
                foreach (string command in new[] { "uvx", "uvx.exe", "uv", "uv.exe" }) candidates.Add(Path.Combine(directory.Trim('"'), command));
            var validator = new PathResolverService();
            foreach (string candidate in candidates.Distinct(StringComparer.Ordinal))
                if (File.Exists(candidate) && validator.TryValidateUvxExecutable(candidate, out _)) return Path.GetFullPath(candidate);
            return null;
        }

        /// <summary>限时检查 loopback TCP 监听；只决定是否可以新增服务，不把端口连通当作 MCP 成功。</summary>
        /// <param name="uri">已验证的本机服务地址。</param>
        /// <param name="cancellationToken">中断信号。</param>
        /// <returns>端口已有监听时为 true。</returns>
        private static async UniTask<bool> CanConnectAsync(Uri uri, CancellationToken cancellationToken)
        {
            using var client = new TcpClient();
            try
            {
                await client.ConnectAsync(uri.Host, uri.Port).AsUniTask().AttachExternalCancellation(cancellationToken)
                    .Timeout(TimeSpan.FromSeconds(1), DelayType.Realtime);
                return true;
            }
            catch (OperationCanceledException) { throw; }
            catch { return false; }
        }

        /// <summary>只新增缺失服务，绝不调用原厂会先停止当前端口服务的总启动入口。</summary>
        /// <param name="projectRoot">当前项目根。</param>
        /// <param name="endpoint">已冻结的 loopback 地址。</param>
        /// <param name="runtime">已验证运行时。</param>
        /// <param name="arguments">原厂固定服务参数和传输参数。</param>
        /// <param name="cancellationToken">中断信号。</param>
        /// <returns>新服务已启动或已有启动接力时为 true。</returns>
        private static async UniTask<bool> EnsureServerProcessAsync(string projectRoot, Uri endpoint, string runtime, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
        {
            string key = LaunchKeyPrefix + StableBindingId(projectRoot + "|" + endpoint.Authority);
            string previous = SessionState.GetString(key, "");
            string[] launchState = previous.Split('|');
            bool hasStarted = long.TryParse(launchState[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out long ticks);
            bool withinStartupBudget = hasStarted && DateTime.UtcNow.Ticks - ticks < TimeSpan.FromMinutes(3).Ticks;
            if (s_LaunchProcess != null && s_LaunchKey == key)
            {
                try
                {
                    if (!s_LaunchProcess.HasExited) return withinStartupBudget;
                    SessionState.EraseString(key);
                    ReleaseLaunchHandle();
                    return false;
                }
                catch { }
            }
            // 重载后只核对本轮登记的 PID 和创建时间，不搜索或停止任何其他进程。
            if (launchState.Length == 3 && int.TryParse(launchState[1], out int pid) && long.TryParse(launchState[2], out long processStarted))
            {
                try
                {
                    using Process launched = Process.GetProcessById(pid);
                    if (!launched.HasExited && launched.StartTime.ToUniversalTime().Ticks == processStarted) return withinStartupBudget;
                    withinStartupBudget = false;
                }
                catch { withinStartupBudget = false; }
            }
            if (withinStartupBudget) return true;
            SessionState.SetString(key, DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture));
            bool windows = Application.platform == RuntimePlatform.WindowsEditor;
            string pathPrepend = new ServerCommandBuilder().GetPlatformSpecificPathPrepend();
            string processPath = System.Environment.GetEnvironmentVariable("PATH") ?? "";
            string logPath = Path.Combine(projectRoot, "Library", "Nova", "Toolchain", "mcp-server-launch.log");
            try
            {
                ReleaseLaunchHandle();
                s_LaunchKey = key;
                s_LaunchProcess = await UniTask.RunOnThreadPool(() =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    string command = string.Join(" ", new[] { runtime }.Concat(arguments).Select(value => QuoteServerArgument(value, windows)));
                    if (!windows) command = "exec " + command;
                    if (windows && logPath.IndexOfAny(new[] { '%', '!', '\r', '\n', '"' }) >= 0) throw new InvalidOperationException("服务日志路径不能安全传给当前启动器。");
                    ProcessStartInfo info = new TerminalLauncher().CreateHeadlessProcessStartInfo(command, logPath);
                    info.WorkingDirectory = projectRoot;
                    info.EnvironmentVariables["PATH"] = Path.GetDirectoryName(runtime) + Path.PathSeparator + pathPrepend + Path.PathSeparator + processPath;
                    return Process.Start(info);
                }, cancellationToken: cancellationToken);
                if (s_LaunchProcess == null) SessionState.EraseString(key);
                else SessionState.SetString(key, SessionState.GetString(key, "") + "|" + s_LaunchProcess.Id.ToString(CultureInfo.InvariantCulture)
                    + "|" + s_LaunchProcess.StartTime.ToUniversalTime().Ticks.ToString(CultureInfo.InvariantCulture));
                return s_LaunchProcess != null;
            }
            catch
            {
                SessionState.EraseString(key);
                throw;
            }
        }

        /// <summary>按原厂后台启动器的 shell 规则引用固定参数，禁止 Windows 环境展开或换行注入。</summary>
        /// <param name="value">运行时路径或固定参数。</param>
        /// <param name="windows">是否使用 cmd 启动器。</param>
        /// <returns>经过准确引用的单参数。</returns>
        private static string QuoteServerArgument(string value, bool windows)
        {
            if (value == null || value.IndexOfAny(new[] { '\0', '\r', '\n' }) >= 0) throw new ArgumentException("服务参数无效。");
            if (!windows) return "'" + value.Replace("'", "'\\''") + "'";
            if (value.IndexOfAny(new[] { '%', '!', '"' }) >= 0) throw new ArgumentException("服务参数不能安全传给当前 Windows 启动器。");
            return "\"" + value + "\"";
        }

        /// <summary>为当前项目生成固定短标识，不把绝对路径或凭据放入服务器名。</summary>
        /// <param name="value">项目和服务身份。</param>
        /// <returns>十六位十六进制标识。</returns>
        private static string StableBindingId(string value)
        {
            using var sha = SHA256.Create();
            return string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(value)).Take(8).Select(item => item.ToString("x2", CultureInfo.InvariantCulture)));
        }

        /// <summary>域重载释放本域句柄；服务是正式运行能力，不能在重载时停止其他已连接会话。</summary>
        private static void ReleaseLaunchHandle()
        {
            s_LaunchProcess?.Dispose();
            s_LaunchProcess = null;
            s_LaunchKey = null;
        }
    }
}
