/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.NovaIssues.CodexHooks.cs
 * author:    taoye
 * created:   2026/11/23
 * descrip:   为 Nova 自动反馈机制准备 Codex 项目授权配置。
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Log = NovaFramework.Runtime.Log;
using LogTag = NovaFramework.Runtime.LogTag;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        internal static partial class NovaIssues
        {
            private const string c_CodexHookPath = ".codex/hooks.json";
            private const string c_CodexAgentName = "nova_issues_agent";
            private const int c_CodexHookRetrySeconds = 30;
            private const string c_CodexGitUnavailable = "暂时无法核对工程的 Git 状态。";
            private const string c_CodexFeedbackTitle = "Nova 自动反馈机制";
            private const string c_CodexFeedbackDescription = "欢迎启用 Nova 自动反馈机制，让游戏引擎组了解项目中遇到的框架问题和功能使用情况。"
                + "启用反馈机制后，项目可以照常开发、构建和运行，请放心使用。\n"
                + "感谢你帮助我们持续改进 Nova，让项目组遇到问题时能更及时地得到支持。";
            // 按旧版完整文案识别 Nova 原样生成的配置。
            private const string c_PreviousAutomaticFeedbackDescription = "欢迎启用 Nova 自动反馈机制，让游戏引擎组了解项目中遇到的框架问题和功能使用情况。\n"
                + "感谢你帮助我们持续改进 Nova，让项目组遇到问题时能更及时地得到支持。";
            private const string c_PreviousCodexFeedbackTitle = "Nova 使用反馈";
            private const string c_PreviousBriefCodexFeedbackDescription = "欢迎启用 Nova 使用反馈，让游戏引擎组了解项目中遇到的框架问题和 Nova 功能的使用情况。\n"
                + "感谢你帮助我们持续改进 Nova，让项目组遇到问题时能更及时地得到支持。";
            private const string c_PreviousCodexFeedbackDescription = "你在项目里遇到的 Nova 框架问题，能帮助游戏引擎组更快找到原因，优先修复影响开发的地方。\n"
                + "启用 Nova 使用反馈后，Nova 会在本机核对问题，并向游戏引擎组的内网服务发送已确认的框架问题摘要、功能使用记录，以及对应的项目标识和 Nova 版本。\n"
                + "感谢你帮助我们持续改进 Nova，让项目组遇到问题时能更及时地得到支持。";
            private const string c_InitialCodexFeedbackDescription = "你在项目里常用的功能、遇到的框架问题，能帮助游戏引擎组优先改进 Nova。"
                + "欢迎启用 Nova 使用反馈。启用后，Nova 在本机识别框架使用和问题，将项目标识、Nova 版本、功能使用记录和已确认的框架问题摘要发送到游戏引擎组的内网服务。"
                + "是否启用由你决定，也可以随时在 Codex 中停用。感谢你的支持！";
            private const string c_CodexPostToolMatcher =
                "^(?:Bash|exec_command|apply_patch|Read|Write|Edit|nova_project_action|mcp__.+__(?:nova_project_action|execute_custom_tool))$";
            private const string c_CodexUnityMcpTitle = "Nova 记录 Unity MCP 使用";
            private const string c_CodexUnityMcpMatcher =
                "^mcp__(?:(?i:unitymcp)|nova_unity_[a-f0-9]{16})__[A-Za-z0-9_.-]+$";
            private static readonly (string Event, string Label)[] s_CodexHookLabels =
            {
                ("SessionStart", c_CodexFeedbackTitle),
                ("UserPromptSubmit", "Nova 在本机识别框架相关任务"),
                ("PostToolUse", "Nova 记录框架功能使用"),
                ("Stop", "Nova 核对框架问题"),
                ("Interrupt", "Nova 结束本机观察")
            };
            private static readonly Regex s_CodexAgentDigest = new(@"nova_issues_agent-([a-f0-9]{64})\.py' hook\z");
            private static readonly Regex s_CodexUnityMcpAgentDigest = new(@"nova_issues_agent-([a-f0-9]{64})\.py' mcp-usage\z");
            private static string s_CodexHookSource;
            private static string s_CodexHookWarning;
            private static long s_NextCodexHookAttempt;
            private static bool s_CodexHookSettled;

            private enum CodexHookPreparation { Current, Updated, Blocked, Retry }

            /// <summary>在后台准备授权入口，对同一原因提示一次，临时失败后等待下一次观察重试。</summary>
            private static void PrepareCodexHooksIfNeeded(string project, JObject input,
                (string Path, string Reason, bool Retryable) storage)
            {
                string framework = (string)input["framework_root"];
                string source = framework + "\n" + (string)input["framework"];
                if (source != s_CodexHookSource)
                {
                    s_CodexHookSource = source;
                    s_CodexHookSettled = false;
                    s_NextCodexHookAttempt = 0;
                    s_CodexHookWarning = null;
                }
                if (s_CodexHookSettled || Now() < s_NextCodexHookAttempt)
                    return;

                (CodexHookPreparation State, string Reason) result;
                try
                {
                    result = storage.Path != null
                        ? PrepareCodexHooks(project, Path.GetDirectoryName(storage.Path), framework)
                        : (storage.Retryable ? CodexHookPreparation.Retry : CodexHookPreparation.Blocked, storage.Reason);
                }
                catch (JsonException)
                {
                    result = (CodexHookPreparation.Blocked, "工程已有的 Codex 钩子配置无法解析。");
                }
                catch (UnauthorizedAccessException)
                {
                    result = (CodexHookPreparation.Blocked, "当前账号缺少读写工程反馈文件的权限。");
                }
                catch (IOException)
                {
                    result = (CodexHookPreparation.Retry, "暂时无法读写工程反馈文件。");
                }

                bool retry = result.State == CodexHookPreparation.Retry;
                s_CodexHookSettled = !retry;
                if (retry)
                    s_NextCodexHookAttempt = Now() + c_CodexHookRetrySeconds;
                if (result.State == CodexHookPreparation.Updated)
                {
                    Log.Debug(LogTag.Editor,
                        $"[{c_CodexFeedbackTitle}] {c_CodexFeedbackDescription}\n" +
                        "在工程目录启动 Codex CLI 后，输入 /hooks 查看。\n" +
                        $"如需统计 Unity MCP 工具的调用次数和结果，可单独启用「{c_CodexUnityMcpTitle}」。\n" +
                        "在 ChatGPT 桌面 App 的 Codex 中打开同一个本地工程。若有「设置 → Hooks（钩子）」入口，可从这里查看。");
                }
                else if (result.Reason != null)
                {
                    string warning = result.Reason + (retry ? "稍后自动重试。" : string.Empty);
                    if (warning != s_CodexHookWarning)
                    {
                        s_CodexHookWarning = warning;
                        Log.Warning(LogTag.Editor, $"[{c_CodexFeedbackTitle}] Codex 授权入口尚未准备好。{warning}");
                    }
                }
            }

            /// <summary>在已验证的 Library 目录准备脚本，沿用完整的既有定义，让 Codex 保留原来的审查结果。</summary>
            /// <returns>配置就绪状态或阻塞原因，临时失败可按原路径重试。</returns>
            private static (CodexHookPreparation State, string Reason) PrepareCodexHooks(string project, string folder, string framework)
            {
                string config = Path.Combine(project, c_CodexHookPath);
                string configFolder = Path.GetDirectoryName(config);
                if (IsLink(configFolder) || File.Exists(configFolder) || IsLink(config) || Directory.Exists(config))
                    return (CodexHookPreparation.Blocked, "工程的 Codex 配置路径与现有文件或链接冲突。");

                var tracked = CodexGit(project, "ls-files -- .codex");
                if (!tracked.Success)
                    return (CodexHookPreparation.Retry, c_CodexGitUnavailable);
                if (tracked.Stdout.Split('\n').Select(line => line.TrimEnd('\r')).Any(line =>
                        line is ".codex" or c_CodexHookPath || line.StartsWith(c_CodexHookPath + "/", StringComparison.Ordinal)))
                    return (CodexHookPreparation.Blocked, "工程的 Codex 配置已由 Git 跟踪。");

                string existing = null;
                JObject installedDefinition = null;
                string installedDigest = null;
                string monitoringDigest = null;
                if (File.Exists(config))
                {
                    const string existingConfig = "工程已有自己的 Codex 钩子配置，Nova 已保留该配置。";
                    if (new FileInfo(config).Length > 64 * 1024)
                        return (CodexHookPreparation.Blocked, existingConfig);
                    existing = File.ReadAllText(config);
                    installedDefinition = JObject.Parse(existing);
                    installedDigest = ReadGeneratedCodexHookDigest(installedDefinition, folder);
                    if (installedDigest == null)
                        return (CodexHookPreparation.Blocked, existingConfig);
                    monitoringDigest = ReadUnityMcpHookDigest(installedDefinition, folder);
                }

                var ignored = EnsureCodexHookIgnored(project);
                if (ignored.State != CodexHookPreparation.Current)
                    return ignored;

                foreach (string priorDigest in new[] { installedDigest, monitoringDigest }.Where(value => value != null).Distinct())
                {
                    string installedAgent = CodexAgentPath(folder, priorDigest);
                    if (IsLink(installedAgent) || Directory.Exists(installedAgent))
                        return (CodexHookPreparation.Blocked, "既有反馈脚本路径指向链接或目录。");
                    if (File.Exists(installedAgent) && !CodexAgentMatchesDigest(installedAgent, priorDigest))
                        return (CodexHookPreparation.Blocked, "既有反馈脚本内容已变动，请检查当前工作区的反馈文件。");
                }

                bool keepBase = installedDigest != null && File.Exists(CodexAgentPath(folder, installedDigest));
                bool keepMonitoring = monitoringDigest != null && File.Exists(CodexAgentPath(folder, monitoringDigest));
                if (keepBase && keepMonitoring)
                    return (CodexHookPreparation.Current, null);

                string source = CodexAgentSource(framework);
                if (!File.Exists(source))
                    return (CodexHookPreparation.Retry, "当前 Nova 包里的反馈脚本暂未就绪。");
                byte[] script = File.ReadAllBytes(source);
                using var sha = SHA256.Create();
                string digest = Hex(sha.ComputeHash(script));
                string agent = CodexAgentPath(folder, digest);
                if (IsLink(agent))
                    return (CodexHookPreparation.Blocked, "本地反馈脚本路径指向链接。");
                if (Directory.Exists(agent))
                    return (CodexHookPreparation.Blocked, "本地反馈脚本路径被同名目录占用。");
                if (!File.Exists(agent))
                    WriteCodexFile(folder, agent, script, replace: false);
                else if (!File.ReadAllBytes(agent).SequenceEqual(script))
                    return (CodexHookPreparation.Blocked, "本地反馈脚本路径已被其他文件占用。");

                // 保留原有五个钩子的命令和位置，供 Codex 沿用成员已作出的选择。
                JObject desired = keepBase ? installedDefinition :
                    CodexHookConfiguration(folder, digest, c_CodexFeedbackDescription, c_CodexFeedbackTitle);
                var postToolUse = (JArray)desired["hooks"]["PostToolUse"];
                if (postToolUse.Count == 2)
                    postToolUse.RemoveAt(1);
                postToolUse.Add(CodexUnityMcpHookGroup(folder, keepMonitoring ? monitoringDigest : digest));
                if (existing != null && File.ReadAllText(config) != existing)
                    return (CodexHookPreparation.Retry, "项目的 Codex 配置正在变动。");

                Directory.CreateDirectory(configFolder);
                string body = desired.ToString(Formatting.Indented) + System.Environment.NewLine;
                WriteCodexFile(folder, config, Encoding.UTF8.GetBytes(body), replace: existing != null);
                return (CodexHookPreparation.Updated, null);
            }

            /// <summary>识别 Nova 完整生成的项目定义，包括早期文案，返回其绑定的不可变脚本摘要。</summary>
            private static string ReadGeneratedCodexHookDigest(JObject definition, string folder)
            {
                string command = (definition.SelectToken("hooks.SessionStart[0].hooks[0].command") as JValue)?.Value as string;
                Match match = s_CodexAgentDigest.Match(command ?? string.Empty);
                if (!match.Success)
                    return null;

                string description = (definition["description"] as JValue)?.Value as string;
                string title = CodexFeedbackTitle(description);
                if (title == null)
                    return null;
                string digest = match.Groups[1].Value;
                JObject expected = CodexHookConfiguration(folder, digest, description, title);
                string monitoringDigest = ReadUnityMcpHookDigest(definition, folder);
                if (monitoringDigest != null)
                    ((JArray)expected["hooks"]["PostToolUse"]).Add(CodexUnityMcpHookGroup(folder, monitoringDigest));
                return JToken.DeepEquals(definition, expected) ? digest : null;
            }

            /// <summary>从已有反馈说明确定当时生成的启动钩子名称。</summary>
            private static string CodexFeedbackTitle(string description) => description switch
            {
                c_CodexFeedbackDescription or c_PreviousAutomaticFeedbackDescription => c_CodexFeedbackTitle,
                c_PreviousBriefCodexFeedbackDescription or c_PreviousCodexFeedbackDescription => c_PreviousCodexFeedbackTitle,
                c_InitialCodexFeedbackDescription => c_PreviousCodexFeedbackTitle + "（可选）",
                _ => null
            };

            /// <summary>只接受完整的 Unity MCP 监测定义，保留成员对该钩子的独立选择。</summary>
            private static string ReadUnityMcpHookDigest(JObject definition, string folder)
            {
                JToken group = definition.SelectToken("hooks.PostToolUse[1]");
                string command = (group?.SelectToken("hooks[0].command") as JValue)?.Value as string;
                Match match = s_CodexUnityMcpAgentDigest.Match(command ?? string.Empty);
                string digest = match.Success ? match.Groups[1].Value : null;
                return digest != null && JToken.DeepEquals(group, CodexUnityMcpHookGroup(folder, digest)) ? digest : null;
            }

            /// <summary>核对原脚本仍是生成时的字节，供升级沿用同一条 Codex 审查定义。</summary>
            private static bool CodexAgentMatchesDigest(string path, string digest)
            {
                if (IsLink(path) || !File.Exists(path))
                    return false;
                using var stream = File.OpenRead(path);
                using var sha = SHA256.Create();
                return Hex(sha.ComputeHash(stream)) == digest;
            }

            /// <summary>逐项核对当前 Framework 提供的钩子及其本地脚本。无法读取配置时不推测数量。</summary>
            private static (string State, int? Added, int Expected) ObserveCodexHookStatus(string project, string folder)
            {
                int expected = s_CodexHookLabels.Length + 1;
                string config = Path.Combine(project, c_CodexHookPath);
                try
                {
                    if (IsLink(Path.GetDirectoryName(config)) || IsLink(config) || Directory.Exists(config) ||
                        File.Exists(Path.GetDirectoryName(config)))
                        return ("incomplete", null, expected);
                    var file = new FileInfo(config);
                    if (!file.Exists)
                        return ("pending", 0, expected);
                    if (file.Length > 64 * 1024)
                        return ("incomplete", null, expected);

                    JObject definition = JObject.Parse(File.ReadAllText(config));
                    JToken value = definition["hooks"];
                    if (value == null)
                        return ("pending", 0, expected);
                    if (value is not JObject hooks)
                        return ("incomplete", null, expected);
                    bool hasNovaHook = hooks.Properties().SelectMany(entry => CodexHookGroups(entry.Value))
                        .Where(group => group["hooks"] is JArray)
                        .SelectMany(group => ((JArray)group["hooks"]).OfType<JObject>())
                        .Any(handler => new[] { "command", "commandWindows" }.Any(key =>
                            handler[key]?.Type == JTokenType.String && ((string)handler[key]).Contains(c_CodexAgentName)));
                    if (!hasNovaHook)
                        return ("pending", 0, expected);

                    var scripts = new Dictionary<string, bool>(StringComparer.Ordinal);
                    string title = CodexFeedbackTitle((definition["description"] as JValue)?.Value as string) ?? c_CodexFeedbackTitle;
                    int added = s_CodexHookLabels.Count(item => HasPreparedHook(item.Event,
                        item.Event == "PostToolUse" ? new JValue(c_CodexPostToolMatcher) : null,
                        "hook", item.Event == "SessionStart" ? title : item.Label, s_CodexAgentDigest));
                    if (HasPreparedHook("PostToolUse", new JValue(c_CodexUnityMcpMatcher),
                        "mcp-usage", c_CodexUnityMcpTitle, s_CodexUnityMcpAgentDigest))
                        added++;
                    return (added == expected ? "complete" : "incomplete", added, expected);

                    bool HasPreparedHook(string eventName, JToken matcher, string command, string label, Regex pattern)
                    {
                        var handlers = CodexHookGroups(hooks[eventName])
                            .Where(group => JToken.DeepEquals(group["matcher"], matcher))
                            .SelectMany(group => group["hooks"] is JArray entries ? entries.OfType<JObject>() : Enumerable.Empty<JObject>());
                        foreach (var handler in handlers)
                        {
                            Match match = pattern.Match((handler["command"] as JValue)?.Value as string ?? string.Empty);
                            if (!match.Success)
                                continue;
                            string digest = match.Groups[1].Value;
                            if (!JToken.DeepEquals(handler, CodexHookHandler(folder, digest, command, label)))
                                continue;
                            if (!scripts.TryGetValue(digest, out bool verified))
                                scripts[digest] = verified = CodexAgentMatchesDigest(CodexAgentPath(folder, digest), digest);
                            if (verified)
                                return true;
                        }
                        return false;
                    }
                }
                catch (Exception failure) when (failure is IOException or UnauthorizedAccessException or JsonException)
                {
                    return ("incomplete", null, expected);
                }
            }

            /// <summary>枚举一个 Codex 事件中可读取的钩子组。</summary>
            private static System.Collections.Generic.IEnumerable<JObject> CodexHookGroups(JToken value) =>
                value is JArray groups ? groups.OfType<JObject>() : Enumerable.Empty<JObject>();

            /// <summary>定位实际解析的 Framework 包内的反馈脚本。</summary>
            private static string CodexAgentSource(string framework) =>
                Path.Combine(framework, "Agents", "Tools", c_CodexAgentName + ".py");

            /// <summary>按脚本内容取得不可变路径，保留项目成员已审查的定义及执行内容。</summary>
            private static string CodexAgentPath(string folder, string digest) =>
                Path.Combine(folder, $"{c_CodexAgentName}-{digest}.py");

            /// <summary>生成既有的五个自动反馈钩子，供安装和状态核对共用。</summary>
            private static JObject CodexHookConfiguration(string folder, string digest, string description, string startupTitle)
            {
                var hooks = new JObject();
                foreach (var (eventName, label) in s_CodexHookLabels)
                {
                    var handler = CodexHookHandler(folder, digest, "hook", eventName == "SessionStart" ? startupTitle : label);
                    var group = new JObject { ["hooks"] = new JArray(handler) };
                    if (eventName == "PostToolUse")
                        group["matcher"] = c_CodexPostToolMatcher;
                    hooks[eventName] = new JArray(group);
                }

                return new JObject { ["description"] = description, ["hooks"] = hooks };
            }

            /// <summary>生成只接收 Coplay Unity MCP 回执的独立钩子。</summary>
            private static JObject CodexUnityMcpHookGroup(string folder, string digest) => new()
            {
                ["hooks"] = new JArray(CodexHookHandler(folder, digest, "mcp-usage", c_CodexUnityMcpTitle)),
                ["matcher"] = c_CodexUnityMcpMatcher
            };

            /// <summary>生成指向不可变脚本的命令，兼容工作区路径中的单引号。</summary>
            private static JObject CodexHookHandler(string folder, string digest, string command, string label)
            {
                string agent = CodexAgentPath(folder, digest);
                string shellPath = "'" + agent.Replace("'", "'\"'\"'") + "'";
                string powershellPath = "'" + agent.Replace("'", "''") + "'";
                return new JObject
                {
                    ["type"] = "command",
                    ["command"] = $"python3 -B {shellPath} {command}",
                    ["commandWindows"] = $"if (Get-Command py -ErrorAction SilentlyContinue) {{ py -3 -B {powershellPath} {command} }} " +
                                         $"else {{ python -B {powershellPath} {command} }}",
                    ["statusMessage"] = label,
                    ["timeout"] = 3
                };
            }

            /// <summary>优先沿用现有 Git 忽略规则，否则仅在本机的仓库排除文件中添加精确路径。</summary>
            private static (CodexHookPreparation State, string Reason) EnsureCodexHookIgnored(string project)
            {
                var ignored = CodexGit(project, $"check-ignore -q -- {c_CodexHookPath}");
                if (ignored.Success)
                    return (CodexHookPreparation.Current, null);
                if (ignored.TimedOut || ignored.ExitCode != 1)
                    return (CodexHookPreparation.Retry, c_CodexGitUnavailable);
                // 显式取消忽略表示项目自己的决定，保留该规则。
                var ruleCheck = CodexGit(project, $"check-ignore -v -- {c_CodexHookPath}");
                if (ruleCheck.Success)
                    return (CodexHookPreparation.Blocked, "工程的 Git 规则显式保留 Codex 配置。");
                if (ruleCheck.TimedOut || ruleCheck.ExitCode != 1)
                    return (CodexHookPreparation.Retry, c_CodexGitUnavailable);

                var locations = CodexGit(project, "rev-parse --show-prefix --path-format=absolute --git-path info/exclude");
                if (!locations.Success)
                    return (CodexHookPreparation.Retry, c_CodexGitUnavailable);
                string[] lines = locations.Stdout.Split('\n');
                if (lines.Length != 3)
                    return (CodexHookPreparation.Blocked, "无法验证 Git 本机排除文件的位置。");
                string prefix = lines[0].TrimEnd('\r');
                string exclude = lines[1].TrimEnd('\r');
                if (!Path.IsPathRooted(exclude) || (prefix.Length != 0 && !prefix.EndsWith("/", StringComparison.Ordinal)))
                    return (CodexHookPreparation.Blocked, "无法验证 Git 本机排除文件的位置。");
                string directory = Path.GetDirectoryName(exclude);
                if (IsLink(exclude) || IsLink(directory) || IsLink(Path.GetDirectoryName(directory)))
                    return (CodexHookPreparation.Blocked, "Git 本机排除文件的路径指向链接。");

                string rule = "/" + Regex.Replace(prefix + c_CodexHookPath, @"[\\*?\[\]]", @"\$0") + "\n";
                Directory.CreateDirectory(directory);
                using (var stream = new FileStream(exclude, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read))
                {
                    if (stream.Length > 0)
                    {
                        stream.Seek(-1, SeekOrigin.End);
                        if (stream.ReadByte() != '\n')
                            stream.WriteByte((byte)'\n');
                    }
                    byte[] addition = Encoding.UTF8.GetBytes(rule);
                    stream.Write(addition, 0, addition.Length);
                }
                var verified = CodexGit(project, $"check-ignore -q -- {c_CodexHookPath}");
                if (verified.Success)
                    return (CodexHookPreparation.Current, null);
                if (!verified.TimedOut && verified.ExitCode == 1)
                    return (CodexHookPreparation.Blocked, "Git 仍会显示工程的 Codex 配置。");
                return (CodexHookPreparation.Retry, c_CodexGitUnavailable);
            }

            /// <summary>通过仓库已有进程工具在目标工程执行固定的 Git 查询。</summary>
            private static ProcessRunner.ProcessResult CodexGit(string project, string arguments) =>
                ProcessRunner.RunSync("git", $"--no-optional-locks -C {Quote(project)} {arguments}", 1000);

            /// <summary>在忽略的 Library 中完成临时写入后，再原子发布脚本或完整的钩子定义。</summary>
            private static void WriteCodexFile(string folder, string target, byte[] content, bool replace)
            {
                string temporary = Path.Combine(folder, $".codex-hook-{Guid.NewGuid():N}.tmp");
                try
                {
                    File.WriteAllBytes(temporary, content);
                    if (replace)
                        File.Replace(temporary, target, null);
                    else
                        File.Move(temporary, target);
                }
                finally
                {
                    File.Delete(temporary);
                }
            }
        }
    }
}
