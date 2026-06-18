---
id: PAT-95
title: Vault 当代码读铁律 矛盾即编译错误
summary: Vault 当 codebase 矛盾=编译错误必修
status: active
category: obs
type: pattern
date: 2026-05-25
source: karpathy-llm-wiki
aliases:
  - PAT-95-vault-as-codebase
  - vault-as-codebase
tags: [pattern, obs, vault, philosophy, ai-collab, codebase, lint]
keywords: [Vault, codebase, IDE, 编译错误, 矛盾, lint, schema, frontmatter, draft, Karpathy LLM Wiki, Obsidian]
related:
  - "[[RES-01-karpathy-llm-wiki|RES-01]]"
  - "[[PAT-89-bookkeeping-cost-near-zero|PAT-89]]"
  - "[[PAT-93-vault-three-layer-architecture|PAT-93]]"
  - "[[PAT-96-lint-five-dimension|PAT-96]]"
---

# PAT-95：Vault 当代码读铁律 矛盾即编译错误

## 模式（What）

Nova Vault **当代码库读**，不是当聊天记录读：

> "Obsidian is the IDE; the LLM is the programmer; the wiki is the codebase." — Karpathy LLM Wiki

| 代码库概念 | Vault 对应 |
|---|---|
| **编辑器（IDE）** | Obsidian（图视图、双向链、Dataview）|
| **程序员** | LLM / AI agent |
| **代码（codebase）** | `2-Areas/` 全部 ADR/PAT/GLO/MOC |
| **代码 schema（类型/接口）** | frontmatter 字段 + `0-Index/0-Index.md` 协作规则 |
| **未提交代码** | `1-Inbox/` 草稿 |
| **已弃用代码** | `4-Archives/` |
| **编译错误** | obs-health 5 维 lint（矛盾/陈旧/孤立/空缺/膨胀）|
| **commit 历史** | `0-Log.md` append-only 时序日志 |
| **PR review** | `/nova-obs inbox-to-areas` 入库时联动检查 |
| **重构（refactor）** | 5 维 lint 报告后的批量更新 + supersedes 链 |

## 为什么（Why）

把 Vault 当代码读 ⇒ 触发**程序员的反射性纪律**，无须额外训练 LLM：

1. **矛盾不是"细节差异"，是 P0 编译错误**
   - 例：ADR-002 表 / 代码 / L2 文档三处 Priority 不一致 → 不是"待对齐"，是 lint **必须**修
   - 不接受"先用着以后再改"——代码不会接受这个借口
2. **frontmatter 是类型签名**
   - `summary: ≤30 字` / `category: 封闭枚举` 是类型约束
   - 校验失败 = 编译报错，不是建议
3. **草稿在 1-Inbox/ = working copy，不是 codebase**
   - 评审入库 = code review + merge
   - 直入 2-Areas（PAT-86 例外）= force push，需用户显式授权
4. **删除/归档要可溯源**
   - `git rm` 留 git history；`/nova-obs areas-to-archives` 留 4-Archives/Records 与 supersedes 链
   - 不能"静默删除"任何 2-Areas/ 条目

## 怎么用（How to apply）

### 写每条 PAT/ADR 时的"程序员问题"

| 问题 | 通过标准 |
|---|---|
| 这条规则与现有条目有冲突吗？ | grep 同义词 + obsidian_search 跑过；冲突需显式 supersedes |
| 这条规则可被 lint 自动检测吗？ | 能机器检测的硬约束写 `.claude/rules/`；只能人审的软约束写 obs PAT |
| 改这条的影响面有多大？ | 列出反向链清单（grep 全 Vault 引用），扇出修改 |
| 这条会过期吗？ | 会过期 → 加 `date: <YYYY-MM-DD>` + 周期复核入 obs-health 陈旧检测 |
| 这条的来源可追溯吗？ | frontmatter 必含 `source:`（cur-session / RES-XX / external-URL）|

### 矛盾发现时的处置（不容妥协）

> 矛盾 = 编译错误，**禁止"两边都保留"**。

| 矛盾类型 | 处置 |
|---|---|
| ADR 表与代码 fact 冲突 | 代码是 raw source，ADR 必须改正（事实层），决策层另行讨论 |
| PAT-A 与 PAT-B 规则冲突 | 跑 supersedes 通道，废弃方加 `superseded-by`；走 ARC 归档 |
| frontmatter status 矛盾（A 说 X accepted, B 说 X superseded） | obs-health 报矛盾对，用户拍板后批量改 |
| MOC 速查表条目与 ADR 本体不一致 | 以 ADR 本体为准，重跑 `--rebuild-index` |

### refactor 心智

写新 PAT 时遇到"现有结构不太合身"——和写代码遇到耦合一样：

- **小修**：调字段命名 / 改 summary 措辞 → 直接 Edit
- **中修**：抽公共 PAT（如 PAT-89 顶层化 PAT-90~92） → 走入库通道，原条目加 `related:`
- **大修**：跨多条 PAT 重组（如 21 MOC 落地） → 走 ADR 留决策记录 + 0-Log.md 记 `migrate`

## 反模式

- "这两条 PAT 看起来是冲突的，但都有道理，留着" → 等于代码里 `if false { ... }` 死分支，必须删一个或合并
- 把 obs PAT 当备忘录写：随心所欲、不查 supersedes 链 → 等于不审 PR 直接 merge
- frontmatter 字段乱加（如自创 `priority: high`） → 等于乱发明类型，破坏 schema
- 用 0-Log.md 记口头讨论 / 个人感受 → 等于把日志当 chat 用；日志只记**结构性动作**
- "wiki 写错了再改就行"——把 Vault 当 chat history → Karpathy 论断的反面

## 关联

- 思想源：[[RES-01-karpathy-llm-wiki|RES-01]] § "wiki is the codebase"
- 顶层哲学：[[PAT-89-bookkeeping-cost-near-zero|PAT-89]]
- 三层架构：[[PAT-93-vault-three-layer-architecture|PAT-93]]
- 5 维 lint：[[PAT-96-lint-five-dimension|PAT-96]]
- 联动机制：[[PAT-90-ingest-cascade-update|PAT-90]]
- 时序日志：[[PAT-92-vault-log-append-only|PAT-92]]
