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
---

# ADR-011：Load/Unload 与 IReference Get/Put 配对收口

## 背景（Context）

历史上踩过两类**可预见但极难排查**的坑：

1. **内存泄漏**：`LoadAsync` / `Resources.Load` / `AssetBundle.LoadFromFile` 取出资源后，业务路径未走 `Unload` / `Release`，资源句柄持续累积；尤其是 UI/Prefab/AB 这种跨场景的资源，Editor 下不易察觉，真机长时间运行后才暴露
2. **频繁 IO 触发的 GC 问题**：`ReferencePool.Get<T>()` 出来的 `IReference` 实例只 Get 不 Put，池退化成无界 `new`，对象分配/回收压力转嫁给 GC，造成卡顿尖刺

这两类问题的共同特征是 —— **风险可预见**（生命周期一旦不闭环必然出事），但**复现链路长**（泄漏要跑很久才显现，GC 尖刺只在特定负载下出现），事后排查成本极高。Nova 已经在 `AssetLoadManager` / `ABLoadManager` / `PrefabLoadManager` / `UIManager` / `ObjectPool` / `Hotfix` 等多模块大量使用这两类设施，没有强制配对约束时，加新模块或重构旧模块极易遗漏，code-reviewer 静态检查也缺乏明确依据。

## 决策（Decision）

框架中已经在使用的 Load 操作必须和 Unload 配对，已经在用的 IReference 必须有 Put 和 Get 收口，**单边操作即视为缺陷**。

**适用范围（Load/Unload）：**

- 所有已使用的资源加载入口，包括但不限于：
  - `Resources.Load` / `Resources.LoadAsync` → `Resources.UnloadAsset`
  - `AssetBundle.LoadFromFile*` / `AssetBundle.LoadAsset*` → `AssetBundle.Unload`
  - 自定义 `IAssetLoadManager.LoadAsync` / `LoadSync` → 对应 `Release` / `Unload` 路径
  - `IABLoadManager.Load*` → 对应释放路径
  - `IPrefabLoadManager.Load*` → 对应回收路径（参考 ADR：PrefabInstanceTag 单路径回收）
- 每条 Load 路径必须存在**至少一条**与之配对的 Unload/Release/Destroy 路径，且业务可达

**适用范围（IReference）：**

- 所有 `: IReference` 类型，从 `ReferencePool.Get<T>` 获取后必须有对应 `ReferencePool.Put` 归还
- 一次性短生命周期使用不豁免（finally 兜底归还）

**不适用：**

- 单例 / 全局长生命周期对象（如 Manager 本身）
- 框架启动期一次性加载、整个进程持有的资源（如 ConfigRuntimeSO）

## 被排除的方案（Alternatives）

| 方案 | 否决理由 |
|---|---|
| **依赖 GC 回收**（不主动 Unload） | Unity 的 `Object.Destroy` 不释放底层资源句柄；AssetBundle/Texture/Mesh 的内存由原生层管理，不归 .NET GC 管 |
| **using / IDisposable 模式** | 跨帧异步加载场景多，using 作用域无法覆盖；且 `IReference` 走对象池语义，不适合 Dispose 即销毁 |
| **静态分析强制配对**（如 Roslyn Analyzer） | 长期目标可做，但短期成本高且无法覆盖运行时分支决定的释放路径；ADR 规则 + code-reviewer 人审更现实 |

## 后果（Consequences）

### 正面

- 内存加载/释放路径形成闭环，杜绝句柄泄漏
- 对象池真正起到对象复用作用，不退化为无界 new
- code-reviewer 有明确审查依据，可一票否决"只 Get 不 Put / 只 Load 不 Unload"的 PR

### 负面

- 业务侧需要显式管理释放时机，新增 try/finally 或 OnDestroy 钩子代码量
- 异步路径（`UniTask` 取消、异常退出）需要额外考虑兜底释放，认知负担上升
- 跨场景共享资源的引用计数语义需要业务侧理解（Get/Put 不等于一次释放）

> 上述代价与"内存泄漏 + GC 卡顿事后排查"的成本相比，明显划算 —— 这正是把规则前置为红线的核心理由。

## 验证依据（Verification）

**grep 关键词清单（code-reviewer 必扫）：**

```bash
# IReference 配对扫查
grep -rn "ReferencePool\.Get<" Assets/Framework/Scripts/Runtime --include="*.cs"
grep -rn "ReferencePool\.Put"   Assets/Framework/Scripts/Runtime --include="*.cs"
# 每个 Get<T> 出现的文件必须能在同模块找到对应 Put

# 资源加载配对扫查
grep -rn "LoadAsync\|LoadSync\|Resources\.Load\|AssetBundle\.Load" Assets/Framework/Scripts/Runtime --include="*.cs"
grep -rn "Unload\|Release" Assets/Framework/Scripts/Runtime --include="*.cs"
```

**已存在使用点（基于 2026-05-15 扫查）：**

`IReference` 实现类（必须个个核对 Put 路径）：

- `Core/Reference/ReferenceHelper`
- `Modules/UI/Managers/UIManager/Implements/UIManager.OpenUIViewInfo`
- `Modules/UI/Managers/UIManager/Implements/UIManager.UIGroup.UIViewInfo`
- `Modules/Asset/Managers/AssetLoadManager/Definitions/PreloadAssetObject`
- `Modules/Asset/Managers/AssetLoadManager/Definitions/AssetObject`
- `Modules/Asset/Managers/ABLoadManager/Definitions/ABObject`
- `Modules/Asset/Managers/PrefabLoadManager/Definitions/PrefabObject`
- `Modules/ObjectPool/Managers/Implements/ObjectPools/Object<T>`

**审查要点：**

- 新增 Load 入口 PR：必须同 PR 给出 Unload 路径，否则 REJECT
- 新增 `: IReference` 类型 PR：必须证明 Put 调用链可达，否则 REJECT
- 重构涉及生命周期的 PR：要求 reviewer 单独画 Get/Put、Load/Unload 配对图

## 关联

- 相关 ADR：[[ADR-001-component-manager-three-layer|ADR-001]]（Manager 持有资源句柄的所有权）
- 相关 ADR：[[ADR-042-assetmanager-load-api-all-return-handle|ADR-042]]（本 ADR 原则在 Handle 接口层面的系统性落地——LoadXxx 全部返回 Handle，调用方持有并主动 Release）
- 规范落点：统一工程约束中的资源加载反模式零容忍条目
- 相关 Pattern：对象池语义、引用计数（待补 Pattern 文档）
