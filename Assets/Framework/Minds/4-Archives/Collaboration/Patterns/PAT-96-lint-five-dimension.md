---
id: PAT-96
title: Vault 5 维 lint 方法论 矛盾陈旧孤立空缺膨胀
summary: Vault 5 维 lint 矛盾陈旧孤立空缺膨胀
status: active
category: obs
type: pattern
date: 2026-05-25
source: karpathy-llm-wiki
aliases:
  - PAT-96-lint-five-dimension
  - lint-five-dimension
tags: [pattern, obs, vault, lint, health, methodology, ai-collab]
keywords: [lint, 5 维, 矛盾, 陈旧, 孤立, 数据空缺, 膨胀, obs-health, Karpathy LLM Wiki, contradictions, stale, orphan]
related:
  - "[[RES-01-karpathy-llm-wiki|RES-01]]"
  - "[[PAT-95-vault-as-codebase|PAT-95]]"
  - "[[PAT-89-bookkeeping-cost-near-zero|PAT-89]]"
  - "[[PAT-90-ingest-cascade-update|PAT-90]]"
---

# PAT-96：Vault 5 维 lint 方法论

## 模式（What）

Vault 健康检查（`obs-health`）必须覆盖 **5 个维度**，缺一不可：

| # | 维度 | 检测的是 | 例 |
|---|---|---|---|
| 1 | **矛盾**（contradictions） | 跨文件数据/状态冲突 | ADR 表与代码 Priority 不一致；A 说 X accepted, B 说 X superseded |
| 2 | **陈旧**（stale claims） | 被新源推翻但本体未更新 | 2 个月前 ADR 引用已 superseded 的 ADR；正文说"待开发"但 ADR 已 accepted |
| 3 | **孤立**（orphan pages） | 无任何入站双链的页面 | 写完没人引用的 PAT-XX；MOC 没收录的工作流 |
| 4 | **数据空缺**（missing pages） | 重要概念被反复提及但无独立成页 | "AssetLocation" 全 Vault 出现 ≥10 次但无 GLO；MOC 引用未存在的 ADR |
| 5 | **膨胀**（bloat） | 索引文件 / 单一 PAT 过大失去导航价值 | `0-Index-PAT.md > 150 条`需拆 Layer 2；单 PAT 正文 > 800 行需切分 |

> "the LLM lints the wiki: it looks for contradictions across pages, claims that have been superseded by new sources, orphan pages, missing concept pages, missing cross-references, and suggests new directions to investigate." — Karpathy LLM Wiki

## 为什么（Why）

人维护 wiki 的失败核心是 **"维护成本增长比价值快"**。LLM 来 lint：

- **矛盾**不修 → 知识库变成"言之有物的胡说八道"，下游决策受污染
- **陈旧**不查 → 老 ADR 半年后变成考古文物，新人按错的来
- **孤立**不连 → 知识沉到底层，搜不到 = 不存在
- **空缺**不补 → 概念在多处隐式存在，每次 ingest 都重新解释一遍
- **膨胀**不拆 → 索引文件超 200 行 LLM 上下文窗口加载成本陡增

5 维不是 Karpathy 凭空发明，而是 **Memex (1945) 80 年来人维护知识库失败的归纳**。LLM 时代终于有"程序员"持续 lint。

## 怎么用（How to apply）

### 5 维与 Nova 现有工具的对应

| 维度 | Nova 实现 | 当前状态 |
|---|---|---|
| 矛盾 | `nova-obs/subcommands/health.md` § 4 矛盾检测 | 启发式（手扫为主） |
| 陈旧 | § 5 陈旧声明检测 | 启发式（date 字段 + 关键词扫描） |
| 孤立 | § 6 孤立页面检测 | 反向链 grep（已可机器检测） |
| 空缺 | § 7 数据空缺检测 | 启发式（高频词 vs GLO 比对） |
| 膨胀 | § 8 + `--check-bloat` | 已可机器检测（行数阈值） |

**理想终态**：5 维全机器化 → 季度全量 + 每次 ingest 增量检测。

### 跑 lint 的节奏

| 节奏 | 命令 |
|---|---|
| 每次 ingest 后 | `/nova-obs inbox-to-areas` 内含 supersedes/MOC/反向链联动检查（PAT-90） |
| 每月 | `/nova-obs health --report-only`（只看 5 维报告，不动索引） |
| 每季度 | `/nova-obs health` 无参（全量 + rebuild-index + check-bloat） |
| 跨 ≥10 文件 bulk-update 后 | `/nova-obs health --rebuild-index` 立刻收口 |

### 5 维 lint 的产出格式

每维独立报告，**不在一份大报告里融合**。原因：

- 矛盾要决策（人审）
- 陈旧要复核（半人工）
- 孤立要补链或删除（人决定）
- 空缺要起草新页（计划性 work）
- 膨胀要拆分（schema 改动，需用户拍板）

5 类产出**性质不同**，硬合在一起 = 用户淹没在噪声里。obs-health 报告按 5 节分别输出。

### 与 PAT-90 的边界

| 时机 | 用 |
|---|---|
| **ingest 时刻**（单条入库） | PAT-90 三联动（supersedes/MOC/反向链） |
| **既有内容修订时刻**（A 改一句话，扇出 B/C/D） | 5 维 lint 之"矛盾检测" + 反向链 grep |
| **周期性 lint** | 5 维 lint 全量 |

PAT-90 是"事中"，5 维 lint 是"事后周期"，互补。

## 反模式

- 只跑膨胀检测不跑前 4 维 → 等于代码只跑 lint warning 不跑 bug 检测
- 把 5 维报告塞进一份 monolith → 用户没法分类决策，等于没报
- "lint 报告太多看不完，下次再说"——典型 Vault 失修起点；建议设上限"每月 ≤30 条 lint 项"，超过停 ingest 先消化
- 让 LLM 自动改 lint 项（如自动改 status）→ 矛盾决策权属于用户，禁自动覆盖
- 把 5 维等同于 frontmatter 校验——frontmatter 校验只是 lint 的一个子项（schema validate），不是全部

## 关联

- 思想源：[[RES-01-karpathy-llm-wiki|RES-01]] § "the LLM lints the wiki"
- 实现：`.claude/skills/nova-obs/subcommands/health.md` § 4-8
- Vault 哲学：[[PAT-95-vault-as-codebase|PAT-95]]
- ingest 联动：[[PAT-90-ingest-cascade-update|PAT-90]]
- 顶层哲学：[[PAT-89-bookkeeping-cost-near-zero|PAT-89]]
