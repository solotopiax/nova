# Nova Agent 快速入口

这是一份给 Agent 的 Level 0 路由。首次接触 Nova、接手已有项目、调整场景或资源流程时，先用本页建立最低限度的正确上下文，再按任务进入 Level 1 页面和现有模块文档。

## AI Project Skills：共同底线、自动发现与执行边界

所有 `nova-project-*` Skill 触发后都先读取本页。本页定义项目组消费态日常任务的共同底线；它不是需要用户手工选择或按顺序执行的 Skill。

需要理解 Skill、Router、Workflow、Operation、Action 与 MCP 各自做什么，先读 [用人话理解 Skill、Action 与 MCP](Onboarding/AI_SKILLS_ACTIONS_MCP.md)。该页把任务拆解和请求传递分别说明，并解释 Nova MCP 包如何扩展 Unity MCP。

所有 `nova-project-*` Skill 的对用户输出必须先说结论，用人话、简洁直接地说明必要信息；只有确有助于决策时才补充术语、过程和细节，不啰嗦。

`Assets/Framework/Agents/` 是开发态和消费态共享的唯一 Git 真源。在 Nova 开发仓中，Editor 会用这份真源在仓库根 `.agents/skills/` 生成 `nova-project-*` 的自动生成受管同步副本（下文简称“投影”），让开发者快速验证项目组消费态下的发现和触发效果；发布后，消费项目从已安装的 Framework 包获得同一份内容。该目录只包含项目组消费态 Skill：每个 Skill 的目录名、Catalog id 与 `SKILL.md` 的 `name` 都必须使用 `nova-project-*`。框架开发态 Skill 保留在仓库根 `.agents/skills/` 的既有 `nova-*` 命名空间；两者可在开发仓并列发现，但只有 `nova-project-*` 进入本 Catalog 与 UPM 同步范围。

Nova Project Skills 随 `com.solotopia.nova.framework` 同版本管理。用户安装或升级 Nova 后首次打开 Unity，Editor 会在编译和包更新结束后检查，并按需把包内 Catalog Skill 同步为项目根 `.agents/skills/` 下的受管同步副本，供宿主和 Agent 发现；不需要执行 `sync` 或手工复制。

Skill 发现与 MCP Tool 发现是两条不同链路：33 个 `nova-project-*` 是 Agent 从 `.agents/skills/` 读取的任务说明，不会被 MCP Provider 逐个注册成 33 个 Tool。对外 Nova MCP UPM 包当前默认适配 Unity MCP，只注册一个 Nova Tool：`nova_project_action`；该 Tool 再通过 live `describe` 公开当前安全开放的 Action。NovaSpark 会先在项目 manifest 中配置 OpenUPM 与精确 scope `com.coplaydev.unity-mcp`，再安装 Framework 及其 Nova MCP 依赖；Unity MCP 由 Nova MCP 包的 semver 依赖自动解析，不作为项目 direct Git dependency。安装包不能替外部 Agent 启动 Server、占用端口或热刷新既有会话；首次连接或包升级后，宿主仍需连接当前 MCP Provider，已有会话未刷新 Tool 列表时需重连或开启新会话。

三处位置的职责不同：

- 开发仓 Git 真源：`Assets/Framework/Agents/Skills/`。
- 已安装包快照：`PackageInfo.resolvedPath/Agents/Skills/`；它随当前 Framework 版本提供，只读且不可反向编辑。
- Agent 发现入口：`<project>/.agents/skills/nova-project-*`；这是自动生成的受管同步副本（简称“投影”），不是第二真源。

`.agents/skills/` 遇到同名用户目录、已修改或缺失的受管同步副本时会保守保留现场，并以 `partial` 报告无法完成的项；其他可安全完成的项仍会继续。同步器先恢复已有合法事务，再解析当前包真源；因此真源缺失时，已有事务仍可能完成已登记的投影写入或清理。当前真源不可用会阻止建立新同步计划，并在 Unity Console 报告错误。

`<project>/.agents/nova-skills.lock.json`、`.agents/nova-skills.transaction.json` 与 `.agents/.nova-skills-staging/` 同样属于项目本地生成状态，不应作为 Framework 或 Skill 真源提交。自动同步器不会创建、修改或覆盖消费者项目的 `.gitignore`。项目未自行跟踪 `.agents/` 时，无需改动 `.gitignore`；只有项目自行跟踪 `.agents/` 且用户已明确确认本次写入消费者 `.gitignore` 时，Agent 才可添加以下规则。不要忽略整个 `.agents/`，以免吞掉项目自己维护的 Agent 配置：

```gitignore
/.agents/skills/nova-project-*/
/.agents/nova-skills.lock.json
/.agents/nova-skills.transaction.json
/.agents/.nova-skills-staging/
```

全量可发现不等于全量按顺序执行。当前 33 项消费态能力只根据自然语言任务的目标、范围和约束匹配需要的 Skill；写入、构建、外部发布和 Git 等副作用仍遵守对应 Skill 的确认门。当前未覆盖的任务仍按本页和具体模块 Docs 推进；完整目录和边界见 [Nova Project Skills](../Agents/INDEX.md)。

函数签名和局部说明来自源码/XML，流程、配置与生命周期说明来自所属 Docs，执行保护来自 Skill/Action 契约。`Agents/coverage.json` 和自动生成的便携符号索引提供导航；本地查询缓存位于 `Library/Nova/AgentIndex/`，可删除重建，不入 Git。任务结果在本次执行后于对话报告，不查询或分发历史验收档案。

函数、重载与参数的确定性查询见 [自然语言开发入口](Onboarding/NATURAL_LANGUAGE_DEVELOPMENT.md)；函数声明与运行时有效配置分别核实，源码位置变化后先刷新索引，不能沿用旧偏移；只按实际行为变化审查受影响实现。

## 日常任务如何无缝使用 Skills

配置或上传 Google Play AAB、排查 supply 权限/版本号失败，进入 `nova-project-upload-google-play`；仅验证仍会真实上传，上传成功与审核完成、用户可下载分开核实。

“导出 Tables”或“生成 Table 数据和类型”直接进入 `nova-project-export-tables`；只有还要配置表源、加载映射并验证运行时读取时，才进入 `nova-project-integrate-table`。

“只补埋点表已有说明文字”复用 SDK/Kit Skill 中的 `tracks-source-text` 分支和已有表源事务。Agent 读取项目自有 Workbook 与精确文字写集，核对计划、实际保存、整表与非目标内容，完成 Verify 和清场；无需重复安装或初始化 SDK。新增事件结构、生成汇总或验证厂商接收时进入相应完整任务。

“只导出已有声音、振动或网络表的代码和数据”复用对应接入 Skill 的 `export-code-data` 分支，读取真实源和导出设置，执行既有导出 Action，再核对正式产物与编译。无需为导出提供 AudioClip、业务调用点、真机体感或测试服务账号；完整业务接入仍按原运行与设备要求验收。分支由 Skill 契约定义，任务只引用 ID，不能自行更改最低证据或省略安全约束。

项目组只需用自然语言说明目标、已知范围和约束，不必记住或手工执行 Skill 名称。Agent 先从项目根 `.agents/skills/nova-project-*` 发现当前 Framework 自带的全量能力：任务不清楚时先用 `nova-project-router`，任务已明确时可直接进入对应 Operation。常见匹配包括：接手项目→`nova-project-check-readiness`；安装/升级最新普通 UPM 包或卸载 direct dependency→`nova-project-manage-upm-package`；升级 Nova Framework 自身→`nova-project-upgrade-framework`，由包外 UPM 宿主执行并在重载后核验 Skills/MCP 恢复；接入已发布 SDK/Kit→`nova-project-onboard-sdk-kit`；启动失败→`nova-project-diagnose-startup`；新增/更新页面→`nova-project-ui-create-view` / `nova-project-update-ui-view`；接入业务事件、存档或资源化 Content 场景→`nova-project-integrate-event` / `nova-project-integrate-persistence` / `nova-project-integrate-content-scene`；接入已确认协议的业务 HTTP API→`nova-project-integrate-network-api`；加入大厅 BGM、按钮点击音效、其他声音或振动→`nova-project-integrate-sound` / `nova-project-integrate-vibration`；新增业务流程→`nova-project-integrate-procedure`；打包前检查、既有构建失败与真机日志问题→`nova-project-preflight-build` / `nova-project-diagnose-build` / `nova-project-diagnose-device-runtime`；Android EDM4U 解析→`nova-project-resolve-android-dependencies`。刷新 HybridCLR 业务热更 DLL→`nova-project-refresh-hotfix-dlls`；生成 HybridCLR bridge/AOT/link.xml 预构建产物→`nova-project-generate-hybridclr-artifacts`。所有已注册 Project Action 默认进入 MCP 显式白名单，但写入、破坏性或外部操作仍必须经过 Plan、一次性确认、资源锁与 Verify。两项 HybridCLR Operation 都不代表 Bundle、最终 Player、CDN 或运行时成功，最终 Player 后的 `CopyAotDlls` 也不属于 Generate All。表、配置、本地化、资源、启动场景、Bundle、Player 分别进入同名领域 Operation；表驱动页面则由 `nova-project-data-driven-ui` 编排。

每个匹配到的 Skill 都按渐进式披露执行：L0 只用 Catalog、frontmatter 与本页发现能力；L1 读取当前 Skill 和契约，冻结输入、写入集、确认门与最低证据；L2 仅按当前分支读取需要的模块 Docs 和项目事实；L3 在确认后调用声明的 Action Adapter（C# API、Unity Editor/MCP、Pipify、CLI 或工作区编辑）；L4 只验证本次需要的证据。这样既能复用日常流程，也不会为了一个任务重复分析所有 Nova 模块。

Workflow 的 `requires` 只描述其内部 Operation 的依赖图，不影响安装、升级或发现范围。所有 Catalog Skill 随当前 Nova 版本全量提供；Workflow 只在确有多个依赖操作时编排 DAG，Unity Editor、AssetDatabase、活动场景和同一输出目录仍按单写者串行。

## 执行任务与清场的共同约束

每个匹配任务先从当前工程读取实参来源、当前值、声明默认与生效时机，冻结目标、最小写入集、实际 Action/API/CLI 和验收入口。路径、类型、GUID、已存在组件和配置由 Agent 查明；玩法、素材、协议或外部账号缺失时只保留依赖它们的具体缺项。固定操作直接复用已有接口；Action 按 live `describe → plan → execute → verify`，一般 Unity MCP 按真实 Schema、草稿检查、精确保存与读回执行。

业务接入可以在已授权的项目自有程序集与源码范围新增必要的 Service、调用点或生命周期胶水，保持项目现有结构，调用对应 Skill 已声明且当前版本确实存在的公开 API。先复用已有 Owner，明确输入/结果/错误、重复调用、异步取消与资源/监听释放；缺少现成业务调用点本身不等于永久阻断。需要 UI、表源、绑定、配置或 DLL 产物时继续复用命中的 Operation，不通过任意代码字符串、私有反射、第二套 Manager/注册器或手改生成物补空。缺真实资源、业务规则、唯一归属或协议时保持准确的 `blocked/partial`。

验证沿本次真实业务入口完成，在对话中分别报告源/配置保存、正式导出、编译、Play、Player、设备和外部服务证据；任务的 `validation` 采用 `verify-at-execution`，按 `required` 验收且 `retention=ephemeral`。调用返回、请求编号或局部 Action 成功只证明其自身范围，查询当前知识也不能替代本次验收。任何测试、诊断或一次性自动化结束后，清理本轮临时脚本与 `.meta`、runner、marker、日志、测试对象和临时产物，恢复测试态及非目标序列化/图集/场景改动。只清理能证明本轮所有权的内容，保留用户原有改动和要求保留的正式产物；删除 Unity 临时脚本后执行必要 Refresh，最后核对工作树及目标范围并明确报告清场。未完成事务仍需使用的备份和恢复标记按对应工具生命周期处理，不作历史验收清理。残留无法安全归属或最低证据不足时，任务仍保持未完成状态。

## 先检查项目，不要套模板

不要假定当前项目来自 Starter，也不要假定它必须采用固定目录、固定场景数或固定资源拓扑。先检查：

1. 当前启动场景和 Build Settings 中实际参与构建的场景。
2. 场景中 Nova 根节点来自哪个 Prefab，是否存在重复或手工拼装的残缺根节点。
3. 项目当前使用的资源系统、配置入口、构建入口和第三方插件。
4. 哪些目录与场景由项目明确交给 Nova 管理；不要接管未声明范围。
5. 用户本次要解决的具体任务，再决定需要阅读哪些模块文档。

若一个场景负责承载 Nova，必须使用框架提供的完整 canonical Prefab：`Assets/Framework/Prefabs/Nova.prefab`。不要新建空 GameObject 后只挂 `Nova`、`AssetComponent` 或少数组件来仿造它。

## 按任务检查与验证

Agent 按当前任务检查场景拓扑、必需引用、配置与生命周期。发现确定错误时说明证据和影响；推荐做法、所有权不明或与 Sample 不同的结构只作为建议，不自动改写工程。

Nova 不再提供 Project Guard 窗口、规则 Profiles 或进入 Play 前的自动检查。需要构建前置检查时，使用 `nova-project-preflight-build`，通过只读 Action `nova.project.build.inspect-readiness` 核对当前平台、启用场景、Config、YooAsset 与 HybridCLR。它不会自动修复或构建，也不代替本次 Play、Player、设备和外部服务验证。

发布任务按项目实际目标补足产物与运行证据；普通开发无需承担与本次修改无关的发布验收。Contract、Pipify 和分目录 `AGENTS.md` 可以帮助项目自动化，检查只覆盖项目声明的范围。

## 场景与资源底线

- 单场景、自定义启动链和多场景都可以；拓扑按项目实际需求选择。
- 多场景项目可以 Additive 加载 Content；Nova 不会自动加载 Content，加载时机和卸载生命周期由项目明确实现。
- `Resources/BuiltIn/**` 是框架的特殊内置资源区，允许通过 Resources 接口加载。
- UPM 包以及导入到 `Assets/**` 的第三方插件可以保留并使用自身的 Resources，不要求 Nova Contract 白名单。
- Nova 业务资源推荐使用 YooAsset/Bundle，以获得版本、卸载和发布能力；这个建议不能据此否定插件或未声明范围内的合法资源方案。
- Unity Build、`BuildPipeline.BuildPlayer` 和 `EditorUtil.Build` 都是合法构建入口；Pipify 是可选自动化入口，不是唯一入口。

## 按任务继续阅读

- [项目与场景结构](Onboarding/PROJECT_STRUCTURE.md)：识别 Nova 根、单/多场景与 Content 边界。
- [资源工作流](Onboarding/RESOURCE_WORKFLOW.md)：选择 YooAsset、BuiltIn Resources 或插件自有 Resources。
- [验证与构建](Onboarding/VALIDATION.md)：理解只读构建预检、按任务验证与直接 Unity Build。
- [框架文档索引](INDEX.md)：进入 Runtime、Editor 与具体模块事实。
- [架构总览](ARCHITECTURE.md)：理解 Component + Manager 分层。

## Agent 开工检查

- 已检查现有工程事实，没有把 Sample 或 Starter 当成强制模板。
- 承载 Nova 的场景引用完整的 `Assets/Framework/Prefabs/Nova.prefab`。
- 已确认运行拓扑中 Nova 的唯一性，以及 Content 的显式加载和卸载责任。
- 已区分业务资源、`Resources/BuiltIn` 与第三方插件 Resources。
- 已选择与任务相称的验证层级，按需调用只读构建预检。
