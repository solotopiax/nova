---
id: PAT-68
title: Reference 与 ObjectPool 辐射使用原则
summary: 数据走 ReferencePool 组件走 ObjectPool
category: runtime
type: pattern
status: active
date: 2026-06-05
aliases:
  - PAT-68-pool-reference-spread
keywords:
  - PAT-68
  - Reference 与 ObjectPool 辐射使用原则
  - PAT-68-pool-reference-spread
tags:
  - pattern
  - runtime
  - gc
  - pool
  - reference
related:
  - "[[ADR-011-load-unload-and-ireference-pairing|ADR-011]]"
---

# PAT-68：Reference 与 ObjectPool 辐射使用原则

## 适用场景

- 新增高频临时数据对象
- 新增高频 `GameObject` / Prefab 实例化路径
- 清理 GC 尖刺或实例化抖动

## 选型矩阵

| 对象类型 | 推荐方案 |
|---|---|
| 纯 C# 数据载体、事件体、参数包、临时结果 | `IReference` + `ReferencePool` |
| 可重复使用的 `GameObject` / Prefab 实例 | `ObjectBase` + `IObjectPool<T>` |
| 持有 `UnityEngine.Object` 生命周期的对象 | 不直接当作 `IReference` 处理，按资源或对象池语义管理 |

## 核心规则

### 1. 高频、瞬态、纯数据对象优先池化

当前代码里已经大量使用 `ReferencePool`：

- Event 数据
- HTTP / WebSocket / SDK 事件对象
- UI 打开参数与中间对象
- Asset Handle 适配器
- Sound 参数与播放信息

这说明 ReferencePool 在 Nova 中不是少数点缀，而是基础做法。

### 2. Unity 对象复用优先走 ObjectPool

`ObjectPool` 适合承载：

- Prefab 实例
- 可重复启停的运行时对象
- 明确有 Spawn / Despawn 生命周期的对象

### 3. 谁取谁还，闭环必须完整

- `ReferencePool.Get()` 之后必须有明确的 `Put()`
- 池化对象跨异步边界时要特别注意异常和取消路径
- Put 后对象字段会被清空，后续不应再继续读取

## 为什么这样定

- Unity 里的 GC 和频繁实例化都是真实成本
- Nova 当前多个核心模块已经以“引用池 + 对象池”作为默认优化路径
- 如果新代码在同类场景里重新回到 `new` / `Instantiate`，会破坏全局一致性

## 反模式

- 高频路径继续无理由 `new`
- 池化对象只取不还
- 把持有 Unity 原生生命周期的对象当作普通 `IReference`
- 跨模块返回池化对象却不写清楚所有权

## 关联

- [[ADR-011-load-unload-and-ireference-pairing|ADR-011]]
