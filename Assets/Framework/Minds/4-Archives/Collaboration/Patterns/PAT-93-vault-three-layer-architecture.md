---
id: PAT-93
title: Vault 三层架构铁律 raw sources / wiki / schema
summary: 外部文献必入 3-Resources 留底 不留=断链
status: active
category: obs
type: pattern
date: 2026-05-25
source: karpathy-llm-wiki
aliases:
  - PAT-93-vault-three-layer-architecture
  - vault-three-layer-architecture
tags:
  - pattern
  - obs
  - vault
  - architecture
  - raw-source
  - schema
  - ai-collab
keywords: [三层架构, raw source, schema, wiki, 不可变源, 留底, 知识断链, Karpathy LLM Wiki, 编译产物, codebase]
related:
  - "[[RES-01-karpathy-llm-wiki|RES-01]]"
  - "[[PAT-89-bookkeeping-cost-near-zero|PAT-89]]"
  - "[[PAT-91-session-output-filing-back|PAT-91]]"
  - "[[PAT-17-obs-memory-split|PAT-17]]"
---

# PAT-93：Vault 三层架构铁律 raw sources / wiki / schema

## 模式（What）

Nova Vault 永久划分为**三层不可混淆**的内容：

| 层 | 路径 | 性质 | 写权限 | 谁拥有 |
|---|---|---|---|---|
| **Raw sources**（原文不可变） | `3-Resources/` + 外部链接快照 | 摘要 + URL + 必要时全文留底 | 仅 `/nova-obs resource-to-inbox` | 用户策划 |
| **The wiki**（LLM 编译产物） | `2-Areas/` 全部（ADR/PAT/GLO/MOC） | 综合、提炼、决策、术语、图谱 | 仅 `/nova-obs inbox-to-areas` | LLM 维护 |
| **The schema**（规则配置） | `0-Index/0-Index.md` + `.claude/CLAUDE.md` + `SKILL.md` + frontmatter 字段定义 | Vault 元规则 + AI 协作规约 | 人工维护 | 用户与 LLM 共同演进 |

**铁律**：

1. **任何被引用 ≥2 次的外部文献必须有对应 RES 留底**。"WebFetch 临时拉一拉"不算留底——原文一旦删除/修订，所有引用归零
2. **wiki 层可以推翻 / 修订 / 综合 raw sources，但不能假装自己是源头**。PAT/ADR frontmatter 必含 `source: <URL 或 cur-session 或 karpathy-llm-wiki>` 之类显式来源
3. **schema 层改动是高危**：改 `0-Index.md` / `frontmatter` 枚举 / SKILL.md 必走"用户拍板 + 显式说明影响面"路径，不属于日常 obs 入库

## 为什么（Why）

> "Obsidian is the IDE; the LLM is the programmer; the wiki is the codebase. Raw sources are the inputs you keep on disk."  — Karpathy LLM Wiki

Nova 历史问题（本会话亲历）：

- Karpathy gist 被 PAT-89~92 引用 4 次，**自身却没入 3-Resources/**——讽刺的自指反模式
- ADR-002 Priority 表与代码两处漂移 2 个月才发现，因为没明确"代码是 raw source / ADR 是 wiki 编译产物"，让 ADR 假装权威
- 多个 PAT frontmatter 缺 `source:` 字段，搞不清是会话产出还是文献提炼

层次混淆 = LLM 把"自己说的"和"外部源说的"混为一谈，未来溯源失败。

## 怎么用（How to apply）

### Raw sources（3-Resources/）扩展规则

| 触发 | 动作 |
|---|---|
| 一篇外部文章被会话引用 ≥2 次 | 必须 `/nova-obs resource-to-inbox` 起草 RES |
| 重要论文/gist/官方文档（即使引用 1 次） | 强烈建议入 RES，frontmatter 含 `source-url` 与作者 |
| Stack Overflow 单条答案 / 临时博客 | 不入，作为 PAT 内部短引用即可 |
| Unity / 库官方文档主页 | 入 `3-Resources/ExternalLinks.md` 列表，不单独建 RES |

### The wiki（2-Areas/）写作纪律

- 综合性 PAT/ADR 必含 `source:` frontmatter，可选值：`cur-session` / `<external-RES-id>` / `<external-URL>`
- 引用 raw source 时用双链：`[[RES-XX-karpathy-llm-wiki|Karpathy LLM Wiki]]`，让 obs graph 能追到源头
- ADR/PAT 可以"修订"raw source 论断，但必须明确"我们采纳/我们部分采纳/我们因 X 不采纳"，不能假装独立发明

### The schema 改动审批

| schema 文件 | 改动须 |
|---|---|
| `0-Index/0-Index.md` | 用户明确指令 + 影响面说明 |
| `.claude/CLAUDE.md` 红线 | 走 ADR 通道留决策记录 |
| frontmatter `category` 枚举扩张 | 走 ADR + 同步 `nova-obs/scripts/rebuild_index.py` 校验逻辑 |
| `SKILL.md` 重大流程改动 | 用户确认 + 0-Log.md 记 `migrate` 类型 |

## 反模式

- **WebFetch 引用不留底**：会话引用 2 次后还不入 RES，是 PAT-91 的近亲反模式
- **frontmatter 缺 source**：拿不准是 cur-session 还是文献提炼时，懒得查 → 留下假独立的 PAT
- **wiki 层夹带 raw source 副本**：把整篇外文 paste 进 PAT 正文（应该入 RES，PAT 只引摘要 + 双链回 RES）
- **schema 偷偷改**：不开 ADR 直接改 `0-Index.md` 协作规则节，未来无法溯源
- **强行把 .claude/rules/ 当 schema 写决策**：rules 是机器版本（会过期），决策入 ADR（详 PAT-25 / 0-Index.md「rules ↔ ADR 分工」节）

## 关联

- 思想源：[[RES-01-karpathy-llm-wiki|RES-01]] § 三层架构
- 顶层哲学：[[PAT-89-bookkeeping-cost-near-zero|PAT-89]]
- 复利原则：[[PAT-91-session-output-filing-back|PAT-91]]
- obs↔memory 边界：[[PAT-17-obs-memory-split|PAT-17]]
- rules↔ADR 边界：[[PAT-25-rules-obs-patterns-collaboration|PAT-25]]
