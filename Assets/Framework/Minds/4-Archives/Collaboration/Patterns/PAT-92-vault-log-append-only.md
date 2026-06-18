---
id: PAT-92
title: Vault 时序日志 0-Log.md append-only 铁律
summary: 结构性动作必入时序日志 append-only 可 grep
category: obs
status: active
type: pattern
date: 2026-05-25
source: karpathy-llm-wiki
aliases:
  - PAT-92-vault-log-append-only
  - vault-log-append-only
tags:
  - pattern
  - obs
  - vault
  - log
  - knowledge-base
  - ai-collab
keywords: [0-Log.md, append-only, ingest, migrate, supersede, lint, grep, 时序日志, 知识库, LLM Wiki]
related:
  - "[[PAT-17-obs-memory-split|PAT-17]]"
  - "[[PAT-52-cc-obs-four-layer-enforcement|PAT-52]]"
  - "[[PAT-89-bookkeeping-cost-near-zero|PAT-89]]"
  - "[[PAT-90-ingest-cascade-update|PAT-90]]"
---

# PAT-92：Vault 时序日志 0-Log.md append-only 铁律

## 模式（What）

`Assets/Framework/Minds/0-Index/0-Log.md` 是 Vault 的 **append-only 时序日志**，所有结构性动作（ingest / migrate / archive / supersede / lint / index-rebuild / bulk-update）完成时**必须**追加一行。

格式固定：

```text
## [YYYY-MM-DD HH:MM] <type> | <title>
<一行说明：动了什么 + 关联文件数 + 影响>
```

## 为什么（Why）

借鉴 Karpathy "LLM Wiki" 模式（[gist](https://gist.github.com/karpathy/442a6bf555914893e9891c11519de94f)）：

- **不靠 git log**：git log 太细（每次 commit 都记），且需切上下文打 git 命令；时序日志是 Vault 自描述
- **可 grep 取最近**：`grep '^## \[' 0-Log.md | tail -10` 直接拿最近 10 条结构性动作，无需 LLM 翻聊天历史
- **跨会话连续性**：`/clear` 后下一会话读 0-Log.md 末尾即可知道"上次干到哪儿"
- **审计追溯**：哪天谁改了 ADR-002、什么时候 121 篇笔记被批量回填——日志是单一真相源

## 怎么用（How to apply）

### 必入日志的动作

| 类型 | 何时记 |
|---|---|
| `ingest` | 草稿入 2-Areas（ADR/PAT/GLO/RES/MOC） |
| `migrate` | 跨目录迁移（如 1-Inbox→2-Areas，2-Areas→4-Archives） |
| `archive` | 走归档通道完成 |
| `supersede` | ADR 推翻链建立或刷新 |
| `lint` | obs-health 跑完一次完整体检 |
| `index-rebuild` | `0-Index-*` 索引重建 |
| `bulk-update` | ≥10 个文件的批量改动（如 keywords 回填） |

### 不入日志的动作

- 单字段 typo 修复
- 未生效的草稿编辑（仍在 1-Inbox）
- 单文件正文小幅润色（无结构变化）

### 维护铁律

- **append-only**：禁修历史行（错了就追加修正行说明）
- **倒序消费**：读时从末尾向上读，最近的动作最有用
- **time 用本地时间**：YYYY-MM-DD HH:MM，不用 UTC（对作者更自然）

## 反模式

- 关键动作完成不写日志（典型：本会话 21 MOC 入库时差点忘记起 0-Log.md）
- 把 0-Log.md 当 obs ADR 用，写决策内容（决策入 ADR，日志只记"何时何动作"）
- 修历史行（git diff 一查就破，且破坏审计追溯）
- 写琐碎动作刷流水（typo 别记，不然真有用的动作会被淹没）

## 关联

- 索引文件：`Assets/Framework/Minds/0-Index/0-Log.md`
- Hook 联动：`.claude/hooks/nova-obs-keyword-guard.sh` 已加 0-Log.md 追加提示节
- 思想源：Karpathy LLM Wiki（持久化复利知识库模式）
