---
id: ADR-052
title: 下载缓存两层清理分工，内存引用计数与磁盘沙盒分离
summary: 内存引用计数与磁盘旧资源文件分两层独立清理
category: asset
status: accepted
date: 2026-05-30
aliases:
  - ADR-052-asset-cache-two-layer-cleanup
keywords:
  - ADR-052
  - 下载缓存两层清理分工，内存引用计数与磁盘沙盒分离
  - ADR-052-asset-cache-two-layer-cleanup
tags: [adr, nova, asset, hotfix]
supersedes: []
superseded-by: []
related:
  - "[[ADR-011-load-unload-and-ireference-pairing|ADR-011]]"
  - "[[ADR-042-assetmanager-load-api-all-return-handle|ADR-042]]"
  - "[[MOC-Asset]]"
---

# ADR-052：下载缓存两层清理分工，内存引用计数与磁盘沙盒分离

## 背景（Context）

策略 B（启动切片 + 运行时增量）下，某 tag 资源更新后，**旧版本的 bundle 会变成"无主缓存"堆在 persistentDataPath 沙盒里**——它们不在当前 manifest 中，既不会被加载也不会自动删除，长期运行会持续占用磁盘。

现状 `IAssetManager.CleanupAsync`（`AssetManager.Cleanup.cs`）只做 YooAsset `UnloadUnusedAssetsAsync`，即**内存引用计数级**回收（销毁 RefCount=0 的 Provider/BundleLoader），完全不触碰磁盘缓存文件。磁盘上的无主旧 bundle 没有任何清理入口。

容易把这两件事混为一谈：「版本检查会不会漏掉某 tag」是**寻址正确性**问题（已由 manifest hash 比对保证），「旧 bundle 占磁盘」是**存储清理**问题——两码事，需分层独立处理。

## 决策（Decision）

### 1. 新增磁盘级清理 API

`IAssetManager` 新增 `ClearUnusedCacheAsync(string package = null, CancellationToken ct = default)`，落在 `AssetManager.Cleanup.cs`，底层调 YooAsset `pkg.ClearCacheFilesAsync(EFileClearMode.ClearUnusedBundleFiles)`。

### 2. 两层清理职责严格分离

| 方法 | 层级 | 清什么 | 何时调 |
|---|---|---|---|
| `CleanupAsync`（已有） | 内存引用计数 | RefCount=0 的 Provider/BundleLoader | 场景卸载后回收内存 |
| `ClearUnusedCacheAsync`（新增） | 磁盘沙盒 | 不在当前 manifest 的旧版本 bundle 文件 | 热更完成后清磁盘 |

### 3. 自动触发：API + 可选开关（不内置强制）

`AssetComponent` 新增 `[SerializeField] private bool m_AutoClearUnusedCacheOnHotfix`（默认 `false`）。`ProcedureHotfix` 下载成功后，若开关开启则自动调一次 `ClearUnusedCacheAsync`。默认关闭——避免框架替业务决定清理时机（业务可能想保留旧版本做快速回滚）。

## 后果（Consequences）

### 正面
- 磁盘无主 bundle 有了明确清理入口，长期运行不堆积
- 两层职责清晰，不会误把内存回收当磁盘清理
- 默认关闭 + 可选开关，框架不强制清理时机，业务自决

### 负面
- 业务需理解"内存回收"与"磁盘清理"是两个独立动作，认知成本上升
- 开启自动清理后无法快速回滚到旧版本（旧 bundle 已删）

## 被排除的方案（Alternatives）

| 方案 | 否决理由 |
|---|---|
| 复用 CleanupAsync 同时清内存+磁盘 | 两层语义不同，合并会让"我只想回收内存别动磁盘"的场景无法表达 |
| 框架内置启动期强制清理 | 剥夺业务保留旧版本回滚的能力；清理时机应由业务决定 |
| 完全不提供磁盘清理（依赖 OS） | 移动端沙盒不会被 OS 自动清，磁盘会无限增长 |

## 验证依据（Verification）
- 源码：`AssetManager.Cleanup.cs`（新增 ClearUnusedCacheAsync 与现有 CleanupAsync 并列）、`AssetComponent.Visitors.cs`（m_AutoClearUnusedCacheOnHotfix）、`ProcedureHotfix.cs`（成功后按开关自动清）
- grep 关键词：`ClearUnusedCacheAsync` / `ClearCacheFilesAsync` / `AutoClearUnusedCacheOnHotfix`
- 审查要点：两方法职责不得交叉；默认开关 false

## 关联
- 上层原则：[[ADR-011-load-unload-and-ireference-pairing|ADR-011]]（Load/Unload 配对，本 ADR 是磁盘层的补充）
- 相关 ADR：[[ADR-042-assetmanager-load-api-all-return-handle|ADR-042]]（Handle 体系，内存回收依赖 Release）
- MOC：[[MOC-Asset]]

---
