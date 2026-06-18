---
id: MOC-Event
title: 事件系统 MOC（Event System）
summary: EventManager + EventPool 速查图谱
category: runtime
status: active
date: 2026-05-24
aliases:
  - MOC-Event
  - 事件系统图谱
  - 广播系统图谱
tags: [moc, nova, event, runtime]
keywords: [EventManager, EventComponent, EventData, EventPool, EventPoolMode, Subscribe, Unsubscribe, Fire, FireNow, IEventManager, pub-sub, 事件, 消息, 广播, 通知]
related:
  - "[[ADR-001-component-manager-three-layer|ADR-001]]"
  - "[[ADR-008-managerbase-internal-abstract|ADR-008]]"
  - "[[ADR-010-validation-on-consumer-side|ADR-010]]"
  - "[[ADR-011-load-unload-and-ireference-pairing|ADR-011]]"
  - "[[ADR-016-framework-vs-business-access|ADR-016]]"
  - "[[PAT-27-config-no-serialize|PAT-27]]"
  - "[[PAT-30-framework-usage-redlines|PAT-30]]"
  - "[[PAT-68-pool-reference-spread|PAT-68]]"
---

# MOC-Event：事件系统图谱

> pub-sub 广播总线。任何跨模块解耦通知、跨线程事件分发必读本图。

这页只回答事件系统的入口、结构和边界；具体重载矩阵与调用细节仍回到 `Docs` 和源码。

## 业务语言入口

| 用户说 | 等同 |
|---|---|
| 事件 / 消息 / 广播 / 通知 | EventManager / EventPool / Subscribe |
| 订阅 / 监听 / 注册回调 | `Nova.Event.Subscribe<T>` / `Nova.Event.Subscribe(id, handler)` |
| 发事件 / 发消息 / 触发 / dispatch | `Nova.Event.Fire` / `Nova.Event.FireNow` |
| 取消订阅 / 注销监听 | `Nova.Event.Unsubscribe<T>` / `Nova.Event.Unsubscribe(id, handler)` |
| 事件数据 / 事件参数 / EventArgs | `EventData`（继承 `EventArgs` + `IReference`，分发后自动归还引用池） |
| 事件 ID / 类型编号 | `EventTypeID.Get(typeof(T))`（自动生成，不手写） |
| 线程安全事件 / 跨线程事件 | `Fire`（下帧主线程分发，线程安全） |
| 立即分发 / 同帧分发 | `FireNow`（主线程立即，非线程安全） |

## 三层继承链（铁律）

```
FrameworkManager
  └── EventManagerBase : IEventManager    (internal abstract)
        └── EventManager                  (internal sealed)
```

- `EventComponent` — Unity MonoBehaviour 入口，`Nova.Event` 全局访问
- `EventPool<EventData>` — 内部事件队列，持有订阅表 + 分发逻辑
- `EventData` — 所有业务事件基类，实现 `IReference`，分发后自动 `ReferencePool.Put`
- `EventTypeID` — 按类型自动分配整数 ID，禁止手工写 ID 魔法数

## 文件入口结构

```
Modules/Event/
├── EventComponent.cs / .Visitors.cs
└── Managers/
    ├── Definitions/
    │   ├── EventData.cs               ← 事件基类（IReference）
    │   ├── EventManagerConfig.cs      ← 配置 DTO
    │   └── EventTypeID.cs             ← 类型 → ID 映射
    ├── Implements/
    │   ├── EventManager.cs / EventManagerBase.cs
    │   └── EventPools/
    │       ├── EventPool.cs / EventPool.Event.cs
    │       └── EventPoolMode.cs       ← [Flags] enum
    └── Interfaces/
        └── IEventManager.cs
```

## 关联 ADR

| ADR | 标题 | 一句要点 | status |
|---|---|---|---|
| [[ADR-001-component-manager-three-layer\|ADR-001]] | Component + Manager 三层继承链 | Event 模块遵守三层结构 | accepted |
| [[ADR-008-managerbase-internal-abstract\|ADR-008]] | ManagerBase internal abstract | EventManagerBase 修饰符约束 | accepted |
| [[ADR-010-validation-on-consumer-side\|ADR-010]] | 谁使用谁校验 | Component 不校验，Manager 内校验 | accepted |
| [[ADR-011-load-unload-and-ireference-pairing\|ADR-011]] | Load/Unload 与 IReference 配对 | EventData 实现 IReference，分发后自动 Put | accepted |
| [[ADR-016-framework-vs-business-access\|ADR-016]] | 框架 vs 业务访问 | 业务侧通过 `Nova.Event` 访问 | accepted |

## 关联 PAT

| PAT | 一句要点 |
|---|---|
| [[PAT-27-config-no-serialize\|PAT-27]] | EventManagerConfig 纯 DTO，禁 [Serializable] |
| [[PAT-30-framework-usage-redlines\|PAT-30]] | EventData 分发后自动归还，handler 外禁持有引用 |
| [[PAT-68-pool-reference-spread\|PAT-68]] | 自定义事件数据默认池化，实现 IReference + Clear() |
