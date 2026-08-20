---
id: ADR-081
title: Nova Project Action 采用按操作性质分类的受控执行层
summary: Project Skill 统一调用受控 C# Action
category: editor
status: accepted
date: 2026-08-19
aliases:
  - ADR-081-project-action-controlled-execution-layer
keywords:
  - Nova Project Action
  - Action Adapter
  - EditorUtil.AgentActions
  - Plan Execute Verify
  - stable action id
tags: [adr, nova, editor, skill, action, automation]
supersedes: []
superseded-by: []
related:
  - "[[ADR-020-assembly-dependency-direction|ADR-020]]"
  - "[[ADR-026-pipify-runner-no-batch-locking|ADR-026]]"
  - "[[PAT-18-editor-window-vs-util-split|PAT-18]]"
  - "[[PAT-49-pipify-step-no-batch-lock-assumption|PAT-49]]"
---

# ADR-081：Nova Project Action 采用按操作性质分类的受控执行层

## 背景（Context）

Nova Project Skills 已能把自然语言任务拆成可验收 Operation，但多个 Skill 仍以 C# 方法签名或文字步骤重复描述同一底层操作。随着 Config、导出、HybridCLR、Bundle、Player、UPM 和诊断能力增长，继续由 Agent 每次重新选择方法、拼装参数和判断结果，会产生重复分析、调用漂移和不一致的安全边界。

Pipify 是顺序 Batch 与 UI/CLI 流水线，不提供每个业务单元统一的计划、确认、收据和证据协议；Unity MCP 的任意代码执行也不能作为项目组 Skill 的长期受控入口。

## 决策（Decision）

1. 在 Framework Editor 程序集中建立 `EditorUtil.AgentActions`，作为 Nova Project Skills 的确定性 C# 执行层。Runtime 不反向依赖该模块。
2. Action 先按操作性质分为 `Inspect`、`Ensure`、`Generate`、`Build`、`Package`、`RuntimeProbe` 和 `Delivery`；业务领域、副作用、幂等性、必需证据、确认门与资源锁分别表达，不把多个维度压进单一分类。
3. Action ID 固定为 `nova.project.<domain>.<verb>`。ID 不包含 Skill 名、C# 类型名或版本号；破坏性语义变化新增 ID，Receipt 通过 `contractMajor` 拒绝静默解释不兼容旧契约。
4. Registry 只发现 Framework Editor 程序集中的强类型 Handler。消费项目脚本不能任意注册 Action，也不开放方法名、类型名、反射参数或 C# 代码字符串。
5. 每个写入 Action 统一采用只读 `Plan`、一次性确认后的 `Execute`、严格只读 `Verify`。计划在执行前原子消费，不自动重放；触发 UPM Resolve、编译或 domain reload 时返回 `partial + Receipt`，稳定后再验证。
6. Handler 只包装既有领域 `EditorUtil.*`，不复制 Config、UPM、HybridCLR、构建或导出业务逻辑，也不依赖 EditorWindow。代码按 `Handlers/<OperationType>/` 分组，每个稳定 Action 使用独立 Handler，不建立巨型分派类。
7. `success` 必须满足 Descriptor 声明的全部必需证据；证据不足由 Dispatcher 降级为 `partial`。工作区、Unity、外部系统、构建产物和凭据副作用必须显式声明。
8. Action 调度锁只做进程内资源互斥，不使用 `LockReloadAssemblies`、`StartAssetEditing` 等 Unity 引用计数批锁，也不跨 domain reload 持锁。
9. Skill 与 Action 不是一对一关系。一个 Action 可以被多个 Skill 复用；只有输入可类型化、写入集可说明、成功可自动验证且跨项目语义稳定的操作才升级为 Action。
10. 传输桥与执行内核分离。Framework 显式依赖 Nova 自有的 `com.solotopia.nova.framework.mcp`，但 Framework Action 内核和 `NovaFramework.Mcp.Editor` 中立契约程序集不硬依赖任何第三方 MCP Provider。Nova MCP UPM 包可以随当前产品选型携带一个默认 Provider Adapter，但必须使用独立程序集单向调用中立契约，切换 Provider 不得修改 Framework Action 或中立协议。Adapter 只能暴露 `describe / plan / execute / verify` 和已注册 Action ID，不得把任意 C# 执行包装成正式协议。

## 后果（Consequences）

### 正面

- Agent 不再为相同底层操作重复推导调用链，Skill 可复用稳定 Action ID。
- 分类、确认、锁、幂等性、证据与结果状态可以统一治理并自动测试。
- 领域实现仍集中在既有 `EditorUtil.*`，Action 扩容不会形成第二套业务逻辑。
- 高风险 Delivery 能力可以晚于本地生成和构建能力独立开放。

### 代价与限制

- 每个 Action 需要专用请求 DTO、计划状态、Receipt 和验证器，不能把现有方法机械批量注册。
- 进程内计划不跨 domain reload；跨 reload 只保留 Receipt，执行前需要重新计划。
- Nova MCP 包安装成功不等于 Agent 已可直连；包内默认 Provider Adapter 仍需在实际 Editor 连接上验证 Tool 发现与调用。
- Procedure、页面布局、Prefab 结构、协议语义和真机体感等开放问题仍需要 Agent、Unity MCP 或人工判断。

## 被排除的方案（Alternatives）

| 方案 | 否决理由 |
|---|---|
| 一个巨型 `AgentEditor` 按字符串反射任意方法 | 无法审计参数、写入集和证据，消费项目也可能注入任意执行入口 |
| 每个 Skill 各复制一套 C# 调用步骤 | 相同操作会漂移，升级成本随 Skill 数量重复增长 |
| 把全部 Pipify Step 一比一变成 Action | Pipify 是顺序 Batch，不是每个业务单元的计划与验证协议 |
| 直接以第三方 MCP Provider 的任意代码执行能力作为正式桥 | 任意 C# 执行面过大，违背只允许稳定 Action ID 的受控边界 |
| 把所有编辑任务都 C# 化 | 开放式业务设计无法由固定 Action 安全替代，会隐藏决策风险 |

## 验证依据（Verification）

- 首个 Handler `nova.project.upm.manage-latest` 复用 `EditorUtil.PlugPals` 的计划、执行和验证实现。
- `AgentActionRegistryTests` 覆盖 Framework-only 注册、稳定描述、非法请求、确认快速路径、未知计划和 Receipt 契约拒绝。
- `NovaFramework.Editor.csproj --no-restore` 编译通过，Action 相关代码无编译错误。
- Nova Project Skills 校验通过，工具测试 97/97 通过。

## 关联

- 程序集方向：[[ADR-020-assembly-dependency-direction|ADR-020]]
- 禁止 Editor 批锁：[[ADR-026-pipify-runner-no-batch-locking|ADR-026]]
- Window 与工具层分离：[[PAT-18-editor-window-vs-util-split|PAT-18]]
- Pipify 不假设批锁：[[PAT-49-pipify-step-no-batch-lock-assumption|PAT-49]]
