---
id: PAT-90
title: 一次入库必触发 supersedes/MOC/反向链三联动
summary: 单条入库强制刷三链 不允许只移文件不联动
category: obs
status: active
type: pattern
date: 2026-05-25
source: karpathy-llm-wiki
aliases:
  - PAT-90-ingest-cascade-update
  - ingest-cascade-update
tags:
  - pattern
  - obs
  - vault
  - ingest
  - supersedes
  - moc
  - cross-reference
  - ai-collab
keywords: [ingest, supersedes, superseded-by, MOC, 反向链, 双向链, dangling link, 联动, cascade, LLM Wiki]
related:
  - "[[PAT-92-vault-log-append-only|PAT-92]]"
  - "[[PAT-17-obs-memory-split|PAT-17]]"
  - "[[PAT-25-rules-obs-patterns-collaboration|PAT-25]]"
  - "[[PAT-89-bookkeeping-cost-near-zero|PAT-89]]"
---

# PAT-90：一次入库必触发 supersedes/MOC/反向链三联动

## 模式（What）

`/nova-obs inbox-to-areas` 每入库一份草稿，**必须**自动执行三项联动检查与回填，不允许"只移文件不联动"。

| 联动 | 触发条件 | 动作 |
|---|---|---|
| **supersedes 双向链** | 草稿 frontmatter 含 `supersedes: [ADR-XXX]` | 自动给 ADR-XXX 加 `superseded-by: [新ID]`；判定"完全推翻"则改 status |
| **MOC 速查表回填** | 按 `category` / 关键字匹配到对应 MOC | 自动追加一行到 `[[MOC-XXX]]` 的速查表 |
| **反向链占位检查** | 草稿正文含 `[[XX-NN-...]]` 双链 | 检查目标存在性；不阻塞入库，但报告 dangling list |

## 为什么（Why）

Karpathy LLM Wiki 核心论断："a single source might touch 10-15 wiki pages"。维护成本被 LLM 摊薄，**而非通过减少联动来回避维护**。Nova 历史问题（本会话亲历）：

- ADR-037 入库时**没自动给 ADR-003 加 superseded-by**，靠用户事后发现
- 21 MOC 落地后**没自动同步 ADR/PAT 速查表**，用户问到 MOC 是什么时才回头补
- ADR-002 Priority 表过期 2 个月才被批 C 报告捎带发现——说明无人定期 lint

bookkeeping 是 LLM 的强项（"LLM 不会忘记更新交叉引用，能一次改 15 个文件"）。把它压成 skill 步骤，cost 就接近零。

## 怎么用（How to apply）

### supersedes 双向链回填规则

```text
读新草稿 frontmatter.supersedes = [ADR-X, ADR-Y, ...]
对每个 ADR-N：
  1. 读 2-Areas/ADR/ADR-N-*.md frontmatter
  2. 若 superseded-by 不含新 ID → 追加
  3. 判定语义：
     - 用户语义"完全推翻" → 改 status: superseded（并提示用户走归档）
     - 用户语义"部分修正" → 保留 status: accepted，仅写 superseded-by
```

### MOC 匹配规则

| 关键信号 | 落 MOC |
|---|---|
| `category: workflow` 或含"调度/agent/Plan-first/并行" | MOC-Workflow |
| 含"Manager / 三层继承 / Priority" | MOC-Manager |
| 含 "UI / View / Panel" | MOC-UI |
| 含 "HybridCLR / 热更 / dll" | MOC-HybridCLR |
| 含 "Inspector / EditorWindow" | MOC-Inspector |
| 含特定模块名（Asset/Network/Sound/...） | MOC-<模块> |

匹配不到时输出警告，让用户选 / 跳过，不阻塞入库。

### 反向链占位策略

- 占位双链合法（标记"待写"），不阻塞入库
- 入库报告必须列出所有 dangling 双链清单，便于后续补齐

## 反模式

- 只移文件，不动 supersedes 链——会让 ADR-003 / ADR-037 这种修正关系失踪
- 入库后不告诉用户"应进哪个 MOC"——MOC 速查表逐渐失修
- 把所有联动塞 rebuild-index 一锅炖——rebuild 是索引层，supersedes 是语义层，不能混
- 联动失败就阻塞入库——dangling 双链是合法的"待写"，必须容忍

## 关联

- 实现：`.claude/skills/nova-obs/subcommands/inbox-to-areas.md` § 7.5
- 时序记录：`0-Log.md` 每次 ingest 必追加一行
- 联动检查：`/nova-obs health` 5 维 lint 中的"矛盾检测"会兜底捕获联动遗漏
- 思想源：Karpathy LLM Wiki "10-15 pages per ingest" 原则
