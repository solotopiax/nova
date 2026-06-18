---
id: PAT-89
title: Vault 维护铁律 bookkeeping 成本压到接近零
summary: 新 skill 必证明压零 bookkeeping 否则不建
status: active
date: 2026-05-25
source: karpathy-llm-wiki
category: obs
type: pattern
aliases:
  - PAT-89-bookkeeping-cost-near-zero
  - bookkeeping-cost-near-zero
tags:
  - pattern
  - obs
  - vault
  - philosophy
  - ai-collab
  - methodology
  - top-level
keywords: [bookkeeping, 维护成本, 哲学, 顶层原则, obs skill 设计, LLM Wiki, Memex, Karpathy, 复利]
related:
  - "[[PAT-92-vault-log-append-only|PAT-92]]"
  - "[[PAT-90-ingest-cascade-update|PAT-90]]"
  - "[[PAT-91-session-output-filing-back|PAT-91]]"
  - "[[PAT-25-rules-obs-patterns-collaboration|PAT-25]]"
---

# PAT-89：Vault 维护铁律 bookkeeping 成本压到接近零

## 模式（What）

Nova Vault 体系的**顶层校准原则**：

> 任何新增 obs skill / hook / agent 都必须证明它把某个原本人工的 bookkeeping 动作（更新交叉引用 / 维护一致性 / 触发联动）的**成本压到接近零**；做不到则不建。

## 为什么（Why）

Karpathy LLM Wiki 核心论断：

> "人类放弃 wiki 是因为维护成本增长比价值快。LLM 不会忘记更新交叉引用，能一次改 15 个文件。wiki 能保持维护，是因为维护成本接近零。"

Vannevar Bush 1945 年的 Memex 设想没能实现的核心障碍是 **"who does the maintenance"**——LLM 来做。

Nova 决策推论：

- **不该 obs skill 化的**：人工已经轻松完成的（如 1 行 README typo），强行 skill 化反成噪声
- **该 obs skill 化的**：每次都要触碰 ≥3 个文件 / ≥2 类联动 / 容易遗漏的（如 supersedes 双向链 / MOC 速查表 / index 重建）
- **判断准则**：这个 skill 完成后，下次同类动作的"人工注意力消耗"是不是接近零？是 → 建；否 → 不建

## 怎么用（How to apply）

### 设计新 obs skill 时的检查清单

| 问题 | 通过标准 |
|---|---|
| 这个动作每次涉及多少文件？ | ≥3 个 |
| 这个动作有几类联动（双向链/索引/状态）？ | ≥2 类 |
| 不做这个 skill 时，人工有没有遗漏过？ | 有 |
| 做了这个 skill 后，人工还需要看几眼？ | ≤1 眼（Just review） |
| 这个 skill 自身的维护成本？ | < 它消除的人工成本 |

通过 ≥4 项 → 该建；否则不建。

### 顶层原则与 PAT-25 的边界

| 层级 | 原则 |
|---|---|
| **顶层哲学**（本 PAT） | bookkeeping 成本接近零 → 决定建不建 obs skill |
| **协作分工**（PAT-25） | rules vs obs Patterns 分工 → 决定**入哪种格式** |
| **禁止入侵**（PAT-17） | obs ↔ memory 分工 → 决定**全文 / 指针的边界** |

本 PAT 是入口校准，PAT-25/PAT-17 是落地分流。

### 实例对照

| 已落地 skill | 是否符合本原则 |
|---|---|
| `/nova-obs inbox-to-areas`（含 supersedes/MOC/反向链联动） | ✓ 涉及 ≥5 文件 ≥3 类联动 |
| `/nova-obs health --rebuild-index` | ✓ 一次扫全 Vault 重建索引，人工无法替代 |
| `nova-obs-keyword-guard.sh` Hook | ✓ 关键字命中即提醒，不依赖 AI 自检 |
| 0-Log.md 时序日志（PAT-vault-log-append-only） | ✓ append 单行成本极低，可 grep 取最近 |

## 反模式

- 为了"显得规范"建 skill：如果没消除人工 bookkeeping，就是噪声
- 把所有动作都 skill 化：单文件单字段改动不需要 skill 包装
- skill 自身比它消除的人工成本还高（典型：复杂前置确认链）
- skill 内部又退回人工"评审完了吗"的形式问询（违反 PAT-47）
- 把哲学顶层与具体 SOP 混写——本 PAT 不写 supersedes 怎么写、index 怎么生成（那是子 skill 的事）

## 关联

- 子原则一：[[PAT-92-vault-log-append-only|PAT-92]] 时序日志
- 子原则二：[[PAT-90-ingest-cascade-update|PAT-90]] 入库三联动
- 子原则三：[[PAT-91-session-output-filing-back|PAT-91]] 探索成果复利
- 协作分工：[[PAT-25-rules-obs-patterns-collaboration|PAT-25]]
- 思想源：Karpathy LLM Wiki + Vannevar Bush Memex (1945)
