---
id: SNP-2026-04-12-05
title: 文档同步偏差汇总（2026-04-12 时点快照）
type: snapshot
date: 2026-04-12
status: archived
keywords: [SNP-2026-04-12-05, 文档同步偏差汇总（2026-04-12 时点快照）]
tags: [snapshot, audit, 2026-04-12]
sources:
  - .claude/plans/audit-reports/00-architecture-review.md
  - .claude/plans/audit-reports/00-risk-registry.md
  - .claude/plans/audit-reports/00-optimization-plan.md
related:
  - "[[ADR-008-managerbase-internal-abstract|ADR-008]]"
  - "[[ADR-009-uimanager-no-addcomponent-fallback|ADR-009]]"
  - "[[ADR-010-validation-on-consumer-side|ADR-010]]"
  - "[[PAT-08-architecture-antipatterns|PAT-08]]"
---

# 文档同步偏差汇总（2026-04-12 时点快照）

> ⚠️ 这是 2026-04-12 时点的审计快照，描述当时状态，**禁止修改**。下次审计请新建 `2026-XX-XX/` 目录。

## 概要

2026-04-12 审计在 358 个文档（Runtime 438 + Editor 131 个文件配套）中识别出系统性同步偏差：Priority 数值在 ObjectPool / Table / Config 三组共 8 处文档与代码完全不一致；ARCHITECTURE.md 自身 Shutdown 顺序排序错误 2 处；4 个 ComponentInspector 文档完全缺失；6 处方法名 / 描述错误；NetworkComponentInspector 等 4 处代码风格违规集中爆发（合计 ~106 处）。本快照归档全部失真清单，作为 Phase 7 文档全量同步的基准。

## 正文

### 1. Priority 文档严重偏差（8 处）

| 文档 | 文档值 | 代码值 | ARCHITECTURE.md |
|------|--------|--------|-----------------|
| ObjectPoolManager.md (6 处) | 16 | **2** | 2 (正确) |
| ObjectPoolManagerBase.md | 16 | **2** | 2 (正确) |
| ObjectPoolComponent.md | 16 | **2** | 2 (正确) |
| TableManagerBase.md | 4 | **14** | 14 (正确) |
| TableManager.md | 4 | **14** | 14 (正确) |
| ConfigManagerBase.md | 3 | **15** | 15 (正确) |

> ARCHITECTURE.md 中的 Priority 数值与代码一致，问题集中在 L2 模块文档。建议建立 Priority 单一真相源（ARCHITECTURE.md 引用），L2 文档不再硬编码数值。

### 2. ARCHITECTURE.md 自身错误（Shutdown 顺序表）

| 位置 | 错误 | 正确值 |
|------|------|--------|
| Shutdown 顺序表 | SoundManager(19) 标注 "第2" | 应为 "第1"（Priority 最大先 Shutdown） |
| Shutdown 顺序表 | DebugManager(17) 标注 "第1" | 应为 "第3" |

### 3. 缺失文档清单（4 个 Inspector）

| 缺失 | 优先级 |
|------|--------|
| SoundComponentInspector.md | 必须补全 |
| ObjectPoolComponentInspector.md | 必须补全 |
| TableComponentInspector.md | 必须补全 |
| ConfigComponentInspector.md | 必须补全 |

### 4. 方法名 / 描述错误（6 处）

| 文档 | 错误描述 | 正确 |
|------|----------|------|
| TableComponent.md Start() | 含 `LoadAsync().Forget()` | Start 只调 Initialize |
| ConfigComponent.md 示例 | `LoadConfigAsync()` | 方法名为 `LoadAsync()` |
| ConfigManagerConfig.md | Namespaces "fallback 为 Game.Runtime" | 实际抛 ArgumentException |
| ObjectPoolComponent.md OnDestroy | 调用 Shutdown() | 仅置空引用 |
| VibrateType.md None | "默认振动" | 实际直接 return 不振动 |
| ReleaseObjectsFilter.md | "值大优先淘汰" | 实际值小先淘汰 |

### 5. 代码风格违规热点（~106 处）

| 模块 | 违规数量 | 主要类型 |
|------|----------|----------|
| **NetworkComponentInspector** | ~60 | 单行 summary、对齐空格、分隔注释 |
| **Debug 全模块** | ~18+ | 命名前缀、公有字段、#region、死代码 |
| **PersistComponentInspector** | ~10 | 分隔注释 |
| **ObjectPool** | ~12 | Partial 未拆分（5 个类） |
| **Table+Config** | ~6 | .Method.cs 命名、#region |

### 6. 文档同步根因分析

- **Priority 三处真相源问题**：当前 Priority 值存在于 3 个位置（代码、ARCHITECTURE.md、各 L2 文档），三者之间同步靠人工维护，已出现系统性失真。建议 L2 文档中的 Priority 链接到 ARCHITECTURE.md 而非硬编码数值，或建立自动化校验脚本。
- **方法名 drift**：API 重构后（如 `LoadConfigAsync` → `LoadAsync`）文档未同步更新，体现了"改 class 不改 doc"的反模式。
- **Inspector 文档缺失**：4 个 ComponentInspector 文档从未建立，反映 Editor 层文档建设的系统性遗漏。

## 时点信息
- 审计日期：2026-04-12
- 审计基线：develop 分支 HEAD
- 审计范围：569 个 C# 文件 + 358 个文档（Runtime 438 + Editor 131）

## 关联
- ADR：[[ADR-008-managerbase-internal-abstract|ADR-008]] [[ADR-009-uimanager-no-addcomponent-fallback|ADR-009]] [[ADR-010-validation-on-consumer-side|ADR-010]]
- Pattern：[[PAT-08-architecture-antipatterns|PAT-08]]
- 同批次快照：[[SNP-2026-04-12-01-module-scoring]] [[SNP-2026-04-12-02-defect-baseline]] [[SNP-2026-04-12-03-dependency-graph]] [[SNP-2026-04-12-04-optimization-phases]]
