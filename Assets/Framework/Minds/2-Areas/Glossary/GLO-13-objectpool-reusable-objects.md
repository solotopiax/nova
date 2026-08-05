---
id: GLO-13
title: ObjectPool 可复用对象池
type: glossary
status: active
date: 2026-08-05
summary: ObjectPool 池化带生命周期对象
category: runtime
source: docs-and-source-verification
aliases:
  - GLO-13-objectpool-reusable-objects
  - ObjectPool
  - IObjectPool
  - ObjectBase
keywords: [GLO-13, ObjectPool, ObjectPoolComponent, ObjectPoolManager, IObjectPool, ObjectBase, GameObject, 对象池]
tags: [glossary, nova, terminology, objectpool, runtime, gc]
related:
  - "[[ADR-001-component-manager-three-layer|ADR-001]]"
  - "[[PAT-68-pool-reference-spread|PAT-68]]"
  - "[[MOC-ObjectPool|MOC-ObjectPool]]"
  - "[[GLO-14-referencepool-data-refs|GLO-14]]"
---

# GLO-13：ObjectPool 可复用对象池

## 定义

ObjectPool 是 Nova 中池化**带生命周期的可复用业务对象**的系统：`ObjectPoolComponent -> ObjectPoolManager`，池内对象继承 `ObjectBase`，经 `IObjectPool<T>` 以 `Get() / Put()` 取还，支持容量、过期时间、自动回收间隔与优先级。

## 边界

- 创建入口只有两个：`CreateSingleGettingObjectPool<T>`（不允许同时多次获取）与 `CreateMultiGettingObjectPool<T>`（允许同对象被多处持有）。
- `ObjectBase.Target` 是 `object`，ObjectPool 不只服务 GameObject/Prefab；Prefab 实例池化通常走 Prefab + ObjectPool 组合，不手动塞池。
- 归还语义是 `Put`；`ReleaseObject / Release(filter)` 是主动销毁池内对象，不是归还。

## 易混淆项

- ObjectPool ≠ ReferencePool：临时纯数据对象用 ReferencePool（GLO-14），带生命周期对象才用 ObjectPool；两者的扩散红线见 PAT-68。
- `Get()` 得到的 `ObjectBase` 必须配对 `Put`，漏还会导致对象泄漏；不要跨池 Put。
- ObjectPool 是 Manager 层组件（ADR-001 三层结构），不是全局静态类；与静态的 ReferencePool 形成对照。

## 示例

```csharp
IObjectPool<MyObjectBase> pool = objectPoolManager.CreateMultiGettingObjectPool<MyObjectBase>(config);
MyObjectBase obj = pool.Get();
try { /* 使用 obj.Target */ }
finally { pool.Put(obj); }
```

## 来源与验证

- `Assets/Framework/Scripts/Runtime/Modules/ObjectPool/Managers/Definitions/IObjectPool.cs`：`Register / CanGet / Get / Put / Release` 接口与池参数。
- `Assets/Framework/Scripts/Runtime/Modules/ObjectPool/Managers/Interfaces/`：`CreateSingleGettingObjectPool / CreateMultiGettingObjectPool` 两个创建入口。
- MOC-ObjectPool：两层池化职责图谱。
