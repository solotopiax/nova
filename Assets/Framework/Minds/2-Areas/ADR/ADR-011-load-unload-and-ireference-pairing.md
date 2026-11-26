---
id: ADR-011
title: Load/Unload 与 IReference Get/Put 配对收口
status: accepted
date: 2026-05-15
summary: Load与Unload及IReference必须配对
category: core
aliases:
  - ADR-011-load-unload-and-ireference-pairing
keywords: [ADR-011, Load/Unload 与 IReference Get/Put 配对收口]
tags: [adr, nova, lifecycle, resource-management]
supersedes: []
superseded-by: []
related:
  - "[[ADR-001-component-manager-three-layer|ADR-001]]"
  - "[[ADR-042-assetmanager-load-api-all-return-handle|ADR-042]]"
---

# ADR-011：Load/Unload 与 IReference Get/Put 配对收口

## 背景

团队曾遇到跨场景资源句柄积累和短生命周期引用对象漏还。二者都会在长时间运行或高负载下放大，分别造成原生资源内存占用和额外分配。把收尾责任落实到拥有者，比等问题在真机出现再倒查可靠。

## 决策

| 场景 | 配对方式 |
|---|---|
| Nova 资源加载 | `IAssetManager.LoadAsync<T>` 和 `LoadSync<T>` 返回 `IAssetHandle<T>`。持有者在消费结束后释放本次句柄 |
| Nova 场景加载 | 持有 `ISceneHandle` 至场景生命周期结束，再调用 `UnloadAsync()` |
| 引用池直接使用 | `ReferencePool.Get<T>()` 获得的实例在使用结束后归还 `ReferencePool.Put` |
| 引用交给其他模块 | 沿接口确认归还责任、归还时机和异常边界。事件的具体归还行为见 [EventManager](../../../Docs/Runtime/Modules/Event/EventManager.md#5-eventdata-在分发后会被归还引用池) |

配对可以跨越不同方法。复核时沿同一个句柄或引用实例确认所有权，覆盖正常结束、失败、取消和拥有者退出。长期持有的对象也按所属模块的生命周期确认结束条件。

## 后果

显式持有资源句柄让同一地址的多次加载各自有释放依据。引用池复用则依赖归还实际取得的对象。生命周期过短会提前释放，缺少结束路径则会累积持有。

## 被排除的方案

| 方案 | 原因 |
|---|---|
| 依靠 C# GC 作为资源句柄的释放时点 | 原生资源的释放由框架持有的句柄控制 |
| 只比较关键词调用次数 | 调用次数无法体现转交所有权、失败或跨帧退出 |
| 用统一销毁动作替代归还 | 资源句柄、场景和引用池对象各有收尾接口 |

## 验证依据

- [资源句柄](../../../Docs/Runtime/Modules/Asset/AssetManager/Interfaces/IAssetHandle.md)和[场景句柄](../../../Docs/Runtime/Modules/Asset/AssetManager/Interfaces/ISceneHandle.md)给出现行接口。
- [ReferencePool](../../../Docs/Runtime/Core/Reference/ReferencePool.md)给出获取、归还和重置边界。
- [[ADR-042-assetmanager-load-api-all-return-handle|ADR-042]] 记录从地址反查释放到句柄所有权的迁移。
- [[PAT-28-luban-load-release-symmetric|PAT-28]] 说明表资源加载时如何追踪本次句柄的释放位置。
