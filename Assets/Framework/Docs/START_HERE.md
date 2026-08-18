# Nova Agent 快速入口

这是一份给 Agent 的 Level 0 路由。首次接触 Nova、接手已有项目、调整场景或资源流程时，先用本页建立最低限度的正确上下文，再按任务进入 Level 1 页面和现有模块文档。

## AI Project Skills：共同底线、自动发现与执行边界

所有 `nova-project-*` Skill 触发后都先读取本页。本页定义项目组消费态日常任务的共同底线；它不是需要用户手工选择或按顺序执行的 Skill。

`Assets/Framework/Agents/` 是开发态和消费态共享的唯一 Git 真源。在 Nova 开发仓中，Editor 会用这份真源在仓库根 `.agents/skills/` 生成 `nova-project-*` 的自动生成受管同步副本（下文简称“投影”），让开发者快速验证项目组消费态下的发现和触发效果；发布后，消费项目从已安装的 Framework 包获得同一份内容。该目录只包含项目组消费态 Skill：每个 Skill 的目录名、Catalog id 与 `SKILL.md` 的 `name` 都必须使用 `nova-project-*`。框架开发态 Skill 保留在仓库根 `.agents/skills/` 的既有 `nova-*` 命名空间；两者可在开发仓并列发现，但只有 `nova-project-*` 进入本 Catalog 与 UPM 同步范围。

Nova Project Skills 随 `com.solotopia.nova.framework` 同版本管理。用户安装或升级 Nova 后首次打开 Unity，Editor 会在编译和包更新结束后检查，并按需把包内 Catalog Skill 同步为项目根 `.agents/skills/` 下的受管同步副本，供宿主和 Agent 发现；不需要执行 `sync` 或手工复制。

三处位置的职责不同：

- 开发仓 Git 真源：`Assets/Framework/Agents/Skills/`。
- 已安装包快照：`PackageInfo.resolvedPath/Agents/Skills/`；它随当前 Framework 版本提供，只读且不可反向编辑。
- Agent 发现入口：`<project>/.agents/skills/nova-project-*`；这是自动生成的受管同步副本（简称“投影”），不是第二真源。

`.agents/skills/` 遇到同名用户目录、已修改或缺失的受管同步副本时会保守保留现场，并以 `partial` 报告无法完成的项；其他可安全完成的项仍会继续。若已安装包的 Agents 真源不可用，自动同步器会 fail-closed 并在 Unity Console 报告错误，不会写入、覆盖或删除项目文件。

`<project>/.agents/nova-skills.lock.json`、`.agents/nova-skills.transaction.json` 与 `.agents/.nova-skills-staging/` 同样属于项目本地生成状态，不应作为 Framework 或 Skill 真源提交。自动同步器不会创建、修改或覆盖消费者项目的 `.gitignore`。项目未自行跟踪 `.agents/` 时，无需改动 `.gitignore`；只有项目自行跟踪 `.agents/` 且用户已明确确认本次写入消费者 `.gitignore` 时，Agent 才可添加以下规则。不要忽略整个 `.agents/`，以免吞掉项目自己维护的 Agent 配置：

```gitignore
/.agents/skills/nova-project-*/
/.agents/nova-skills.lock.json
/.agents/nova-skills.transaction.json
/.agents/.nova-skills-staging/
```

全量可发现不等于全量按顺序执行。当前 16 项消费态能力只根据自然语言任务的目标、范围和约束匹配需要的 Skill；写入、构建、外部发布和 Git 等副作用仍遵守对应 Skill 的确认门。当前未覆盖的任务仍按本页和具体模块 Docs 推进；完整目录和边界见 [Nova Project Skills](../Agents/INDEX.md)。

## 日常任务如何无缝使用 Skills

项目组只需用自然语言说明目标、已知范围和约束，不必记住或手工执行 Skill 名称。Agent 先从项目根 `.agents/skills/nova-project-*` 发现当前 Framework 自带的全量能力：任务不清楚时先用 `nova-project-router`，任务已明确时可直接进入对应 Operation。常见匹配包括：接手项目→`nova-project-check-readiness`；启动失败→`nova-project-diagnose-startup`；新增/更新页面→`nova-project-ui-create-view` / `nova-project-update-ui-view`；接入已确认协议的业务 HTTP API→`nova-project-integrate-network-api`；加入大厅 BGM、按钮点击音效或其他已确认业务声音→`nova-project-integrate-sound`；刷新 HybridCLR 业务热更 DLL→`nova-project-refresh-hotfix-dlls`（只覆盖当前 Target、DevelopmentBuild 与激活 ConfigMaster 当前坐标的本地 compile -> copy/import，不代表 AOT、Bundle、Player、CDN 或运行时成功）；表、配置、本地化、资源、启动场景、Bundle、Player 分别进入同名领域 Operation；表驱动页面则由 `nova-project-data-driven-ui` 编排。

每个匹配到的 Skill 都按渐进式披露执行：L0 只用 Catalog、frontmatter 与本页发现能力；L1 读取当前 Skill 和契约，冻结输入、写入集、确认门与最低证据；L2 仅按当前分支读取需要的模块 Docs 和项目事实；L3 在确认后调用声明的 Action Adapter（C# API、Unity Editor/MCP、Pipify、CLI 或工作区编辑）；L4 只验证本次需要的证据。这样既能复用日常流程，也不会为了一个任务重复分析所有 Nova 模块。

Workflow 的 `requires` 只描述其内部 Operation 的依赖图，不影响安装、升级或发现范围。所有 Catalog Skill 随当前 Nova 版本全量提供；Workflow 只在确有多个依赖操作时编排 DAG，Unity Editor、AssetDatabase、活动场景和同一输出目录仍按单写者串行。

## 先检查项目，不要套模板

不要假定当前项目来自 Starter，也不要假定它必须采用固定目录、固定场景数或固定资源拓扑。先检查：

1. 当前启动场景和 Build Settings 中实际参与构建的场景。
2. 场景中 Nova 根节点来自哪个 Prefab，是否存在重复或手工拼装的残缺根节点。
3. 项目当前使用的资源系统、配置入口、构建入口和第三方插件。
4. 哪些目录与场景由项目明确交给 Nova 管理；不要接管未声明范围。
5. 用户本次要解决的具体任务，再决定需要阅读哪些模块文档。

若一个场景负责承载 Nova，必须使用框架提供的完整 canonical Prefab：`Assets/Framework/Prefabs/Nova.prefab`。不要新建空 GameObject 后只挂 `Nova`、`AssetComponent` 或少数组件来仿造它。

## 三层约束

### Hard Error：最小、确定的结构错误

Hard Error 只用于能确定破坏 Nova 运行完整性的情况，例如 Nova 托管入口使用残缺或错误的根 Prefab、同一运行拓扑出现多个 Nova、已明确声明为 Content 的场景又包含 Nova，或必需的序列化引用已经损坏。Hard Error 可以阻止 Play；发布流程也可显式选择把它作为门禁，但 ProjectGuard 不通过全局预处理器阻断 Unity Build。

### Warning：建议与可疑偏离

Warning 用于尚不能判定非法的情况，例如业务代码直接使用 `Resources.Load`、场景未分类、没有自动化流水线，或项目采用了与 Sample 不同的目录和加载方式。Warning 用来提示 Agent 向用户核实，不阻止日常 Play 与 Unity Build。

### Release Strict：项目显式启用的发布验收

Release Strict 只在项目或发布流程显式启用时检查 Bundle、Smoke、证据或项目自定义发布条件。它不是所有 Nova 项目的默认开发门禁，也不应自动施加到普通 Play、Unity Build 或开发 CI。

Contract、Pipify、representative asset 和分目录 `AGENTS.md` 都可以帮助特定项目形成更强的自动化，但不是 Nova 普遍必需条件。存在 Contract 时，只验证项目主动声明的 Nova 托管范围。

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
- [验证与构建](Onboarding/VALIDATION.md)：理解 Hard Error、Warning、Release Strict 以及直接 Unity Build。
- [框架文档索引](INDEX.md)：进入 Runtime、Editor 与具体模块事实。
- [架构总览](ARCHITECTURE.md)：理解 Component + Manager 分层。

## Agent 开工检查

- 已检查现有工程事实，没有把 Sample 或 Starter 当成强制模板。
- 承载 Nova 的场景引用完整的 `Assets/Framework/Prefabs/Nova.prefab`。
- 已确认运行拓扑中 Nova 的唯一性，以及 Content 的显式加载和卸载责任。
- 已区分业务资源、`Resources/BuiltIn` 与第三方插件 Resources。
- 已选择与任务相称的验证层级，没有把 Release Strict 强加给日常开发。
