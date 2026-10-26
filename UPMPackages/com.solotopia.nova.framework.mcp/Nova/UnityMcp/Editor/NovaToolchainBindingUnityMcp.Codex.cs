/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  NovaToolchainBindingUnityMcp.Codex.cs
 * author:    taoye
 * created:   2026/10/26
 * descrip:   当前项目 Codex MCP 配置的保守合并与原子保存
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using MCPForUnity.External.Tommy;

namespace NovaFramework.Mcp.UnityMcp.Editor
{
    internal static partial class NovaToolchainBindingUnityMcp
    {
        /// <summary>复用已匹配的绑定，否则只更新当前项目专属受管区块；不覆盖全局或其他项目的配置。</summary>
        /// <param name="projectRoot">当前项目根。</param>
        /// <param name="codexHome">尊重 CODEX_HOME 后的用户配置目录，只读。</param>
        /// <param name="endpoint">HTTP 精确地址，stdio 时为空。</param>
        /// <param name="runtime">已验证的 stdio 运行时。</param>
        /// <param name="arguments">stdio 固定参数。</param>
        /// <param name="cancellationToken">写入前核对的中断信号。</param>
        /// <returns>已经完成的配置动作，不推断当前宿主是否已加载。</returns>
        private static string EnsureCodexConfiguration(string projectRoot, string codexHome, string endpoint, string runtime, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
        {
            string directory = Path.Combine(projectRoot, ".codex");
            string path = Path.Combine(directory, "config.toml");
            RejectConfigurationLink(directory);
            RejectConfigurationLink(path);
            string existing = ReadBoundedConfiguration(path);
            TomlTable local = ParseBindingConfiguration(existing);
            TomlTable global = ParseBindingConfiguration(ReadBoundedConfiguration(Path.Combine(codexHome, "config.toml")));
            TomlTable localServers = GetBindingTable(local, "mcp_servers");
            TomlTable globalServers = GetBindingTable(global, "mcp_servers");
            if (IsProjectExplicitlyUntrusted(global, projectRoot)) throw new InvalidOperationException("当前宿主明确拒绝信任此项目，不能绕过宿主策略。");

            if (localServers != null && localServers.Keys.Any(key => MatchesBinding(localServers[key], endpoint, runtime, arguments)))
                return "已复用当前项目中精确匹配的 Codex MCP 绑定，未改写其他配置。";
            if (globalServers != null && globalServers.Keys.Any(key => (localServers == null || !localServers.HasKey(key))
                && MatchesBinding(globalServers[key], endpoint, runtime, arguments)))
                return "已复用 Codex 用户配置中的精确绑定，未改写全局 unityMCP 或其他项目。";
            if (endpoint != null && (!Uri.TryCreate(endpoint, UriKind.Absolute, out Uri endpointUri)
                || !endpointUri.IsLoopback || !string.IsNullOrEmpty(endpointUri.UserInfo)))
                throw new InvalidOperationException("远程服务缺少可复用的精确绑定，不能把其认证复制到项目配置或生成未经认证验证的绑定。");

            string id = StableBindingId(projectRoot);
            string serverName = "nova_unity_" + id;
            string begin = "# BEGIN NOVA UNITY MCP " + id;
            string end = "# END NOVA UNITY MCP " + id;
            int start = existing.IndexOf(begin, StringComparison.Ordinal);
            int finish = existing.IndexOf(end, StringComparison.Ordinal);
            if ((start < 0) != (finish < 0) || (start >= 0 && (finish < start
                || existing.IndexOf(begin, start + begin.Length, StringComparison.Ordinal) >= 0
                || existing.IndexOf(end, finish + end.Length, StringComparison.Ordinal) >= 0)))
                throw new InvalidOperationException("已有受管配置区块不完整，不能覆盖现场。");
            if (start < 0 && localServers != null && localServers.HasKey(serverName))
                throw new InvalidOperationException("项目专属服务器名称已由其他配置使用。");

            var server = new TomlTable();
            server["enabled"] = true;
            server["startup_timeout_sec"] = 120;
            if (endpoint != null) server["url"] = endpoint;
            else
            {
                server["command"] = runtime;
                var values = new TomlArray();
                foreach (string value in arguments) values.Add(value);
                server["args"] = values;
            }
            var servers = new TomlTable();
            servers[serverName] = server;
            var generated = new TomlTable();
            generated["mcp_servers"] = servers;
            using var writer = new StringWriter();
            generated.WriteTo(writer);
            string block = begin + "\n" + writer.ToString().TrimEnd() + "\n" + end;
            string merged = start >= 0
                ? existing.Substring(0, start) + block + existing.Substring(finish + end.Length)
                : existing + (existing.EndsWith("\n", StringComparison.Ordinal) || existing.Length == 0 ? "" : "\n") + "\n" + block + "\n";
            ParseBindingConfiguration(merged);
            cancellationToken.ThrowIfCancellationRequested();
            Directory.CreateDirectory(directory);
            RejectConfigurationLink(directory);
            RejectConfigurationLink(path);
            if (!string.Equals(existing, ReadBoundedConfiguration(path), StringComparison.Ordinal)) throw new InvalidOperationException("配置在自动合并期间已变化，稍后重新读取。");
            WriteBindingConfiguration(path, merged, cancellationToken);
            if (!MatchesBinding(GetBindingTable(ParseBindingConfiguration(ReadBoundedConfiguration(path)), "mcp_servers")?[serverName], endpoint, runtime, arguments))
                throw new InvalidOperationException("自动保存后的绑定核对失败。");
            return "已自动保存并核对当前项目的 Codex MCP 绑定，保留所有非受管内容；宿主将在加载项目配置时连接。";
        }

        /// <summary>核对传输目标和启用状态；同名、同端口提示或存在配置文件都不能替代精确匹配。</summary>
        /// <param name="node">一个服务器配置。</param>
        /// <param name="endpoint">当前 HTTP 地址。</param>
        /// <param name="runtime">当前 stdio 命令。</param>
        /// <param name="arguments">当前 stdio 参数。</param>
        /// <returns>有效启用且传输精确匹配时为 true。</returns>
        private static bool MatchesBinding(TomlNode node, string endpoint, string runtime, IReadOnlyList<string> arguments)
        {
            if (!(node is TomlTable server)) return false;
            if (server.TryGetNode("enabled", out TomlNode enabled) && (!enabled.IsBoolean || !enabled.AsBoolean.Value)) return false;
            if (endpoint != null)
            {
                return server.TryGetNode("url", out TomlNode url) && url.IsString && string.Equals(url.AsString.Value, endpoint, StringComparison.Ordinal)
                    && !server.HasKey("command");
            }
            if (string.IsNullOrEmpty(runtime) || arguments == null || !server.TryGetNode("command", out TomlNode command)
                || !command.IsString || !string.Equals(command.AsString.Value, runtime, StringComparison.Ordinal)
                || !server.TryGetNode("args", out TomlNode args) || !(args is TomlArray array) || array.ChildrenCount != arguments.Count) return false;
            return array.Children.Select(value => value.IsString ? value.AsString.Value : null).SequenceEqual(arguments);
        }

        /// <summary>读取有限大小的 TOML，空文件保持有效；不把无权限或损坏文件当作空配置覆盖。</summary>
        /// <param name="path">精确配置路径。</param>
        /// <returns>原始配置文本。</returns>
        private static string ReadBoundedConfiguration(string path)
        {
            if (!File.Exists(path)) return "";
            if (new FileInfo(path).Length > 1024 * 1024) throw new InvalidOperationException("宿主配置超过本次自动合并的大小限制。");
            return File.ReadAllText(path, Encoding.UTF8);
        }

        /// <summary>严格解析 TOML；解析异常交由中立层脱敏为 blocked，不能丢弃已有内容。</summary>
        /// <param name="content">完整配置文本。</param>
        /// <returns>原厂随包解析器产生的配置树。</returns>
        private static TomlTable ParseBindingConfiguration(string content)
        {
            using var reader = new StringReader(content);
            return TOML.Parse(reader);
        }

        /// <summary>读取一个明确表节点，已存在但类型错误时阻断自动合并。</summary>
        /// <param name="table">父表。</param>
        /// <param name="key">精确表名。</param>
        /// <returns>子表或不存在时的 null。</returns>
        private static TomlTable GetBindingTable(TomlTable table, string key)
        {
            if (!table.TryGetNode(key, out TomlNode node)) return null;
            return node as TomlTable ?? throw new InvalidOperationException("宿主配置表类型不正确。");
        }

        /// <summary>尊重宿主已经明确设置的项目不信任状态，不自动提升信任权限。</summary>
        /// <param name="global">用户级 Codex 配置。</param>
        /// <param name="projectRoot">当前项目根。</param>
        /// <returns>明确标记 untrusted 时为 true。</returns>
        private static bool IsProjectExplicitlyUntrusted(TomlTable global, string projectRoot)
        {
            TomlTable projects = GetBindingTable(global, "projects");
            return projects != null && projects.TryGetNode(projectRoot, out TomlNode project) && project is TomlTable settings
                && settings.TryGetNode("trust_level", out TomlNode trust) && trust.IsString && trust.AsString.Value == "untrusted";
        }

        /// <summary>拒绝通过项目配置符号链接写入外部或共享配置，避免影响其他项目。</summary>
        /// <param name="path">配置目录或文件。</param>
        private static void RejectConfigurationLink(string path)
        {
            if ((Directory.Exists(path) || File.Exists(path)) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException("项目配置是外部链接，不能自动覆盖共享目标。");
        }

        /// <summary>用同目录临时文件原子替换，并在成功、取消或失败后清理本次临时文件。</summary>
        /// <param name="path">目标配置。</param>
        /// <param name="content">已经解析核对的合并内容。</param>
        /// <param name="cancellationToken">写入前中断信号。</param>
        private static void WriteBindingConfiguration(string path, string content, CancellationToken cancellationToken)
        {
            string temporary = path + ".nova-" + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temporary, content, new UTF8Encoding(false));
                cancellationToken.ThrowIfCancellationRequested();
                if (File.Exists(path)) File.Replace(temporary, path, null);
                else File.Move(temporary, path);
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }
    }
}
