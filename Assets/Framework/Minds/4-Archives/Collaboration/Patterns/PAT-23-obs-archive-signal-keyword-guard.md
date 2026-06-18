---
id: PAT-23
title: 废弃信号关键字触发 obs 旧方案标记
type: pattern
status: active
date: 2026-05-16
summary: obs废弃关键字命中需同步标记旧方案
category: obs
aliases:
  - PAT-23-obs-archive-signal-keyword-guard
keywords: [PAT-23, PAT-23-obs-archive-signal-keyword-guard, 废弃信号关键字触发 obs 旧方案标记]
tags: [pattern, ai-collab, knowledge-management, obs, archive]
related:
  - "[[PAT-16-obs-keyword-trigger|PAT-16]]"
  - "[[PAT-17-obs-memory-split|PAT-17]]"
supersedes: []
---

# PAT-23：废弃信号关键字触发 obs 旧方案标记

## 适用场景（When）

- AI 协作项目维护当前方案集合。
- 用户口头提出废弃、取代、改用新方案等信号时。
- 需要让 AI 在写新方案时同步处理旧方案归档。

## 核心做法（What & How）

### 关键字白名单

用户消息出现以下任一关键字 → UserPromptSubmit hook 注入「废弃/更换信号」reminder：

| 关键字组 | 说明 |
|---|---|
| 废弃 / 弃用 / 淘汰 | 直接弃用，可能无替代 |
| 取代 / 替换方案 / 替代方案 / 改用 | 有新方案替代 |
| 不再使用 / 迁移到 / 换成 / 重写 | 隐式弃用旧方案 |
| supersede / deprecated / obsolete | 英文等价词 |

> 与「规则关键字组」（PAT-16）独立判定，可同时命中。同时命中时 hook 输出主 reminder + 「废弃」追加段。

### 三选一处置清单

命中后回复必须选 A/B/C 之一：新草稿写 `supersedes`、direct 归档，或起草 ARC-DRAFT。

### 回复结尾签收

走完处置必须用一行明示：

- `旧方案标记：<旧 ID> → supersedes 字段已写入新草稿 <文件名>`，或
- `旧方案标记：<旧 ID> → 已 direct 归档至 4-Archives/`，或
- `旧方案标记：<旧 ID> → ARC-DRAFT 已起草 1-Inbox/<文件名>`，或
- `旧方案标记：无（用户讨论的是新增非替代）`——仅当确认本轮不涉及任何已有 obs 条目的废弃时才允许

### 与扫描器的契约

`/nova-obs areas-to-archives` 扫描器依赖以下证据自动检出疑似废弃：

| 证据 | 来源 | 强度 |
|---|---|---|
| frontmatter `status: superseded/deprecated/archived` | 选项 B/C 落地后写入 | 强 |
| frontmatter `superseded-by` 非空 | 选项 B/C 落地后写入 | 强 |
| 其他文件 frontmatter `supersedes:` 列表含本 ID | **选项 A 的产物** | 强 |
| 正文「取代 ADR-XX」「替换 PAT-XX」 | 选项 A 顺便落地 | 强 |

**关键**：选项 A 看似没动旧条目，但通过给新条目写 `supersedes:` 让扫描器**反向检出**——这是默认推荐路径的根本原因。

## 为什么这么做（Why）

- 新方案和旧方案的处置必须同轮闭环。
- 扫描器可以依靠 supersedes / superseded-by / 取代证据自动检出旧条目。
- A/B/C 覆盖新增文档、直接归档和延后处理三种场景。
- 签收行便于审计。

## 反模式（Anti-patterns）

- **只写新方案不动旧方案**：知识库的"当前方案"集合膨胀且自相矛盾
- **新草稿不写 supersedes**：扫描器拿不到证据 → 等同于"没说取代"
- **跳过签收行**：用户无法审计是否真处置了旧方案
- **滥用「无（新增非替代）」**：把替代场景伪装成新增以逃避归档动作
- **关键字白名单老化**：用户用了新词（如"切到 v2"）但 hook 没更新 → 漏检
- **同时改 supersedes 又走归档**：选项 A 与 B 应二选一，重复操作会导致 0-Index.md 重复处理

## 关联

- 上游 hook：`.claude/hooks/nova-obs-keyword-guard.sh`（ARCHIVE_KEYWORDS 数组）
- Memory 指针：`feedback_obs_archive_signal.md`
- 扫描器：`/nova-obs areas-to-archives`（scan 模式三类规则打分）
- 关联 Pattern：[[PAT-16-obs-keyword-trigger|PAT-16]] 规则关键字双通道（同一 hook 的姊妹规则）
