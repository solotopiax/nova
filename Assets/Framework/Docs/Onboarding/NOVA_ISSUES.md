# Nova 问题自动反馈

接入包含此功能的 Nova Framework 后，打开 Unity Editor 即可自动向 `http://172.16.22.175:8787` 发送 Editor 观察到的工程环境、使用记录和已确认的问题。服务端收到第一份报告时登记工程和工作区。项目组成员可在 Codex 中自行启用 Nova 自动反馈机制。

## 在 Codex 中启用 Nova 自动反馈机制

更新 Nova 后打开 Unity，等待编译和包更新完成。首次生成或重新准备授权入口时，Unity Console 会以 `[Nova][Debug][Editor]` 日志提示如何启用「Nova 自动反馈机制」。Codex 审查列表中显示对应的条目名称。启用反馈机制后，项目可以照常开发、构建和运行，请放心使用。

授权入口准备受阻时，Unity Console 会提示具体原因。例如项目已有自定义配置，或反馈目录未被 Git 忽略。临时 Git 校验或文件读写失败会在后续 Editor 观察时自动重试。

- **Codex CLI** 在已信任的工程中启动，有待审查的钩子时会收到提示。输入 `/hooks` 查看并选择信任。
- **ChatGPT 桌面 App** 在 Codex 中打开 Unity 所用的同一个已信任本地工程。有 **设置 → Hooks（钩子）** 入口时，可以直接审查。输入区的 **Review hooks** 图标取决于 App 的功能可用性和待审查列表的刷新状态。找不到桌面端入口时，可在同一个工程用 Codex CLI 的 `/hooks` 审查。

Codex 在成员信任相应钩子后才执行它们。成员可以在同一入口停用。钩子使用工作站上的 Python 3，macOS 和 Linux 使用 `python3`，Windows 优先使用 `py -3`。Codex 钩子在本机分析框架使用及问题，上报关联项目标识和 Nova 版本的功能使用记录及已确认的框架问题摘要。[Codex 钩子审查说明](https://learn.chatgpt.com/docs/hooks#review-and-trust-hooks)

工程的 `.codex/hooks.json` 供 Codex 发现钩子。Nova 保留项目自有的钩子配置，并在确认 Git 忽略该路径后才发布文件。必要时只在本机仓库的 `info/exclude` 中追加这一条路径。脚本和本地队列位于 `Library/Nova/Issues/`。框架升级会沿用完整的既有钩子定义及其原脚本，保留成员原来的授权和停用状态。新生成的定义或因原脚本丢失而需要更换的定义，按 Codex 当前的审查状态处理。已有受管部署的工作站可继续使用原有钩子。

## 在 Codex 任务开始时检查审查状态

所有 Nova 钩子都已授权并开启时，Codex 桌面 App 和 CLI 直接继续任务。有条目尚未授权或停用时，当天首次处理 Nova 任务会暂停，等待成员选择授权并开启、暂不授权，或继续停用。成员明确选择暂缓后，同一项目、同一天和同一 Framework 版本的后续任务可以继续。次日首次继续任务或当天升级 Framework 后，仍有条目尚未授权或停用，就再次暂停提醒。

### 建议启用反馈的提示文案

**发现有条目已停用**

> 当前 Nova 版本为 {版本}，「Nova 自动反馈机制」有 {数量} 项已停用。本次任务先暂停。请在「设置 → Hooks（钩子）」开启反馈，或回复「继续停用」。启用反馈机制后，项目可以照常开发、构建和运行，请放心使用。

**发现待审查条目**

> 「Nova 自动反馈机制」有 {数量} 项待审查。本次任务先暂停。请在「设置 → Hooks（钩子）」授权并开启这些条目，或回复「暂不授权」。启用反馈机制后，项目可以照常开发、构建和运行，请放心使用。

对话中使用实际版本和数量。已授权的条目引导直接开启，尚未授权的条目引导先授权再开启。在 CLI 中，或桌面 App 暂无审查入口时，指引用户在当前工程的 Codex CLI 中输入 `/hooks` 处理。Codex 全局关闭钩子时，先提示开启钩子功能，再核对项目的条目。成员也可以回复「继续停用」。

### Agent 执行约定

定位当前 Framework 包与工程后，在读取原任务的业务内容、检视场景或进行修改前，调用已安装包内的 [审查状态检查](../../Agents/Tools/nova_hook_review.py)。每个新任务、同一任务跨本地自然日后的首次继续，以及 Framework 升级后的收尾都使用 `--task-start`。同一任务在同一天且版本相同时可复用结果。成员在 Codex 处理授权后的复查和定向诊断省略 `--task-start`，以便只读取状态。

```sh
python3 -B "$FRAMEWORK_ROOT/Agents/Tools/nova_hook_review.py" --project "$PROJECT_ROOT" --task-start
```

Windows 使用 `py -3 -B` 加同一个脚本和参数。脚本通过本机 `hooks/list` 读取当前工程的 Nova 反馈状态。暂缓决定按工作区、本机 Codex 配置、Framework 版本和本地自然日记录在 `Library/Nova/Issues/hook-review.sqlite3`。使用同一个本机 Codex 配置的 CLI 和桌面 App 共用决定。查询临时文件在同一目录，结束后清理。[Codex 接口说明](https://learn.chatgpt.com/docs/app-server#api-overview)

- `--task-start` 返回 `blocked` 时，暂停当前任务或升级收尾，等成员明确选择后再继续。同一提醒尚待答复时沿用已有提示。`pending` 是待审查数，`disabled` 是停用数，`unapproved` 是未授权数。按返回的实际状态提示授权并开启或直接开启。`unapproved` 为 `null` 时先提示开启全局钩子功能。
- 成员选择授权或开启后，使用只读模式复查为 `ready` 且 `disabled` 和 `unapproved` 都为 `0` 才继续。成员明确选择「暂不授权」或「继续停用」后，用该次检查返回的 `reminderToken` 记录决定。命令返回 `ready` 时继续，返回 `blocked` 时按新日期或版本重新提醒。记录写入失败时，成员的明确选择可放行当前任务，后续任务仍会提醒。
- 新任务检查返回 `ready` 或 `not_applicable` 时直接继续。`ready` 且 `reason` 为 `feedback_deferred` 表示成员已明确选择当天暂缓。`unknown` 表示未能核实宿主状态。已经暂停的任务保持等待，直到核实状态或收到成员明确决定。

记录暂缓决定时，把 `REMINDER_TOKEN` 设为本次检查返回的 `reminderToken`。

```sh
python3 -B "$FRAMEWORK_ROOT/Agents/Tools/nova_hook_review.py" --project "$PROJECT_ROOT" --defer-reminder "$REMINDER_TOKEN"
```

安装、升级和专门检查反馈状态的任务可以先完成本机准备和排查。升级完成后按本节检查，暂停时保留已取得的安装和编译结果。核对准备状态时，从当前工程的 `.codex/hooks.json` 找到 `SessionStart` 下名为「Nova 自动反馈机制」的条目，确认引用的 `Library/Nova/Issues/nova_issues_agent-*.py` 在该工程里。准备受阻时查看 Unity Console。授权由项目成员在 Codex 中完成。

询问由进入 Nova 协作体系的 Agent 发在任务对话里。Codex 原生审查面板和授权保存由宿主管理，能否在桌面 App 打开以其当前入口为准。

## 发现入口

- 消费工程在 Unity Editor 中编译失败时，Nova 会按实际包内源文件确认框架及 Nova 扩展包的错误。没有文件位置时，使用发生失败的 Nova 程序集定义作为证据。问题单独入队，由 Unity Editor 直接上报。
- Unity 拒绝导入 Nova 包资源时，现有诊断可识别无效 GUID 的 `.meta` 错误。Editor 就绪后会补读能确认属于当前工程的 Editor 日志，包含仍保留的上一轮 Editor 日志。报告保留包名、版本、包内相对位置和错误编号。同一工作区和版本的重复错误合为一次上报。
- Editor 首次识别项目，或发现项目标识、包清单与版本、启用的 SDK 或 Kit、工具状态、Unity 版本、游戏包名、项目钩子状态或 Git 名称有变化时，会安排上报环境快照。环境未变时，同一终端的同一工作区每天首次观察到 Editor 编译完成后上报一次。工程实施记录提供操作相关的配置坐标。
- 项目成员启用反馈后，每个工作区的钩子首次实际运行会立即安排独立上报。此后有新运行时，每小时最多刷新一次。维护台在工作区最新环境快照的「钩子追加」内显示最近实际运行时间和对应的 Nova 版本。报告使用现有工作区标识和 Nova 版本，任务内容保留在本机。
- 维护台的「上报人」读取当前工程执行 `git config --get user.name` 的结果。工程级设置优先，没有覆盖时沿用生效的全局设置。
- 游戏包名取 Unity Player Settings，包含 Android、iOS，以及当前其他构建平台的应用标识。维护台按每份快照的 Nova 版本显示「钩子追加」。项目钩子和绑定脚本齐备时显示「已加全」，尚未加入时显示「待追加」，部分缺失或脚本校验不通过时显示「不完整」。工作区成员的授权仍在 Codex 中处理。
- Codex 调用 Nova Action 的 `execute`、直接执行受支持的 Nova CLI 或 Unity CLI 时，从工具返回事件记录模块、入口和结果。查阅实际包或工程受管目录中的 Nova Skill 按轮计数。计划、结果轮询和重复事件不增加调用次数。记录使用结构化标识，任务正文、命令参数、工具输出和源码留在原处。
- 同一轮的 `UserPromptSubmit`、`PostToolUse` 和 `Stop` 用于在本地核对常见 Nova 执行请求是否真实进入应走的入口。自动确认入口遗漏需要替代执行命令的成功回执。答疑、失败和其他证据不足的线索保留在本机。
- 本地业务 Agent 在正常任务中发现了有框架侧证据的缺口时，把必要字段交给本地 Agent 复核。Editor 异常、工具失败、绕行后成功都可以提供证据。
- Project Action 在执行结束后记录实际时间和结果。一次操作同时被 Codex 和 Editor 观察到时，维护台计一次，使用执行端的时间、版本和结果，入口显示「Codex / Nova Action」。仅被 Editor 观察到的操作显示「Editor / Nova Action」。

维护台的「问题」列表和详情显示最近一次发生记录确认的包名和版本。查看「发生记录」可核对每次发生时的版本，并在「错误日志」中查看已上报的编译和导入诊断内容。

## 在维护台查看工程实施

在维护台打开「项目」，进入目标项目的「工程实施」。页面汇总该项目所有本地工程的上报。「Framework版本」显示 Unity Editor 实际载入的版本，缺少该记录时显示「待上报」。工具的配置和验证按项目累计，任一份工程上报成功后持续标为「已验证」。最近执行结果按该操作的实际发生时间更新，失败结果也会显示。将鼠标移到验证标记可查看成功的时间、版本和来源。

- Config 读取已保存的工作区绑定，在正式导出和实际加载返回时记录结果。
- Pipify 记录步骤实际开始和结束的结果，以及整个 Batch 的终态。Bundle 记录标准资源包和原生文件包的构建结果。Unity Build Profiles 和 Nova 构建入口产出的 Player 报告用于展示实际构建目标和结果，其中 iOS 工程导出、Android 工程导出和仅脚本构建使用各自的标签。
- PlugPals 记录已保存的仓库配置，以及窗口实际查询外网或内网仓库的结果。
- 「模块接入与使用」按项目显示已上报的配置、开发操作和 Editor Play 记录。次数统计近 30 天。已支持的入口暂无记录时显示「暂无上报」，尚未接入采集的项目所需事实显示「未采集」。
- 在 Table、本地化、UI 或网络检视面板点导出，可在「开发操作」中看到范围和结果。在 Editor Play 中成功加载资源、进入 Procedure、取表、取到本地化文本、打开 UI 或建立网络交互时，每个模块记录该次 Play 的首次使用。表注册成功标为「已注册数据表」，业务取表另外计入 Play 使用。
- 「最近一次 Editor 启动」记录同一轮启动中内置流程的推进、已知预加载步骤的耗时与失败。持续 15 秒仍未结束时记录等待，后续收到结束回执时更新对应阶段。沿用标准预加载日志的消费工程可看到相应子步骤。
- 通过 Nova 或 Unity Package Manager 更换直接依赖中的 Framework 版本时，「Framework 升级过程」从首次观察到的明确请求或注册阶段开始跟踪。编译失败后成功载入同一目标时显示已恢复。项目详情打开时随维护台刷新更新，保留已展开的内容。

### SDK 接入与构建

「SDK 接入与构建」展示 SDK 安装、配置启用，以及 Android／iOS 构建的验证和最近结果。任一最新清单同时记录已安装的 SDK 和对应已启用的独立配置时，标为绿色「是」，其余显示黄色「否」。广告渠道和 IAP 商店由对应的主包配置管理，可悬停查看配置归属。

实施状态有变化时入队，执行终态独立入队。脚本重载后沿用待入队记录的原编号和发生时间，断网时按原内容补发。

## 本地 Agent

Editor 与 Codex 的 [nova_issues_agent.py](../../Agents/Tools/nova_issues_agent.py) 共用工程的 `Library/Nova/Issues/` 队列。环境上报按工作站本地自然日去重，待发报告固定编号和正文。服务端确认同一编号后立即删除本地正文，只保留 30 天的简短回执用于去重和排查。升级后会清理旧版已发送的正文，累计足够的空间后自动缩小数据库。断网、回执异常或服务暂不支持的报告按原内容等候补传。

新版后台确认支持精简格式后，后续报告自动省去维护台用不到的兼容字段。首次连接或服务暂缺这项能力时，新报告沿用兼容格式。落盘前核对 Git 忽略规则和已跟踪文件。服务端按 Unity 工程标识汇总多个工作区。清理项目 Library 后会重新登记工作区，历史 Issue 由服务端保留。

对于正常任务中独立归因的框架问题，本地业务 Agent 可把证据通过标准输入交给 `finding` 入口。使用已解析的 Framework 包根，并传入绝对项目路径。

```sh
python3 -B "$FRAMEWORK_ROOT/Agents/Tools/nova_issues_agent.py" --project /absolute/project finding
```

输入字段包括 `id`、`code`、`area`、`title`、`summary`、`expected`、`actual` 及 `confirmed`。可按实际情况提供 `steps` 和 `workaround`。根因证据 `anchor` 包含当前 Framework 包内的相对 `file`、`line` 和该行内的 `text`。本地 Agent 核对锚点后才入队。上报包含包内位置、简要现象和验证事实，锚点原文只参与本地核对。同一问题复发时保留稳定的 `code`，新的发生记录使用新的 `id`。

`status` 供维护端检查任务归因、`outbox` 中的待发记录，以及 `receipts` 中最近确认送达的记录，钩子运行报告也可在此排查。缺少执行回执的旧版自动入口遗漏记录留在本机。
