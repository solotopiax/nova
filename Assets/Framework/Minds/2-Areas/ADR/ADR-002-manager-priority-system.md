---
id: ADR-002
title: Manager Priority 体系（以代码常量为单一真相源）
status: accepted
date: 2026-05-14
summary: Manager Priority 只以代码常量为准
category: arch
aliases:
  - ADR-002
keywords: [ADR-002, Manager Priority 体系（以代码常量为单一真相源）]
tags: [adr, nova, architecture, priority, lifecycle]
supersedes: []
superseded-by: []
related:
  - "[[ADR-001-component-manager-three-layer|ADR-001]]"
  - "[[GLO-02-framework-manager-tiers]]"
  - "[[PAT-08-architecture-antipatterns]]"
---

# ADR-002：Manager Priority 体系（以代码常量为单一真相源）

## 背景

`FrameworkManagersGroup` 按 `Priority` 升序组织 Manager，`Update` 正序执行，`Shutdown` 逆序执行。  
只要 Priority 失真，模块初始化顺序、依赖可见时机和退出释放顺序就会一起失真。

历史上同一组 Priority 同时散落在代码、`Docs` 和知识条目中，人工同步很容易漂移。因此需要锁定单一真相源。

## 决策

**Manager Priority 的单一真相源是代码中的 `public override int Priority => N`。**

截至当前代码，Priority 如下：

| Manager | Priority | Manager | Priority |
|---|---|---|---|
| Debug | 0 | HttpManager | 8 |
| Persist | 0 | WebSocketManager | 9 |
| Event | 1 | Config | 10 |
| ObjectPool | 2 | NetworkManager | 10 |
| Asset | 4 | PrefabManager | 10 |
| Localization | 6 | AppManager | 11 |
| Procedure | 6 | DoHManager | 11 |
| UI | 7 | Table | 14 |
|  |  | SDK | 16 |
|  |  | Vibrate | 18 |
|  |  | Sound | 19 |

补充约束：

- 同优先级只表示同一档，不表示可依赖的先后顺序。
- 新增 Manager 时，必须先确定插入档位，再同步 `Docs` 与相关索引。
- `Docs` 和 `Minds` 中若出现与代码冲突的 Priority，默认视为知识漂移，应回到代码修正。

## 后果

### 正面

- 模块顺序判断有稳定锚点。
- 文档和知识条目不再各自维护独立真值。
- Review 时可以直接 grep `Priority =>` 做核验。

### 负面

- 仍需人工在重构后同步文档和知识条目。
- 同档位内部不应形成隐式顺序依赖，否则后续重排风险高。

## 验证方式

- 代码检查：`Assets/Framework/Scripts/Runtime/**/{Xxx}ManagerBase.cs`
- 检索关键词：`override int Priority =>`
- 文档检查：`Docs` 中不应再维护与代码分离的另一套 Priority 真值

## 关联

- [[ADR-001-component-manager-three-layer|ADR-001]]
- [[ADR-008-managerbase-internal-abstract|ADR-008]]
- [[GLO-02-framework-manager-tiers]]
