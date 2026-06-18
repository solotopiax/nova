---
id: MOC-Persist
title: 持久化系统图谱
summary: Persist 三后端结构与使用边界速查
category: module
status: active
date: 2026-06-05
aliases:
  - MOC-Persist
  - 持久化系统图谱
  - 存档图谱
tags: [moc, nova, persist, sqlite, playerprefs, filefragment, runtime]
keywords: [PersistComponent, IPersistManager, ISQLiteManager, IPlayerPrefsManager, IFileFragmentManager, Load, Save]
related:
  - "[[ADR-001-component-manager-three-layer|ADR-001]]"
  - "[[ADR-002-manager-priority-system|ADR-002]]"
  - "[[ADR-008-managerbase-internal-abstract|ADR-008]]"
  - "[[ADR-010-validation-on-consumer-side|ADR-010]]"
  - "[[ADR-023-no-editor-prefs-in-framework|ADR-023]]"
---

# MOC-Persist：持久化系统图谱

## 一句话

Persist 模块把本地存储拆成三个并列后端：`PlayerPrefs / FileFragment / SQLite`；统一入口是 `Nova.Persist`，统一基础契约是 `IPersistManager`。

## 何时查这页

- 要决定数据该落哪种本地存储
- 要改持久化初始化顺序或公共读写契约
- 要分清 `PersistComponent` 和三个后端 Manager 的边界

## 当前结构

```text
Nova.Persist
  -> PersistComponent
     -> IPlayerPrefsManager
     -> IFileFragmentManager
     -> ISQLiteManager

共享基类：
PersistManagerBase<TConfig> : IPersistManager
```

组件事实：

- `Awake()` 里装配三套后端
- `LoadAsync()` 并行初始化三个 Manager
- 业务侧随后通过 `Nova.Persist.PlayerPrefs / FileFragment / SQLite` 使用对应后端

## 三个后端怎么分

| 后端 | 适合什么 |
|---|---|
| `PlayerPrefs` | 简单设置、小量键值 |
| `FileFragment` | 分片文件、存档槽、二进制块 |
| `SQLite` | 结构化数据、大量数据、查询型场景 |

## 共享契约只需要记住这些

- `Load()`
- `Save()` / `Save(string classify)`
- `HasItem / RemoveItem / RemoveAll`
- `GetXxx / SetXxx`

换句话说，三种后端的“用法心智模型”是一致的，差别主要在底层介质和适用场景。

## 当前边界

- `PersistComponent` 负责装配和统一初始化，不负责具体存储算法
- 参数校验、序列化细节、底层读写都在各自 Manager 内
- `EditorPrefs` 不属于 Nova Framework 的持久化体系

## 导航提醒

- `PersistComponent.LoadAsync()` 是启动期统一准备入口，不要把初始化散落到各业务模块里
- `Save(string classify)` 和 `Save()` 是两层粒度，不要把它们混成同一语义
- 具体优先级与后端实现细节，以 `Docs` 和源码为准

## 常见误区

- 直接持有具体实现类而不是接口入口
- 还没完成加载就开始读值
- 用 `EditorPrefs` 保存框架长期状态
- 把“选后端”问题写成一长串 API 手册，而不是先判断存储模型

## 先往哪看

- 改结构：[[ADR-001-component-manager-three-layer]]
- 改优先级：[[ADR-002-manager-priority-system]]
- 改框架存储边界：[[ADR-023-no-editor-prefs-in-framework]]

## 关联

- 图谱：[[MOC-Manager]]
