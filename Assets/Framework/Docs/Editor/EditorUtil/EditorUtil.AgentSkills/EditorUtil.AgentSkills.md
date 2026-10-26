# Nova Skills 与 AI 工具链安装

`EditorUtil.AgentSkills` 在安装或升级 Framework 后，自动补齐 Unity 官方 Agent 插件、Unity CLI、Coplay Unity MCP、Nova MCP 和 Nova Skills，并绑定当前项目。项目组无需执行补装命令。

## 自动触发与完成条件

- NovaSpark 首装、配置矫正或重复导入：先核对 UPM 实际包图和编译结果，再调用 `Nova/Check and Setup AI Toolchain`。本地安装、绑定及复核通过后，NovaSpark 才清除接力标记并自删除。
- Package Manager 或 PlugPals 手动升级 Framework：包注册事件和编译重载后自动同步 Skills，再执行同一安装流程；同版本重新安装也会触发。
- Editor 启动和域重载：编译与包更新稳定后接续未完成任务。相同来源和版本在当前会话已成功时去重；显式重试绕过去重。
- 失败：保留未完成状态，依次等待 30、120、300 秒重试，之后每 300 秒重试。网络、权限或宿主策略导致的失败会说明原因，不当作成功，不要求项目组自行补装。

安装进程在线程池限时运行，单次安装最多 120 秒，插件清单查询最多 45 秒，下载最多 30 秒；版本探测最多 8 秒。编译、导入或 Play 切换期间不启动新一轮；域重载和退出会取消本轮安装进程并保留接力状态。跨工程主机安装使用互斥锁，避免重复写入。batchmode 不自动启动主机安装，CI 可显式调用相应 API。

## 自动处理范围

| 工具 | 自动处理与实际验证 |
|---|---|
| Unity 官方 Agent 插件 | 通过 Codex 插件管理器安装、启用 Unity Technologies 的 Unity 插件；优先 OpenAI 插件目录，必要时使用 Unity 官方 `Unity-Technologies/unity-agent-plugin` 市场。缺失 Codex CLI 时自动从 OpenAI 官方入口安装。以实际清单中的 installed、enabled 和官方身份验证，不以缓存代替。 |
| Unity CLI | 复用能正常返回版本的现有 CLI；缺失时从 Unity 官方安装入口补齐，重新执行 `--version`。不自动安装或升级 Unity Editor。 |
| CLI 的 Editor 连接 | 通过 `unity pipeline install --project-path` 补齐 `com.unity.pipeline`，待 Unity 实际注册，再限定当前项目路径读取命令清单确认连接。 |
| Coplay Unity MCP、Nova MCP | 由 Framework 的精确 UPM 依赖闭包安装；核对实际注册身份与版本。默认 Provider 自动准备本地运行环境、启动或复用服务、连接本 Editor，并为 Codex 写入当前项目的匹配绑定。保留已有服务和有效配置，不覆盖其他项目或客户端。 |
| Nova Skills | 按当前 Framework Catalog 自动同步 `.agents/skills/`，再执行 dry-run 核对全部受管项；本地内容冲突会保留并报告，避免覆盖用户数据。 |

Unity 官方 `com.unity.pipeline` 用于 CLI，不能替代 Nova 当前使用的 `com.coplaydev.unity-mcp`。两条连接各自验证。正常安装会修改主机工具、插件状态、项目 MCP 配置及 Pipeline 包依赖；不会调用模型推理，不修改业务资源或凭据，不执行 Git 操作。

## 结果与连接边界

Console 的 `[Nova AI 工具链]` 以及 `LastToolchainReport` 提供逐项结果。`success` 表示本地工具安装、配置绑定和当前 Editor 服务复核通过；`partial` 表示仍等待解析、连接或查询证据；`blocked` 表示本轮自动处理失败或存在不能覆盖的冲突。未完成任务继续自动处理，相同失败不会反复刷屏。

工具链及自动 Skills 同步日志统一通过 Nova 日志入口输出，使用 `LogTag.Editor`；普通日志前缀为 `[Nova][Debug][Editor]`，警告为 `[Nova][Warning][Editor]`。

点击 **Nova → Check and Setup AI Toolchain** 会立即显示请求已收到、正在检查或等待编译/导入/退出运行模式的提示。本次完成结果会重新输出，即使与上次内容相同；自动重试的重复结果仍去重。

外部 Agent 当前聊天是否已刷新工具列表仍要现场验证：确认连接同一项目，`tools/list` 能看到 `nova_project_action`，并执行只读 `describe`。项目配置已写入、插件已启用和 Editor 桥接成功分别是明确证据，不能推定当前聊天已自动刷新。宿主不支持即时加载时保留这一限制，不把缺项交给项目组安装。

临时下载脚本在本轮结束、失败或取消后删除。SessionState 只保存会话状态、成功身份和未完成标记；NovaSpark 使用项目专属 EditorPrefs 接续安装，不生成长期验收档案。

## 可调用入口

| 入口 | 作用 |
|---|---|
| `EditorUtil.AgentSkills.Reconcile(bool dryRun = false)` | Skills 安全同步或只读规划，保留原有冲突和事务恢复语义。 |
| `EditorUtil.AgentSkills.EnsureToolchainAsync(CancellationToken)` | 实际自动补齐、绑定并返回 `UniTask<ToolchainReport>`。 |
| `EditorUtil.AgentSkills.ProvisionHostToolsAsync(CancellationToken)` | 确定性主机安装实现，供完整流程复用；项目组无需单独调用。 |
| `EditorUtil.AgentSkills.ValidateToolchainAsync()` | 独立只读检查；不安装、不绑定，外部连接单独验收。 |
| `EditorUtil.AgentSkills.RequestToolchainSetup(bool force = true)` | 合并请求，等待 Editor 稳定后自动处理；失败自动重试。 |
| `EditorUtil.AgentSkills.LastToolchainReport` / `IsToolchainSetupRunning` | 当前域最近结果及是否正在执行。 |
| `Nova/Check and Setup AI Toolchain` | NovaSpark 与手动重试共用入口；调用返回仅代表已安排任务。 |

报告包含 `Status`、`Summary`、`Items`、`FormatMessage()`；逐项字段为 `Id`、`Name`、`Status`、`Detail`、`Remediation`。外部进程原始输出不直接转储到项目组 Console。默认 Provider 的服务与绑定实现属于 Nova MCP 包，详见其随包文档。

Skills 真源、投影和 Git 边界见 [Agent 快速入口](../../../START_HERE.md)。
