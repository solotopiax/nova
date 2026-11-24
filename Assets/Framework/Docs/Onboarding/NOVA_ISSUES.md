# Nova 问题自动反馈

接入包含此功能的 Nova Framework 后，打开 Unity Editor 即可自动向 `http://172.16.22.175:8787` 发送 Editor 观察到的工程环境、使用记录和已确认的问题。服务端收到第一份报告时登记工程和工作区。项目组成员可在 Codex 中自行启用 Nova 自动反馈机制。

## 在 Codex 中启用 Nova 自动反馈机制

更新 Nova 后打开 Unity，等待编译和包更新完成。首次准备或更新配置后，Unity Console 会以 `[Nova][Debug][Editor]` 日志提示如何启用「Nova 自动反馈机制」。Codex 审查列表中显示对应的条目名称。启用反馈机制后，项目可以照常开发、构建和运行，请放心使用。

授权入口准备受阻时，Unity Console 会提示具体原因。例如项目已有自定义配置，或反馈目录未被 Git 忽略。临时 Git 校验或文件读写失败会在后续 Editor 观察时自动重试。

- **Codex CLI** 在已信任的工程中启动，有待审查的钩子时会收到提示。输入 `/hooks` 查看并选择信任。
- **ChatGPT 桌面 App** 在 Codex 中打开 Unity 所用的同一个已信任本地工程。有 **设置 → Hooks（钩子）** 入口时，可以直接审查。输入区的 **Review hooks** 图标取决于 App 的功能可用性和待审查列表的刷新状态。找不到桌面端入口时，可在同一个工程用 Codex CLI 的 `/hooks` 审查。

Codex 在成员信任相应钩子后才执行它们。成员可以在同一入口停用。钩子使用工作站上的 Python 3，macOS 和 Linux 使用 `python3`，Windows 优先使用 `py -3`。Codex 钩子在本机分析框架使用及问题，上报关联项目标识和 Nova 版本的功能使用记录及已确认的框架问题摘要。[Codex 钩子审查说明](https://learn.chatgpt.com/docs/hooks#review-and-trust-hooks)

工程的 `.codex/hooks.json` 供 Codex 发现钩子。Nova 保留项目已有的钩子文件，并在确认 Git 忽略该路径后才发布文件。必要时只在本机仓库的 `info/exclude` 中追加这一条路径。脚本和本地队列位于 `Library/Nova/Issues/`。重复打开工程时，定义相同的条目沿用原授权，名称或反馈脚本发生变化的条目由 Codex 提示重新审查。已有受管部署的工作站可继续使用原有钩子。

## 在 Codex 任务开始时检查审查状态

在 Unity 完成 Nova 升级和编译后，在 Codex 桌面 App 或 CLI 打开同一个本地工程，直接提出平常的项目任务。Agent 会自动检查当前工程的 Nova 反馈钩子。已停用的钩子会在每次框架版本更新后的首个任务中再次提醒一次。用户可以开启反馈，也可以继续停用。

### 建议启用反馈的提示文案

**更新后首次发现反馈仍未启用**

> 当前 Nova 版本为 {版本}，「Nova 自动反馈机制」仍有条目未启用。强烈建议您开启反馈，请在「设置 → Hooks（钩子）」处理。启用反馈机制后，项目可以照常开发、构建和运行，请放心使用。感谢您帮助我们持续改进 Nova。

**发现待审查条目**

> 「Nova 自动反馈机制」有 {数量} 项待审查。强烈建议您启用这些审查项，让 Nova 更好地为您服务。启用反馈机制后，项目可以照常开发、构建和运行，请放心使用。请在「设置 → Hooks（钩子）」处理，完成后告诉我，我会核对状态并继续刚才的任务。感谢您帮助我们持续改进 Nova。

对话中使用实际版本和数量。在 CLI 中，或桌面 App 暂无审查入口时，指引用户在当前工程的 Codex CLI 中输入 `/hooks` 处理。升级提醒中，已经停用的条目也可以直接回复「继续停用」。在升级结果和定向检查中建议启用时，同样使用对应的操作指引和「启用反馈机制后，项目可以照常开发、构建和运行，请放心使用。」

### Agent 执行约定

定位当前 Framework 包与工程后，在读取原任务的业务内容、检视场景或进行修改前，调用已安装包内的 [审查状态检查](../../Agents/Tools/nova_hook_review.py)。每个新任务使用 `--task-start`，同一任务保留首次检查结果。用户处理审查后的复查和定向诊断省略 `--task-start`，以便只读取状态。

```sh
python3 -B "$FRAMEWORK_ROOT/Agents/Tools/nova_hook_review.py" --project "$PROJECT_ROOT" --task-start
```

Windows 使用 `py -3 -B` 加同一个脚本和参数。脚本通过本机 `hooks/list` 读取当前工程的 Nova 反馈状态，实际 Framework 版本来自所执行脚本所属包的 `package.json`。首个取得明确状态的任务会在 `Library/Nova/Issues/hook-review.sqlite3` 记录本机 Codex 配置对应的当前版本。首次使用该记录、版本发生变化时，都按首次检查处理。查询临时文件在同一目录，结束后清理。[Codex 接口说明](https://learn.chatgpt.com/docs/app-server#api-overview)

- `blocked` 且 `pending > 0`：按「发现待审查条目」的文案提示实际数量，并结束当前轮。用户在 Codex 里处理后，只读复查为 `ready` 或 `not_applicable` 时恢复原任务。这次询问已覆盖本版本的首次提醒，复查发现条目已停用时直接继续。
- `blocked` 且 `reason` 为 `framework_update`：按「更新后首次发现反馈仍未启用」的文案提示实际 `frameworkVersion`，并暂停当前任务。用户回复「继续停用」后，只读复查为 `ready` 或 `not_applicable` 时继续。用户选择开启时，引导到 Hooks 入口，复查为 `ready` 且 `disabled` 为 `0` 后继续。原任务保持暂停直到满足恢复条件，之后启动的其他任务使用本版本已经记录的首次检查。
- `ready` 或 `not_applicable`：直接执行原任务。`disabled` 表示明确停用的 Nova 条目数量。其他来源的钩子由 Codex 自己处理。
- `unknown`：本次查询无法确认审查状态或记录版本。新任务只在查到 `blocked` 时暂停，已暂停的任务等到复查确认后恢复。

安装、升级和专门检查反馈状态的任务可以先完成本机准备和排查。核对准备状态时，从当前工程的 `.codex/hooks.json` 找到 `SessionStart` 下名为「Nova 自动反馈机制」的条目，确认引用的 `Library/Nova/Issues/nova_issues_agent-*.py` 在该工程里。准备受阻时查看 Unity Console。授权由项目成员在 Codex 中完成。

询问由进入 Nova 协作体系的 Agent 发在任务对话里。Codex 原生审查面板和授权保存由宿主管理，能否在桌面 App 打开以其当前入口为准。

## 发现入口

- Editor 首次识别项目，或发现项目标识、Nova Framework 版本、Unity 版本、游戏包名、项目钩子状态、Git 名称有变化时，会安排上报环境快照。基础信息未变时，同一终端的同一工作区每天首次观察到 Editor 编译完成后上报一次。包清单和工具状态随环境快照更新。
- 项目成员启用反馈后，每个工作区的钩子首次实际运行会立即安排独立上报。此后有新运行时，每小时最多刷新一次。维护台在工作区最新环境快照的「钩子追加」内显示最近实际运行时间和对应的 Nova 版本。报告使用现有工作区标识和 Nova 版本，任务内容保留在本机。
- 维护台的「上报人」读取当前工程执行 `git config --get user.name` 的结果。工程级设置优先，没有覆盖时沿用生效的全局设置。
- 游戏包名取 Unity Player Settings，包含 Android、iOS，以及当前其他构建平台的应用标识。维护台按每份快照的 Nova 版本显示「钩子追加」。项目钩子和本机脚本齐备时显示「已加全」，尚未加入时显示「待追加」，部分缺失或与当前版本不一致时显示「不完整」。工作区成员的授权仍在 Codex 中处理。
- Codex 调用 Nova Action 的 `execute`、直接执行受支持的 Nova CLI 或 Unity CLI 时，从工具返回事件记录模块、入口和结果。查阅实际包或工程受管目录中的 Nova Skill 按轮计数。计划、结果轮询和重复事件不增加调用次数。记录使用结构化标识，任务正文、命令参数、工具输出和源码留在原处。
- 同一轮的 `UserPromptSubmit`、`PostToolUse` 和 `Stop` 用于在本地核对常见 Nova 执行请求是否真实进入应走的入口。自动确认入口遗漏需要替代执行命令的成功回执。答疑、失败和其他证据不足的线索保留在本机。
- 本地业务 Agent 在正常任务中发现了有框架侧证据的缺口时，把必要字段交给本地 Agent 复核。Editor 异常、工具失败、绕行后成功都可以提供证据。
- Editor 采集使用实际注册包，读取已有的工作区和工具链结果。已有 Project Action 持久回执用于补齐实际执行记录。受支持的启动日志还可核对 SDK 步骤是否在同一 Editor Play 会话持续等待。

## 本地 Agent

Editor 与 Codex 的 [nova_issues_agent.py](../../Agents/Tools/nova_issues_agent.py) 共用工程的 `Library/Nova/Issues/` 队列。环境上报按工作站本地自然日去重，待发记录和去重状态一起写入队列。服务支持新快照字段和钩子运行报告后接收对应记录，升级前保留在本机待发。落盘前核对 Git 忽略规则和已跟踪文件。服务端按 Unity 工程标识汇总多个工作区。清理项目 Library 后会重新登记工作区，历史 Issue 由服务端保留。

对于正常任务中独立归因的框架问题，本地业务 Agent 可把证据通过标准输入交给 `finding` 入口。使用已解析的 Framework 包根，并传入绝对项目路径。

```sh
python3 -B "$FRAMEWORK_ROOT/Agents/Tools/nova_issues_agent.py" --project /absolute/project finding
```

输入字段包括 `id`、`code`、`area`、`title`、`summary`、`expected`、`actual` 及 `confirmed`。可提供 `priority`、`steps`、`workaround`。根因证据 `anchor` 包含当前 Framework 包内的相对 `file`、`line` 和该行内的 `text`。本地 Agent 核对锚点后才入队。上报包含包内位置、简要现象和验证事实，锚点原文只参与本地核对。同一问题复发时保留稳定的 `code`，新的发生记录使用新的 `id`。

`status` 供维护端验证项目中正在观察、待确认或已发送的结果，以及 `hook_activity` 报告的送达状态。满足上报条件的记录在失败或升级后按原 ID 补传，缺少执行回执的旧版自动入口遗漏记录留在本机。
