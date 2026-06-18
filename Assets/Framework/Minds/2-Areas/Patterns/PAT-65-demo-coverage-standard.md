---
id: PAT-65
title: Demo 接口覆盖标准：8 维 + 取舍规则
summary: 8 维覆盖矩阵 + 重载族折叠 + 模块 12 叶子上限
category: docs
type: pattern
status: active
date: 2026-05-22
aliases:
  - PAT-65-demo-coverage-standard
keywords:
  - Demo 接口覆盖标准：8 维 + 取舍规则
  - PAT-65
  - PAT-65-demo-coverage-standard
tags:
  - pattern
  - methodology
  - sample
  - demo
  - coverage
related:
  - "[[ADR-033-maindemo-isolated-topology|ADR-033]]"
---

# PAT-65：Demo 接口覆盖标准：8 维 + 取舍规则

## 适用场景（When）

- 框架 / SDK 编写演示 sample 时，需要回答"覆盖了多少接口"
- 跨模块 demo 设计无统一标准，叶子节点取舍随意时
- 接口重载族过多导致 demo 爆炸时
- code-reviewer / qa-reviewer 验证 sample 完备性时缺判定标准

## 核心做法（What & How）

### 8 维覆盖矩阵

每个模块按以下 8 维过一遍，缺哪维补哪维：

| 维度 | 含义 | 举例（UI 模块） |
|---|---|---|
| **核心生命周期** | Open / Close / Update / Destroy 全链路 | OpenUI / CloseUI / OnEnter / OnExit / OnPause / OnResume |
| **重载族（举代表）** | 同一动作多重载，挑 1-2 个代表 | `OpenUI<T>()` / `OpenUI<T>(userData)` / `OpenUI(Type)` |
| **异步形态** | 同步 / UniTask / 取消 | `OpenUIAsync + CancellationToken` |
| **配置驱动** | 通过 ScriptableObject / Inspector 配置注入 | UIGroup 优先级、UIForm 的 PauseCoveredUI |
| **事件 / 回调** | 公开事件、UnityEvent、Action | OnUIFormOpen / OnUIFormClose 订阅 |
| **错误 / 边界** | 重复打开、不存在的 Type、关闭已关闭、加载失败 | 重复 OpenUI 同类型 / Close 已关 |
| **跨模块协同** | 与其他 Manager 协同的最小用例 | UI + Localization 切语言 |
| **Editor 工具** | Inspector 自定义、EditorWindow（仅暴露给业务的部分） | UIComponentInspector 字段 |

### 取舍规则（避免树爆炸）

| 规则 | 说明 |
|---|---|
| **每模块 ≤ 12 个叶子** | 超过则按子目录再分，硬上限保住可读性 |
| **每叶子 ≤ 1 个 Panel prefab + 1 个 MonoBehaviour** | 单一职责，一个 demo 不塞多用例 |
| **重载族折叠规则** | 5+ 重载只演示「无参 / 主流参数 / 全参 / 异步+取消」4 类代表 |
| **错误用例归到模块根目录的 `Edge Cases` 子节点** | 不与正常用例混排，避免新手被劝退 |
| **跨模块协同归到根目录 `Integration` 大节点** | 不在每个模块下重复出现 |

### 实施顺序

| Phase | 输出物 |
|---|---|
| 1 | architect 输出 **接口覆盖矩阵**（markdown 表）：扫所有 public/protected API，按 8 维归类，标注「代表性 / 完整覆盖 / 跳过」 |
| 2 | 用户审矩阵，砍/加/调整覆盖度 |
| 3 | architect 基于审定矩阵反推树结构 |
| 4 | runtime-coder + editor-coder 按 prefab 清单分批落地 |

**关键**：先有矩阵再有树，否则树画得再漂亮也是空中楼阁。

## 为什么这么做（Why）

- "全接口演示"不可达——重载族穷举会让 prefab 数量过百，维护崩溃
- 8 维是从"用户怎么用 API"反推出来的最小必要集，少一维就有真实场景遗漏
- 12 叶子上限是经验数字：超过 12 在树形 UI 中需要二次滚动，体验断裂
- "代表性优于穷举"避免开发者在重载族里堆砌相似 demo
- Edge Cases / Integration 独立分组让正常路径保持简洁——新手只看正常用例就能上手

## 反模式（Anti-patterns）

- **重载穷举型**：把 `OpenUI` 的 7 个重载每个都做一个 demo prefab，结果 7 个 demo 95% 代码重复，用户也分不清差异 → 应折叠为 4 类代表
- **错误用例混排**：把"重复 OpenUI 打开"和"基础 OpenUI"放同级叶子，新手第一次点开就看到异常处理代码，被劝退 → Edge Cases 必须独立子节点
- **跨模块协同重复**：UI + Localization 既出现在 UI 模块下又出现在 Localization 模块下，维护时两份不同步 → 必须统一归到 Integration 节点
- **没有矩阵直接画树**：直接按"我觉得这些重要"列叶子，三个月后发现一半接口零覆盖——必须先 Phase 1 矩阵后 Phase 3 树
- **一个 prefab 塞多用例**：把"同步 / 异步 / 取消"塞同一个 Panel 三个按钮，违反单一职责，演示代码无法当模板复用

## 跨项目复用提示

- 8 维矩阵直接搬到任何 framework / SDK sample 编写场景适用
- "代表性 vs 完整覆盖"取舍规则与具体技术栈无关
- 12 叶子上限可按导航 UI 形态调整（侧栏 vs 卡片墙阈值不同）
- Phase 1 矩阵驱动 Phase 3 树的方法论通用，适合任何"接口体量大、demo 体量也大"的项目

## 关联

- 规范落点：待补充至统一 Sample 覆盖规范（暂未落地）
- 相关 ADR：[[ADR-033-maindemo-isolated-topology|ADR-033]]（MainDemo 演示拓扑）
- 相关 Pattern：[[PAT-42-naming-concrete-deduplicate|PAT-42]]（命名具体化原则——叶子命名风格）
