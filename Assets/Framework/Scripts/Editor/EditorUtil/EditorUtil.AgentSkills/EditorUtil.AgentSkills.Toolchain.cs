/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.AgentSkills.Toolchain.cs
 * author:    taoye
 * created:   2026/10/26
 * descrip:   Nova AI 开发环境的只读安装检查
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        public static partial class AgentSkills
        {
            private const string c_ToolchainNovaMcpPackage = "com.solotopia.nova.framework.mcp";
            private const string c_ToolchainUnityMcpPackage = "com.coplaydev.unity-mcp";
            private const int c_ToolchainProbeTimeoutMs = 8000;
            private const int c_ToolchainPluginQueryTimeoutMs = 45000;
            private const string c_UnityPluginInstallHelp = "在 Codex 或 ChatGPT 的插件目录中安装并启用 Unity（开发者 Unity Technologies）。官方说明：https://docs.unity.com/en-us/ai/unity-plugin/codex";

            /// <summary>一项本地安装检查；通过只表示该项说明的证据成立，不代表已连接 Editor。</summary>
            public sealed class ToolchainItem
            {
                /// <summary>建立只读结果，不保存命令原始输出或凭据。</summary>
                /// <param name="id">固定检查标识。</param>
                /// <param name="name">面向项目组的工具名称及作用。</param>
                /// <param name="status">success、partial 或 blocked。</param>
                /// <param name="detail">本次已经取得的证据及限制。</param>
                /// <param name="remediation">尚需执行的操作；已通过时可为空。</param>
                internal ToolchainItem(string id, string name, string status, string detail, string remediation = "")
                {
                    Id = id;
                    Name = name;
                    Status = status;
                    Detail = detail;
                    Remediation = remediation;
                }

                /// <summary>固定检查标识。</summary>
                public string Id { get; }
                /// <summary>工具名称及作用。</summary>
                public string Name { get; }
                /// <summary>本项的 success、partial 或 blocked 状态。</summary>
                public string Status { get; }
                /// <summary>已经确认的证据及未覆盖的边界。</summary>
                public string Detail { get; }
                /// <summary>补齐本项所需的操作。</summary>
                public string Remediation { get; }
            }

            /// <summary>本地工具链只读报告；外部 Agent 的当前连接必须另行现场验证。</summary>
            public sealed class ToolchainReport
            {
                /// <summary>冻结结果并汇总状态；任一阻断优先于待核验状态。</summary>
                /// <param name="items">本轮所有检查结果。</param>
                internal ToolchainReport(IReadOnlyList<ToolchainItem> items)
                {
                    Items = Array.AsReadOnly(items.ToArray());
                    int blocked = Items.Count(item => item.Status == "blocked");
                    int pending = Items.Count(item => item.Status == "partial");
                    Status = blocked > 0 ? "blocked" : pending > 0 ? "partial" : "success";
                    Summary = blocked > 0
                        ? $"Nova AI 开发环境尚未就绪：{blocked} 项需要处理，{pending} 项待核验。"
                        : pending > 0
                            ? $"本地检查结束：{pending} 项仍待核验；尚不能确认完整环境可用。"
                            : "Nova AI 开发环境检查已通过。";
                }

                /// <summary>整体状态；未验证外部连接时不会返回 success。</summary>
                public string Status { get; }
                /// <summary>面向项目组的结果摘要。</summary>
                public string Summary { get; }
                /// <summary>本次冻结的逐项结果。</summary>
                public IReadOnlyList<ToolchainItem> Items { get; }

                /// <summary>生成可直接用于日志或提示窗口的中文说明，不包含原始进程输出。</summary>
                /// <returns>摘要、逐项状态与必要操作。</returns>
                public string FormatMessage()
                {
                    var builder = new StringBuilder(Summary);
                    foreach (ToolchainItem item in Items)
                    {
                        string state = item.Status == "success" ? "已通过" : item.Status == "blocked" ? "需处理" : "待核验";
                        builder.Append("\n\n").Append(item.Name).Append("（").Append(state).Append("）\n").Append(item.Detail);
                        if (!string.IsNullOrEmpty(item.Remediation))
                        {
                            builder.Append("\n操作：").Append(item.Remediation);
                        }
                    }
                    return builder.ToString();
                }
            }

            /// <summary>冻结 Unity 注册包的必要字段；后台不访问 PackageInfo 或其他 Unity API。</summary>
            private sealed class ToolchainPackageSnapshot
            {
                public string Name;
                public string Version;
                public string ResolvedPath;
                public bool HasErrors;
            }

            /// <summary>检查五项安装状态及外部连接边界，不弹窗、不修复、不安装、不启动 MCP 服务。</summary>
            /// <remarks>外部命令在线程池限时执行；Skills 仅执行 dry-run，不恢复或修改同步事务。</remarks>
            /// <returns>只包含已取得证据的报告；当前 Agent 的 MCP describe 始终需要另行验证。</returns>
            public static async UniTask<ToolchainReport> ValidateToolchainAsync()
            {
                await UniTask.SwitchToMainThread();
                string projectRoot = ResolveProjectRoot();
                string userDirectory = System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile);
                string codexHome = System.Environment.GetEnvironmentVariable("CODEX_HOME");
                codexHome = string.IsNullOrWhiteSpace(codexHome) ? Path.Combine(userDirectory, ".codex") : codexHome;
                string processPath = System.Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
                bool windows = Application.platform == RuntimePlatform.WindowsEditor;
                string programFiles = System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFiles);
                ToolchainPackageSnapshot[] packages;
                try
                {
                    packages = (PackageInfo.GetAllRegisteredPackages() ?? Array.Empty<PackageInfo>())
                        .Where(package => package != null)
                        .Select(package => new ToolchainPackageSnapshot
                        {
                            Name = package.name,
                            Version = package.version,
                            ResolvedPath = package.resolvedPath,
                            HasErrors = package.errors != null && package.errors.Length > 0,
                        }).ToArray();
                    if (!packages.Any(package => package.Name == c_FrameworkPackageName))
                    {
                        // Framework 开发仓可直接把源码放在 Assets/Framework，而不注册为 UPM 包。
                        // 仅复用既有解析器确认的固定开发真源，不把任意缓存目录补成注册包。
                        string agentsRoot = ResolveAgentsRoot();
                        string developmentRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "Framework", c_AgentsDirectoryName));
                        if (string.Equals(agentsRoot, developmentRoot, StringComparison.Ordinal))
                        {
                            string packageRoot = Path.GetDirectoryName(agentsRoot);
                            JObject metadata = JObject.Parse(File.ReadAllText(Path.Combine(packageRoot, "package.json")));
                            if ((string)metadata["name"] == c_FrameworkPackageName)
                            {
                                packages = packages.Concat(new[] { new ToolchainPackageSnapshot
                                {
                                    Name = c_FrameworkPackageName,
                                    Version = (string)metadata["version"],
                                    ResolvedPath = packageRoot,
                                } }).ToArray();
                            }
                        }
                    }
                }
                catch
                {
                    packages = Array.Empty<ToolchainPackageSnapshot>();
                }

                ToolchainItem skills = InspectToolchainSkills();
                UniTask<ToolchainItem[]> packageTask = UniTask.RunOnThreadPool(() => InspectToolchainPackages(packages));
                UniTask<ToolchainItem> unityTask = UniTask.RunOnThreadPool(() => InspectToolchainUnityCli(processPath, userDirectory, programFiles, windows));
                UniTask<ToolchainItem> pluginTask = UniTask.RunOnThreadPool(() => InspectToolchainUnityPlugin(processPath, userDirectory, codexHome, projectRoot, windows));
                var results = await UniTask.WhenAll(packageTask, unityTask, pluginTask);
                bool pipelineRegistered = packages.Any(package => package.Name == "com.unity.pipeline" && !package.HasErrors);
                string cliConnection = pipelineRegistered
                    ? "本工程已注册 com.unity.pipeline，但 Unity CLI 与当前 Editor 的连接仍待验证。"
                    : "本工程未确认有效注册 com.unity.pipeline；Unity CLI 即使能返回版本，也不能据此确认它能操作当前 Editor。";
                return new ToolchainReport(new[]
                {
                    results.Item3, results.Item2, results.Item1[0], results.Item1[1], skills,
                    new ToolchainItem("mcp-connection", "MCP 连接：让当前 Agent 调用 Unity 和 Nova", "partial",
                        "本地安装检查不能证明当前 Agent 已连接到这个项目。" + cliConnection + "本检查不安装该包、不启动服务，也不修改外部 Agent 配置。",
                        "由当前 Agent 重连正确项目的 Coplay Unity MCP，确认 nova_project_action 可见，并执行 describe；Unity CLI 的 Editor 连接也需另行检查。"),
                });
            }

            /// <summary>用既有只读同步计划检查全部 Skills，不把即将新增或更新视作已经同步。</summary>
            /// <returns>完整同步、待同步或异常状态。</returns>
            private static ToolchainItem InspectToolchainSkills()
            {
                try
                {
                    ReconcileResult result = Reconcile(dryRun: true);
                    int pending = result.Added.Count + result.Updated.Count + result.Removed.Count;
                    bool ready = pending == 0 && result.Conflicts.Count == 0;
                    return new ToolchainItem("nova-skills", "Nova Skills：让 Agent 按 Nova 流程完成项目任务", ready ? "success" : "blocked",
                        ready ? $"已核对当前 Framework 的全部受管 Skills，{result.Unchanged.Count} 项内容一致。"
                            : $"尚需新增 {result.Added.Count} 项、更新 {result.Updated.Count} 项、移除 {result.Removed.Count} 项；有 {result.Conflicts.Count} 项本地冲突。",
                        ready ? "" : "等待 Unity 完成自动同步；有冲突或未完成事务时，按 Console 提示处理后重新检查。本检查不会覆盖本地内容。");
                }
                catch
                {
                    return new ToolchainItem("nova-skills", "Nova Skills：让 Agent 按 Nova 流程完成项目任务", "blocked",
                        "未能完成当前包来源、Skill 契约和受管同步副本的只读核对。",
                        "先处理 Unity Console 中的包解析、Skill 冲突或未完成同步事务，再重新检查。");
                }
            }

            /// <summary>读取实际注册包元数据，并按当前 Framework 声明核对 Nova MCP 与 Coplay 的精确依赖版本。</summary>
            /// <param name="packages">主线程冻结的注册包快照。</param>
            /// <returns>Coplay Unity MCP 和 Nova MCP 两项结果。</returns>
            private static ToolchainItem[] InspectToolchainPackages(ToolchainPackageSnapshot[] packages)
            {
                bool frameworkReady = TryReadToolchainPackage(packages, c_FrameworkPackageName, out JObject framework);
                bool novaReady = TryReadToolchainPackage(packages, c_ToolchainNovaMcpPackage, out JObject novaMcp);
                bool unityReady = TryReadToolchainPackage(packages, c_ToolchainUnityMcpPackage, out JObject unityMcp);
                string novaExpected = (string)framework?["dependencies"]?[c_ToolchainNovaMcpPackage];
                string unityExpected = (string)novaMcp?["dependencies"]?[c_ToolchainUnityMcpPackage];
                bool novaMatches = frameworkReady && novaReady && !string.IsNullOrEmpty(novaExpected)
                    && string.Equals(novaExpected, (string)novaMcp["version"], StringComparison.Ordinal);
                bool unityMatches = novaMatches && unityReady && !string.IsNullOrEmpty(unityExpected)
                    && string.Equals(unityExpected, (string)unityMcp["version"], StringComparison.Ordinal);
                const string repair = "在 PlugPals 或 Package Manager 中处理包解析问题；确认 Solotopia 和 OpenUPM 来源可用，再等待编译完成后检查。";
                return new[]
                {
                    new ToolchainItem("coplay-unity-mcp", "Coplay Unity MCP：提供 Unity 通用工具", unityMatches ? "success" : "blocked",
                        unityMatches ? $"实际注册的 {c_ToolchainUnityMcpPackage}@{unityExpected} 与 Nova MCP 的依赖要求一致；尚未验证外部连接。"
                            : $"未能确认 {c_ToolchainUnityMcpPackage} 的实际安装身份及依赖版本一致。Unity 官方 com.unity.pipeline 不能替代此依赖。",
                        unityMatches ? "" : repair),
                    new ToolchainItem("nova-mcp", "Nova MCP：提供 Nova 受控项目工具", novaMatches ? "success" : "blocked",
                        novaMatches ? $"实际注册的 {c_ToolchainNovaMcpPackage}@{novaExpected} 与当前 Framework 的依赖要求一致；尚未验证 Agent 可见性。"
                            : $"未能确认 {c_ToolchainNovaMcpPackage} 的实际安装身份及与当前 Framework 的依赖版本一致。",
                        novaMatches ? "" : repair),
                };
            }

            /// <summary>核对唯一注册包及实际 package.json，不使用 manifest 声明或缓存目录代替已安装证据。</summary>
            /// <param name="packages">主线程冻结的注册包。</param>
            /// <param name="name">精确包名。</param>
            /// <param name="metadata">身份、版本均一致的实际元数据。</param>
            /// <returns>唯一注册包无解析错误且实际文件身份一致时为 true。</returns>
            private static bool TryReadToolchainPackage(ToolchainPackageSnapshot[] packages, string name, out JObject metadata)
            {
                metadata = null;
                ToolchainPackageSnapshot[] matches = packages.Where(package => package.Name == name).ToArray();
                if (matches.Length != 1 || matches[0].HasErrors || string.IsNullOrEmpty(matches[0].ResolvedPath)) return false;
                try
                {
                    JObject json = JObject.Parse(File.ReadAllText(Path.Combine(matches[0].ResolvedPath, "package.json")));
                    if ((string)json["name"] != name || (string)json["version"] != matches[0].Version) return false;
                    metadata = json;
                    return true;
                }
                catch
                {
                    return false;
                }
            }

            /// <summary>限时执行 Unity CLI 的版本命令，排除 Unity Editor 版本号和无法识别的输出。</summary>
            /// <param name="processPath">主线程冻结的 PATH。</param>
            /// <param name="userDirectory">当前用户目录。</param>
            /// <param name="programFiles">Windows 程序目录。</param>
            /// <param name="windows">是否为 Windows Editor。</param>
            /// <returns>命令证据；不包含 Editor 连接结论。</returns>
            private static ToolchainItem InspectToolchainUnityCli(string processPath, string userDirectory, string programFiles, bool windows)
            {
                string command = windows ? "unity.exe" : "unity";
                string hub = windows ? Path.Combine(programFiles, "Unity Hub", "resources", "cli", command)
                    : "/Applications/Unity Hub.app/Contents/Resources/cli/unity";
                List<string> candidates = FindToolchainExecutables(processPath, command,
                    Path.Combine(userDirectory, ".unity", "bin", command),
                    Path.Combine(userDirectory, ".local", "bin", command),
                    Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData), "Unity", "bin", command),
                    string.IsNullOrWhiteSpace(System.Environment.GetEnvironmentVariable("UNITY_CLI_HOME")) ? null
                        : Path.Combine(System.Environment.GetEnvironmentVariable("UNITY_CLI_HOME"), "bin", command), hub);
                const string name = "Unity CLI：从命令行操作 Unity";
                const string help = "通过 Unity 官方插件的 Unity CLI 安装说明补齐命令，再重新检查；不要把 Unity Editor 可执行文件当作 Unity CLI。";
                if (candidates.Count == 0) return new ToolchainItem("unity-cli", name, "blocked", "未在 PATH、用户 .unity/bin 或 Unity Hub 的已知位置找到 Unity CLI。", help);
                var timer = Stopwatch.StartNew();
                bool timedOut = false;
                foreach (string executable in candidates)
                {
                    int remaining = c_ToolchainProbeTimeoutMs - (int)timer.ElapsedMilliseconds;
                    if (remaining <= 0) { timedOut = true; break; }
                    ProcessRunner.ProcessResult result = ProcessRunner.RunSync(executable, "--version", remaining);
                    timedOut |= result.TimedOut;
                    string output = result.Stdout.Trim();
                    if (!result.Success || output.Length > 128) continue;
                    Match match = Regex.Match(output, @"\A(?:Unity(?: CLI)?\s+)?v?(?<version>\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?(?:\+[0-9A-Za-z.-]+)?)\z", RegexOptions.IgnoreCase);
                    if (match.Success) return new ToolchainItem("unity-cli", name, "success",
                        $"Unity CLI --version 已正常返回，版本为 {match.Groups["version"].Value}。这只证明命令可用，尚未验证与当前 Editor 的连接。");
                }
                return new ToolchainItem("unity-cli", name, "partial", timedOut ? "Unity CLI 版本检查超时，当前无法确认命令状态。" : "找到了候选命令，但未取得可识别的 Unity CLI 版本。", help);
            }

            /// <summary>查询当前 Codex 的真实插件状态；CLI 缺失、超时或来源不明均不能据此断言插件未安装。</summary>
            /// <param name="processPath">当前 PATH。</param>
            /// <param name="userDirectory">当前用户目录。</param>
            /// <param name="codexHome">尊重 CODEX_HOME 后的 Codex 配置目录。</param>
            /// <param name="projectRoot">当前项目根，用于 Codex 项目配置。</param>
            /// <param name="windows">是否为 Windows Editor。</param>
            /// <returns>当前 Codex 视角的官方 Unity 插件状态。</returns>
            private static ToolchainItem InspectToolchainUnityPlugin(string processPath, string userDirectory, string codexHome, string projectRoot, bool windows)
            {
                const string name = "Unity 官方 Agent 插件：提供 Unity 开发任务说明";
                string command = windows ? "codex.exe" : "codex";
                List<string> candidates = FindToolchainExecutables(processPath, command,
                    Path.Combine(userDirectory, ".local", "bin", command),
                    Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData), "Programs", "OpenAI", "Codex", "bin", command),
                    "/Applications/Codex.app/Contents/Resources/codex");
                const string bundledCodex = "/Applications/ChatGPT.app/Contents/Resources/codex-cli/CodexCLI.app/Contents/MacOS/codex";
                if (!windows && File.Exists(bundledCodex)) candidates.Insert(0, bundledCodex);
                if (candidates.Count == 0) return new ToolchainItem("unity-agent-plugin", name, "partial",
                    "当前未找到可查询插件状态的 Codex CLI；这不能证明 Codex、ChatGPT 或其他 Agent 中没有安装 Unity 插件。", c_UnityPluginInstallHelp);
                ProcessRunner.ProcessResult result = ProcessRunner.RunSync(candidates[0], "-C " + QuoteToolchainArgument(projectRoot) + " plugin list --json", c_ToolchainPluginQueryTimeoutMs);
                if (!result.Success) return new ToolchainItem("unity-agent-plugin", name, "partial",
                    result.TimedOut ? "Codex 插件状态查询超时；安装及启用状态仍待确认。" : "当前 Codex 未能返回插件状态；可能是命令版本、宿主或网络条件限制。", c_UnityPluginInstallHelp);
                try
                {
                    JObject root = JObject.Parse(result.Stdout);
                    List<JObject> entries = ReadToolchainPluginEntries(root);
                    if (entries == null) return new ToolchainItem("unity-agent-plugin", name, "partial", "Codex 返回的插件清单格式尚无法核对，未据此判断安装状态。", c_UnityPluginInstallHelp);
                    List<JObject> unity = entries.Where(entry => (string)entry["name"] == "unity"
                        || ((string)entry["pluginId"] ?? (string)entry["id"] ?? "").StartsWith("unity@", StringComparison.Ordinal)).ToList();
                    if (unity.Count == 0) return new ToolchainItem("unity-agent-plugin", name, "blocked", "当前 Codex 的插件清单未列出 Unity 插件；其他 Agent 的状态未检查。", c_UnityPluginInstallHelp);
                    List<JObject> official = unity.Where(entry => IsOfficialToolchainUnityPlugin(entry, codexHome)).ToList();
                    // 同一插件可能同时出现在 installed 和 available；先查找有效安装，不能被目录中的未安装条目覆盖。
                    if (official.Any(entry => (bool?)entry["installed"] == true && (bool?)entry["enabled"] == true))
                        return new ToolchainItem("unity-agent-plugin", name, "success", "当前 Codex 的插件清单已确认 Unity 官方插件已安装并启用；不代表其 Unity CLI 或 Editor 连接已可用。");
                    List<JObject> installed = official.Where(entry => (bool?)entry["installed"] == true).ToList();
                    bool disabled = installed.Count > 0 && installed.All(entry => (bool?)entry["enabled"] == false);
                    bool absent = official.Count > 0 && official.All(entry => (bool?)entry["installed"] == false);
                    if (disabled || absent)
                        return new ToolchainItem("unity-agent-plugin", name, "blocked", "当前 Codex 的 Unity 官方插件尚未安装或处于禁用状态。", c_UnityPluginInstallHelp);
                    return new ToolchainItem("unity-agent-plugin", name, "partial", official.Count == 0
                        ? "发现同名 Unity 插件，但未能确认其官方来源；未将同名第三方或本地插件当作官方插件。"
                        : "已识别 Unity 官方插件来源，但当前清单未明确确认 installed 和 enabled 状态。", c_UnityPluginInstallHelp);
                }
                catch
                {
                    return new ToolchainItem("unity-agent-plugin", name, "partial", "无法解析当前 Codex 的插件状态；原始输出未写入报告。", c_UnityPluginInstallHelp);
                }
            }

            /// <summary>只读取已知的两种插件清单结构，不递归搜索任意同名对象。</summary>
            /// <param name="root">Codex JSON 输出。</param>
            /// <returns>规范化的插件项；未知结构为 null。</returns>
            private static List<JObject> ReadToolchainPluginEntries(JObject root)
            {
                var entries = new List<JObject>();
                bool recognized = false;
                foreach (string key in new[] { "installed", "available" })
                {
                    if (!(root[key] is JArray array)) continue;
                    recognized = true;
                    foreach (JObject source in array.OfType<JObject>())
                    {
                        var entry = (JObject)source.DeepClone();
                        if (key == "installed" && entry["installed"] == null) entry["installed"] = true;
                        entries.Add(entry);
                    }
                }
                if (root["marketplaces"] is JArray marketplaces)
                {
                    recognized = true;
                    foreach (JObject marketplace in marketplaces.OfType<JObject>())
                    {
                        if (!(marketplace["plugins"] is JArray plugins)) continue;
                        foreach (JObject source in plugins.OfType<JObject>())
                        {
                            var entry = (JObject)source.DeepClone();
                            entry["marketplaceName"] = entry["marketplaceName"] ?? marketplace["name"]?.DeepClone();
                            entry["marketplaceSource"] = entry["marketplaceSource"] ?? marketplace["source"]?.DeepClone();
                            entries.Add(entry);
                        }
                    }
                }
                return recognized ? entries : null;
            }

            /// <summary>区分 Unity 官方市场与 OpenAI 目录来源；缓存元数据只补身份，不能代替当前 installed/enabled 证据。</summary>
            /// <param name="entry">已规范化的插件项。</param>
            /// <param name="codexHome">当前 Codex 配置目录。</param>
            /// <returns>具有可核实官方来源时为 true。</returns>
            private static bool IsOfficialToolchainUnityPlugin(JObject entry, string codexHome)
            {
                string id = (string)entry["pluginId"] ?? (string)entry["id"] ?? "";
                string marketplace = (string)entry["marketplaceName"] ?? (id.Contains("@") ? id.Substring(id.IndexOf('@') + 1) : "");
                JToken source = entry["marketplaceSource"];
                string url = source?.Type == JTokenType.String ? (string)source : (string)source?["url"] ?? (string)source?["repo"] ?? (string)source?["source"];
                if (marketplace == "unity-agent-plugin" && !string.IsNullOrEmpty(url))
                {
                    string normalized = url.TrimEnd('/');
                    if (normalized.EndsWith(".git", StringComparison.OrdinalIgnoreCase)) normalized = normalized.Substring(0, normalized.Length - 4);
                    if (normalized.Equals("https://github.com/Unity-Technologies/unity-agent-plugin", StringComparison.OrdinalIgnoreCase)
                        || normalized.Equals("Unity-Technologies/unity-agent-plugin", StringComparison.OrdinalIgnoreCase)) return true;
                }
                if (marketplace != "openai-curated-remote") return false;
                string version = (string)entry["localVersion"] ?? (string)entry["version"];
                if (string.IsNullOrEmpty(version) || !Regex.IsMatch(version, @"\A[0-9A-Za-z][0-9A-Za-z.+-]{0,79}\z")) return false;
                try
                {
                    string directory = Path.Combine(codexHome, "plugins", "cache", marketplace, "unity", version);
                    string manifest = Path.Combine(directory, "plugin.json");
                    if (!File.Exists(manifest)) manifest = Path.Combine(directory, ".codex-plugin", "plugin.json");
                    JObject metadata = JObject.Parse(File.ReadAllText(manifest));
                    return (string)metadata["name"] == "unity" && (string)metadata["version"] == version
                        && (string)metadata["author"]?["name"] == "Unity Technologies";
                }
                catch
                {
                    return false;
                }
            }

            /// <summary>依次收集 PATH 与有限已知位置中真实存在的命令文件，不执行 shell 或扫描整台机器。</summary>
            /// <param name="processPath">当前 PATH。</param>
            /// <param name="command">精确命令文件名。</param>
            /// <param name="knownPaths">用户工具和已知应用位置。</param>
            /// <returns>去重后的候选文件路径。</returns>
            private static List<string> FindToolchainExecutables(string processPath, string command, params string[] knownPaths)
            {
                var candidates = new List<string>();
                foreach (string directory in processPath.Split(Path.PathSeparator))
                {
                    if (string.IsNullOrWhiteSpace(directory)) continue;
                    try
                    {
                        string candidate = Path.Combine(directory.Trim('"'), command);
                        if (File.Exists(candidate) && !candidates.Contains(candidate)) candidates.Add(candidate);
                    }
                    catch (ArgumentException) { }
                }
                foreach (string candidate in knownPaths)
                {
                    if (!string.IsNullOrWhiteSpace(candidate) && File.Exists(candidate) && !candidates.Contains(candidate)) candidates.Add(candidate);
                }
                return candidates;
            }

            /// <summary>为直接传给外部进程的单个路径参数加引号，不经过 shell 展开。</summary>
            /// <param name="value">项目根路径。</param>
            /// <returns>兼容空格及引号的参数。</returns>
            private static string QuoteToolchainArgument(string value)
            {
                var builder = new StringBuilder("\"");
                int backslashes = 0;
                foreach (char character in value)
                {
                    if (character == '\\')
                    {
                        backslashes++;
                        continue;
                    }
                    // ProcessStartInfo 使用引号分组；仅在双引号前和参数末尾加倍反斜杠。
                    builder.Append('\\', character == '"' ? backslashes * 2 + 1 : backslashes);
                    builder.Append(character);
                    backslashes = 0;
                }
                builder.Append('\\', backslashes * 2).Append('"');
                return builder.ToString();
            }
        }
    }
}
