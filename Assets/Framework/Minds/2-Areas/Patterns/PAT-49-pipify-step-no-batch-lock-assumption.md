---
id: PAT-49
title: Pipify Step 严禁假设 Runner 持有批锁
summary: Step 内严禁做批锁豁免脚手架
category: editor
type: pattern
status: active
date: 2026-05-20
aliases:
  - PAT-49-pipify-step-no-batch-lock-assumption
keywords:
  - PAT-49
  - Pipify Step 严禁假设 Runner 持有批锁
tags:
  - pattern
  - pipify
  - editor
  - hybridclr
related:
  - "[[ADR-026-pipify-runner-no-batch-locking|ADR-026]]"
  - "[[PAT-48-editor-refcount-api-leak-drain|PAT-48]]"
---

# PAT-49：Pipify Step 严禁假设 Runner 持有批锁

## 适用场景（When）

写 / 改 / 评审 `EditorUtil/Pipify/Steps/PipifySteps.*.cs` 内的任何 Step 时。

## 核心做法（What & How）

- **Step 必须自洽**：进入时不依赖任何"批锁状态"，退出时不留下任何引用计数变化
- **禁止"豁免"模式**：以下结构是真凶模板，零容忍：
  ```csharp
  // ❌ 反模式：假设 Runner 持有 Start/Lock，本 Step 临时释放
  AssetDatabase.StopAssetEditing();
  EditorApplication.UnlockReloadAssemblies();
  try
  {
      DoWork();
  }
  finally
  {
      EditorApplication.LockReloadAssemblies();   // 净 +1
      AssetDatabase.StartAssetEditing();          // 净 +1
  }
  ```
  Runner 不锁的话，前置 Stop/Unlock 是 no-op（或把已 0 的计数拍下负），finally 的 Lock/Start 每次跑都净增 1 → **跑一次全量批泄漏一次** → 修 cs 不编译。
- **正确写法**：直接调用业务 API，让 Runner 裸跑机制自然生效
  ```csharp
  // ✅
  [PipifyStep("edm4u.android_resolve", "解析 Android 依赖", "EDM4U")]
  internal static UniTask RunResolveAndroidDependencies(PipifyContext ctx)
  {
      EditorUtil.AndroidResolver.Resolve();
      return UniTask.CompletedTask;
  }
  ```
- **若 Step 内部确需 importer 立刻生效**（如 `File.Copy(.bytes)` 之后）：在 Step 内同步调 `AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport)`，单文件即时刷，无需任何批锁

## 为什么这么做（Why）

`[[ADR-026-pipify-runner-no-batch-locking|ADR-026]]` 已确立 Runner 全程裸跑。Step 若仍按"老 Runner 持锁"假设写代码，等于在 Step 边界手工泄漏引用计数，比泄漏点本身更隐蔽——因为代码"看起来像配对的 try/finally"。

## 反模式（Anti-patterns）

- ❌ Step XML 注释写"临时豁免 Runner 的批锁定"——Runner 已经不锁了
- ❌ Step 内部任何对 `LockReloadAssemblies` / `StartAssetEditing` 的调用——已被 `csharp-code-style.md §4·1` 全局禁用
- ❌ 多 Step 之间靠"上一个 Step 留下的状态"协作——Step 必须自洽

## 跨项目复用提示

通用于任何 Step / Pipeline / Plugin 框架——只要框架核心走"裸跑 + Step 自治"路线，就要在文档/代码里显式禁止 Step 内置"豁免"模式。

