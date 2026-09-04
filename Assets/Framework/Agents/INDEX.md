# Nova Project Skills

这是 Nova Framework 随 UPM 包发布的、面向项目组的 AI Skill 系统。它帮助 Agent 完成项目接入、业务 UI、数据配置、构建和诊断；不用于开发或改造 Nova 框架本身。

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

### C# Project Action

稳定、可类型化、可验证且可跨项目复用的单元操作优先注册为 `EditorUtil.AgentActions` 下的 C# Project Action。Action 使用 `nova.project.<domain>.<verb>` 稳定 ID，并独立声明操作类型、副作用、幂等性、必需证据、确认门和资源锁；一个 Action 可以被多个 Skill 复用，不与 Skill 一对一绑定。

Skill 仍负责自然语言路由、冻结业务输入和开放式决策；Action 负责确定性执行。业务代码设计、Prefab/Scene 结构设计、协议语义和真机体感不能被“万能反射 Action”替代。完整执行协议见 [EditorUtil.AgentActions](../Docs/Editor/EditorUtil/EditorUtil.AgentActions/EditorUtil.AgentActions.md)。

当前 Registry 已实现的 Project Action 全部进入 MCP 显式白名单。`Destructive`、`ExternalWrite`、`Credential` 与 `Delivery` 不再作为隐藏 Action 的理由，但仍必须经过强类型 Schema、只读 Plan、一次性确认、资源锁、Receipt 与 Verify；Registry 与白名单不一致时 Gateway 整体 fail-closed，不能退化到 `execute_code`、反射或临时 C#。

## 目录、真源与自动发现

| 位置 | 责任 |
|---|---|
| `Assets/Framework/Agents/Skills/<skill-id>/` | 开发态和消费态共享的唯一 Git 真源；仅存项目组 `nova-project-*` Skill，并保持一层平铺 |
| `PackageInfo.resolvedPath/Agents/Skills/<skill-id>/` | 已安装 Framework 包内的只读真源快照，来自同一份 Git 真源 |
| `<Nova 开发仓>/.agents/skills/nova-project-*/` | 开发仓打开 Unity 时从 `Assets/Framework/Agents/` 生成的受管同步副本（简称“投影”），用于快速验证消费态发现效果；可与根目录既有开发态 `nova-*` Skill 并列 |
| `<consumer project>/.agents/skills/<skill-id>/` | 首次打开 Unity 后自动生成的全量受管同步副本（简称“投影”），仅供宿主和 Agent 发现，不是第二真源 |
| `catalog.json` | 全量 Skill 清单、能力分组、用户旅程、副作用、最低证据与机器可选项 |
| `Schemas/` | Catalog 的可校验结构 |

`Assets/Framework/Agents/` 只管理项目组消费态的 `nova-project-*` 命名空间；框架开发态 Skill 继续留在仓库根 `.agents/skills/` 的既有 `nova-*` 命名空间，不进入 Catalog 或 UPM 包。开发仓与消费项目使用同一 Catalog 和同一份 Skill 内容：前者用于快速验证，后者是正式项目组使用路径。

用户安装或升级 Nova 后，首次打开 Unity 时 Editor 会在编译和包更新结束后检查，并按需把包内全部 Catalog Skill 同步为项目根 `.agents/skills/` 下的受管同步副本；不需要执行 `sync` 或手工复制。该目录不是第二真源，不能反向编辑真源。

遇到同名用户目录、已修改或缺失的受管同步副本时，自动同步 fail-closed：不会静默覆盖或删除，并将结果标为 `partial`；其他可安全完成的 Skill 仍继续处理。真源不可用时自动同步器只报告错误，不写入项目。项目本地状态和 Git 忽略边界统一见 [Agent 快速入口](../Docs/START_HERE.md)。能力分组只帮助 Router、导航和展示当前能力，不控制同步范围。Skill 也不是固定执行序列：Agent 只调用当前自然语言任务匹配的 Router、Workflow 或 Operation；多个有依赖的操作才由 Workflow 形成 DAG，且 Unity Editor、AssetDatabase、活动场景和同一导出目录仍是单写者资源。

## 当前 Skill 范围

| Skill | 类型 | 面向的自然语言任务 | 最低证据 |
|---|---|---|---|
| `nova-project-router` | Router | 不确定应调用哪个消费端能力时路由任务 | 静态定位 |
| `nova-project-check-readiness` | Operation | 接手、接入或修改前评估项目就绪条件 | 静态定位 |
| `nova-project-diagnose-startup` | Operation | 编译失败、Play 门禁、黑屏或启动链故障的只读诊断 | 静态定位 |
| `nova-project-setup-entry-scene` | Operation | 配置入口场景、Build Settings、Nova 根和 Content 职责 | Play |
| `nova-project-configure-runtime` | Operation | 配置一个三维坐标并导出 `ConfigRuntimeSO` | 编译 |
| `nova-project-manage-upm-package` | Operation | 计划、确认并验证单个最新版本安装/升级或 direct dependency 卸载 | UPM 精确解析状态 |
| `nova-project-upgrade-framework` | Operation | 由包外 UPM 宿主升级 direct Nova Framework，并在重载后验证 Skills 与 MCP 恢复 | 编译 + UPM/Skill/MCP 恢复状态 |
| `nova-project-export-tables` | Operation | 直接导出当前 TableSettings 的全部或指定代码、数据 | 编译 |
| `nova-project-integrate-table` | Operation | 接入已确认 Luban 表并验证运行时读取 | Play |
| `nova-project-update-localization` | Operation | 更新已有文本、字体或绑定并切换语言验证 | Play |
| `nova-project-ui-create-view` | Operation | 创建并注册业务 UIView、Prefab 与 UI 导出项 | 编译 |
| `nova-project-update-ui-view` | Operation | 定向更新已注册 UIView、Prefab 或注册内容 | 编译 |
| `nova-project-data-driven-ui` | Workflow | 编排表接入和页面创建，完成数据驱动 UI 闭环 | Play |
| `nova-project-integrate-resource` | Operation | 接入业务资源、Collector、地址、加载和释放链 | Play |
| `nova-project-integrate-network-api` | Operation | 接入已确认协议的业务 HTTP API，并验证路由、请求与响应 | Play |
| `nova-project-integrate-sound` | Operation | 接入已确认 BGM、音效、声音表、声音组和业务触发，并验证实际播放 | Play |
| `nova-project-integrate-vibration` | Operation | 接入 Emphasis / Custom 振动数据、业务触发与停止，并验证真机体感 | 真机反馈 |
| `nova-project-integrate-procedure` | Operation | 新增或调整业务 Procedure，并验证进入、离开、异步取消与跳转 | Play |
| `nova-project-refresh-hotfix-dlls` | Operation | 在当前 Target、DevelopmentBuild 与激活 ConfigMaster 当前坐标下编译并整批刷新 StartupGameDlls + RunningGameDlls；仅刷新本地业务 DLL，不是发布 | 编译 + 映射/导入/哈希 |
| `nova-project-generate-hybridclr-artifacts` | Operation | 执行 HybridCLR Generate All 与 link.xml 验证；不含 DLL Copy、最终 Player 或发布 | 生成物 + 编译 |
| `nova-project-build-bundles` | Operation | 构建并核验本地 YooAsset Bundle 产物 | Bundle 构建产物 |
| `nova-project-build-player` | Operation | 构建并核验本地 Player 或平台工程 | Player `BuildReport` 与产物 |
| `nova-project-integrate-event` | Operation | 接入业务事件定义、发布订阅、分发时序、注销和回池生命周期 | Play |
| `nova-project-integrate-persistence` | Operation | 接入 PlayerPrefs、FileFragment 或 SQLite 业务存储并验证耐久性 | Play |
| `nova-project-integrate-content-scene` | Operation | 接入 Nova 资源化 Content 场景并验证 ISceneHandle 加载/卸载生命周期 | Play |
| `nova-project-diagnose-build` | Operation | 只读定位 Player、Gradle、Xcode 或 WebGL 构建链的最早失败阶段 | 静态定位 |
| `nova-project-onboard-sdk-kit` | Workflow | 编排已发布 SDK/Kit 的安装/升级、三维配置、平台前置与最小本地探针 | Play |
| `nova-project-diagnose-device-runtime` | Operation | 在冻结设备、Bundle ID、时间窗与脱敏规则下只读诊断真机日志 | 静态定位 |
| `nova-project-preflight-build` | Operation | 构建前只读检查 Target、场景、Config、YooAsset Package 与 HybridCLR 前置 | 静态规则 |
| `nova-project-resolve-android-dependencies` | Operation | 冻结 EDM4U 图并受控重建、核验 Android 依赖输出 | 解析状态与产物摘要 |

当前 Catalog 共 30 项。Table 单次导出由 `nova-project-export-tables` 直接调用受控 Action；完整表接入仍由 `nova-project-integrate-table` 负责。Framework 自升级独立为 `nova-project-upgrade-framework`，由包外 UPM 宿主跨 reload 执行；其余包继续走通用包管理 Skill。Build Preflight 与 Android Resolve 均使用已开放 Action，后者仍需精确确认其 Force Resolve 写入集。HybridCLR 两项继续严格分工：`refresh-hotfix-dlls` 只做当前坐标的业务 DLL compile -> copy/import，`generate-hybridclr-artifacts` 只做 Generate All 与 link.xml 验证；最终 Player 后的 `CopyAotDlls` 不归入二者。CDN 与本地 RC Workflow 仍等待发布顺序和独立 Operation 在真实项目中稳定后再组合。

## 当前限制

- Skill 定义与 Framework UPM 同版本发布，不维护独立 Skill 版本。
- 自动同步遇到冲突或来源问题时只做安全项，并以 `partial` 提示项目组处理；不要把受管同步副本当作可反向编辑的来源。
- 当前包含 30 个实验性 Skill；新增 Skill 前应先按用户旅程和风险面确认其是否应成为顶层 Skill。
- 静态检查、Unity 编译、Play 验证和真机/服务端证据彼此独立；报告必须标明实际达到的层级。
