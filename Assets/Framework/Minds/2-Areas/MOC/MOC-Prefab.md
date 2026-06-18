---
id: MOC-Prefab
title: Prefab 加载系统图谱
summary: Prefab 实例化、销毁与句柄回收机制速查
category: module
status: active
date: 2026-06-05
aliases:
  - MOC-Prefab
  - Prefab 加载系统图谱
tags: [moc, nova, prefab, asset, runtime]
keywords: [PrefabComponent, IPrefabManager, PrefabInstanceTag, InstantiateSync, InstantiateAsync, Destroy, RecordedInstances]
related:
  - "[[ADR-001-component-manager-three-layer|ADR-001]]"
  - "[[ADR-010-validation-on-consumer-side|ADR-010]]"
  - "[[ADR-011-load-unload-and-ireference-pairing|ADR-011]]"
  - "[[ADR-017-component-manager-isolation|ADR-017]]"
  - "[[ADR-042-assetmanager-load-api-all-return-handle|ADR-042]]"
  - "[[PAT-68-pool-reference-spread|PAT-68]]"
  - "[[MOC-Asset]]"
---

# MOC-Prefab：Prefab 加载系统图谱

## 一句话

Prefab 模块是 Nova 内“实例化 GameObject 资源”的统一入口；它自己只负责实例化与销毁编排，底层资源生命周期仍依附 `Asset` 模块。

## 何时查这页

- 要改 Prefab 实例化、销毁或回收链路
- 要理解为什么不能直接 `Instantiate / Destroy`
- 要判断对象该走 `Prefab` 还是 `ObjectPool`

## 当前结构

```text
Nova.Prefab
  -> PrefabComponent
  -> IPrefabManager
  -> PrefabManagerBase
  -> PrefabManager
```

对外高频入口：

- `InstantiateSync`
- `InstantiateAsync`
- `Destroy`
- `RecordedInstanceCount / RecordedInstances`

## 关键边界

- `Prefab` 负责“实例化出对象”
- `Asset` 负责“底层资源句柄与引用计数”
- `PrefabManager` 通过 `IAssetManager` 申请句柄，不单独发明一套资源生命周期

## 必须记住的机制

每个实例都会挂一个 `PrefabInstanceTag`：

```text
Load Prefab handle
  -> Instantiate
  -> 挂 PrefabInstanceTag
  -> 记录实例与 handle 的对应关系
  -> 任意销毁路径最终回到 OnDestroy
  -> 释放 handle 并清理记录
```

这意味着：

- `PrefabComponent.Destroy()` 只是统一入口
- 就算对象被父节点销毁或走原生销毁链，最终也会通过 `PrefabInstanceTag` 回到统一回收路径
- 不应该手工重复补一遍 release

## 与其他模块的关系

- `Asset`：提供 Prefab 资源加载与 handle
- `ObjectPool`：适合高频复用对象；不是所有 Prefab 都必须走池

## 常见误区

- 直接用 Unity 的 `Instantiate(prefab)` 绕开模块入口
- 直接 `Object.Destroy(go)` 之后再手工补一层资源释放
- 把 `Prefab` 当成资源系统总入口，而不是“实例化层”
- 高频复用对象不评估池化，导致不必要的反复加载和销毁

## 先往哪看

- 改 Load/Release 配对：[[ADR-011-load-unload-and-ireference-pairing]]
- 改资源句柄语义：[[ADR-042-assetmanager-load-api-all-return-handle]]
- 判断是否该走池：[[PAT-68-pool-reference-spread]]

## 关联

- 图谱：[[MOC-Asset]]、[[MOC-Manager]]
