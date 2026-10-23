# Nova Project Skills

这是 Nova Framework 随 UPM 包发布的、面向项目组的 AI Skill 系统。它帮助 Agent 完成项目接入、业务 UI、数据配置、构建和诊断；不用于开发或改造 Nova 框架本身。

首次了解这些概念时，先读 [用人话理解 Skill、Action 与 MCP](../Docs/Onboarding/AI_SKILLS_ACTIONS_MCP.md)：Skill 安排任务，Action 执行具体操作，MCP 传递工具请求；Router、Workflow、Operation 是三种 Skill。

## 执行模型

```text
用户意图（不是 Skill）
  → Router Skill
  → Workflow Skill（按依赖图编排，可选）
  → Operation Skill（可单独验收）
  → Action Adapter（Unity Editor 自动化通道 / Pipify / 脚本 / API）
```

这不是固定顺序：用户提出一个独立操作时可直接进入 Operation；Workflow 只在多个操作存在依赖时生成 DAG。Unity Editor、AssetDatabase、活动场景和同一导出目录均视为单写者资源，不能因 DAG 而盲目并行。

## 渐进式披露与真实执行

每次只加载完成当前任务所需的层级，避免让 Agent 重复分析无关模块：

1. L0：Catalog、frontmatter 与 [Agent 快速入口](../Docs/START_HERE.md) 只用于发现和共同底线。
2. L1：命中后只读当前 `SKILL.md` 与 `references/contract.json`，冻结输入、写入集、确认门和最低证据。
3. L2：仅在当前决策分支需要时读取该 Skill 指向的模块 Docs、项目事实或直接依赖；不预加载所有 Nova 文档。
4. L3：用户确认写入、构建或外部副作用后，才调用已声明且当前真实可用的 Action Adapter。交互 Agent 调 Unity C# Project Action 目前只能通过已安装 MCP 的 `nova_project_action`，且仅限 Tool `describe` 返回的 Action ID；`csharp-api` 只描述 Framework、人工或既有 CI 的底层实现入口，不等于 Agent 可直接执行任意 C#。
5. L4：只收集本次契约要求的最小证据；必填输入未确认按 Skill 返回 `blocked`，输入已确认且已执行但未达到更高证据层级时才返回 `partial`，不把编译或旧产物冒充成功。

`requires` 只表示某个 Workflow 内部的 Operation DAG，既不控制安装，也不控制全量同步。每次安装或升级 Nova 后，Catalog 的全部 Skill 仍会自动发现；自然语言任务只触发其中匹配的一项或少数组合。

无依赖 Operation 可在同一 `references/contract.json` 的 `taskProfiles` 中定义独立任务分支；Workflow 分支还必须显式声明 `requires`，选择原依赖图中的 Operation 子集。任务只引用已授权的分支 ID，直接入口必须与分支白名单一致；选中子节点的输入、副作用和最低证据继续生效，未选子节点的任何执行入口不能借入。分支继承父及子节点原写入范围、禁止项、锁、幂等与确认规则，证据仍冻结完整父工作流和全部原依赖的 `SKILL.md/contract.json`。省略分支继续原完整合同。 Unity 自动同步器与 Python 校验器都在建立投影前校验分支及完整原依赖：未选子节点入口即使也列在父契约中，仍必须显式选中该子节点；父与已选子节点的外部、凭据和构建门不能省略。

Tracks 的 `tracks-source-text` 分支显式选择空子依赖，仅通过共享表源 CLI 维护项目自有既有事件页文字，以真实源表、收据、Verify 和清场验收；结构、正式汇总、SDK 初始化与厂商结果另验。

声音、振动与网络的 `export-code-data` 分支处理已有代码/数据的独立导出，要求实际产物与 Unity 编译；它无需业务播放、体感或服务请求输入。完整接入任务继续要求原 Play、设备或服务证据。`export-code-data` 仅适用于 all/code；仅数据导出未定义独立分支时沿用原全局契约，不从任务侧自降证据。

### C# Project Action

稳定、可类型化、可验证且可跨项目复用的单元操作优先注册为 `EditorUtil.AgentActions` 下的 C# Project Action。Action 使用 `nova.project.<domain>.<verb>` 稳定 ID，并独立声明操作类型、副作用、幂等性、必需证据、确认门和资源锁；一个 Action 可以被多个 Skill 复用，不与 Skill 一对一绑定。

Skill 仍负责自然语言路由、冻结业务输入和开放式决策；Action 负责确定性执行。业务代码设计、Prefab/Scene 结构设计、协议语义和真机体感不能被“万能反射 Action”替代。完整执行协议见 [EditorUtil.AgentActions](../Docs/Editor/EditorUtil/EditorUtil.AgentActions/EditorUtil.AgentActions.md)。

Catalog 与 Skill 契约通过 `external-write`、`credential` 单独声明外部写入和凭据使用；Python 校验器、JSON Schema 与 Unity 自动投影器使用同一组允许值。Google Play 上传复用已有 Pipify Action，其 `static` 最低证据并不省略 fastlane 上传结果；具体配置、诊断、验证上传和保存上传的验收按 Skill 模式执行。

当前源码 Registry 与 MCP 显式白名单须由工具核对精确集合；登记数量和本次 live `describe` 可用性分别报告。`Destructive`、`ExternalWrite`、`Credential` 与 `Delivery` 不作为隐藏 Action 的理由，但仍必须经过强类型 Schema、只读 Plan、一次性确认、资源锁、Receipt 与 Verify；Registry 与白名单不一致时 Gateway 整体 fail-closed，不能退化到 `execute_code`、反射或临时 C#。

## 目录、真源与自动发现

| 位置 | 责任 |
|---|---|
| `Assets/Framework/Agents/Skills/<skill-id>/` | 开发态和消费态共享的唯一 Git 真源；仅存项目组 `nova-project-*` Skill，并保持一层平铺 |
| `PackageInfo.resolvedPath/Agents/Skills/<skill-id>/` | 已安装 Framework 包内的只读真源快照，来自同一份 Git 真源 |
| `<Nova 开发仓>/.agents/skills/nova-project-*/` | 开发仓打开 Unity 时从 `Assets/Framework/Agents/` 生成的受管同步副本（简称“投影”），用于快速验证消费态发现效果；可与根目录既有开发态 `nova-*` Skill 并列 |
| `<consumer project>/.agents/skills/<skill-id>/` | 首次打开 Unity 后自动生成的全量受管同步副本（简称“投影”），仅供宿主和 Agent 发现，不是第二真源 |
| `catalog.json` | 全量 Skill 清单、能力分组、用户旅程、副作用、最低证据与机器可选项 |
| `coverage.json` | 功能族、Docs、既有工具与用户任务的导航；任务引用真实 Skill/入口，不维护第二份安全契约或 Action Catalog |
| `api-index.jsonl` | 发布器自动生成的便携符号/输入定位索引；开发快照与 SQLite 缓存只放 `Library/Nova/AgentIndex/` |
| `Schemas/` | Catalog、Skill 与任务覆盖的可校验结构 |

`Assets/Framework/Agents/` 只管理项目组消费态的 `nova-project-*` 命名空间；框架开发态 Skill 继续留在仓库根 `.agents/skills/` 的既有 `nova-*` 命名空间，不进入 Catalog 或 UPM 包。开发仓与消费项目使用同一 Catalog 和同一份 Skill 内容：前者用于快速验证，后者是正式项目组使用路径。

用户安装或升级 Nova 后，首次打开 Unity 时 Editor 会在编译和包更新结束后检查，并按需把包内全部 Catalog Skill 同步为项目根 `.agents/skills/` 下的受管同步副本；不需要执行 `sync` 或手工复制。该目录不是第二真源，不能反向编辑真源。

受管同步副本的冲突保留、已有事务恢复、真源缺失、项目本地状态和 Git 忽略边界统一见 [Agent 快速入口](../Docs/START_HERE.md)。能力分组只帮助 Router、导航和展示当前能力，不控制同步范围。Skill 也不是固定执行序列：Agent 只调用当前自然语言任务匹配的 Router、Workflow 或 Operation；多个有依赖的操作才由 Workflow 形成 DAG，且 Unity Editor、AssetDatabase、活动场景和同一导出目录仍是单写者资源。

## 当前 Skill 范围

| Skill | 类型 | 面向的自然语言任务 | 最低证据 |
|---|---|---|---|
| `nova-project-router` | Router | 不确定应调用哪个消费端能力时路由任务 | 静态定位 |
| `nova-project-check-readiness` | Operation | 接手、接入或修改前评估项目就绪条件 | 静态定位 |
| `nova-project-diagnose-startup` | Operation | 编译失败、Play 门禁、黑屏或启动链故障的只读诊断 | 静态定位 |
| `nova-project-setup-entry-scene` | Operation | 配置入口场景、Build Settings、Nova 根和 Content 职责 | Play |
| `nova-project-configure-runtime` | Operation | 检查、规范化或修改一个 Master 的三维配置及已开放面板，再正式导出 `ConfigRuntimeSO` | 编译；仅保存不代替 Runtime 导出 |
| `nova-project-edit-settings` | Operation | 解释或修改已有 Scene、Prefab、普通 SO 的字段与 UnityEvent，并核对保存 | 精确字段与文件保存；游戏行为另验收 |
| `nova-project-configure-pipeline` | Operation | 创建、克隆或配置已保存 Pipify Batch；按明确意图运行本地或远端步骤 | 配置保存；运行须有实际步骤/产物或外部证据 |
| `nova-project-manage-upm-package` | Operation | 计划、确认并验证单个最新版本安装/升级或 direct dependency 卸载 | UPM 精确解析状态 |
| `nova-project-upgrade-framework` | Operation | 由包外 UPM 宿主升级 direct Nova Framework，并在重载后验证 Skills 与 MCP 恢复 | 编译 + UPM/Skill/MCP 恢复状态 |
| `nova-project-export-tables` | Operation | 直接导出当前 TableSettings 的全部或指定代码、数据 | 编译 |
| `nova-project-integrate-table` | Operation | 接入已确认 Luban 表并验证运行时读取 | Play |
| `nova-project-update-localization` | Operation | 更新或追加本地化 Key、语言、字体与绑定，复用同一源表 CLI 并切换语言验证 | Play |
| `nova-project-ui-create-view` | Operation | 创建并注册业务 UIView、Prefab 与 UI 导出项 | 编译 |
| `nova-project-update-ui-view` | Operation | 定向更新已注册 UIView、Prefab 或注册内容 | 编译 |
| `nova-project-data-driven-ui` | Workflow | 编排表接入和页面创建，完成数据驱动 UI 闭环 | Play |
| `nova-project-integrate-resource` | Operation | 接入业务资源、Collector、加载释放链，或处理资源密钥与 Editor 缓存 | 按模式：静态配置/文件证据；资源加载须 Play，Bundle/Player 链分别验收 |
| `nova-project-integrate-network-api` | Operation | 接入已确认协议的业务 HTTP API，并验证路由、请求与响应 | Play |
| `nova-project-integrate-sound` | Operation | 接入已确认 BGM、音效、声音表、声音组和业务触发，并验证实际播放 | Play |
| `nova-project-integrate-vibration` | Operation | 接入 Emphasis / Custom 振动数据、业务触发与停止，并验证真机体感 | 真机反馈 |
| `nova-project-integrate-procedure` | Operation | 新增或调整业务 Procedure，并验证进入、离开、异步取消与跳转 | Play |
| `nova-project-refresh-hotfix-dlls` | Operation | 在当前 Target、DevelopmentBuild 与激活 ConfigMaster 当前坐标下编译并整批刷新 StartupGameDlls + RunningGameDlls；仅刷新本地业务 DLL，不是发布 | 编译 + 映射/导入/哈希 |
| `nova-project-generate-hybridclr-artifacts` | Operation | 执行 HybridCLR Generate All 与 link.xml 验证；不含 DLL Copy、最终 Player 或发布 | 生成物 + 编译 |
| `nova-project-build-bundles` | Operation | 构建并核验本地 YooAsset Bundle 产物 | Bundle 构建产物 |
| `nova-project-build-player` | Operation | 构建并核验本地 Player 或平台工程 | Player `BuildReport` 与产物 |
| `nova-project-upload-google-play` | Operation | 配置 Google Play AAB 上传、执行已授权 Pipify Batch，或只读排查权限/版本号/提交失败 | 配置/诊断证据；上传须有匹配模式的 fastlane 成功结果 |
| `nova-project-integrate-event` | Operation | 接入业务事件定义、发布订阅、分发时序、注销和回池生命周期 | Play |
| `nova-project-integrate-persistence` | Operation | 接入本地存储、只读检查、业务版本迁移；复用同三 Action 维护当前 Editor 限定值 AES，或在 macOS Editor 轮换 SQLite Cipher 与当前组 Privacy 默认 AES | 本地维护须精确范围 Verify；业务接入编译/Play，完整数据恢复、设备与外部消费者另验收 |
| `nova-project-integrate-content-scene` | Operation | 接入 Nova 资源化 Content 场景并验证 ISceneHandle 加载/卸载生命周期 | Play |
| `nova-project-diagnose-build` | Operation | 只读定位 Player、Gradle、Xcode 或 WebGL 构建链的最早失败阶段 | 静态定位 |
| `nova-project-onboard-sdk-kit` | Workflow | 复用已发布 SDK/Kit 的安装、配置、平台前置与项目业务接入，按需组合现有 Operation | Play；账号、交易、设备与厂商结果独立验收 |
| `nova-project-diagnose-device-runtime` | Operation | 在冻结设备、Bundle ID、时间窗与脱敏规则下只读诊断真机日志 | 静态定位 |
| `nova-project-preflight-build` | Operation | 构建前只读检查 Target、场景、Config、YooAsset Package 与 HybridCLR 前置 | 静态规则 |
| `nova-project-resolve-android-dependencies` | Operation | 冻结 EDM4U 图并受控重建、核验 Android 依赖输出 | 解析状态与产物摘要 |

当前 Catalog 共 33 项。Table 标量源修改复用随包 `nova_table_source.py`，单次导出由 `nova-project-export-tables` 调用受控 Action；完整表接入由 `nova-project-integrate-table` 负责。普通对象配置、三维 Master 配置与 Pipify Batch 使用各自既有入口，参数、渠道和平台差异进入同一 Skill 的 reference，不逐项新增 Skill。Framework 自升级由包外 UPM 宿主跨 reload 执行，其余包复用通用包管理。HybridCLR 两项分别处理业务 DLL compile/copy/import 与 Generate All/link.xml，最终 Player 后的 `CopyAotDlls` 仍独立。CDN 部署复用 Pipify 与平台部署参考；更大的 Workflow 只在实际操作稳定后按依赖组合。

## 当前限制

- Skill 定义与 Framework UPM 同版本发布，不维护独立 Skill 版本。
- 自动同步遇到冲突或来源问题时只做安全项，并以 `partial` 提示项目组处理；不要把受管同步副本当作可反向编辑的来源。
- 当前包含 33 个实验性 Skill；新增 Skill 前先复用领域 API、已有 Action/CLI/Pipify、Operation 参数/reference 和 Workflow，只有独立目标与风险确实无法容纳时才新增。
- 静态检查、Unity 编译、Play 验证和真机/服务端证据彼此独立；报告必须标明实际达到的层级。

## 功能、参数与复用入口

函数与参数说明直接来自源码/XML和所属 Docs；`coverage.json` 与自动生成的便携符号索引提供导航；任务按 [执行与清场约束](../Docs/START_HERE.md#执行任务与清场的共同约束)在本次验收并于对话报告。知识查询和结构检查不代表运行通过。

`coverage.json` 维护功能族到源码、Docs、现有 Skill/Adapter、用户任务及限制的映射，`Tools/nova_capabilities.py` 负责确定性清单和查询。新增字段、Inspector/Window 操作、Excel 或 SDK/Kit 能力优先进入已有功能族与操作契约；参数和固定导出使用现有工具，不为每个参数新增顶层 Skill。Agent 先取得当前任务说明和契约；固定操作直接复用真实 Action/CLI/Pipify，开放业务实现才进入已授权项目源码。

```text
python3 -B "<resolved Framework package>/Agents/Tools/nova_capabilities.py" --root <projectRoot> inventory --kind features --detail summary --json
python3 -B "<resolved Framework package>/Agents/Tools/nova_capabilities.py" --root <projectRoot> inventory --feature <id> --kind fields --detail summary --json
python3 -B "<resolved Framework package>/Agents/Tools/nova_capabilities.py" --root <projectRoot> inventory --kind tasks --query "存档" --limit 25 --detail summary --json
python3 -B "<resolved Framework package>/Agents/Tools/nova_capabilities.py" --root <projectRoot> inventory --kind tasks --feature runtime.Persist --detail summary --json
python3 -B "<resolved Framework package>/Agents/Tools/nova_capabilities.py" --root <projectRoot> describe <feature-id> --detail full --json
python3 -B "<resolved Framework package>/Agents/Tools/nova_capabilities.py" --root <projectRoot> describe <task-id> --detail full --json
```

先用 `summary` 筛选，再以精确 ID 的 `full` 读取当前任务；默认 `summary`，精确展开正文时使用 `--detail full`。Windows 使用 `py -3`；消费项目由实际解析 Framework 定位随包工具，必要时传 `--agents-root "<resolved Framework package>/Agents"`。源码声明、默认值和文档说明与当前生效配置是不同证据；查询不证明当前值、业务运行或自动执行已完成。维护者使用 `check --json` 检查映射完整性，消费项目不依赖主仓 `.nova/`、`Minds/` 或未安装 SDK。

消费查询严格使用实际安装包源；开发仓工具、未安装扩展和未导入 Sample 为 `not_applicable`，不计消费者待补。已解析包缺源码/Docs，或导入 Sample 的版本、manifest 与精确脚本 GUID 绑定不符仍阻断，残留开发目录和同名文件不能覆盖真实包。已导入 Sample 的静态模板审查不证明当前游戏运行。

任务查询分别显示 `sourceStatus` 和 `semanticReview=not-performed`、入口引用、`mode`、执行缺口及 `validation`。`controlled` 只表示有真实固定直接入口；`business-integration` 使用明确业务源码或共享 Unity 编辑路线；`read-only`、`external-host`、`manual`、`unimplemented` 保留各自边界。前置工具、Router、裸 C# API、函数已审和非空 Adapter 不计执行完成。

任务 `validation` 使用 `verify-at-execution`，由 `required` 给出本次最低要求，`retention=ephemeral` 表明结果不留作历史档案。查询不会执行任务；实际宿主可用性、平台、设备、应用、服务和目标须在本次冻结并验证。静态、编译、Play、Bundle、Player、设备、外部证据互不替代，未执行或证据不足不能标为通过。

参数含义、空值、失败与局部所有权在相邻 XML 维护；跨方法流程与生命周期在已有 Docs 维护。查询只读命中条目和来源文件；首次打开快照会在项目 Library 构建派生 SQLite 缓存，后续按键查询。显式 `index` 重建开发快照，`check` 才执行全量结构、指纹和契约检查。便携快照随发布副本生成，安装包查询无需 .NET，也不隐式全库重建。

导航与路径变化只更新链接和定位索引，不触发历史审查续签；行为变化按实际受影响实现、共享依赖和调用方审查。生成成功、XML/Docs 存在均不代表人工已审或运行通过。维护流程见 [变更影响规则](../Docs/Onboarding/NATURAL_LANGUAGE_DEVELOPMENT.md#变更只复核实际影响)。

当前执行边界：导出 Action 复用正式生成器并在本次编译/重载后核验；Config、普通对象与 Pipify 使用各自强类型入口。场景、内置组件、Prefab 变体/嵌套与事件引用复用 `nova-project-edit-settings` 的共享 Unity MCP 分支，其逐步写入与保存没有 Nova Plan/Receipt 的完整保证。`nova.project.player.build` 只支持 Standalone 桌面与 WebGL，Android/iOS 复用已审 Pipify Batch 和实际平台工具。SDK/Kit、广告、APK 下载/安装及存档维护的设备、服务和恢复效果均按当前契约在本次验证；实现存在不表示已验收。存档范围、保存副作用、事务和恢复边界见 [存档维护](../Docs/Editor/EditorUtil/EditorUtil.AgentActions/EditorUtil.AgentActions.PersistenceMaintenance.md)，其余任务按对应 Skill 进入模块文档。

## 查询、同步与底层 CLI 的边界

符号查询不会执行源码或推断运行时配置，静态调用文本也不能视为已绑定的调用图。全量 AST 构建在临时目录运行 Roslyn，解析 C#/Python 的声明与条件分支；解析失败时保留旧快照并明确失败，不生成已审状态。安装包便携索引只包含实际分发的源码、输入和文档引用。

Skill 同步按当前工程 manifest/lock 和精确包元数据定位真源；残留目录、同名缓存及外层工程不能替代当前解析结果。静态 Handler 注册与 ExposurePolicy 检查不能替代运行宿主的 live describe。doctor/计划只读，reconcile 非 dry-run 才执行既有同步事务；目录树 hash 包含引用和辅助文件，不只看 SKILL.md。进程内锁和内核锁共同串行同步；异常时保留 journal 和已登记恢复材料，完成元数据写入不代表清理阶段必然成功。

表源、Tracks、商品转换和 WebGL 各自复用已有专用参考：普通表源的 Receipt 只供 Verify，Tracks 有独立确认的整源备份恢复；商品转换仅 prepared-only，保存仍走 Config；WebGL 只服务冻结目录，本机 HTTP 不证明 Unity 启动或生产 CDN。辅助函数的纯解析/校验职责不构成额外用户入口，也不扩大对应 Skill 的写集、授权或最低证据。
