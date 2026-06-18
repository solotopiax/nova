---
id: PAT-15
title: Agent 派活硬守 scope 边界
type: pattern
status: active
date: 2026-05-16
summary: Agent派活硬守scope禁触清单外文件
category: ai-collab
aliases:
  - PAT-15-agent-scope-discipline
keywords:
  - PAT-15
  - PAT-15-agent-scope-discipline
  - Agent 派活硬守 scope 边界
tags:
  - pattern
  - ai-collab
  - scope
  - discipline
related:
  - "[[PAT-06-main-session-dispatch|PAT-06]]"
  - "[[PAT-14-plan-to-subagent-driven|PAT-14]]"
---

# PAT-15：Agent 派活硬守 scope 边界

## 适用场景（When）

- 主会话调度多个子 agent 并行/串行执行 plan 内任务
- Plan 已分 group / wave，且不同 group 间存在依赖关系
- 子 agent 容易"顺手把相关的活也干了"（编译能跑、看似贴心，实际越权）
- 团队对 diff 审查有明确边界要求（reviewer / doc-writer 角色独立）

## 核心做法（What & How）

### 派 prompt 写硬约束

每次派活 prompt 顶部加一段「## 严禁触碰」：

```
## 严禁触碰
- 文件 A.cs（属于下游 Wave 3，本轮禁动）
- 文件 B.cs（属于 doc-writer 职责，coder 不动）
- 模块 X 的全部代码（独立 wave，不在本批次）
```

清单要明确具体到文件路径或模块名，不要写"相关代码"这种模糊表述。

### 接收时第一步对 diff

agent 返回交接报告后：

1. `git status` / `git diff --stat` 列出实际改动文件集合
2. 对照 prompt 声明的允许集合
3. 超出范围立即标红
4. 不能只看交接报告就往下走

### 越权处置

| 情况 | 处置 |
|---|---|
| 越权且编译绿、逻辑合理 | **既成事实接收，但在汇报里明写"agent 越权了 X 范围"**，用户可选 revert 或接受 |
| 越权且编译炸 / 逻辑错 | revert 越权部分，重派 |
| 越权改了下游 wave 的活 | 即使可保留，也需在 agent 历史里留警告，下次派同 agent 时 prompt 显式提醒 |

### 教训沉淀

下一次派同名 agent 时，prompt 里显式引用"上次越权事件"作为反例。

## 为什么这么做（Why）

- **diff 审查面**：原本 4 个子 agent 独立完成的改动挤进一份 diff，reviewer 审查面放大 5x，bug 漏检率上升
- **职责分层失效**："coder 只写代码、doc-writer 管文档"是有原因的——doc-writer 改文档要查规则、coder 改文档容易凭印象写
- **plan 节点失控**：越权改动没经 plan 模式确认，用户失去中途 override 机会
- **细节疏漏**：越权"顺带完成"的改动通常没经过仔细思考，常伴随细节错误（如把 `Nova.Self.DevelopMode` 迁错 API）
- **历史踩坑**：2026-04-30 Config 重构 Wave 1-5 中 editor-coder1 顺带把 W2-3 + W2-2 + 全量 L2 文档同步 + INDEX.md 删改 + 业务层代码都改了，编译绿但 reviewer 审查面爆炸

## 反模式（Anti-patterns）

- **prompt 不写 scope**：只说"做 X"，不说"严禁碰 Y"——agent 拿不准时倾向"多做点"
- **接收只看交接报告**："agent 说成功了"不等于"diff 符合 scope"，必须自己对清单
- **越权后 revert + 重派**：除非编译炸，否则保留越权改动只是失去了独立审查窗口；重派浪费 token
- **沉默接收越权**：下次同 agent 还会越权，因为没沉淀教训
- **scope 写得太松**："改 Config 模块" → agent 把 Config 模块所有 partial / 文档 / 测试都动了，因为"都属于 Config 模块"
- **prompt 只写允许，不写禁止**：人脑读 prompt 容易跳过"允许"清单的边界感，禁止清单更显眼

## 跨项目复用提示

- **思想完全可迁移**：所有多 agent 协作工具（Cursor / Devin / Aider / 自建 LangGraph）都适用
- 关键是工具支持 diff 反查；如果 agent 改完无法 git diff 对比，本规则无法落地
- 配合 plan 阶段的 group 划分使用：plan 写清楚 group 边界 → 派活 prompt 直接抄过来作为允许集合
- 团队规模越大，越权代价越大；单人开发可适当放松（自己越权自己扛）
- 不适合的场景：探索性任务（"看看代码库有什么问题"）——本就没明确 scope

## 关联

- Memory 指针：`feedback_agent_scope_discipline.md`
- 相关 Pattern：[[PAT-06-main-session-dispatch|PAT-06]]（主会话调度规则）
- 历史源头：2026-04-30 Config Wave 1-5 editor-coder1 越权事件
