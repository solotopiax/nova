---
id: PAT-75
title: 主会话调度四大并行模板
summary: 主会话四大并行模板 ABCD 速览
category: workflow
type: pattern
status: active
date: 2026-05-23
aliases:
  - PAT-75
  - PAT-75-main-session-four-parallel-templates
keywords:
  - PAT-75
  - 主会话调度四大并行模板
tags:
  - pattern
  - workflow
  - ai-collab
  - parallel
  - orchestration
related:
  - "[[PAT-06-main-session-dispatch|PAT-06]]"
  - "[[PAT-14-plan-to-subagent-driven|PAT-14]]"
---

# PAT-75：主会话调度四大并行模板

## 适用场景（When）

主会话作为调度中心（team-leader 职责内嵌）派发 ≥2 个子 agent 时，统一按本模板执行。覆盖：

- code-reviewer 静态审查
- runtime-coder / editor-coder 多模块并行编码
- architect 前置触发
- qa-reviewer 三步运行时验证

## 核心做法（What & How）

### 模板 A：code-reviewer 双路并行（diff > 100 行触发）

变更累计 diff > 100 行时单消息内并行 spawn 两个 code-reviewer 实例：

| 实例 | path 入参 | 覆盖维度 |
|---|---|---|
| 逻辑路 | `path: logic` | 维度一 + 维度三（C# 安全 CRITICAL） |
| 规范路 | `path: spec` | 维度二 + 维度四（性能） |

任一 REJECT 则整体 REJECT；双 PASS 才合并为最终 `[CHECK-PASS]`。

**单实例兜底**：diff ≤ 100 行 / 重命名 / 格式化 → 单实例 `path: all`。

### 模板 B：worktree 隔离多 coder 并行（≥2 coder 触发）

≥2 个 coder 实例（同名多实例 或 runtime + editor）必须开 worktree：

1. Agent 调用传 `isolation: "worktree"`
2. 各 coder 在独立 worktree 内编译验证
3. 主会话回到主分支逐个 merge → 跑一次合并后完整 `read_console` 兜底
4. 路径默认 `.worktrees/<branch-name>`（已加 `.gitignore`）

执行流程委托 superpowers skill：`superpowers:using-git-worktrees`。

### 模板 C：architect 强制前置（命中即触发，无需用户确认）

任一项命中必须先派 architect，方案出来直接派 coder（无需用户审阅环节）：

- 新建 Component / Manager / Procedure
- 修改三层 Manager 继承链
- 跨 ≥2 模块的接口变更
- 新增 partial class 文件 ≥3 个
- 涉及 HybridCLR / 业务 DLL / Procedure 注册顺序

### 模板 D：qa-reviewer 内部 batch 化

三步保持串行依赖（编译 → Inspector → Play Mode），但每步内部用 `mcp__UnityMCP__batch_execute` 合并往返：

| 步骤 | batch 包装 |
|---|---|
| ① 编译 | `[refresh_unity, read_console]` |
| ② Inspector | `[find_gameobjects, unity_reflect]` |
| ③ Play Mode | 多个用例一次性 `run_tests` |

## 为什么这么做（Why）

- **A**：把"逻辑/规范"两类零容忍维度并行扫，PR 周转时间 -40%；100 行阈值避免小改动双实例返回结果合并的开销
- **B**：物理隔离编译产物，"两实例不得同改一文件"从规则上约束变成文件系统强约束
- **C**：架构改动早期发现成本远低于事后返工；用户确认环节牺牲流畅性，对已明确的触发条件不必再卡
- **D**：每次 UnityMCP 单独调用都触发域重载，连续调用域重载次数线性爆炸；batch 化单次域重载完成多操作

## 反模式（Anti-patterns）

- diff > 100 行仍单实例 reviewer → 逻辑/规范互相干扰，bug 漏网率提升
- ≥2 coder 不开 worktree → 编译产物互污，"两实例不得同改一文件"靠人盯
- 触发模板 C 条件却跳过 architect 直接派 coder → 红线违规
- 每改一个 .cs 立即 `refresh_unity` → 域重载次数翻倍
- 多个测试用例逐个 `run_tests` → Editor 反复进出 Play Mode

## 跨项目复用提示

A/B/C 的"调度结构"普遍适用于任何 multi-agent 项目；D 的 batch 思路适用于任何 MCP 协议下的连续操作场景。Nova 项目内的具体阈值（100 行 / 5 类 architect 触发条件）需要按项目规模重新校准。

## 来源（Origin）

- 会话日期：2026-05-23
- 关键对话节选：
  > 用户："对于我当前现有的方法是否还可以有质的飞跃的优化手段？"
  > AI 给出 P0-P3 优化清单，覆盖 reviewer 拆并行、worktree、UnityMCP batch、architect 前置
  > 用户："1.后者 2.保留，且可以直接推进，不需要确认 3.SessionStart hook 可以先不做 4.一次性全做完"

## 关联

- 规则文件：`.claude/agents/team-leader.md` §并行调度模板（A/B/C/D）
- 相关 Pattern：[[PAT-06-main-session-dispatch|PAT-06]]（主会话调度总纲）
- 相关 Pattern：[[PAT-14-plan-to-subagent-driven|PAT-14]]（subagent-driven 默认）
- 引用的 superpowers skill：`superpowers:dispatching-parallel-agents` / `superpowers:using-git-worktrees`
