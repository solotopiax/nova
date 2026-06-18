---
id: MOC-ObjectPool
title: 对象池系统图谱
summary: ObjectPool 与 ReferencePool 的分工和入口速查
category: runtime
status: active
date: 2026-06-05
aliases:
  - MOC-ObjectPool
  - 对象池系统图谱
  - 池化系统图谱
tags: [moc, nova, objectpool, referencepool, runtime, gc]
keywords: [ObjectPoolComponent, IObjectPoolManager, ObjectPoolBase, IObjectPool, ObjectBase, ReferencePool, IReference]
related:
  - "[[ADR-001-component-manager-three-layer|ADR-001]]"
  - "[[ADR-008-managerbase-internal-abstract|ADR-008]]"
  - "[[ADR-010-validation-on-consumer-side|ADR-010]]"
  - "[[ADR-042-assetmanager-load-api-all-return-handle|ADR-042]]"
  - "[[ADR-016-framework-vs-business-access|ADR-016]]"
  - "[[PAT-30-framework-usage-redlines|PAT-30]]"
  - "[[PAT-68-pool-reference-spread|PAT-68]]"
---

# MOC-ObjectPool：对象池系统图谱

## 一句话

Nova 的池化分两层：`ObjectPool` 负责可复用业务对象，`ReferencePool` 负责纯数据引用对象；两者有关联，但不是同一个系统。

## 何时查这页

- 要决定对象该走 `ObjectPool` 还是 `ReferencePool`
- 要改对象池创建、释放、自动回收策略
- 要理解 `ObjectBase / IObjectPool / ReferencePool` 的职责边界

## 当前结构

```text
Nova.ObjectPool
  -> ObjectPoolComponent
  -> IObjectPoolManager
  -> ObjectPoolManagerBase
  -> ObjectPoolManager

池对象：
ObjectPoolBase
IObjectPool<T>
ObjectBase

旁路静态池：
ReferencePool
IReference
```

## 两套池化怎么选

- 纯 C# 临时数据对象：`ReferencePool`
- 带生命周期的可复用业务对象：`ObjectPool`
- GameObject / Prefab 实例：优先看 `Prefab / ObjectPool` 组合，不直接塞进 `ReferencePool`
- `ObjectBase.Target` 当前是 `object`，所以 `ObjectPool` 不只服务于 `GameObject`
- `ReferencePool` 才是纯 `IReference` 静态池

## 当前高频入口

- `GetObjectPool<T>()`
- `CreateSingleGettingObjectPool<T>()`
- `CreateMultiGettingObjectPool<T>()`
- `GetAllObjectPools(...)`
- `Release()`
- `ReleaseAllUnused()`

## 当前边界

- 面向 `ObjectBase`
- 管理池实例、`Get / Put`、自动释放
- 公开 `ObjectPoolBase / IObjectPool<T>` 这套对象池抽象
- `ReferencePool` 面向 `IReference`，适合高频、短生命周期、纯数据对象
- `ReferencePool` 常作为其他模块的内部支撑，而不是“带复杂状态的业务池”

## 导航提醒

- `ObjectPool` 支持 single-get 和 multi-get 两种模式
- 池对象释放不仅看是否空闲，还看 `Locked / CustomCanReleaseFlag / Priority / ExpireTime`
- `ReferencePool` 也被用作包装对象和数据对象的回收基础设施
- 具体优先级与释放实现，以 `Docs` 和源码为准

## 常见误区

- 把所有复用对象都塞进 `ReferencePool`
- 直接自己维护一套容器而绕开 `Nova.ObjectPool`
- 不区分 single-get 与 multi-get 的语义
- 只看“取和还”，忽略释放策略、锁定标记和自动回收条件

## 先往哪看

- 改结构：[[ADR-001-component-manager-three-layer]]
- 改访问边界：[[ADR-016-framework-vs-business-access]]
- 改资源句柄语义：[[ADR-042-assetmanager-load-api-all-return-handle]]

## 关联

- 图谱：[[MOC-Prefab]]、[[MOC-Manager]]
