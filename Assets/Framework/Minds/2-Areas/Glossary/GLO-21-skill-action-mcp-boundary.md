---
id: GLO-21
title: Skill、Action 与 MCP 的职责边界
summary: Skill 安排任务，Action 执行，MCP 传递调用
category: editor
status: active
date: 2026-10-10
aliases:
  - GLO-21-skill-action-mcp-boundary
  - Skill 与 Action
  - Nova MCP 与 Unity MCP
keywords:
  - Skill
  - Router
  - Workflow
  - Operation
  - Action
  - MCP
  - Unity MCP
  - nova_project_action
tags: [glossary, nova, editor, skill, action, mcp]
related:
  - "[[ADR-081-project-action-controlled-execution-layer|ADR-081]]"
  - "[[GLO-20-asmdef-namespace-boundary|GLO-20]]"
---

# GLO-21：Skill、Action 与 MCP 的职责边界

## 定义

| 术语 | 人话解释 | 职责 |
|---|---|---|
| Skill | 任务说明书 | 由 AI 读取，明确输入、步骤、执行边界和验收要求 |
| Router Skill | 接待人员 | 任务范围不清楚时选择合适的 Skill |
| Workflow Skill | 施工安排 | 组织多个有依赖、可分别验收的 Operation |
| Operation Skill | 一项独立任务 | 完成一个明确目标，按需调用 Action、通用工具或 CLI |
| Action | 已经封装好的执行动作 | 接收明确参数，检查条件、执行并返回证据 |
| MCP | 工具通信协议 | 让 AI 发现和调用外部工具；当前 Unity MCP 提供通向 Editor 的连接与通用工具 |

## 两种关系要分别看

**任务关系：**AI 按 Nova Skill 理解需求、安排步骤、选择工具。范围明确时可以直接进入 Operation；Router 和 Workflow 按需使用。

**调用关系：**外部 AI 通过当前连接把已经选好的工具请求送进 Unity。默认路径是 `Unity MCP → nova_project_action → Nova 网关 → Nova Action → Unity API / Nova 既有工具`。

这条调用路径只描述请求如何送达。AI 在请求发出前就已选择 Nova 工具，Unity MCP 不承担 Skill 的业务拆解职责。Action 已在 Editor 内部执行，可以直接调用内部 API。

## Nova 边界

- Router、Workflow、Operation 是 Skill 的分类，Skill 不是自运行的 C# 执行器。
- 一个 Skill 可以使用多个 Action，一个 Action 也可以被多个 Skill 复用；只读或工作区任务可以不经过 Unity MCP。
- 当前 Nova MCP 包扩展默认 Unity MCP Provider，复用其服务；包名含 MCP 不代表另外启动独立服务。
- 注册工具声明工具名、参数与处理函数，解决 AI 如何发现工具以及请求交给谁的问题。
- Skill 发现、MCP Tool 发现和 Action 列表是三层不同的发现机制，数量没有一一对应关系。
- `nova_project_action` 是工具入口，`nova.project.<domain>.<verb>` 是入口内选择的稳定 Action ID。
- 连接适配器、中立协议与 Action 内核分开；复用 Unity MCP 是当前默认实现，其他传输方案需要对应适配，不能把当前选型表述成技术上唯一可行的路径。

## 易混淆项

- “Nova 流程先拆解任务，再使用 Unity MCP 执行”描述任务安排顺序；“请求先经过 Unity MCP，再进入 Nova 工具”描述通信路径。两者可以同时成立。
- Action 成功只证明它自己的操作满足证据要求，整个 Skill 或 Workflow 还需完成其余验收。
- Operation Skill 与 Tool 的 `operation=describe / plan / execute / verify` 含义不同，后者是调用阶段。
- 安装适配包不会替外部 AI 宿主启动 MCP 服务或刷新已有会话；发现工具和实际连接仍需落实。

## 示例

“导出表”直接使用导表 Operation，通过统一工具选择 `nova.project.table.export`，执行并核对导出结果。“接入 SDK 并跑起来”则使用 Workflow，组织安装、配置、平台检查和运行验证，复用多个 Operation 与 Action。

## 来源、影响面与验证依据

- [Docs：用人话理解 Skill、Action 与 MCP](../../../Docs/Onboarding/AI_SKILLS_ACTIONS_MCP.md)：当前说明、两类关系图和任务示例。
- [Nova Project Skills](../../../Agents/INDEX.md)：Skill 类型、自动发现和 Action 复用契约。
- [[ADR-081-project-action-controlled-execution-layer|ADR-081]]：受控 Action 执行层与传输桥分离的既有决策。
- [NovaProjectActionContractProvider.cs](../../../Scripts/Editor/EditorUtil/EditorUtil.AgentActions/NovaProjectActionContractProvider.cs)：中立请求转交 Framework Action 的实现。
- 开发仓 `UPMPackages/com.solotopia.nova.framework.mcp/Nova/UnityMcp/Editor/NovaProjectActionUnityMcpTool.cs`：`McpForUnityTool`、`AutoRegister=true` 与请求转交中立网关；对应 Adapter asmdef 引用 `MCPForUnity.Editor` 和 `NovaFramework.Mcp.Editor`。

本条用于术语解释、调用关系图和消费端文档，不新增架构决策。依据当前 Skill、Docs 与注册/分发源码核对；不把这份静态解释当作设备、构建或外部服务已运行成功的证据。
