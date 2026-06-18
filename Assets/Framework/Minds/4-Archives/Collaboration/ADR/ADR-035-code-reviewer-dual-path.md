---
id: ADR-035
title: code-reviewer 双路并行 path 模式
summary: diff>100 行触发 logic/spec 双路并行
category: review
status: accepted
date: 2026-05-23
aliases:
  - ADR-035
  - ADR-035-code-reviewer-dual-path
keywords: [ADR-035, ADR-035-code-reviewer-dual-path, code-reviewer 双路并行 path 模式]
tags: [adr, nova, review, ai-collab, parallel]
supersedes: []
superseded-by: []
related:
  - "[[ADR-004-static-runtime-review-split|ADR-004]]"
  - "[[PAT-06-main-session-dispatch|PAT-06]]"
---

# ADR-035：code-reviewer 双路并行 path 模式

## 背景（Context）

ADR-004 把 reviewer 拆为「静态 code-reviewer + 运行时 qa-reviewer」两关，但 code-reviewer 内部仍是单实例串行处理四大维度（逻辑 / 规范 / 安全 / 性能）。当 PR diff 较大时：

- 维度一（逻辑 bug 猎杀）和维度二（风格/架构）互不依赖，本可并行
- 单实例同时承担两路审查，注意力分散，bug 漏网率提升
- PR 周转时间被 reviewer 串行处理拖慢

## 决策（Decision）

引入 `path` 入参，code-reviewer 支持三种路径模式：

| `path` | 覆盖维度 | 触发场景 |
|---|---|---|
| `logic` | 维度一（逻辑正确性）+ 维度三（C# 安全 CRITICAL） | 双路并行的逻辑路实例 |
| `spec` | 维度二（规范与架构合规）+ 维度四（性能） | 双路并行的规范路实例 |
| `all` | 四维度全审 | 单实例兜底 |

**触发规则**：

- 累计 diff > 100 行 → 主会话**单条消息内**并行 spawn `path: logic` + `path: spec` 两实例
- 累计 diff ≤ 100 行 / 单纯重命名 / 格式化 → 单实例 `path: all`
- 未指定 path 时默认 `path: all`

**合并规则**：任一实例 REJECT 则整体 REJECT；双 PASS 合并为 `[CHECK-PASS]`；输出报告标题加 `[path: logic|spec|all]` 标识便于主会话合并。

## 后果（Consequences）

### 正面

- 大 PR 静态审查耗时显著降低（两路并发约 -40%）
- 双实例职责正交，互不干扰，专注度提升
- 100 行阈值避免小改动开双实例的收益/开销倒挂

### 负面

- 主会话需要做合并逻辑（聚合两份报告 + 处理状态）
- 双实例可能对"位于路径边界"的问题（如某个修复同时改善逻辑与性能）出现归属歧义——通过 `path` 入参的覆盖维度明确划分缓解，但仍需 reviewer 自律
- 100 行阈值需要主会话先快速判断 diff 体量

## 被排除的方案（Alternatives）

| 方案 | 否决理由 |
|---|---|
| 始终双路并行 | 小改动开销大于收益（双实例返回合并的开销） |
| 按文件并行（非按维度） | 同一文件的逻辑/规范交叉审查仍是同人做，未真正拆解维度 |
| 把规范路下沉到 coder 自检 | coder 易盲区，需要外部审查者验证；且违反 ADR-004 静态/运行时分层 |

## 验证依据（Verification）

- `.claude/agents/code-reviewer.md` §路径模式（path 入参）
- `.claude/agents/team-leader.md` §并行调度模板 → 模板 A
- `.claude/CLAUDE.md` §调度（四大并行模板速览）

## 来源（Origin）

- 会话日期：2026-05-23
- 关键对话节选：
  > AI："code-reviewer 拆并行（逻辑路 + 规范路）"
  > 用户："1.后者"（即 100 行阈值，而非全部双路）

## 关联

- 规则文件：`.claude/agents/code-reviewer.md` §路径模式
- 相关 ADR：[[ADR-004-static-runtime-review-split|ADR-004]]（静态/运行时 reviewer 二分）
- 相关 Pattern：[[PAT-06-main-session-dispatch|PAT-06]]（主会话调度总纲）
