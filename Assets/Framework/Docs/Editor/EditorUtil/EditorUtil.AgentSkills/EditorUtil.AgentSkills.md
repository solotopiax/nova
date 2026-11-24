# Nova Skills 与 AI 工具链安装

`EditorUtil.AgentSkills` 在安装或升级 Framework 后，自动补齐 Unity 官方 Agent 插件、Unity CLI、Coplay Unity MCP、Nova MCP 和 Nova Skills，并绑定当前项目。项目组无需执行补装命令。

## 自动触发与完成条件

- NovaSpark 首装、配置矫正或重复导入：先核对 UPM 实际包图和编译结果，再调用 `Nova/Check and Setup AI Toolchain`。本地安装、绑定及复核通过后，NovaSpark 才清除接力标记并自删除。
- Package Manager 或 PlugPals 手动升级 Framework：包注册事件和编译重载后自动同步 Skills，再执行同一安装流程；同版本重新安装也会触发。
- Editor 启动和域重载：编译与包更新稳定后接续未完成任务。相同来源和版本在当前会话已成功时去重；显式重试绕过去重。
- 可恢复失败：保留未完成状态，依次等待 30、120、300 秒重试，之后每 300 秒重试。未连接、超时、编译、重载或等待窗口会说明实际原因，不当作成功。

安装或更新 Framework 后，Nova 会在本机准备 [Nova 自动反馈机制的授权入口](../../../Onboarding/NOVA_ISSUES.md#在-codex-中启用-nova-自动反馈机制)。成员可以在 Codex 中自行启用。启用反馈机制后，项目可以照常开发、构建和运行，请放心使用。
- CLI 返回错误工程、非法 JSON、不支持的结构或截断输出：保留未完成状态并暂停当前工程、当前 Framework 来源及版本的自动重试；域重载不会重复相同错误。处理原因后使用 `Nova/Check and Setup AI Toolchain` 显式重检，或由新的 Framework 身份重新核验，不伪造成功、不提前删除 NovaSpark。

NovaSpark 的四项默认直接依赖以脚本声明的正式版本为下限，保留项目已安装的更高正式版本；完成条件同时核对实际 registry、当前注册来源和当前 Framework 的工具链成功身份。旧版脚本仍按精确版本等待时，Framework 的同一成功链可接续收尾。要求固定 `Assets/Editor/NovaSpark.cs`、该工程的接力标记、命中已核实原厂快照的全文摘要及实际直接依赖全部吻合。等待重要声明处理后沿原入口打开 PlugPals，再删除脚本。接力标记保留到 Unity 域重载、旧脚本类型已卸载，避免残存回调重新进入旧版配置矫正。未知、本地修改或软链文件保留。收尾须按实际加载版本核验。

安装进程在线程池限时运行，单次安装最多 120 秒，插件清单查询最多 45 秒，下载最多 30 秒；版本探测最多 8 秒。编译、导入或 Play 切换期间不启动新一轮；域重载和退出会取消本轮安装进程并保留接力状态。跨工程主机安装使用互斥锁，避免重复写入。batchmode 不自动启动主机安装，CI 可显式调用相应 API。

## 自动处理范围

| 工具 | 自动处理与实际验证 |
|---|---|
| Unity 官方 Agent 插件 | 通过 Codex 插件管理器安装、启用 Unity Technologies 的 Unity 插件；优先 OpenAI 插件目录，必要时使用 Unity 官方 `Unity-Technologies/unity-agent-plugin` 市场。缺失 Codex CLI 时自动从 OpenAI 官方入口安装。以实际清单中的 installed、enabled 和官方身份验证，不以缓存代替。 |
| Unity CLI | 复用能正常返回版本的现有 CLI；缺失时从 Unity 官方安装入口补齐，重新执行 `--version`。不自动安装或升级 Unity Editor。 |
| CLI 的 Editor 连接 | 通过 `unity pipeline install --project-path` 补齐 `com.unity.pipeline`，待 Unity 实际注册，再限定当前项目路径执行只读 `editor status --result-only`。仅当实际返回的 `projectPath` 与当前工程一致、`status=ready` 且未编译或重载时通过。 |
| Coplay Unity MCP、Nova MCP | 由 Framework 的精确 UPM 依赖闭包安装；核对实际注册身份与版本。默认 Provider 自动准备本地运行环境、启动或复用服务、连接本 Editor，并为 Codex 写入当前项目的匹配绑定。保留已有服务和有效配置，不覆盖其他项目或客户端。 |
| Nova Skills | 按当前 Framework Catalog 自动同步 `.agents/skills/`，再执行 dry-run 核对全部受管项；本地内容冲突会保留并报告，避免覆盖用户数据。 |

共同入口校验同时支持旧版文字引用和准确指向 [Docs/START_HERE.md](../../../START_HERE.md) 的同包相对链接，仍要求它出现在 Skill 首个正文段落；错误链接或后置引用不能通过。Router 的发现描述覆盖未点名 Nova 的一般 Unity 请求，项目识别与工具优先规则统一见 [NA-002：先核对当前能力，再选择接口或提出补充](../../../Onboarding/DEVELOPMENT_CONSTRAINTS.md#na-002先核对当前能力再选择接口或提出补充)。

Unity 官方 `com.unity.pipeline` 用于 CLI，不能替代 Nova 当前使用的 `com.coplaydev.unity-mcp`。两条连接各自验证。正常安装会修改主机工具、插件状态、项目 MCP 配置及 Pipeline 包依赖；不会调用模型推理，不修改业务资源或凭据，不执行 Git 操作。

## Skill 投影链接与内容校验

项目 `.agents/skills/` 中的受管 Skill 是普通复制目录。同步时，Editor 与随包 [nova_skills.py](../../../../Agents/Tools/nova_skills.py) 会按当前已解析包的真实位置，重写副本 Markdown 中指向同包 Docs、Tools 和其他包内素材的相对链接；`references/` 等子目录按各文件的最终位置分别计算。Skill 内部和相邻 Skill 的链接、代码示例、链接标题、查询参数和锚点保留，原始包文件不修改，也不额外复制 Docs 或工具。

包内真源继续使用可分发的同包相对链接。副本中的相对链接属于当前绑定快照：registry 安装时可以包含本轮真实解析出的缓存目录名，不能从旧目录名、固定 hash 或其他工程推测位置。包升级、同版本重装、file 依赖位置变化或项目移动后，同步会重新计算投影；仅真源字节相同不足以判定副本无需更新。不同磁盘无法组成相对路径时会明确阻断，不退回本机绝对路径。

受管状态中的 `sourceHash` 记录原文身份，`targetHash` 记录链接转换后的完整副本身份，二者可以不同；格式仍为版本 1。旧版逐字节副本只有在未被本地修改时才自动迁移。同步先核对原始 staging 与真源，再转换链接并核对预期副本摘要及真源是否变化；本地修改、同名未受管目录和链接目录仍按原冲突保护处理。`doctor` 保持只读：原文变化归入 `sourceChanged`，同一原文的投影位置变化归入 `resolutionChanged`，以 `projection:<skill-id>` 标明受影响项。

锁、事务日志和 staging 继续承担中断恢复。已按新规则转换的事务，在当前真源字节相同但链接绑定已变化时会保留现场并阻断恢复；恢复原绑定后再重试，不自动删除事务或覆盖用户数据。真源暂不可解析、来源内容已更新，以及旧版逐字节事务仍沿原冻结快照恢复，随后重新解析并规划当前投影；恢复完成不代表缺失包或当前导航已经就绪。

## 结果与连接边界

Console 的 `[Nova AI 工具链]` 以及 `LastToolchainReport` 提供逐项结果。`success` 表示本地工具安装、配置绑定和当前 Editor 服务复核通过；`partial` 表示仍等待解析、连接或查询证据；`blocked` 表示本轮自动处理失败或存在不能覆盖的冲突。可恢复的未完成任务继续自动处理；CLI 响应协议及工程身份错误按上方规则暂停，不用“正在等待连接”掩盖真实原因，相同失败不会反复刷屏。

NovaSpark 接力期间，成功日志补充当前启用的构建场景。对于已打开、已保存的单一启用场景，会核对是否存在唯一启用且连接当前 Framework [Nova 预制体](../../../Onboarding/PROJECT_STRUCTURE.md#nova-根节点)的实例，并显示实际来源。多场景或无法确认时，日志提示待核对的入口或实例状态。通过日志中的就绪检查入口继续核对[首装工程文件](../../../Onboarding/PROJECT_STRUCTURE.md#首装收尾的工程文件)，按[场景交接](../../../Onboarding/PROJECT_STRUCTURE.md#首装后的入口场景交接)完成具体接入。

CLI 连接检查使用 `unity command editor status --project-path <当前工程绝对路径> --result-only --timeout 10 --non-interactive --format json`。`--result-only` 读取 Editor 状态本身；失败仍按 CLI 的错误信封分类。命令清单、版本号和退出码为 0 均不能单独证明连接到当前工程。检查器按实际字段及类型验收，不按版本号猜测格式；未识别的版本或响应形状保留失败边界，不能据此宣称所有 CLI 版本兼容。

工具链及自动 Skills 同步日志统一通过 Nova 日志入口输出，使用 `LogTag.Editor`；普通日志前缀为 `[Nova][Debug][Editor]`，警告为 `[Nova][Warning][Editor]`。

点击 **Nova → Check and Setup AI Toolchain** 会立即显示请求已收到、正在检查或等待编译/导入/退出运行模式的提示。本次完成结果会重新输出，即使与上次内容相同；自动重试的重复结果仍去重。

外部 Agent 当前聊天是否已刷新工具列表仍要现场验证：确认连接同一项目，`tools/list` 能看到 `nova_project_action`，并执行只读 `describe`。项目配置已写入、插件已启用和 Editor 桥接成功分别是明确证据，不能推定当前聊天已自动刷新。宿主不支持即时加载时保留这一限制，不把缺项交给项目组安装。

临时下载脚本在本轮结束、失败或取消后删除。SessionState 只保存会话状态、成功身份和未完成标记；NovaSpark 使用项目专属 EditorPrefs 接续安装，不生成长期验收档案。

### 资产移动与重命名

当实际注册的 `com.coplaydev.unity-mcp` 为 `10.3.0` 时，`manage_asset` 的移动和重命名会反向判断 Unity 返回值。用 Unity Project 窗口或本次获授权的 Unity 编辑入口执行，并记录移动前的 GUID。成功时 `AssetDatabase.MoveAsset` 返回空字符串，失败时返回错误文本。最终以原路径消失、目标路径存在且 GUID 一致为准。若工具已报告移动失败，先对照原路径、目标路径和原 GUID，再决定是否重试。 [Unity AssetDatabase.MoveAsset](https://docs.unity3d.com/6000.4/Documentation/ScriptReference/AssetDatabase.MoveAsset.html)

## 安装阶段与自动窗口

`Nova.AgentSkills.Toolchain.Active.v1` 是 SessionState 中的只读观察键：立即排队或正在执行的当前一轮安装为 true；延时重试、不可恢复错误、等待重要声明人工确认及完成为 false。自动窗口只等待这个有限阶段，不能因工具链 `partial` 队列长期不展示。重要声明窗口仍在等待人工确认时，不启动下一轮工具链安装；窗口本身非模态，编辑器更新和只读连接继续可用。工具链最终结果只写 Nova 日志、报告及会话状态，不弹出阻塞式完成对话框；由 NovaSpark 菜单接力也沿用这条结果路径。

`Nova.AgentSkills.Toolchain.Phase.v1` 提供 `queued / running / waiting-editor / waiting-retry / waiting-important-updates / blocked / complete`，`Nova.AgentSkills.Toolchain.WaitingReason.v1` 提供中文原因。独立引导脚本可读取这些键安排后续窗口；最终完成仍须使用既有 `ToolchainStateSessionKey` 和匹配的成功身份，不从 Active=false 推断成功。重要声明和普通更新的排队语义见[重要升级声明](../EditorUtil.ImportantUpdates/EditorUtil.ImportantUpdates.md#只读等待状态)及[普通更新检查](../EditorUtil.CheckUpdate/EditorUtil.CheckUpdate.md#查询来源和跳过存档边界)。Unity 6000.4 下不能自动识别所有第三方原生模态，首次超时仍需检查真实窗口。

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
