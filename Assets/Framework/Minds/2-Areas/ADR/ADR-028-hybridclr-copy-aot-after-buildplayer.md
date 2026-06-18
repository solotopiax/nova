---
id: ADR-028
title: HybridCLR 拷贝 AOT 裁剪 dll 必须在 BuildPlayer 之后
summary: CopyAotDll 必须在 BuildPlayer 之后
category: workflow
status: accepted
date: 2026-05-20
aliases:
  - ADR-028-hybridclr-copy-aot-after-buildplayer
keywords: [ADR-028]
tags: [adr, nova, hybridclr, pipify, build]
supersedes: []
superseded-by: []
related: []
---

# ADR-028：HybridCLR 拷贝 AOT 裁剪 dll 必须在 BuildPlayer 之后

## 背景

Nova Pipify 分两档：完整档出 APK + AB + AOT/热更 dll，更新档只做 CompileDll + CopyGameDll + BuildBundle。需要明确 `CopyAotDll` 与 `BuildPlayer` 的顺序。

## 决策

**铁律**：`CopyAotDll` 必须发生在 `BuildPipeline.BuildPlayer(...)` **之后**。

官方推荐两类流程：

1. **出底包流程（首次 / 改了 AOT 代码）**：
   - `PrebuildCommand.GenerateAll()` →
   - `BuildPipeline.BuildPlayer(...)`（出 APK，IL2CPP 此时才产出裁剪后的 AOT dll）→
   - `BuildAssetBundleByTarget` + `CompileDll` + `CopyABAOTHotUpdateDlls`（含 AOT/热更 dll 拷贝）

2. **仅热更流程（不重出底包）**：
   - `CompileDll` → `CopyHotUpdateAssembliesToStreamingAssets` → `BuildAssetBundle`
   - 不调 `GenerateAll`、不调 `CopyAOTAssembliesToStreamingAssets`、不 `BuildPlayer`
   - AOT 裁剪 dll 沿用上次出底包落地的 bytes，字节级稳定

官方示例也明确说明裁剪后的 AOT dll 只能在 BuildPlayer 时生成。

## 替代方案

- BuildPlayer 之前拷 AOT：会拿到旧 dll。
- 每次更新都重出 APK：浪费。
- 仅热更也调 `GenerateAll`：会引入不必要的脏文件抖动。

## 后果

- 第一档流程要把 `copy_aot_dll` 放在 `unity.build_player` 之后。
- 第二档仅热更时沿用上次出底包生成的 AOT bytes。
- 含变更 dll 的 bundle 每次必新是预期行为。
