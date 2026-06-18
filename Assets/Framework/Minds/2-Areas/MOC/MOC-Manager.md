---
id: MOC-Manager
title: Component + Manager 核心图谱
summary: Nova 框架的 Component、Manager、Priority、全局入口速查
category: arch
status: active
date: 2026-06-05
aliases:
  - MOC-Manager
  - 框架核心图谱
tags: [moc, nova, core, manager, runtime]
keywords: [Manager, ManagerBase, FrameworkManager, Component, FrameworkComponent, Priority, TypeCreator, FrameworkManagersGroup, Nova]
related:
  - "[[ADR-001-component-manager-three-layer|ADR-001]]"
  - "[[ADR-002-manager-priority-system|ADR-002]]"
  - "[[ADR-008-managerbase-internal-abstract|ADR-008]]"
  - "[[ADR-016-framework-vs-business-access|ADR-016]]"
  - "[[ADR-017-component-manager-isolation|ADR-017]]"
  - "[[GLO-02-framework-manager-tiers|GLO-02]]"
  - "[[GLO-03-component-procedure-manager|GLO-03]]"
---

# MOC-Manager：框架核心图谱

## 一句话

Nova 的主骨架是 `Nova 静态入口 -> FrameworkComponent -> FrameworkManager`：Component 负责 Unity 入口，Manager 负责模块逻辑，`FrameworkManagersGroup` 负责统一调度。

## 业务语言入口

| 常见说法 | 在 Nova 中对应什么 |
|---|---|
| 全局入口 | `Nova.Xxx` 静态属性 |
| 模块逻辑 | `XxxManagerBase` / `XxxManager` |
| Unity 侧入口 | `XxxComponent` |
| 启动顺序 | `Priority` + `FrameworkManagersGroup` |
| 实现注入 | `Util.TypeCreator.Create<I...>()` |

## 继承链

```text
FrameworkManager
  -> XxxManagerBase : IXxxManager
  -> XxxManager
```

硬约束：

- 不跳过 `ManagerBase`
- `ManagerBase` 保持 `internal abstract`
- 外部通过接口或 Component 访问，不直接把具体实现暴露成公共契约

```text
FrameworkComponent
  -> XxxComponent
```

硬约束：

- Component 持有 Manager，并负责把 Unity 生命周期接到模块逻辑
- 复杂业务分支不应堆在 Component 内部

## Priority 速查

完整优先级以 `Docs/ARCHITECTURE.md` 和源码为准；这里保留高频心智模型：

- `Persist`、`Debug` 最靠前
- `Event`、`ObjectPool` 先于大多数业务模块
- `Asset` 在中前段
- `Config`、`Network`、`Prefab`、`App` 构成启动链路的核心一段
- `SDK`、`Vibrate`、`Sound` 靠后

## 改动前先想清楚

- 这是 Component 职责，还是 Manager 职责？
- 这是模块内部实现，还是要变成 `Nova.Xxx` 公共能力？
- 这个新逻辑会不会改变 Priority、生命周期或跨模块依赖方向？

## 反模式

- `new XxxManager()` 直接创建管理器
- 让 Component 写复杂调度逻辑
- 让平级模块直接偷用彼此内部实现
- 把具体三方实现类型扩散到模块边界外
