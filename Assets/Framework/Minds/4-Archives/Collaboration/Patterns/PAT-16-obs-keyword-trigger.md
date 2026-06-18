---
id: PAT-16
title: 规范关键字触发 obs 双通道
type: pattern
status: active
date: 2026-05-16
summary: obs关键字命中必须memory与obs双通道
category: obs
aliases:
  - PAT-16-obs-keyword-trigger
keywords:
  - PAT-16
  - PAT-16-obs-keyword-trigger
  - 规范关键字触发 obs 双通道
tags:
  - pattern
  - ai-collab
  - knowledge-management
  - obs
related:
  - "[[PAT-17-obs-memory-split|PAT-17]]"
---

# PAT-16：规范关键字触发 obs 双通道

## 适用场景（When）

- AI 协作项目使用 memory + obs 双系统。
- 用户说出规则、决策、术语、资源等可沉淀内容时。
- 需要保证规则能跨会话和跨 agent 检索。

## 核心做法（What & How）

### 关键字白名单

用户消息出现以下任一关键字，且后接可作为规则 / 约定 / 术语 / 资源的内容时，**强制触发 obs 双通道落地**：

| 关键字 | 默认 skill |
|---|---|
| 规范 / 规则 / 约定 / 标准 / 铁律 | `/nova-obs patterns-to-inbox` |
| 决策 / 定下来 / 拍板 | `/nova-obs adr-to-inbox` |
| 名词 / 术语 / 这个叫 X | `/nova-obs glossary-to-inbox` |
| 资源 / 工具 / 这里有个链接 | `/nova-obs resource-to-inbox` |
| 沉淀下来 / 记下来 / 入库 / 落库 | 按内容性质选最合适 |

### 双通道铁律

| 通道 | 内容形态 | 用途 |
|---|---|---|
| **memory**（`feedback_*.md` + `MEMORY.md` 索引） | 应用纲要 + Why（短） | AI 当下读取的偏好提醒 |
| **obs**（`1-Inbox/*-DRAFT-*.md` 草稿） | 完整规则 + 示例 + 反例（长） | 团队 / 跨会话长期可检索 |

不允许"只写 memory 不走 obs"，也不允许"只走 obs 不写 memory"。

### 三层硬防线

| 层级 | 实现 | 文件 |
|---|---|---|
| L1 软提醒 | 本 Pattern + memory feedback 条目 | `feedback_obs_trigger_on_rule_keywords.md` |
| L2 中提醒 | CLAUDE.md 红线节明文要求双通道 | `.claude/CLAUDE.md` |
| L3 硬阻断 | UserPromptSubmit hook 命中关键字注入 system-reminder | `.claude/hooks/nova-obs-keyword-guard.sh` |

### 回复结尾签收

每次走完双通道必须用一行明示 `obs 落地` 或 `obs 已覆盖`。

## 为什么这么做（Why）

- memory 不能跨 agent 检索，obs 可以。
- 稳定知识沉到 obs，临时偏好放 memory。
- 没有硬约束时 AI 容易漏走 obs。
- 双通道分别写短纲要和完整规则。

## 反模式（Anti-patterns）

- **只写 memory 就完事**：信息只活在 cc 主会话内，下次 `/clear` 就消失（memory 还在但 obs 缺）
- **只走 obs 不写 memory**：obs 草稿沉得太深，AI 没主动 query 时不会读到，等于没存
- **关键字命中但内容不沉淀**：用户说"这是个规范"但内容是临时讨论 → 仍要起草为 obs DRAFT，由用户评审决定是否入库
- **草稿质量糟糕**：obs DRAFT 只有标题没有 Why / 反例 / 示例 → 入库后没有信息密度
- **跳过结尾签收行**：没写 `obs 落地：...` → 用户无法审计是否真走了
- **重复全文**：memory 和 obs 写一模一样的全文 → 违反"两边禁止互相复制"，未来同步噩梦
- **关键字白名单老化**：新加了关键字（如"硬性要求"）但 hook 没更新 → 漏检

## 跨项目复用提示

- **思想完全可迁移**：所有 cc + 知识库 双系统都适用（Obsidian / Notion / Logseq / 自建 wiki）
- 实现层关键：UserPromptSubmit hook 是 Claude Code 提供的能力，其他平台需找等价机制
- 关键字白名单要对接团队的"日常术语"，不是固定的——团队习惯说"硬性要求"那就加进白名单
- 配合 SessionEnd hook（蒸馏对话） + Inbox / Areas 双层结构，形成完整知识管道
- 不适合的场景：纯个人临时实验项目（无团队、无跨会话需求） → 双通道开销过高

## 关联

- Memory 指针：`feedback_obs_trigger_on_rule_keywords.md`
- Hook 实现：`.claude/hooks/nova-obs-keyword-guard.sh`
- Skill 入口：`/nova-obs patterns-to-inbox` / `/nova-obs adr-to-inbox` / `/nova-obs glossary-to-inbox` / `/nova-obs resource-to-inbox`
- 关联 Pattern：[[PAT-17-obs-memory-split|PAT-17]] obs ↔ memory 分工铁律（同期下沉）
