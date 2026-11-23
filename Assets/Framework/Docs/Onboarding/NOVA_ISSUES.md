# Nova 问题自动反馈

接入包含此功能的 Nova Framework 后，打开 Unity Editor 即可自动向 `http://172.16.22.175:8787` 发送 Editor 观察到的工程环境、使用记录和已确认的问题。服务端收到第一份报告时登记工程和工作区。项目组成员可在 Codex 中自行启用 Nova 自动反馈机制。

## 在 Codex 中启用 Nova 自动反馈机制

更新 Nova 后打开 Unity，等待编译和包更新完成。首次准备或更新配置后，Unity Console 会展示 `[Nova 自动反馈机制]` 的说明。Codex 审查列表中显示对应的条目名称。

授权入口准备受阻时，Unity Console 会提示具体原因。例如项目已有自定义配置，或反馈目录未被 Git 忽略。临时 Git 校验或文件读写失败会在后续 Editor 观察时自动重试。

- **Codex CLI** 在已信任的工程中启动后提示有钩子待审查。输入 `/hooks` 查看并选择信任。
- **ChatGPT 桌面 App** 重新打开已信任的工程，在 Codex 输入区选择 **Review hooks**，也可进入 **设置 → Hooks** 审查。

Codex 在成员信任相应钩子后才执行它们。成员可以在同一入口停用。钩子使用工作站上的 Python 3，macOS 和 Linux 使用 `python3`，Windows 优先使用 `py -3`。Codex 钩子在本机分析框架使用及问题，上报关联项目标识和 Nova 版本的功能使用记录及已确认的框架问题摘要。日常开发可照常进行。[Codex 钩子审查说明](https://learn.chatgpt.com/docs/hooks#review-and-trust-hooks)

工程的 `.codex/hooks.json` 供 Codex 发现钩子。Nova 保留项目已有的钩子文件，并在确认 Git 忽略该路径后才发布文件。必要时只在本机仓库的 `info/exclude` 中追加这一条路径。脚本和本地队列位于 `Library/Nova/Issues/`。重复打开工程时，定义相同的条目沿用原授权，名称或反馈脚本发生变化的条目由 Codex 提示重新审查。已有受管部署的工作站可继续使用原有钩子。

## 发现入口

- Editor 首次识别项目，或发现已登记的项目标识、Nova Framework 版本、Unity 版本有变化时，会安排上报环境快照。基础信息未变时，同一终端的同一工作区每天首次观察到 Editor 编译完成后上报一次。当天已有环境上报时跳过重复快照。包清单和工具状态随环境快照更新。
- Codex 调用 Nova Action 的 `execute`、直接执行受支持的 Nova CLI 或 Unity CLI 时，从工具返回事件记录模块、入口和结果。查阅实际包或工程受管目录中的 Nova Skill 按轮计数。计划、结果轮询和重复事件不增加调用次数。记录使用结构化标识，任务正文、命令参数、工具输出和源码留在原处。
- 同一轮的 `UserPromptSubmit`、`PostToolUse` 和 `Stop` 用于在本地核对常见 Nova 执行请求是否真实进入应走的入口。自动确认入口遗漏需要替代执行命令的成功回执。答疑、失败和其他证据不足的线索保留在本机。
- 本地业务 Agent 在正常任务中发现了有框架侧证据的缺口时，把必要字段交给本地 Agent 复核。Editor 异常、工具失败、绕行后成功都可以提供证据。
- Editor 采集使用实际注册包，读取已有的工作区和工具链结果。已有 Project Action 持久回执用于补齐实际执行记录。受支持的启动日志还可核对 SDK 步骤是否在同一 Editor Play 会话持续等待。

## 本地 Agent

Editor 与 Codex 的 [nova_issues_agent.py](../../Agents/Tools/nova_issues_agent.py) 共用工程的 `Library/Nova/Issues/` 队列。环境上报按工作站本地自然日去重，待发记录和去重状态一起写入队列。落盘前核对 Git 忽略规则和已跟踪文件。服务端按 Unity 工程标识汇总多个工作区。清理项目 Library 后会重新登记工作区，历史 Issue 由服务端保留。

对于正常任务中独立归因的框架问题，本地业务 Agent 可把证据通过标准输入交给 `finding` 入口。使用已解析的 Framework 包根，并传入绝对项目路径。

```sh
python3 -B "$FRAMEWORK_ROOT/Agents/Tools/nova_issues_agent.py" --project /absolute/project finding
```

输入字段包括 `id`、`code`、`area`、`title`、`summary`、`expected`、`actual` 及 `confirmed`。可提供 `priority`、`steps`、`workaround`。根因证据 `anchor` 包含当前 Framework 包内的相对 `file`、`line` 和该行内的 `text`。本地 Agent 核对锚点后才入队。上报包含包内位置、简要现象和验证事实，锚点原文只参与本地核对。同一问题复发时保留稳定的 `code`，新的发生记录使用新的 `id`。

`status` 供维护端验证项目中正在观察、待确认或已发送的结果。满足上报条件的记录在失败或升级后按原 ID 补传，缺少执行回执的旧版自动入口遗漏记录留在本机。
