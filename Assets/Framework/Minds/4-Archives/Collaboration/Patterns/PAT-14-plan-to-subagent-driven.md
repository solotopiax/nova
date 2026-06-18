---
id: PAT-14
title: Plan 落地默认 Subagent-Driven
type: pattern
status: active
date: 2026-05-16
summary: Plan落地默认Subagent-Driven直接派活
category: ai-collab
aliases:
  - PAT-14-plan-to-subagent-driven
keywords:
  - PAT-14
  - PAT-14-plan-to-subagent-driven
  - Plan 落地默认 Subagent-Driven
tags:
  - pattern
  - ai-collab
  - workflow
  - planning
related:
  - "[[PAT-06-main-session-dispatch|PAT-06]]"
  - "[[PAT-15-agent-scope-discipline|PAT-15]]"
---

# PAT-14：Plan 落地默认 Subagent-Driven

## 适用场景（When）

- 用 Claude Code（含 superpowers）等 AI 协作工具完成大型重构 / 多模块改动
- 已经走完 `brainstorming` + `writing-plans` 产出 implementation plan
- Plan 内含多个独立 group / wave，可并行
- 团队建立了固定的角色分工（coder / reviewer / qa / doc-writer）

## 核心做法（What & How）

### 默认行为

写完任何 implementation plan 后，**直接进入 Subagent-Driven 执行**（superpowers:subagent-driven-development）：

1. 不再用 `AskUserQuestion` 询问 "Subagent-Driven vs Inline"
2. 提取 plan 内任务 → `TodoWrite`
3. 派发 implementer 子 agent

### 项目流程对接

实际审查走 Nova 项目的三道工序，**不是** superpowers 的两阶段：

| 阶段 | Nova 工序 | superpowers 默认 |
|---|---|---|
| 静态审查 | `code-reviewer` | spec-reviewer |
| 运行时验证 | `qa-reviewer` | code-quality-reviewer |
| 文档同步 | `doc-writer` | （无） |

CLAUDE.md 优先级高于 skill。

### 并行原则

Group 内独立叶子文件按"能并行必须并行"派发不同文件的 implementer：

- 独立类型 / 接口 / 异常 / 数据结构 → 全部并行
- 两实例**不得同改一文件**
- 接口就绪后再派 Manager / Component 实现（串行卡点）

### 跳过条件

仅当满足以下任一才允许跳过子 agent 直接交主会话调度：

- 用户主动改口
- Plan 体量极小（单文件 < 50 行）
- 落入 CLAUDE.md「轻量修改快速通道」

## 为什么这么做（Why）

- **询问就是噪音**：每次写完 plan 都询问 "Subagent-Driven vs Inline" 等于每次让用户重复同一个决策；用户原话"不用再问了，以后都选择 Subagent-Driven"
- **并行 throughput**：subagent 模式下独立任务可并发，相比 inline 串行快 N 倍
- **scope 隔离**：每个 subagent 有独立 prompt，不会污染主会话上下文
- **角色分工生效**：Subagent-Driven 下 coder / reviewer / qa 自然走完整流程，inline 容易省略 reviewer
- **Plan 质量倒逼**：用 subagent-driven 模式跑就要求 plan 写清楚 group / blocked-by 关系，反向逼迫 plan 质量

## 反模式（Anti-patterns）

- **每次写完 plan 询问执行方式**：用户已表态偏好后还问 = 噪音
- **Inline 全程一气呵成**：主会话上下文爆炸，coder / reviewer / qa 角色全混在一起
- **subagent 内部 spawn 当二级 team-leader**：技术上允许嵌套 spawn（见 [[ADR-037-allow-subagent-nested-spawn|ADR-037]]），但仅作为"自我加速"手段（如 architect 内部并行 Explore）。subagent 是阻塞调用，用它当二级调度等于让用户失去对调度的实时介入
- **走 superpowers 默认两阶段审查**：spec-reviewer + code-quality-reviewer 是通用模板，Nova 应走自家三道工序
- **两实例同改一文件**：并行加速反而引入 race condition / merge conflict
- **plan 写得糙就 subagent-driven**：subagent 拿到糙 plan 输出还是糙；plan 阶段必须把任务边界划清
- **小改动也启动 subagent 流水线**：单文件 50 行的 typo 修复走完整流程是杀鸡用牛刀

## 跨项目复用提示

- **思想完全可迁移**：所有支持子 agent / sub-task 的 AI 协作平台都适用（Cursor、Aider、Devin、自建 LangGraph）
- 关键前提是 plan 阶段产出有清晰 group / dependency 结构，否则并行无从谈起
- 不同平台的 subagent 嵌套限制不同——Claude Code 技术上允许嵌套（[[ADR-037-allow-subagent-nested-spawn|ADR-037]] 解除），但仍不建议用作"二级调度"，调度链多一跳就失去用户实时介入能力
- 默认偏好不要写死，应作为项目级偏好（让用户能在项目级 settings 里改）
- 不适合的场景：单文件 / 单 commit 级别的小改动；探索性原型（plan 还在变，subagent 派活后无法更新）

## 关联

- Memory 指针：`feedback_subagent_driven_default.md`
- 相关 Pattern：[[PAT-06-main-session-dispatch|PAT-06]]（主会话调度规则）
- 工具：superpowers:subagent-driven-development
- 历史源头：Wave 3A plan 落盘后用户明确说"不用再问了"
