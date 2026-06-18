---
id: GLO-02
title: Manager 三层继承链（FrameworkManager / ManagerBase / Manager）
type: glossary
status: active
date: 2026-05-14
summary: Manager 采用接口 + Base + 唯一实现的固定结构
category: arch
keywords: [FrameworkManager, GLO-02, IXxxManager, Manager 三层, ManagerBase, 三层继承链]
tags: [glossary, nova, architecture, manager]
aliases:
  - FrameworkManager
  - ManagerBase
  - IXxxManager
  - Manager 三层
  - 三层继承链
related:
  - "[[ADR-001-component-manager-three-layer]]"
  - "[[ADR-008-managerbase-internal-abstract]]"
  - "[[GLO-03-component-procedure-manager]]"
---

# Manager 三层继承链

## 一句话定义

对外只暴露接口，内部通过 `ManagerBase + Manager` 承担骨架与实现。

## 结构

```text
FrameworkManager
  -> {Xxx}ManagerBase
  -> {Xxx}Manager
```

| 层 | 职责 |
|---|---|
| `I{Xxx}Manager` | 对外契约 |
| `{Xxx}ManagerBase` | 抽象骨架 |
| `{Xxx}Manager` | 唯一实现 |

## 关键点

- `ManagerBase` 不承载完整业务实现。
- `Manager` 承担真实业务逻辑。
- `Component` 应持有接口，不持有具体实现类型。

## 关联

- [[ADR-001-component-manager-three-layer]]
- [[ADR-008-managerbase-internal-abstract]]
- [[GLO-03-component-procedure-manager]]
