# Nova Project Skills 与 C# Action 当前架构及路线图

> 文档归属：本文位于 Docs，用于描述当前版本可核验的架构、能力边界与实施路线图；“为什么采用受控执行层”的历史决策由 Minds 的 ADR-081 保存。文件名保留 `Overall-Design` 以维持既有链接，本文不充当新的 ADR，也不能把候选项当作已实现能力。

## 1. 总体结论

Nova Project Skill 负责理解项目组自然语言任务、冻结输入和组织证据；`EditorUtil.AgentActions` 负责可重复、可审计的 Unity Editor 单元操作。两者不是一对一关系：一个 Skill 可以组合多个 Action，一个稳定 Action 也可以被多个 Skill 复用。

当前基线：

- 消费端 Skills 真源：`Assets/Framework/Agents/Skills/`，共 30 项，统一使用 `nova-project-*`。
- Skill 发现 Catalog：`Assets/Framework/Agents/catalog.json`。它只路由 Skill，不复制 Action Descriptor。
- Action 唯一执行真相：运行中的 C# Registry Descriptor；交互 Agent 只能对 MCP 已开放的 ID 通过 `describe` 渐进读取 Schema，未开放 Action 只从文档得知注册与阻断状态。
- 已注册 Action：19 个。
- 当前 MCP 明确开放：13 个。
- 已实现但因风险门未开放：6 个。
- 原 47 项不是实施 KPI；经源码审计后分成“已实现、需前置能力、已合并、不产品化、封闭”五类。

最终目标仍是项目组“零手写 Coding”：Agent 根据自然语言选择 Skill，再调用受控 Action 完成确定性单元操作。它不等于零确认、零评审、零验证，也不允许用任意 C# 执行隐藏开放式业务判断。

## 2. 调用模型

```text
用户自然语言意图
  → nova-project-router 或直接命中 Operation Skill
    → 可选 Workflow DAG
      → 一个或多个 Project Skill
        → nova_project_action（受限传输）
          → EditorUtil.AgentActions（契约、计划、锁、收据、证据）
            → 强类型 Handler
              → 既有 EditorUtil.* 领域 API
```

- Intent 不是 Skill。
- Workflow 只在多个 Operation 存在依赖时编排 DAG，不是固定三级顺序。
- Skill 决定“做什么、范围是什么、何时算完成”。
- Action 决定“如何以固定 DTO、固定副作用和固定证据执行一个闭环”。
- Handler 不复制 Config、HybridCLR、YooAsset、Build 或 UPM 算法，只包装既有领域 API。

例如“刷新业务 DLL 后打包”会形成两个 Operation；当前 DLL Action 可由 MCP 执行，而 Player Action 因高风险审批未开放，所以 Workflow 必须停在明确阻断点，不能偷偷退化为任意代码。

## 3. Skills 的真源、命名与发现

```text
Assets/Framework/Agents/
├── catalog.json              # Skill 轻量发现与路由
├── Skills/nova-project-*/    # 项目组消费态 Skill 真源
└── Tools/                    # 安装包内的发现、同步和校验工具
```

- `Assets/Framework/Agents/` 是开发态和消费态共享、受 Git 管理的真源；共享表示 Nova 开发仓可直接验证消费效果，不表示这里存放框架开发 Skill。
- 框架开发态 Skill 继续位于仓库根 `.agents/skills/nova-*`。
- Framework 安装或升级后，包内全量消费 Skill 会同步到消费项目 `.agents/skills/` 的受管副本；不需要 `sync --profile`。
- Framework 删除或重命名 Skill 时，只删除 Nova 管理且未被用户修改的旧副本；冲突时返回 `partial`，不覆盖用户内容。
- 不新增第二份静态 Action Catalog。Action 数量、Schema、副作用和证据必须从 live Registry `describe` 获取；MCP 暴露范围由 Adapter 的显式策略控制并由测试冻结。

## 4. 渐进式披露

1. L0：`catalog.json`、Skill frontmatter、`Docs/START_HERE.md` 完成意图路由。
2. L1：只读命中 Skill 的 `references/contract.json`，冻结输入、副作用、锁、确认门和最低证据。
3. L2：只读当前分支真正需要的模块 Docs。
4. L3：需要且 MCP 已开放 Action 时才调用 `describe(action_id)` 获取当前 Request Schema 和 Descriptor；未开放 Action 不可通过 Tool 探测或执行。
5. L4：Plan 后只展示本次写入集、风险与证据；确认后 Execute，稳定后 Verify。

不得在路由阶段加载全部 Nova Docs、全部 Action Schema 或原 47 项清单。

三个版本字段彼此独立：`Agents/catalog.json.schemaVersion` 表示 Skill Catalog 文件格式，单 Skill `references/contract.json.schemaVersion` 表示 Skill 契约文件格式，`AgentActionDescriptor.ContractMajor` 表示 Action 与 Recovery Receipt 的兼容语义；数值不能横向比较，也不代表 Framework 包版本。

## 5. Action 内核

### 5.1 代码位置

```text
Assets/Framework/Scripts/Editor/EditorUtil/EditorUtil.AgentActions/
├── Definitions/                       # 公共枚举、Descriptor、Plan、Result、Receipt
├── Core/                              # Handler、PlanStore、OperationStore、锁、主线程边界
├── Handlers/<OperationType>/          # 每个稳定 Action 一个强类型 Handler
├── EditorUtil.AgentActions.Registry.cs
└── EditorUtil.AgentActions.Dispatcher.cs
```

Action 只存在于 Editor 层；Runtime 不反向依赖。

### 5.2 多维契约

Action 先按操作性质分组：`Inspect`、`Ensure`、`Generate`、`Build`、`Package`、`RuntimeProbe`、`Delivery`。领域、操作性质、副作用、幂等性、证据、确认门和锁是独立维度，不能压成一个分类。

稳定 ID：

```text
nova.project.<domain>.<verb[-qualifier]>
```

破坏性语义变化应新增 ID；Receipt 用 `actionId + contractMajor + payload` 拒绝静默解释不兼容旧契约。Registry 只扫描 Framework Editor 程序集，消费项目不能注册可由 Agent 执行的 Handler。

### 5.3 准入门槛

只有同时满足以下条件才升级为顶层 Action：

1. 输入能用专用 DTO 表达并冻结；
2. Plan 前能说明精确写入集与副作用；
3. Execute 能调用稳定领域 API，而不是复制算法；
4. Verify 能自动判断最低证据；
5. 语义能跨消费项目复用；
6. 统一包装能显著减少重复分析或提高安全性。

开放式页面设计、Prefab 布局、协议语义、业务 Procedure 决策和真机体感不满足该门槛，继续由 Skill、Agent、Unity Editor 自动化通道或人工判断。

## 6. Plan、Execute、Verify 与恢复

```text
Describe
  → Plan
    → 展示精确计划
      → 确认（写入型）
        → Execute（原子消费 Plan）
          → Verify（只读领域状态）
```

### Plan

- 严格解析 DTO，拒绝未知/重复字段、错误类型和超限输入；
- 冻结坐标、Target、场景、资产身份、输出路径与前置 Hash；
- 只读领域与项目状态，不调用领域写入 API；
- ready 计划进入有界内存 PlanStore：最多 256 项、TTL 30 分钟、原子单次消费；
- ready 同时在 `Library/Nova/AgentActions/Operations/` 写一条最小恢复记录。该记录是 Action 基础设施元数据，不是项目资产写入，也不包含原始请求。

### Execute

- 先原子消费 Plan；确认失败、锁冲突、Registry 变化或 Editor 漂移后也不会复用；
- MCP 必须同时传 `action_id + plan_id`，Core 在任何领域写入前原子核对计划所属 Action，防止未开放计划绕过 allowlist；
- `RequiresConfirmation=true` 时还必须传 `confirmation_token=plan_id`；该令牌只绑定计划，不证明 MCP 已验证真人身份；
- 写入前持久化 `executing`，无法记录则 fail-closed；
- 不自动重试、不扩大范围、不在 domain reload 后恢复 Execute。

### Verify

- 只读领域、项目资产和外部目标；不得因验证失败重放 Execute；
- 可以更新 `Library/Nova/AgentActions/Operations/` 的最近验证状态，这是基础设施审计记录，不是领域写入；
- `success` 必须覆盖 Descriptor 的全部 `RequiredEvidence`，否则 Dispatcher 降级为 `partial`。

### Domain reload

Plan 的可执行 HandlerState 只在内存存在，domain reload 后失效。`recovery_token` 只能取回 Receipt 并重新 Verify；永不恢复、推断或重放 Execute。恢复记录不是签名审计凭证，只证明当前状态能否满足目标。

最终状态统一为 `success`、`partial`、`blocked`、`not_applicable`；Plan 额外使用 `ready`。

## 7. MCP Adapter

Framework 必需的 Editor 配套包：

```text
UPMPackages/com.solotopia.nova.framework.mcp/
```

它是 Nova 自有的 MCP 契约与 Tool Adapter 包。UPM 安装链当前为 `Framework -> Nova MCP -> Unity MCP`；编译链通过独立程序集保持单向：`NovaFramework.Mcp.Editor` 只包含中立 Provider SPI 与 Gateway，`NovaFramework.Mcp.UnityMcp.Editor` 只做默认 Provider 的薄适配，`NovaFramework.Editor` 依赖中立 SPI 并在 domain load 注册唯一 Action Provider。

正式消费链还要求每个依赖版本及其 registry scope 在安装 Framework 之前就可解析。当前默认依赖为 `com.coplaydev.unity-mcp@10.1.2`，由 NovaSpark 预先配置 OpenUPM 精确 scope 后按 Nova MCP 包的 semver 传递依赖解析；正式发布前必须用真实消费工程验证该来源。开发工程中另外安装的 Provider 只用于开发态验证，不改变对外包默认适配 Unity MCP 的事实。

30 个 Skill 不会逐个注册成 MCP Tool。Skill 由 Agent 从 `.agents/skills/` 发现；当前默认 Adapter 只从 `tools/list` 暴露 `nova_project_action`，再由该 Tool 的 live `describe` 返回当前安全开放的 Action。

唯一 Tool：

```text
nova_project_action
```

只支持 `describe / plan / execute / verify`。不暴露 `RunAsync`、任意 C#、类型名、方法名或反射注册。

安全门：

- 64 KiB 请求上限、字符串与标识符上限；
- 严格 operation envelope；
- Action 顶层未知字段在 MCP 拒绝，完整 Schema/类型/语义由 Core 唯一解析；
- 单请求串行；
- Registry 有任何 issue 时整个桥 fail-closed；
- Action 必须进入显式 ExposurePolicy 且通过副作用门；
- `Delivery`、`Destructive`、`ExternalWrite`、`Credential` 在可信审批通道完成前不开放；
- 输出隐藏 CLR 类型、项目绝对根路径、凭据、URL query 和认证信息。

## 8. 当前 19 个 Action

| Action ID | 类型 | 核心闭环 | MCP | Skill |
|---|---|---|---|---|
| `nova.project.upm.manage-latest` | Package | 安装最新、升级最新并在 Resolve 后核验 | 开放 | `nova-project-manage-upm-package` |
| `nova.project.upm.uninstall-direct` | Package | 卸载 direct dependency 并核验 manifest/lock 双重消失 | 未开放：Destructive | `nova-project-manage-upm-package` |
| `nova.project.config.validate-coordinate` | Inspect | 校验一个 Config 三维坐标 | 开放 | `nova-project-configure-runtime` |
| `nova.project.config.inspect-plugin-types` | Inspect | 稳定排序扫描 SDK/Kit Config 类型 | 开放 | `nova-project-configure-runtime` |
| `nova.project.config.ensure-plugin-instances` | Ensure | 按坐标或矩阵补实例并可选启用 | 开放 | `nova-project-configure-runtime` |
| `nova.project.config.inspect-bundle-collector` | Inspect | 定位并摘要 Bundle Collector | 开放 | Config / Bundle 前置 |
| `nova.project.config.export-runtime` | Generate | 冻结时间与坐标导出 ConfigRuntimeSO | 开放 | `nova-project-configure-runtime` |
| `nova.project.hotfix.refresh-game-dlls` | Generate | Compile 当前 Target 后整批 Copy Game DLL | 开放 | `nova-project-refresh-hotfix-dlls` |
| `nova.project.build.inspect-readiness` | Inspect | 只读冻结 Target、场景、Config、YooAsset Package 与 HybridCLR 构建前置 | 开放 | `nova-project-preflight-build` |
| `nova.project.table.export` | Generate | 冻结活动场景 TableSettings 与 Project/Description，按 all/code/data 导出并核验目录摘要 | 开放 | `nova-project-integrate-table` |
| `nova.project.network.export` | Generate | 冻结活动场景 NetworkSettings，按 HostKey/NetCmd/Proto 与 all/code/data 导出并核验产物 | 开放 | `nova-project-integrate-network-api` |
| `nova.project.sound.export` | Generate | 冻结活动场景 SoundSettings 与 Unit，按 all/code/data 导出并核验产物 | 开放 | `nova-project-integrate-sound` |
| `nova.project.vibration.export` | Generate | 冻结 Emphasis/Custom 区域与 Unit，按 all/code/data 导出并核验产物 | 开放 | `nova-project-integrate-vibration` |
| `nova.project.localization.export` | Generate | 冻结 Text/Font/Languages 范围，按 all/code/data 导出并核验产物 | 开放 | `nova-project-update-localization` |
| `nova.project.android.resolve-dependencies` | Generate | 冻结 EDM4U 图并重建、核验受管 Android 依赖输出 | 未开放：Destructive | `nova-project-resolve-android-dependencies` |
| `nova.project.hotfix.generate-artifacts` | Generate | GenerateAll + ValidateLinkXml | 未开放：Destructive | `nova-project-generate-hybridclr-artifacts` |
| `nova.project.bundle.build-asset` | Build | 构建 AssetBundle 与 artifact receipt | 未开放：Destructive | `nova-project-build-bundles` |
| `nova.project.bundle.build-raw-file` | Build | 构建 RawFile Bundle 与 artifact receipt | 未开放：Destructive | `nova-project-build-bundles` |
| `nova.project.player.build` | Build | 精确路径 BuildPlayer + BuildReport/Hash | 未开放：Destructive + ExternalWrite | `nova-project-build-player` |

`ConfigValidateCoordinateAction` 返回执行 `success` 但数据内 `valid=false` 是正确语义：Action 成功完成了校验，业务配置是否有效由结构化结果表达。

## 9. 原 47 项评审结论

### 9.1 已实现或已合并

- 原 UPM、Config Export、Config Validate、Bundle Collector、RawFile、AssetBundle、Player 已实现。
- Build Preflight 已抽取无 SessionState/保存/生成副作用的 pure probe，并实现只读 `build.inspect-readiness`。
- Table Export 已补齐活动场景 Settings resolver、Project/Description 输出规划与 code/data 分证据，并实现 `table.export`。
- Localization、Network、Sound、Vibration 已补齐稳定 resolver、精确范围、输出规划与 code/data 分证据，并分别实现受控导出 Action。
- Android Resolver 已冻结完整 EDM4U 图、固定受管写入边界与跨 reload Receipt，并实现 `android.resolve-dependencies`；因 Force Resolve 属于 Destructive，MCP 仍保持关闭。
- 原 SDK/Kit 类型扫描两项合并为 `config.inspect-plugin-types`。
- 原 SDK/Kit 单格 Ensure 两项升格并合并为 `config.ensure-plugin-instances`，补齐启用名单、保存与矩阵语义。
- 原 HybridCLR MethodBridge、AOT Generic、Il2CppDef、AOT DLL、link.xml 等碎片 Generate 合并进 `hotfix.generate-artifacts`；不把同一闭环拆成可误用的小按钮。

### 9.2 需先补稳定前置能力

| 领域 | 原候选方向 | 前置条件 |
|---|---|---|
| ProjectGuard / Environment | 项目守卫、Luban、Python、HybridCLR 检查 | 抽取完全无 `SessionState` 写入的 pure probe，并修正 package resolved path |
| UI | export all/code/data | 稳定 Settings resolver、精确输出规划、data/code 证据拆分 |
| Config 高风险变更 | scene mode、grid、missing refs、migration、inject | 每项拆精确 write set、迁移/删除计划与资产级 Verify |
| RuntimeProbe | UI、资源、声音、网络、设备 | 先建立隔离场景/探针、超时、清理和 Play/Device 证据契约 |

这些能力缺的是稳定产品契约，不是多写几个 Handler 文件。前置条件未完成时不登记假 Action。

### 9.3 不作为顶层 Action 产品化

- 任意 `pipeline.run-batch`：Pipify 继续是顺序流水线，不是受控原子 Action。
- `pipeline.inspect-steps`：属于开发诊断，不是项目组闭环成果。
- 独立 `table.validate-descriptions`：校验能力折入 Table Export Plan。
- 独立 HybridCLR 碎片 Generate 与 `copy-aot-dlls`：分别折入完整 Generate / 最终 Player 流程。
- 任意脚本宏修改、Tracks 汇总、打开目录：缺少足够的 Nova 项目组业务闭环价值。
- 单独清热更缓存：优先作为明确排障/下载流程内部步骤，不提供宽泛删除 Action。

### 9.4 保持封闭

`cdn.deploy`、`cdn.deploy-whitelist`、`cdn.purge` 属于 Delivery，涉及凭据、外部写入和不可逆/延迟可见结果。只有完成可信人类审批、凭据隔离、幂等键、外部审计和恢复策略后，才进入独立 P3 设计；当前不注册、不开放。

## 10. Skill 迁移状态

- 已完整迁移：`nova-project-manage-upm-package`、`nova-project-refresh-hotfix-dlls`。
- 已部分迁移：`nova-project-configure-runtime` 的 Validate、Plugin Inspect/Ensure、Bundle Collector Inspect、Runtime Export；任意业务字段编辑仍是受确认 Unity 编辑。
- Action 已实现并由 MCP 开放：`nova-project-preflight-build`。
- Action 已实现但 Skill 只能报告阻断：`nova-project-resolve-android-dependencies`、`nova-project-generate-hybridclr-artifacts`、`nova-project-build-bundles`、`nova-project-build-player`。
- 确定性导出已迁移：`nova-project-export-tables`、`nova-project-integrate-table`、`nova-project-integrate-network-api`、`nova-project-integrate-sound`、`nova-project-integrate-vibration`、`nova-project-update-localization`；各 Skill 中的源编辑、业务代码和运行时验证仍由对应受控入口完成。
- 未达到 Action 准入门的 UI 开放式布局与业务编辑保留现有受控工作流，不伪称已 C# Action 化。

## 11. 每个 Action 的完成定义

1. 独立强类型 DTO 与严格 Schema；
2. Descriptor 的 effects、idempotency、evidence、confirmation、locks 准确；
3. Plan 领域只读并冻结所有关键上下文；
4. Execute 在写前复核漂移，只调用既有领域 API；
5. Recovery Receipt 在 Plan/Execute 两侧语义明确；
6. Verify 不写领域状态、不重放；
7. `success` 满足全部 RequiredEvidence；
8. Registry 与 Handler 定向测试通过；
9. 进入 MCP 时有独立 ExposurePolicy 审查与安全测试；
10. 对应 Skill 只声明真实可用的 Adapter，并遵循渐进式披露。

## 12. 后续实施顺序

1. 完成当前 19 项的 Unity EditMode、live `describe`、安全 Plan、Console 与消费包验证。
2. 建立可信审批通道，再按风险逐项开放 GenerateAll、Bundle、Player；不能仅删除 `Destructive` 标志。
3. 优先补 Settings resolver、结果 DTO、输出规划和 pure probe，而不是继续堆 Action 数量。
4. 先完成 UI 的稳定设置与输出契约，再评估是否新增 Handler。
5. 最后独立设计 RuntimeProbe 与 Delivery。

## 13. 明确不做

- 不开放任意 C#、反射类型名、方法名或消费项目自注册 Handler；
- 不把全部 Pipify Step 一比一包装；
- 不为追求数量把开放式业务设计伪装成固定操作；
- 不用 Profile 拆分安装 Skill；
- 不建立重复的静态 Action Catalog；
- 不在 domain reload 后自动恢复 Execute；
- 不把构建产物存在等同于 Play、设备、商店或远端成功。

## 14. 关联入口

- 当前实现事实：[EditorUtil.AgentActions.md](EditorUtil.AgentActions.md)
- 消费端 Skills：[Assets/Framework/Agents/INDEX.md](../../../../Agents/INDEX.md)
- Agent 快速入口：[Docs/START_HERE.md](../../../START_HERE.md)
- 架构决策：`ADR-081`
- Excel 领域边界：`ADR-073`
- MCP 必需配套包：`UPMPackages/com.solotopia.nova.framework.mcp/`
