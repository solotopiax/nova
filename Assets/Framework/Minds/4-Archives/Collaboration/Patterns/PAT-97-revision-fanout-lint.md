---
id: PAT-97
title: 既有内容修订必扇出反向链复核
summary: 改一处必扇出全部引用复核 不可只改源
status: active
category: obs
type: pattern
date: 2026-05-25
source: karpathy-llm-wiki
aliases:
  - PAT-97-revision-fanout-lint
  - revision-fanout-lint
tags: [pattern, obs, vault, revision, fanout, lint, ai-collab]
keywords: [修订, 扇出, fanout, 反向链, 引用, 一致性, bulk-update, 漂移, drift, ADR-002]
related:
  - "[[RES-01-karpathy-llm-wiki|RES-01]]"
  - "[[PAT-90-ingest-cascade-update|PAT-90]]"
  - "[[PAT-96-lint-five-dimension|PAT-96]]"
  - "[[PAT-89-bookkeeping-cost-near-zero|PAT-89]]"
---

# PAT-97：既有内容修订必扇出反向链复核

## 模式（What）

修订 Vault **既有条目**（非 ingest 新条目）时，**必须**扇出该条目的所有反向链 / 引用文件并核对一致性，不允许"只改源不动引用"。

操作三步：

1. **改前先扇**：`grep -rln "<被改条目 id 或关键 fact>" 2-Areas/ 0-Index/ 3-Resources/` 列全清单
2. **改源 + 改扇**：源条目修订的同时，扇出文件中受影响的句子 / 表 / 双链同步更新
3. **追加 lint 行**：`0-Log.md` 记 `bulk-update` 类型，注明扇出文件数

## 为什么（Why）

PAT-90 解决 **ingest 时刻**（新条目入库）的 supersedes/MOC/反向链三联动；本 PAT 解决**既有条目修订时刻**——两者时机不同，缺一不可。

Karpathy 范式：

> "the wiki is a persistent, compounding artifact. The cross-references are already there. The contradictions have already been flagged. The knowledge is compiled once and then kept current, not re-derived."

**"kept current"**的工程含义：单源改动必须扇出所有派生条目，否则知识库进入"漂移"状态。

Nova 历史问题：

- **ADR-002 Priority 表漂移 2 个月**：18 个 Manager Priority 在代码里改了，但 ADR 表 / L2 文档没扇出更新；后被批 C 报告捎带发现
- **GLO-01 IBaseLife 废止**：废止决策落 ADR-009，但 ADR-006 / PAT 内引用未扇出标注「已废止」，新会话还以为这是有效方案
- **CLAUDE.md 红线节调整**：本会话多次微调红线表，扇出文件（agent 定义 / SKILL.md / rules/）漂移风险高

漂移成本 = LLM 不会主动注意到（除非定期跑 5 维 lint），积累 N 次后老条目变 wiki 黑洞。

## 怎么用（How to apply）

### 触发条件（满足任一）

- 修订 ADR/PAT/GLO 的 frontmatter `status` / `summary` / `category`
- 修订 ADR/PAT/GLO 正文中的**事实层**（数值、API 名、文件路径、决策结论）
- 修订 `0-Index/0-Index.md` 协作规则节的硬约束
- 修订 `.claude/CLAUDE.md` 红线表
- ≥10 个文件的 frontmatter 批量回填（如 keywords 全量补齐）

### 扇出 SOP（4 步）

```
Step 1: 列扇出清单
  grep -rln "<id 或关键 fact>" Assets/Framework/Minds/{2-Areas,3-Resources,0-Index} \
    .claude/{rules,agents,CLAUDE.md} 2>/dev/null

Step 2: 分类扇出文件
  - frontmatter related/supersedes 引用 → 跟随源文件状态调整
  - 正文双链 [[...]] → 检查目标存在性 + 措辞是否仍准确
  - 表格 / 数值 / API 字面量 → 必须同步改（否则即矛盾）
  - 描述性引用（"详见 ADR-XX"） → 仅当源 ADR 大改时复核

Step 3: 扇出修订
  - 同会话内一次性改完，不留半成品
  - 改不动的（如 .claude/rules/ 与 obs 分工不同）→ 至少在源条目正文标注"扇出范围 + 已遗漏项"

Step 4: 追加 lint 行
  ## [YYYY-MM-DD HH:MM] bulk-update | <源条目 id> 扇出修订
  改了 <源 id>，扇出 N 个文件（详 grep 清单），完成 K 个，遗留 M 个待复核
```

### 不需要扇出的修订

| 修订类型 | 理由 |
|---|---|
| typo / 标点 / 措辞润色 | 不影响事实层，扇出文件无需改 |
| frontmatter `keywords:` 增补（不删除） | keywords 是辅助 grep 的元数据，不构成跨文件依赖 |
| 1-Inbox/ 草稿内自改 | 还没入库，无反向链 |
| 4-Archives/ 内归档文件改正 typo | 归档区不再是 active codebase |

### 与 PAT-90 / 5 维 lint 的边界

```
ingest 新条目  →  PAT-90 三联动（supersedes/MOC/反向链）
既有条目修订   →  本 PAT 扇出复核（反向链 + 事实层一致性）
周期性体检    →  5 维 lint（矛盾/陈旧/孤立/空缺/膨胀）
```

三层时机互补：事中（ingest）、事中（修订）、事后（周期）。

## 反模式

- "我就改一行 typo，不用扇出"——typo 和事实层一墙之隔，先 grep 一下成本极低
- 改完源不追 0-Log.md → 未来 5 维 lint 矛盾报警时无线索追溯
- 扇出文件改了一半剩下"明天再说"——半成品比不改更糟（部分一致 = 隐性矛盾）
- 让 LLM 自动扇出全替换 → 无人审定的批量改易引入次生矛盾，必须用户分批审 + 提交
- 跨域扇出（obs PAT 改了，扇出到 .claude/rules/）不走"扇出三步"——rules 改动是跨 schema 操作，需走 ADR

## 关联

- 思想源：[[RES-01-karpathy-llm-wiki|RES-01]] § "kept current, not re-derived"
- 时机互补：[[PAT-90-ingest-cascade-update|PAT-90]]（ingest 时刻）
- 周期 lint：[[PAT-96-lint-five-dimension|PAT-96]]（事后体检）
- 时序日志：[[PAT-92-vault-log-append-only|PAT-92]] `bulk-update` 类型
- 顶层哲学：[[PAT-89-bookkeeping-cost-near-zero|PAT-89]]
