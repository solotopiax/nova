---
id: ARC-02
title: 废弃 NovaBehaviour + IBaseLife 桥接方案（回归 HybridCLR 原生 MonoBehaviour）
status: recorded
archive-mode: direct
needs-review: false
date: 2026-05-23
deprecated-on: 2026-05-21
archived-date: 2026-05-23
source: cur-session-all-to-archives
aliases:
  - novabehaviour-bridge-deprecated
tags:
  - archive
  - adr-deprecated
  - glossary-deprecated
  - nova
  - hybridclr
  - novabehaviour
related:
  - "[[ADR-032-drop-novabehaviour-bridge|ADR-032]]"
---

# ARC-02：废弃 NovaBehaviour + IBaseLife 桥接方案

## 原决策（已废弃）

为了在 HybridCLR 业务 dll 加载完成前避免 Prefab 反序列化拿不到业务类型，曾设计 `NovaBehaviour` 桥接基类 + `IBaseLife` / `IEnableLife` / `IUpdateLife` 等生命周期接口 + `Host.Get<T>` / `NovaBehaviourAdapter` / `IBaseLifeAdapter` 一整套桥接体系，业务侧把 MonoBehaviour 子类改写为 `NovaBehaviour` 派生 + 接口实现，由框架在 dll 加载完成后用反射桥接。

涉及条目：

- `ADR-006`：NovaBehaviour + IBaseLife 替代业务 MonoBehaviour（accepted → superseded）
- `ADR-009`：UIManager 取消 AddComponent 兜底（Prefab 必须预挂 NovaBehaviour）（accepted → superseded）
- `GLO-01`：NovaBehaviour / IBaseLife / Host.Get<T> 术语（active → archived）

## 废弃日期与原因

- **废弃日期**：2026-05-21（ADR-032 落地之日）
- **归档日期**：2026-05-23
- **替代方案**：[[ADR-032-drop-novabehaviour-bridge|ADR-032]]——业务直接写 MonoBehaviour 子类预挂 Prefab，框架不再桥接

### 根因

桥接方案存在三重问题：

1. **侵入性**：业务每个 MB 都得改写为 NovaBehaviour 派生 + 实现 IBaseLife，新人接入心智负担大
2. **生命周期一致性差**：桥接层无法 100% 复刻 Unity 原生 MB 生命周期（特别是 OnEnable/OnDisable 与 Awake/Start 的相对顺序在反射桥接路径上经常错位）
3. **HybridCLR 实际上原生支持**：`Prefab.m_Script` 在编辑期可正确写入业务 dll 类型 GUID + fileID，运行时 `Instantiate` 由 Unity 反序列化机制挂上业务组件——前提是先走 `ProcedureLoadDll` 加载 dll，桥接根本不必要

## 替代方案要点

业务直接写 `MonoBehaviour` 子类，按 Unity 原生写法写 `Awake/Start/OnEnable/Update/...`，无需感知 HybridCLR；Prefab `m_Script` 直接指向业务 dll 类型；启动顺序由框架层 `ProcedureLoadDll` 保证（dll 加载晚于 manifest，早于业务 Prefab 加载）。

完整契约见 `framework-hybridclr-constraints.md` §二「Prefab 与组件承载（HybridCLR 原生方案）」节。

## 影响面

- 业务 DLL 层：所有 NovaBehaviour 派生类回归普通 MonoBehaviour，删除 IBaseLife 系列接口实现
- 框架层：删除 `Host.Get<T>` / `NovaBehaviourAdapter` / `IBaseLifeAdapter` 反射桥接代码
- Prefab：旧 `m_Script` 指向 NovaBehaviour 派生的需重新指向业务 MB 子类

## 关联

- 替代方案：[[ADR-032-drop-novabehaviour-bridge|ADR-032]]
- 已归档原方案：
  - [[ADR-006-novabehaviour-ibaselife-replace-monobehaviour|ADR-006]]（status: superseded）
  - [[ADR-009-uimanager-no-addcomponent-fallback|ADR-009]]（status: superseded）
  - [[GLO-01-novabehaviour|GLO-01]]（status: archived）
