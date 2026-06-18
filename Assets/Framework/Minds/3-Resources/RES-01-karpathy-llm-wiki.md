---
id: RES-01
title: Karpathy LLM Wiki — 持久复利知识库范式
summary: Karpathy 的 LLM wiki 方法摘要与 Nova 借鉴点
status: active
category: ai-collab
type: resource
date: 2026-05-25
source-url: https://gist.github.com/karpathy/442a6bf555914893e9891c11519de94f
author: Andrej Karpathy
license: gist 公开（个人 fair-use 引用）
aliases:
  - RES-01-karpathy-llm-wiki
  - karpathy-llm-wiki
tags: [resource, ai-collab, knowledge-base, methodology, llm, wiki]
keywords: [Karpathy, LLM Wiki, Memex, Vannevar Bush, Obsidian, RAG, ingest, lint, filing back, compounding, bookkeeping]
related:
  - "[[PAT-89-bookkeeping-cost-near-zero|PAT-89]]"
  - "[[PAT-91-session-output-filing-back|PAT-91]]"
  - "[[PAT-95-vault-as-codebase|PAT-95]]"
  - "[[PAT-96-lint-five-dimension|PAT-96]]"
---

# RES-01：Karpathy LLM Wiki — 持久复利知识库范式

## 是什么（What）

Andrej Karpathy 2026 年发布的 gist，提出 **LLM Wiki** 模式：把传统需要人手维护的 wiki，改为由 LLM 负责 ingest、整理、交叉引用与持续重写；人类主要负责给出可靠来源与提出正确问题。它本质上是 Memex 思路在 LLM 时代的可执行版本。

**原文 URL**：https://gist.github.com/karpathy/442a6bf555914893e9891c11519de94f

## 为什么对 Nova 有价值

它对 Nova 的价值不在于复刻某一套旧协作栈，而在于确认了三件长期成立的事：

1. **知识库要被当作持续积累的资产**，而不是一次性对话缓存。
2. **高频 bookkeeping 适合交给 LLM**，否则知识层会很快失养。
3. **源材料、沉淀页、索引规则要分层**，否则查询、更新与纠错会互相污染。

## 核心论断（按主题分组）

### 三层架构

| 层 | 作用 | Nova 对应 |
|---|---|
| **Raw sources** | 保留外部原始材料与出处 | `3-Resources/` |
| **The wiki** | 把事实整理成结构化知识页 | `2-Areas/` |
| **The schema** | 约束索引、命名与治理规则 | `0-Index/` 与 vault 维护脚本 |

可借用的核心比喻是：**wiki 像代码库，LLM 像维护它的程序员**。一旦交叉引用失效、术语冲突或来源缺失，就应该被当作知识层编译错误处理。

### 操作动作

- **Ingest**：读源并抽出可长期复用的事实。
- **Query**：围绕问题跨页综合，而不是只返回单篇摘要。
- **Lint**：持续检查矛盾、陈旧主张、孤立页与缺失引用。
- **Filing back**：把本轮分析结果回填为后续可复用知识。

### Nova 里最值得复用的部分

最值得保留的是方法，不是原文里的具体工具举例：

- 把 `3-Resources` 当成外部材料留底层。
- 把 `2-Areas` 当成整理后的长期知识层。
- 把索引、关键字、审计脚本当成 schema 层治理工具。
- 把回查与回填动作视为知识维护的日常成本，而不是额外负担。

## 如何使用

- 涉及 Nova 知识库治理、索引维护或回填策略时，优先回看本资源。
- 讨论具体落地规则时，再跳转到对应 PAT。
- 原文若日后被删除或修订，本条仍保留最小可复用提炼版，作为 Vault 内的来源留底。

## 反模式

- **只借口号，不落到具体维护动作**：没有 ingest、lint、回填链路，知识层仍会失养。
- **把 Karpathy 的表述误当 Nova 原创**：需要溯源时应回指本条与原文。
- **照搬原文里的非技术场景示例**：Nova 只需要其中的知识治理方法，不需要业务情境映射。

## 关联

- 思想源：Vannevar Bush *As We May Think* (1945) Memex 设想
- 上游 PAT：[[PAT-89-bookkeeping-cost-near-zero|PAT-89]]、[[PAT-91-session-output-filing-back|PAT-91]]、[[PAT-95-vault-as-codebase|PAT-95]]、[[PAT-96-lint-five-dimension|PAT-96]]
