# nova-issues-server

在一台局域网机器上启动服务。维护者用浏览器查看各项目的环境、使用记录和已确认的 Nova Issue。消费端的上报配置由维护者集中下发。

## 启动

需要 Python 3.10 或更新版本。将本目录复制到服务机，保留 `web/` 和私有的 `.local/accounts.json`。账号文件含两个维护账号的加盐密码摘要。

```sh
python3 -B serve.py --host 0.0.0.0 --port 8787
```

Windows 可用 `py -3 -B serve.py --host 0.0.0.0 --port 8787`。用服务机的局域网 IP 和端口打开浏览器。默认监听 `127.0.0.1`，适合本机验证。服务支持 `--tls-cert` 和 `--tls-key`。使用 HTTPS 时，把对应证书信任链下发给工作站。

网页只有 `taoye` 和 `yingzheng` 两个内置账号。新部署可用 `--init-accounts` 在服务机本地交互生成摘要。Web 提供项目登记，项目凭据与维护账号各自独立。

维护台登录后可以关闭页面或浏览器。再次打开时自动恢复登录，并续期浏览器的登录 Cookie。主动退出或更新账号凭据会结束对应的长期会话。旧版签发的短期会话到期后，登录一次即可启用新规则。

服务数据默认在 `data/`，可用 `--data-dir` 指定。备份示例为 `python3 -B serve.py --backup ./nova-backup.sqlite3`。恢复时将备份作为新数据目录中的 `nova-issues.sqlite3`，用 `--data-dir` 指向该目录启动。

### 数据库自动维护

服务每天在本机时间 03:00 自动维护。错过时在启动后补跑，失败后每 15 分钟重试。每次先校验当天的 SQLite 备份，再归档到期记录。`maintenance-backups/` 位于服务数据目录下，保留最近 7 份按日生成且通过校验的备份。

使用、Play 调用和协作明细在线查询最近 30 天，按发生时间筛选。明细按接收和发生两者较晚的时间计算，满 30 天后按项目、月份压缩到同一数据库的归档表。每批归档以其中最新的记录为起点，满 365 天自动清理。问题及处理记录、环境快照、配置验证和构建结果保留，模块的历史上报和最近成功时间也继续可用。工作区的钩子和 Unity MCP 状态保留最近上报。

`/health` 下的 `database_maintenance` 显示最近成功时间、连续失败次数和保留天数。释放的数据库空间供后续上报复用，空闲量较大时服务会尝试在低峰回收。

### macOS 开机自启与更新

在服务 Mac 上，准备 `.local/accounts.json` 后，用有 `sudo` 权限的部署账号从 Nova 根目录运行。脚本会在注册时请求管理员权限。

```sh
python3 Tools/nova-issues/nova-issues-server-macos.py
```

这个入口负责 Nova Issues 的开机自启、启动和更新。首次执行会接管可确认的手动实例，由 `launchd` 管理。以后在服务机上更新 Git 后执行同一条命令。脚本发现服务代码或 Web 文件有变化时会重启 Nova Issues。显示运行版本后，刷新浏览器即可使用新页面。

一次启动两个服务时，在 Nova 根目录运行 `python3 Tools/nova-services-macos.py`。Verdaccio 的独立入口和配置见 [内网 Verdaccio](../nova-verdaccio/README.md)。

端口由无法确认归属的进程占用时，脚本会报告端口和 PID。

重启后由 macOS 自动启动，无需登录桌面。服务日志在 `~/Library/Logs/nova-services/`。如果后台服务无法读取位于桌面或文稿中的项目，请将 Nova 放在普通目录，例如 `~/Nova`。

## 接入项目

内部部署地址固定为 `http://172.16.22.175:8787`。更新服务机代码并启动服务后，安装或升级到包含自动上报的 Nova Framework，再打开 Unity Editor。第一份报告会自动登记项目和工作区。工作站无需设置环境变量或填写地址和凭据。

自动登记只接受私有 IPv4 或本机环回来源。服务按来源限制新增工作区的频率。工作区凭据仅用于上报自身数据，维护台仍使用登录账号。

项目组更新 Nova 并打开 Unity 后，可在 Codex 内审查「Nova 自动反馈机制」并自行启用。启用反馈机制后，项目可以照常开发、构建和运行，请放心使用。授权后的用量和问题报告沿用现有上报接口。详见 [项目组使用说明](../../Assets/Framework/Docs/Onboarding/NOVA_ISSUES.md#在-codex-中启用-nova-自动反馈机制)。

已有统一部署的工作站可用下列脚本准备 [Codex 受管 Hooks](https://learn.chatgpt.com/docs/hooks#managed-hooks-in-local-codex)，继续沿用原钩子。

```sh
python3 -B provision.py --output /absolute/managed/nova-issues
```

在单独复制的服务目录运行此命令时，用 `--agent-source` 指向 Framework 包内的 `Agents/Tools/nova_issues_agent.py`。通过 `--python` 指定钩子所用的 Python。

点击「总览」的「已登记项目」可查看全部项目，点击「过去 24 小时有上报」会在「项目」页选中同名上报时间筛选。点击「待处理问题」或「已处理问题」会打开「问题」页并选中对应状态，同时清除关键词和项目筛选。

侧栏各页的列表可在表格底部独立翻页和设置每页条数。「总览」和「项目」默认每页 10 条，可选 50、100 或 200 条。「问题」和「使用记录」默认每页 50 条，可选 100 或 200 条。筛选、排序或调整每页条数时回到第一页，维护台刷新数据时保留当前页。

维护台在后台检查新上报，先更新侧栏的未读数字，阅读时页面保持当前位置。点击右上角「刷新」可更新当前的列表、使用分析和条目红点。

「问题」页默认显示待处理问题，按最新发生时间排列。「排序」可切换为按发生次数从多到少排列，也可组合项目、状态和关键词筛选。点击「影响项目」列的数字可查看项目名单，再点击名称打开项目详情。点击项目详情、问题详情或影响项目名单外的空白即可关闭。问题详情有未保存的修改时，关闭前会确认是否放弃。

「所属包 / 版本」显示最近一次发生记录确认的包名和版本。问题详情中的「发生记录」显示各次实际版本，编译和导入问题的「错误日志」展示已上报的包内位置、错误编号及诊断内容。

打开问题即可快速处理。状态可选「待处理」或「已处理」，负责人从现有维护账号中选择，也可设为「未分配」。每次保存都会追加一条处理记录，备注选填。旧记录中的已关闭和已合并归为「已处理」，其余归为「待处理」。已合并的问题仍可跳到原合并目标查看。

项目和问题详情底部都有红色「删除」按钮。确认后直接删除服务端记录。删除问题会一并清理发生记录和处理记录。删除项目会清理该项目的工作区和全部上报，仅由该项目上报的问题也会删除。共同上报的问题按剩余记录重新统计。删除后的自动登记项目仍可由新的自动上报重新登记。

侧栏红色数字表示还有多少项待查看。「项目」按项目数，「问题」按问题数，「使用记录」按项目、模块和操作分组，只提醒近 30 天的上报。一个项目、问题或操作组收到多条上报时，侧栏计 1。

有未读上报的条目名称旁显示红点。点击项目、问题或操作名称，详情打开后将该条目标为已读。操作组内的未读条目全部查看后，侧栏数字减 1。同一浏览器按账号保留已读进度，首次登录从当时的数据开始计数。

这三个栏目都可勾选「仅未读」，在当前筛选条件内查看所有带红点的条目。关闭已查阅的详情后，该条目会从未读列表移出。「使用记录」的勾选项在「操作明细」上方。

问题的「发生次数」和使用记录的「记录数」会显示 `+N`。例如 `23 +2` 表示该条目有 23 条上报，其中 2 条未读。红点和增量使用同一批未读上报。

点击栏目右上角的「全部已读」，可将本栏已收到的未读上报一次标为已读，包括筛选隐藏的条目。后续收到的新上报会继续提示。

「项目」显示「Framework版本」、Unity 版本和最近环境上报时间。「Framework版本」取 Unity Editor 实际载入的版本，缺少该记录时显示「待上报」。点击团队列的「设置团队」或「修改」保存便于识别的团队名称，留空即可清除。自动登记的新项目团队留空。服务升级时清空旧自动登记项目的默认团队占位。

项目详情的「环境快照」按采集时间倒序排列，默认每页 10 条。标题显示该次的 Unity 版本、Nova 版本、上报人和采集时间。「最近环境上报」也按采集时间取最新一份，项目页可查看近 24 小时采集并上报的快照数。展开可看两项版本、游戏包名和「钩子追加」。新版快照优先使用当时 Editor 实际载入的 Framework 版本，旧快照沿用自己的已解析包版本。包名来自 Unity Player Settings。

「钩子追加」显示该份快照的配置状态和数量，例如「已加全（6/6）」「待追加（0/6）」或「不完整（5/6）」。总数按该次 Framework 提供的钩子核对，已加数量按匹配的定义和校验通过的绑定脚本计数。数量未记录时显示原来的状态。升级沿用可校验的既有定义及脚本，成员的授权状态在 Codex 中查看。

自动登记的「标识」通常由 `auto-` 和 Unity 工程的 `productGUID` 组成，用于把同一工程的多份上报归到同一项目。工作区编号由 Nova 在当前本地工程首次初始化上报时随机生成，用于识别各工程副本。两者保存在工程的 `Library/Nova/Issues/agent.sqlite3`。

环境快照的「上报人」取该工程执行 `git config --get user.name` 的结果。工程级设置优先，没有覆盖时沿用生效的全局设置。快照缺少可显示名称时显示「暂无 Git 名称」。

「操作系统」显示上报机器运行 Unity Editor 所用的 macOS、Windows 或 Linux。旧快照缺少该字段时显示「待上报」。

环境快照的「已启用」以多列展示 SDK 和 Kit 的模块名称，例如 TGA、Ad 和 GameLogin。

展开环境快照后，「包清单」下分为「非最新版本」「最新版本」「随 Unity 版本」「其他版本」。各组标题显示包的数量，默认收起。缺少可比较版本的包归入「其他版本」。各组按 `com.solotopia.*`、内网仓库包、其他包的顺序排列，同类按包名排序。Solotopia 仓库版本来自 `dist-tags.latest`。第三方注册包可从 Unity 官方仓库、AppLovin 和 OpenUPM 补查。维护台用快照中的 Unity 版本匹配包清单声明的最低要求，选择可用的最高版本。已使用预发布版的包也检查兼容的预发布版。项目安装时由 Unity Package Manager 解析实际依赖。

绿色表示当前版本已经是查到的最新版本，橙色表示版本不同。内置包显示「随 Unity」，缺少可核实版本的包显示横线。成功的仓库查询缓存五分钟。展开历史快照时会按该快照的 Unity 版本查询，已有上报可直接使用。

「模块接入与使用」的「状态」列同时展示工具配置、连接和模块使用情况。一般模块收到实际成功上报时显示绿色「有成功记录」，只有尝试上报时显示「待验证」，没有记录时显示「暂无上报」。表只收到注册上报时显示「已注册」。维护台用「PlugPals」显示仓库查询及其包操作。

每个工作区的 Editor 每小时检查一次环境。首次采集或发现变化时上报，环境相同则跳过。编译和重启沿用本地最近一次检查时间。工作区队列从工程 Library 后台发送，服务暂不可用时按原编号和发生时间补发。

「使用记录」默认显示全部来源，按每组操作最近一次实际发生的时间排列。时间筛选可选 1、7、14 或 30 天。「排序」可切换为按记录数从多到少排列。`Editor` 记录来自实际执行或手动导出，`Codex` 记录来自已启用的反馈钩子。同一 Action 同时被两侧观察时只保留一条 Action 记录，采用 Editor 结果并保留 Codex 入口。缺少来源的历史记录显示「未标注」。已识别的 Unity 检视面板导出归为「手动操作」。点击操作名称可查看用途，点击「记录数」可查看每条记录的时间与结果。

「使用分析」和「操作明细」使用同一批上报。「记录数」统计收到几条上报，「调用次数」统计实际调用几次。一次 Play 上报可以包含多次调用。模块排行的开发次数包含手动操作。Config 导出等专项工具按 Editor 的执行结果计数，封装它们的 Action 回执只计记录数。展开 Play 记录可看到每批最后一次调用的时间和批内次数。旧版只上报 Play 场次时，调用次数显示「—」。

两个排行各有独立的区域和分页。宽屏并排显示，分页栏沿底部对齐，窄屏上下排列。明细和图表共用时间、项目、模块、来源、操作类型和「仅未读」筛选。图表默认每页 10 条，随整页滚动。搜索名称只筛选当前排行。左侧的「只看历史无上报」按所选项目的全部历史判断。服务更新前已收到的专项和 Play 上报直接进入明细，从新收到的上报开始提示未读。

操作、来源和操作类型按明细的记录数计算占比，包含失败、阻塞和 Skill 查阅。有过上报但当前筛选下为零的类型会显示「有历史记录」。

项目详情的「协作记录」展示最近 30 天发生的任务阶段、查阅和操作回执，也收录 Editor 启动、Framework 升级、队列恢复及正式发布的结果。按任务或操作编号查看相关记录。

## 接口

支持 `days` 的维护接口可查询 1～30 天，按实际上报的发生时间筛选。

| 接口 | 用途 |
|---|---|
| `GET /health` | 查看服务、协议版本、自动上报能力和当前运行版本 `revision`。`project_snapshot_details`、`git_user_name_reporting`、`hook_activity`、`compact_reports` 分别表示支持项目详情、Git 名称、钩子运行和精简报告。 |
| `POST /api/v1/auto-reports` | 接收工作区上报。请求包含项目名和 `report` 信封，首次成功时自动登记项目和工作区。 |
| `POST /api/v1/reports` | 接收旧版客户端或接入方凭项目令牌提交的报告。 |
| `GET /api/v1/issues/<id>` | 用项目令牌查询该项目已上报问题的处理状态及修复版本。 |
| `POST /api/session` | 浏览器登录。维护接口使用会话 Cookie，变更请求校验来源和 CSRF。 |
| `GET /api/session` | 恢复维护台登录并续期新版会话的 Cookie。 |
| `GET /api/maintainer/issues` | 登录后按 `project`、`status` 和 `q` 筛选问题。`sort=latest` 按最近发生时间排序，`sort=count` 按发生次数排序。 |
| `GET /api/maintainer/usage` | 登录后按 `days`、`project`、`module`、`source` 和 `entry` 筛选使用记录。`entry=legacy` 筛选未标注操作类型。`sort=count` 按记录数排序，`sort=latest` 按最近使用时间排序。 |
| `GET /api/maintainer/usage-occurrences` | 登录后按操作分组 `key` 和 `days` 分页读取发生时间和 Play 批次次数。 |
| `POST /api/maintainer/issues`、`POST /api/maintainer/usage`、`POST /api/maintainer/usage-analysis` | 沿用各自的查询参数，请求体传入当前浏览器的 `after`、`through` 和 `viewed`，按未读条目分页或统计。 |
| `GET /api/maintainer/usage-analysis` | 登录后按与明细相同的时间、项目、模块、来源和操作类型返回上报条数、模块开发及 Play 次数和所选项目的历史证据。 |
| `GET /api/maintainer/issues/<id>/projects` | 登录后读取该问题影响的全部项目名称和标识。 |
| `GET /api/maintainer/issues/<id>` | 登录后读取问题详情、发生记录、处理记录及可选的负责人账号。 |
| `POST /api/maintainer/issues/<id>` | 登录后用 `revision` 保存 `status`、`assignee` 和 `note`。留空 `assignee` 表示未分配，留空 `note` 表示本次没有备注。 |
| `DELETE /api/maintainer/issues/<id>` | 登录后删除问题及其发生记录和处理记录。 |
| `GET /api/maintainer/projects/<id>` | 登录后读取项目详情，包括环境快照及各工作区最新的钩子运行记录。 |
| `GET /api/maintainer/projects/<id>/workflow` | 登录后分页读取协作记录。传 `trace` 可匹配任务或操作回执。 |
| `GET /api/maintainer/projects/<id>/snapshots` | 登录后按采集时间倒序查看项目快照。使用 `page` 和 `page_size` 翻页，默认每页 10 条。 |
| `DELETE /api/maintainer/projects/<id>` | 登录后删除项目及关联工作区和上报。 |
| `POST /api/maintainer/projects/<id>/team` | 登录后保存项目团队名称 `team`，用编辑时读到的 `previous_team` 校验并发修改。 |
| `GET /api/maintainer/projects/<id>/package-versions` | 登录后读取 Solotopia 包版本。传 `snapshot=<id>` 时按该环境快照补查第三方包的兼容版本，`pending` 表示还有查询进行中。 |

上报信封包含 `version=1`、`id`、`workspace`、`observed_at`、`kind` 和 `data`。两项 ID 为 32 位小写十六进制，时间为 Unix 秒。接收成功返回 `accepted`、原 `id` 和接收时间，Issue 同时返回编号。同一项目同一上报 ID 重试返回原回执。相同 ID 内容不同返回 409。

新版环境快照可携带 `application_identifiers`、`codex_hooks`、`git_user_name` 和 `operating_system`。服务通过 `snapshot_operating_system` 声明接受操作系统字段。`snapshot_version_and_hook_counts` 表示可接收 `editor_framework` 和 `codex_hook_counts`，分别为 Editor 实际载入的 Framework 版本和匹配脚本的已加、应加数量。服务升级并声明支持对应字段前，携带这些字段的快照保留在本机队列，服务就绪后按原编号补发。

`workflow_monitoring` 表示支持协作回执、批处理与桌面端构建结果，以及新接入的 Editor Play 模块。旧服务运行期间，这些报告在本地等待服务升级。

服务声明 `compact_reports` 后，新报告的包清单只携带标识、版本和来源，问题省去旧优先级，工作区实施快照以实际载入版本为准。升级过程仍携带请求目标版本。旧客户端原有格式继续可用，失败重试沿用已入队的原编号和正文。服务确认编号后，本地删掉正文并保留短回执供去重。

新版工作站通过 `snapshot` 上报基础环境。`usage` 中的 Codex 调用带 `source=codex` 和入口类型 `entry`，Editor 回执带 `source=editor` 和 `entry=action`。可关联的 Action 带 `action_operation` 摘要，范围为同一项目工作区。服务端继续兼容旧版 `task` 报告。旧版服务未声明 `codex_telemetry` 时，带来源的使用记录留在本地待补发。

`implementation` 记录本地工程配置、Editor 实际载入的 Framework 版本和工具终态。「项目」列表和「工程实施」按项目汇总已确认的工具配置。项目收到一条成功结果后，该项持续标记为「已验证」，最近结果按实际发生时间更新。将鼠标移到验证标记可查看成功的时间、版本和来源。旧版服务缺少 `implementation_tracking` 时，新报告留在工程本地待补发。

支持 `unity_mcp_monitoring` 的服务接收 Coplay Unity MCP 状态 `stage=unity-mcp-connection` 和工具调用 `entry=unity-mcp`。项目详情按实际发生时间选取各工作区最新状态。工具调用由成员启用「Nova 记录 Unity MCP 使用」后上报，每个工具回执计一次，Nova Action 沿用原入口计数。

「模块接入与使用」下方的「项目构建记录」集中显示 Android、iOS 和 WebGL，默认每页 10 条。各平台按构建类型、开发构建设置和入口显示最近结果，验证标记对应实际工作区、版本和时间。

支持 `execution_evidence` 的服务接收 Editor Play 模块使用。`feedback_timings` 支持 Action 关联和表注册等记录。携带 `calls` 的 Play 批次在服务声明支持 `play_call_counts` 后补发，每批取最后一次调用的发生时间。仅含场次的旧记录保留原义。

「模块接入与使用」显示近 30 天的开发调用数和 Editor Play 成功调用数，时间统一显示在「最近记录」。同一模块的不同上报标识合并为一行，存档和振动各自汇总开发与 Play 记录。开发调用按实际上报执行计数，已由工具入口记录的 Config 导出、Pipify Batch、Bundle 构建不再叠加 Action 调用。表注册保留独立记录，实际取表才计入 Play 调用。

旧客户端发送 Editor 启动阶段、Framework 升级阶段、Pipify 步骤、仅脚本构建或非 Android／iOS 的 Player 构建时，服务确认原编号并跳过记录。SDK 预加载及相关超时反馈沿用同一处理方式。已经收到的历史保存在原表。工具终态在接收时精简为汇总所用的字段。

`hook_activity` 对应实际运行。`data` 仅包含 `framework`，工作区和运行时间使用信封里的 `workspace`、`observed_at`。服务就绪并在 `/health` 声明支持 `hook_activity` 后，工作区按原编号送出待发记录。

## 验证

操作提示和模块清单随服务目录部署。[Nova 操作说明](usage-descriptions.json) 来自 Project Action 声明、Skill 目录和现有上报入口。[Unity CLI 操作说明](unity-usage-descriptions.json) 预置当前 Unity Pipeline 自带命令及 Unity Editor 弹窗命令。[Runtime 模块清单](usage-modules.json) 来自 Framework Runtime 文档。项目或第三方包自行扩展的 Unity CLI 命令显示已知的调用入口，具体用途以提供方的定义为准。

修改 Nova Action、Skill 或 Runtime 模块导航后，从 Nova 开发仓运行 `python3 -B Tools/nova-issues/build_usage_descriptions.py` 更新目录。升级 Unity Pipeline 后，可在同一命令后加 `--unity-project .`，从已打开的 Nova Editor 补齐新增官方命令，同时保留已有中文说明和历史操作。三份目录一同纳入服务运行版本。

```sh
python3 -B -m unittest discover -s Tools/nova-issues -p 'test_*.py' -v
```

测试使用独立临时项目与环回服务。两台实际工作站的 LAN 路由、服务机证书和公司下发策略在部署时验证。
