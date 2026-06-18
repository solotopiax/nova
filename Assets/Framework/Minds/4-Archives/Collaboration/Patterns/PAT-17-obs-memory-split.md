---
id: PAT-17
title: obs ↔ memory 分工铁律
type: pattern
status: active
date: 2026-05-16
summary: obs留全文memory留指针禁互相复制
category: obs
aliases:
  - PAT-17-obs-memory-split
keywords:
  - PAT-17
  - PAT-17-obs-memory-split
  - obs ↔ memory 分工铁律
tags:
  - pattern
  - ai-collab
  - knowledge-management
  - obs
  - memory
related:
  - "[[PAT-16-obs-keyword-trigger|PAT-16]]"
---

# PAT-17：obs ↔ memory 分工铁律

## 适用场景（When）

- AI 协作项目同时使用 memory 和 obs。
- 信息既需要当下能读到，又需要长期可查。

## 核心做法（What & How）

### 分工矩阵

| 维度 | memory | obs |
|---|---|---|
| **存活周期** | 当下（AI 启动即读） | 长期（团队资产） |
| **读取方** | cc 主会话自动加载 | AI 按需 query / 团队人工查阅 |
| **内容形态** | 一行指针 + 详 PAT-XX | 完整规则 + 示例 + 反例 + 反模式 |
| **典型条目** | "Plan 默认 Subagent-Driven（详 PAT-XX）" | PAT-XX 完整 5 段（适用场景 / 核心做法 / 为什么 / 反模式 / 复用） |
| **写权限** | AI 自由写 | AI 仅写 1-Inbox/ 草稿，2-Areas/ 由人工评审入库 |

### 关键铁律

**memory 与 obs 禁止互相复制内容**：

- memory 只留"指针 + 一句话"（≤30 字 + 详 PAT-XX 引用）
- obs 写完整规则细节
- 相同信息两份全文 = 维护噩梦（一边改另一边过期）

### 启动 token 节约

memory 索引尽量精简；已落 obs 的条目只留指针，不保留全文。

### 信息流向

```text
 用户对话
   ↓
 [关键字命中]
   ↓
 memory（短期手感）  ←─→  obs Inbox/（长期草稿）
                              ↓ 人工评审
                          obs Areas/（长期知识）
                              ↓
                          AI / 团队按需 query
```

memory 是 AI 启动时的「即时面板」，obs 是「档案库」。

## 为什么这么做（Why）

- memory 索引会占启动 token。
- subagent 看不到主会话 memory，但能 query obs。
- obs 有编号和双链，memory 只是短指针。
- 双系统互相复制会脱钩。

## 反模式（Anti-patterns）

- **memory 写完整规则全文**：本属 obs 内容塞进 memory → 启动 token 爆炸
- **obs 写偏好性指针**："不要自动 commit" 是 cc 行为偏好，不该作为团队 PAT
- **两边各写一份全文**：违反"互相禁止复制"铁律 → 改一边忘改另一边
- **memory 当成笔记本随便写**：今天写下"昨天那个 bug 的根因..."→ 这种调试日志不是 memory，应进 obs Snapshots 或不存
- **obs 起草后忘记更新 memory 指针**：obs 已沉淀但 memory 没指过去，cc 启动看不见
- **memory 不删过期条目**：旧偏好已被新偏好覆盖但 memory 不更新 → 自相矛盾
- **跨项目共用 memory**：~/.claude/projects 是按项目分目录的，但有人想共享 → 应放 ~/.claude/CLAUDE.md 全局而非 memory

## 跨项目复用提示

- **思想完全可迁移**：所有"AI 当下偏好 + 长期团队知识"双系统都适用
- 工具替换灵活：obs 可换 Notion / Logseq / 自建 wiki；memory 可换 cc memory / Cursor rules / 项目级 CLAUDE.md
- 关键是想清楚两类信息：① 改了立刻影响 AI 行为（→ memory）；② 长期可检索（→ obs）
- 启动占用极敏感的项目可极简化 memory（仅 1 行：「所有偏好已沉淀 obs PAT-XX 系列，按需 query」），但需加强 hook 触发
- 不适合的场景：单人项目无跨会话需求 → 单一系统已足够；分双系统反而增加同步成本

## 关联

- Memory 指针：`feedback_obs_memory_split.md`（待写）
- 关联 Pattern：[[PAT-16-obs-keyword-trigger|PAT-16]] 规范关键字触发 obs 双通道（同期下沉）
- 历史源头：2026-05-16 上下文优化讨论中提出 cc 短期 / obs 长期分工
