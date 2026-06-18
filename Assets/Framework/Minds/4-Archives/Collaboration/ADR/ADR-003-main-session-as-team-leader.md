---
id: ADR-003
title: 主会话即调度中心（取消嵌套 spawn 的 team-leader agent）
status: accepted
date: 2026-05-14
summary: 主会话即team-leader不再嵌套spawn
category: workflow
aliases:
  - ADR-003
keywords: [ADR-003, 主会话即调度中心（取消嵌套 spawn 的 team-leader agent）]
tags: [adr, nova, agent, workflow, dispatch]
supersedes: []
superseded-by: []
related:
  - "[[ADR-004-static-runtime-review-split|ADR-004]]"
  - "[[ADR-037-allow-subagent-nested-spawn|ADR-037]]"
  - "[[PAT-06-main-session-dispatch]]"
  - "[[PAT-07-tradeoff-phased-delivery]]"
---

# ADR-003：主会话即调度中心（取消嵌套 spawn 的 team-leader agent）

## 背景（Context）

Nova Framework 早期方案设想由独立 `team-leader` agent（opus + plan 模式）作为调度中心，主会话只负责将用户需求转发给 team-leader，由 team-leader spawn `runtime-coder / editor-coder / code-reviewer / qa-reviewer / doc-writer`。

实施时撞到 Claude Code 工具链的硬限制：**subagent 不能嵌套 spawn**。team-leader 自己是一个 subagent，无法再通过 Task 工具拉起其他 agent，导致原方案无法执行。

> **2026-05-24 更新（[[ADR-037-allow-subagent-nested-spawn|ADR-037]]）**：上述"不支持嵌套 spawn"叙述已过时，Claude Code 实际允许 subagent 调用 `Agent()` 派下级 subagent。但本 ADR 主体决策（主会话即 team-leader）**继续生效**，依据从"工具链硬限制"改为"调度权必须留主会话——subagent 是同步阻塞调用，把调度委派给 subagent 等于让用户失去对调度的实时介入能力"。

## 决策（Decision）

**主会话直接承担 team-leader 全部职责，原 `team-leader` agent 不再被 spawn。**

要点：

- `.claude/agents/team-leader.md` **保留**作为「职责说明书」，描述调度规范（六步工作流、并行原则、质量卡点）；主会话依据该文件执行调度。
- 主会话**只做调度**，不直接写代码 / 文档 / 审查（保留 `CLAUDE.md` / `settings.json` 等 meta 配置修改权限）。
- 主会话**不**预加载规则文件；规则按需由各子 agent 自行 Read（按「改什么读什么」策略）。
- 主会话**禁止尝试** `Task(team-leader)` —— 这是流程红线（见 `CLAUDE.md` 红线表）。
- 复刻规则替代原 1..N 编号：`.claude/agents/` 每种角色只保留一份模板（如 `runtime-coder.md`），人手不够时主会话**并行派发同名 agent 多个实例**，但**两实例不得同改一文件**。

调度流程固化为：

```
需求 → 主会话 ─┬→ runtime-coder ─┐
              ├→ editor-coder  ─┤→ code-reviewer → qa-reviewer → doc-writer → 交付
              └→ (可选) architect 出方案后回到主会话
```

### 轻量改动快速通道

- 跳 reviewer：低风险（命名/措辞/UI 数值/显而易见小 bug）且无逻辑分支新增
- 跳 doc-writer：未改 public API、未增删 Inspector/序列化字段、未改生命周期语义
- 两条可叠加；主会话有疑虑必走完整流程

## 后果（Consequences）

### 正面

- 与 Claude Code 工具链限制对齐，调度链不再断裂。
- 主会话直接掌握全局上下文（用户需求、agent 输出），减少 team-leader 转译损耗。
- 主会话不预读规则，token 预算保留给规划与决策。
- 多实例并行派发由主会话直接 Task 调用，并行度更高。

### 负面

- 主会话职责扩大：除调度外还需监控质量卡点、判断快速通道适用性，认知负担高。
- `team-leader.md` 沦为说明书文件，与"agent definition"语义不完全一致，新人易困惑。
- 多实例隔离仅靠"两实例不得同改一文件"约定，无机制强制保障。

## 被排除的方案（Alternatives）

| 方案 | 否决理由 |
|---|---|
| 独立 team-leader agent 嵌套 spawn 子 agent | 早期判定为工具链硬限制；ADR-037 校正后真因是 subagent 同步阻塞，调度权委派给阻塞节点会让用户失去实时介入能力 |
| team-leader 仅做规划，把任务清单交回主会话执行 | 主会话仍需 spawn agent，team-leader 多了一跳无价值 |
| 取消 team-leader.md，调度规则散落各 agent 内部 | 缺少统一职责说明，调度规则不可追溯 |
| 用 hook 或 settings.json 编排自动调度 | 编排灵活性不足，无法处理 REJECT 回环 |

## 验证依据（Verification）

- 配置文件：`.claude/CLAUDE.md` 顶部说明 + Team 编制表 + 红线表（"主会话尝试 spawn team-leader"列入零容忍）
- 职责说明书：`.claude/agents/team-leader.md`（文件保留，但 frontmatter 不再被 spawn 触发）
- 工作流证据：`CLAUDE.md` 「调度规则（主会话执行）」节列出标准通道与跳过判定

## 关联

- 配置文件：`.claude/CLAUDE.md`、`.claude/agents/team-leader.md`
- 相关 ADR：[[ADR-004-static-runtime-review-split|ADR-004]]（静态/运行时审查二段制）
- 相关 Pattern：[[PAT-06-main-session-dispatch]]、[[PAT-07-tradeoff-phased-delivery]]
