---
id: PAT-51
title: SBP BuildCache 与 LockReloadAssemblies 不兼容
summary: Lock 期 hash 不刷致 bundle 命中陈旧
category: editor
type: pattern
status: active
date: 2026-05-20
aliases:
  - PAT-51-sbp-buildcache-vs-lock-reload-incompat
keywords:
  - PAT-51
tags:
  - pattern
  - yooasset
  - hybridclr
  - sbp
  - editor
related:
  - "[[ADR-026-pipify-runner-no-batch-locking|ADR-026]]"
  - "[[PAT-48-editor-refcount-api-leak-drain|PAT-48]]"
---

# PAT-51：SBP BuildCache 与 LockReloadAssemblies 不兼容

## 适用场景（When）

设计任何"先写 .bytes / .json / .asset，再调 SBP `bundlebuilder.build` / `BundleBuilder.Run`"的流水线时。

## 核心做法（What & How）

- **不要把 SBP 调用包在 `LockReloadAssemblies` 里**，即使是为了"防 Domain Reload 冲日志"
- 单文件写盘后立即同步 import：
  ```csharp
  File.Copy(srcDll, dstBytes, overwrite: true);
  AssetDatabase.ImportAsset(dstAssetRelative, ImportAssetOptions.ForceSynchronousImport);
  ```
- 让 SBP 自己读 AssetDatabase，命中真实 contentHash，cache miss 命中正常发生
- 若担心 Console 日志被 "Clear on Recompile" 清空：**故意不主动调 `AssetDatabase.Refresh()`**，让 Unity AutoRefresh 在窗口失焦时自动扫，用户先看完日志再切焦点触发编译

## 为什么这么做（Why）

SBP（ScriptableBuildPipeline）`BuildCache` 用 `AssetDatabase.GetAssetDependencyHash` 当 cache key。`LockReloadAssemblies` 期间：
- `ImportAsset` 仍能跑（不像 `StartAssetEditing` 会排队），但 contentHash 计算路径中的某些状态不刷新
- 表象：SBP 看到的还是上一轮 .bytes 的 hash → cache 命中陈旧 entry → 复用上一轮 bundle 产物
- 直接后果：bundle 内嵌的 dll 永远落后一轮，第一次跑批没用，第二次才生效

实测复现路径：
- PipifyWindow → "更新bundle" → 运行 → bundle 内嵌 `Game.Runtime.dll` 没变化
- 移除 Lock/Unlock → bundle 内嵌 dll 当批就是最新，UnityPy 在 UTF-16 偏移 47132 命中 `PROBE_TAG_CCC003` ✅

## 反模式（Anti-patterns）

- ❌ "为了防 Domain Reload 冲掉日志而 Lock，反正 ImportAsset 还能跑"——错，会触发 SBP cache 陈旧
- ❌ "在 SBP 调用前显式调一次 `AssetDatabase.Refresh()`"——Lock 期间 Refresh 也救不了
- ❌ "Lock 全程 + 在每次 Import 前后手动 Unlock/Lock"——计数 ping-pong 极易泄漏（参见 `[[PAT-48-editor-refcount-api-leak-drain|PAT-48]]`）

## 跨项目复用提示

通用于任何使用 Unity ScriptableBuildPipeline + 自研流水线的项目。HybridCLR + YooAsset 组合是高发场景，但凡走 SBP `BundleBuilder.BuildAssetBundles_Internal` / `ContentPipeline.BuildAssetBundles` 都适用。

