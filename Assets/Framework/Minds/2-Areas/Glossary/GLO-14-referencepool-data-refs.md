---
id: GLO-14
title: ReferencePool 纯数据引用池
type: glossary
status: active
date: 2026-08-05
summary: ReferencePool 池化纯数据对象
category: runtime
source: docs-and-source-verification
aliases:
  - GLO-14-referencepool-data-refs
  - ReferencePool
  - IReference
keywords: [GLO-14, ReferencePool, IReference, IReferenceHelper, YooAssetHandleAdapter, 引用池, GC]
tags: [glossary, nova, terminology, referencepool, runtime, gc]
related:
  - "[[PAT-68-pool-reference-spread|PAT-68]]"
  - "[[MOC-ObjectPool|MOC-ObjectPool]]"
  - "[[GLO-13-objectpool-reusable-objects|GLO-13]]"
---

# GLO-14：ReferencePool 纯数据引用池

## 定义

ReferencePool 是 Nova 的静态引用池（`Assets/Framework/Scripts/Runtime/Core/Reference/ReferencePool.cs`），用于复用**纯 C# 临时数据对象**以降低重复分配与 GC：实现 `IReference` 的类经 `ReferencePool.Get<T>()` 获取、`Put(IReference)` 归还；池为空时仍会创建新实例。

## 边界

- 静态类、按 Type 分桶；`SetHelper` 允许框架层注入统一回收钩子。
- `Add<T>(count)` 预热、`Remove<T>(count)` 缩容用于容量调节，不是业务 API。
- 典型 Nova 用法是适配器类：YooAsset 句柄适配器（`YooAssetHandleAdapter` 等）以 ReferencePool 承载，把第三方 handle 纳入统一取还口径。

## 易混淆项

- ReferencePool 不是 ObjectPool（GLO-13）：无生命周期状态、无 GameObject 语义，只服务短生命周期数据对象。
- `IReference.Clear()` 必须完整复位全部字段；调用方不需要手动调用，`ReferencePool.Put` 会在入池前自动执行 `Clear()`，实现不完整才会造成下一次 `Get` 串数据。
- 不要把 GameObject、带 Unity 对象引用的长生命周期对象塞进 ReferencePool（扩散红线见 PAT-68）。

## 示例

```csharp
var adapter = ReferencePool.Get<YooAssetHandleAdapter>();
// ... 使用 ...
ReferencePool.Put(adapter);
```

## 来源与验证

- `Assets/Framework/Scripts/Runtime/Core/Reference/ReferencePool.cs`：`Get / Put / Add / Remove / SetHelper` 静态 API。
- `IReference` 接口的 `Clear()` 契约（同目录定义）。
- `Assets/Framework/Docs/INDEX.md`：`YooAssetHandleAdapter` 为 ReferencePool 适配器的说明。
