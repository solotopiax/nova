---
id: PAT-25
title: rules 与 obs Patterns 的协作分工范式
status: active
date: 2026-05-16
summary: rules与obs分工规则常驻obs长期沉淀
category: obs
aliases:
  - PAT-25-rules-obs-patterns-collaboration
keywords: [PAT-25, PAT-25-rules-obs-patterns-collaboration, rules 与 obs Patterns 的协作分工范式]
tags:
  - nova
  - knowledge-base
  - claude-rules
  - obsidian
---

# PAT-25：rules 与 obs Patterns 的协作分工范式

## 适用场景

- 在 Nova 框架中沉淀一条新的执行性铁律（如 Inspector 对齐规则）时，需要决定：放进 `.claude/rules/` 还是 `Assets/Framework/Minds/2-Areas/Patterns/`，还是两者都放？
- 已有 obs Pattern 想要让 AI（subagent / hook 触发）在写代码时强制遵守。
- 评审 rules 文件是否对 obs 内容造成冗余复刻。

## 核心做法

**两者本质差异：**

| 维度 | `.claude/rules/` | `Assets/Framework/Minds/2-Areas/Patterns/`（obs） |
|---|---|---|
| 读者 | AI（subagent）+ hook 触发加载 | 人 + AI 主会话查询 |
| 加载方式 | `paths:` 命中后强制注入 prompt | 主动 `/nova-obs query` 或人工翻阅 |
| 回答 | **What / How**：现在该怎么写 | **Why / When**：为什么这么定、跨项目复用 |
| 形态 | 命令式、清单化、短 | 叙事式、含历史与 trade-off |
| 生命周期 | 跟着代码走，持续修订 | 决策当时的快照 + 修订记录 |

**配合范式：**

```text
发现痛点 → PAT-DRAFT → /nova-obs inbox-to-areas → PAT-XX（obs 全文）
                                                       ↓
                       （若需 AI 写代码时遵守）
                                                       ↓
                                .claude/rules/xxx.md（命令式裁剪 + 一行回链 obs）
```

**三种落点判断：**
- **只在 obs**：跨项目复用经验、设计权衡讨论、被 supersede 的旧方案。AI 写代码不需要被打扰。
- **只在 rules**：纯工具链规范（路径约定、命名规则）。通常没有 Why，直接命令即可。
- **两边都有**：执行性铁律。**rules 是 obs 的命令式裁剪，不是复制**。

**rules 文件的非冗余模板：**

```markdown
---
paths: [...]
---

# <规则标题>

## 铁律
- 短 bullet ×N

## 合规 / 反模式（每类 1-2 行代码片段）

> 详细动机、历史成因、跨项目复用见 [[PAT-XX-<slug>]]
```

只留**红线 + 最小代码片段 + 一行回链**。

## 反模式

- ❌ 把 obs PAT 的「为什么这么做」段原文搬进 rules——AI 写代码不需要 Why，看到铁律执行即可。
- ❌ 在 rules 里维护一份关联 PAT 链接列表——导航职责归 obs `0-Index.md` 与 PAT frontmatter 的 `related`。
- ❌ 在 rules 里写长篇反模式分析与历史背景——这是决策档案的工作。
- ❌ rules 与 obs 同时维护两份 Why，导致后续修订两边漂移。
- ❌ 工具链型规范（无 Why）也强行起一份 obs Pattern——徒增噪音。

## 来源对话摘录

> 用户：rules下的内容和obs的内容怎么配合？rules下是否冗余？

> 助手：rules 是"执行手册"，obs 是"决策档案"。一个回答"现在该怎么写"，一个回答"当初为什么这么定"。两边都有时，rules 是 obs 的命令式裁剪，不是复制。

> 助手（评审 framework-inspector-alignment.md）：§二 Why 是 obs PAT-24 的原文搬运，冗余；§三关联规则列表更像 obs 的导航职责，半冗余。建议压成「铁律 + 合规/反模式各 1-2 例 + 一行 obs 回链」。

> 用户：好（确认按该标准重写 rules 文件）

---
