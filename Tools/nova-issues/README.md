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

「问题」页默认显示待处理问题，按最新发生时间排列。「排序」可切换为按发生次数从多到少排列，也可组合项目、状态和关键词筛选。点击「影响项目」列的数字可查看项目名单，再点击名称打开项目详情。点击项目详情、问题详情或影响项目名单外的空白即可关闭。问题详情有未保存的修改时，关闭前会确认是否放弃。

「所属包 / 版本」显示最近一次发生记录确认的包名和版本。问题详情中的「发生记录」显示各次实际版本，编译和导入问题的「错误日志」展示已上报的包内位置、错误编号及诊断内容。

打开问题即可快速处理。状态可选「待处理」或「已处理」，负责人从现有维护账号中选择，也可设为「未分配」。每次保存都会追加一条处理记录，备注选填。旧记录中的已关闭和已合并归为「已处理」，其余归为「待处理」。已合并的问题仍可跳到原合并目标查看。

项目和问题详情底部都有红色「删除」按钮。确认后直接删除服务端记录。删除问题会一并清理发生记录和处理记录。删除项目会清理该项目的工作区和全部上报，仅由该项目上报的问题也会删除。共同上报的问题按剩余记录重新统计。删除后的自动登记项目仍可由新的自动上报重新登记。

「项目」「问题」「使用记录」侧栏的红色数字是该栏的未读上报总数，有未读上报的条目名称旁显示红点。打开栏目、刷新、筛选或排序时，未读状态会保留。点击项目、问题或使用记录的操作名称，详情打开后，该条目的红点消失，侧栏数字减去这条对应的未读数量。同一浏览器按账号保留已读进度，首次登录从当时的数据开始计数。

问题的「发生次数」和使用记录的「次数」会在原次数后显示 `+N`。例如 `23 +2` 表示这条记录的总次数为 23，仍有 2 条未读上报。红点和增量使用同一批未读上报。

点击栏目右上角的「全部已读」，可将本栏已收到的未读上报一次标为已读，包括筛选隐藏的条目。后续收到的新上报会继续提示。

「项目」显示「Framework版本」、Unity 版本和最近环境上报时间。「Framework版本」取 Unity Editor 实际载入的版本，缺少该记录时显示「待上报」。点击团队列的「设置团队」或「修改」保存便于识别的团队名称，留空即可清除。自动登记的新项目团队留空。服务升级时清空旧自动登记项目的默认团队占位。

项目详情的环境快照显示游戏包名和「钩子追加」，各工作区的历史快照各自保留对应的版本及状态。包名来自 Unity Player Settings。反馈配置和绑定脚本齐备、尚未加入及部分缺失或校验失败分别显示「已加全」「待追加」「不完整」。升级会沿用完整的既有定义及脚本，成员的授权状态仍在 Codex 中查看。旧快照缺少这两项时显示「待上报」。

每个工作区的最新环境快照会在「钩子追加」内附上最近实际运行时间和对应的 Framework 版本。工作区首次成功运行时立即安排上报，之后有新运行时每小时最多刷新一次。维护台按客户端的实际运行时间取最新记录。

自动登记的「标识」通常由 `auto-` 和 Unity 工程的 `productGUID` 组成，用于把同一工程的多份上报归到同一项目。工作区编号由 Nova 在当前本地工程首次初始化上报时随机生成，用于识别各工程副本。两者保存在工程的 `Library/Nova/Issues/agent.sqlite3`。

环境快照的「上报人」取该工程执行 `git config --get user.name` 的结果。工程级设置优先，没有覆盖时沿用生效的全局设置。快照缺少可显示名称时显示「暂无 Git 名称」。

「操作系统」显示上报机器运行 Unity Editor 所用的 macOS、Windows 或 Linux。旧快照缺少该字段时显示「待上报」。

环境快照的「已启用」以多列展示 SDK 和 Kit 的模块名称，例如 TGA、Ad 和 GameLogin。

包清单先显示查到最新版本的包，其中版本不同的条目置顶。随后是「随 Unity」的包，最后是最新版本显示「—」的包。各组按 `com.solotopia.*`、内网仓库包、其它包的顺序排列，同类按包名排序。Solotopia 仓库版本来自 `dist-tags.latest`。第三方注册包可从 Unity 官方仓库、AppLovin 和 OpenUPM 补查。维护台用快照中的 Unity 版本匹配包清单声明的最低要求，选择可用的最高版本。已使用预发布版的包也检查兼容的预发布版。项目安装时由 Unity Package Manager 解析实际依赖。

绿色表示当前版本已经是查到的最新版本，橙色表示版本不同。内置包显示「随 Unity」，缺少可核实版本的包显示横线。成功的仓库查询缓存五分钟。展开历史快照时会按该快照的 Unity 版本查询，已有上报可直接使用。

环境首次识别，或 Framework、Unity 版本、游戏包名、项目钩子状态、Git 名称发生变化时上报。信息未变时，按工作站本地自然日在同一工作区首次 Editor 编译完成后上报一次。工作区队列从工程 Library 后台发送，服务暂不可用时按原编号补发。

「使用记录」默认显示全部来源，按每组操作最近一次实际触发的时间排列。「排序」可切换为按触发次数从多到少排列。`Editor` 记录来自实际执行或手动导出，`Codex` 记录来自已启用的反馈钩子。同一 Action 同时被两侧观察时只计一次，使用 Editor 执行结果并保留 Codex 入口。能按标识识别的历史记录显示对应入口，缺少来源的历史记录显示「未标注」。将鼠标停在带点线的操作名称上即可查看用途。

顶部「使用分析」可切换模块、操作、来源和入口，按次数排列并显示占比。明细和图表共用时间、项目、模块和来源筛选。图表统计筛选范围内去重后的全部使用记录，包含失败和阻塞。模块按已识别的开发调用统计，其他维度包含 Skill 查阅。模块和操作可以搜索，搜索只改变图中显示的类型。

图表保留已知类型和历史类型。本期为零时，有过上报的类型标为「有历史记录」。「历史无上报」按所选项目的全部上报时间和来源判断。模块查看历史时也参考专项工具和 Editor Play 的上报，可用「只看历史无上报」找出有开发调用采集入口但尚无使用证据的模块。缺少开发调用采集的模块显示「暂无开发调用采集」。各模块的 Editor Play 次数可在项目详情的「模块接入与使用」查看。

## 接口

| 接口 | 用途 |
|---|---|
| `GET /health` | 查看服务、协议版本、自动上报能力和当前运行版本 `revision`。`project_snapshot_details`、`git_user_name_reporting`、`hook_activity`、`compact_reports` 分别表示支持项目详情、Git 名称、钩子运行和精简报告。 |
| `POST /api/v1/auto-reports` | 接收工作区上报。请求包含项目名和 `report` 信封，首次成功时自动登记项目和工作区。 |
| `POST /api/v1/reports` | 接收旧版客户端或接入方凭项目令牌提交的报告。 |
| `GET /api/v1/issues/<id>` | 用项目令牌查询该项目已上报问题的处理状态及修复版本。 |
| `POST /api/session` | 浏览器登录。维护接口使用会话 Cookie，变更请求校验来源和 CSRF。 |
| `GET /api/session` | 恢复维护台登录并续期新版会话的 Cookie。 |
| `GET /api/maintainer/issues` | 登录后按 `project`、`status` 和 `q` 筛选问题。`sort=latest` 按最近发生时间排序，`sort=count` 按发生次数排序。 |
| `GET /api/maintainer/usage` | 登录后按 `days`、`project`、`module` 和 `source` 筛选使用记录。`sort=count` 按触发次数排序，`sort=latest` 按最近使用时间排序。 |
| `GET /api/maintainer/usage-analysis` | 登录后以使用记录的相同筛选返回四个维度的完整计数及所选项目的历史计数。 |
| `GET /api/maintainer/issues/<id>/projects` | 登录后读取该问题影响的全部项目名称和标识。 |
| `GET /api/maintainer/issues/<id>` | 登录后读取问题详情、发生记录、处理记录及可选的负责人账号。 |
| `POST /api/maintainer/issues/<id>` | 登录后用 `revision` 保存 `status`、`assignee` 和 `note`。留空 `assignee` 表示未分配，留空 `note` 表示本次没有备注。 |
| `DELETE /api/maintainer/issues/<id>` | 登录后删除问题及其发生记录和处理记录。 |
| `GET /api/maintainer/projects/<id>` | 登录后读取项目详情，包括环境快照及各工作区最新的钩子运行记录。 |
| `DELETE /api/maintainer/projects/<id>` | 登录后删除项目及关联工作区和上报。 |
| `POST /api/maintainer/projects/<id>/team` | 登录后保存项目团队名称 `team`，用编辑时读到的 `previous_team` 校验并发修改。 |
| `GET /api/maintainer/projects/<id>/package-versions` | 登录后读取 Solotopia 包版本。传 `snapshot=<id>` 时按该环境快照补查第三方包的兼容版本，`pending` 表示还有查询进行中。 |

上报信封包含 `version=1`、`id`、`workspace`、`observed_at`、`kind` 和 `data`。两项 ID 为 32 位小写十六进制，时间为 Unix 秒。接收成功返回 `accepted`、原 `id` 和接收时间，Issue 同时返回编号。同一项目同一上报 ID 重试返回原回执。相同 ID 内容不同返回 409。

新版环境快照可携带 `application_identifiers`、`codex_hooks`、`git_user_name` 和 `operating_system`。服务通过 `snapshot_operating_system` 声明接受操作系统字段。服务升级并声明支持对应字段前，携带这些字段的快照保留在本机队列，服务就绪后按原编号补发。

服务声明 `compact_reports` 后，新报告的包清单只携带标识、版本和来源，问题省去旧优先级，工作区实施快照以实际载入版本为准。升级过程仍携带请求目标版本。旧客户端原有格式继续可用，失败重试沿用已入队的原编号和正文。服务确认编号后，本地删掉正文并保留短回执供去重。

新版工作站通过 `snapshot` 上报基础环境。`usage` 中的 Codex 调用带 `source=codex` 和入口类型 `entry`，Editor 回执带 `source=editor` 和 `entry=action`。可关联的 Action 带 `action_operation` 摘要，范围为同一项目工作区。服务端继续兼容旧版 `task` 报告。旧版服务未声明 `codex_telemetry` 时，带来源的使用记录留在本地待补发。

`implementation` 记录本地工程配置、Editor 实际载入的 Framework 版本和工具终态。「项目」列表和「工程实施」按项目汇总已确认的工具配置。项目收到一条成功结果后，该项持续标记为「已验证」，最近结果按实际发生时间更新。将鼠标移到验证标记可查看成功的时间、版本和来源。旧版服务缺少 `implementation_tracking` 时，新报告留在工程本地待补发。

支持 `unity_mcp_monitoring` 的服务接收 Coplay Unity MCP 状态 `stage=unity-mcp-connection` 和工具调用 `entry=unity-mcp`。项目详情按实际发生时间选取各工作区最新状态。工具调用由成员启用「Nova 记录 Unity MCP 使用」后上报，每个工具回执计一次，Nova Action 沿用原入口计数。

「SDK 接入与构建」汇总各本地工程最新上报的 SDK 清单。任一清单同时显示 SDK 已安装、独立配置已启用时，标为绿色「是」，其余显示黄色「否」。广告渠道和 IAP 商店由主包配置管理，适配包悬停显示配置归属。Android／iOS 构建按平台、构建类型和开发构建设置显示验证状态和最近结果。

支持 `execution_evidence` 的服务接收 Editor Play 模块使用。`feedback_timings` 支持 Action 关联和表注册等记录。携带 `calls` 的 Play 批次在服务声明支持 `play_call_counts` 后补发，每批取最后一次调用的发生时间。仅含场次的旧记录保留原义。

「模块接入与使用」显示近 30 天的开发调用数和 Editor Play 成功调用数，时间统一显示在「最近记录」。开发调用按实际上报执行计数，已由工具入口记录的 Config 导出、Pipify Batch、Bundle 构建不再叠加 Action 调用。表注册保留独立记录，实际取表才计入 Play 调用。

旧客户端发送 Editor 启动阶段、Framework 升级阶段、Pipify 步骤、仅脚本构建或非 Android／iOS 的 Player 构建时，服务确认原编号并跳过记录。SDK 预加载及相关超时反馈沿用同一处理方式。已经收到的历史保存在原表。工具终态在接收时精简为汇总所用的字段。

`hook_activity` 对应实际运行。`data` 仅包含 `framework`，工作区和运行时间使用信封里的 `workspace`、`observed_at`。服务就绪并在 `/health` 声明支持 `hook_activity` 后，工作区按原编号送出待发记录。

## 验证

操作提示和模块清单随服务目录部署。[Nova 操作说明](usage-descriptions.json) 来自 Project Action 声明、Skill 目录和现有上报入口。[Unity CLI 操作说明](unity-usage-descriptions.json) 预置当前 Unity Pipeline 自带命令及 Unity Editor 弹窗命令。[Runtime 模块清单](usage-modules.json) 来自 Framework Runtime 文档。项目或第三方包自行扩展的 Unity CLI 命令显示已知的调用入口，具体用途以提供方的定义为准。

修改 Nova Action、Skill 或 Runtime 模块导航后，从 Nova 开发仓运行 `python3 -B Tools/nova-issues/build_usage_descriptions.py` 更新目录。升级 Unity Pipeline 后，可在同一命令后加 `--unity-project .`，从已打开的 Nova Editor 补齐新增官方命令，同时保留已有中文说明和历史操作。三份目录一同纳入服务运行版本。

```sh
python3 -B -m unittest discover -s Tools/nova-issues -p 'test_*.py' -v
```

测试使用独立临时项目与环回服务。两台实际工作站的 LAN 路由、服务机证书和公司下发策略在部署时验证。
