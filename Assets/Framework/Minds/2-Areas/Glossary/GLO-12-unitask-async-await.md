---
id: GLO-12
title: UniTask 异步基础设施
type: glossary
status: active
date: 2026-08-05
summary: UniTask 是零分配异步库
category: external
source: docs-and-source-verification
aliases:
  - GLO-12-unitask-async-await
  - UniTask
  - unitask
keywords: [GLO-12, UniTask, Cysharp, async, await, UniTask<T>, Preserve, CancellationToken]
tags: [glossary, nova, terminology, unitask, async, upm]
related:
  - "[[PAT-146-unitask-cached-completion-for-retry|PAT-146]]"
  - "[[PAT-152-runtime-initialize-unitask-cross-stage|PAT-152]]"
---

# GLO-12：UniTask 异步基础设施

## 定义

UniTask（Cysharp）是 Nova 的零分配 async/await 异步库，以本地 UPM 包 `com.solotopia.unitask`（`Packages/manifest.json` 中 `file:../UPMPackages/com.solotopia.unitask`）引入。Framework Runtime 的异步 API 统一返回 `UniTask` / `UniTask<T>`，例如 `IAssetManager.LoadAsync<T>`、`BootstrapAsync`、网络层 DoH/DNS 查询等。

## 边界

- UniTask 只解决“异步等待与取消”，不替代 Fsm 的流程编排（见 GLO-15）：启动阶段推进仍由 Procedure 状态机决定。
- 包版本一致性由 `Assets/Tests/Editor/UniTaskPackageConsistencyTests.cs` 守护；UniTask 本体 DLL 也出现在热更 AOT metadata / DLL 打包清单中。
- 取消统一走 `CancellationToken` 参数，不发明私有取消协议。

## 易混淆项

- 已完成的 UniTask 不能被重复消费；启动重试等场景必须缓存完成结果或使用独立共享完成源（PAT-146）。
- `Preserve()` 只承诺“完成后可重复等待结果”，不等于把任务变成可多次启动。
- UniTask 不是协程（IEnumerator）：不要在 Framework 新代码里混用协程风格。

## 示例

```csharp
// Framework 异步 API 口径：返回 UniTask，支持取消。
UniTask<IAssetHandle<T>> LoadAsync<T>(string location, CancellationToken ct = default);
```

## 来源与验证

- `Packages/manifest.json`：`com.solotopia.unitask` 本地 UPM 包引入。
- `Assets/Framework/Scripts/Runtime/Modules/Asset/Managers/AssetManager/Interfaces/IAssetManager.cs`：UniTask 返回签名。
- `Assets/Framework/Scripts/Runtime/Modules/Network/NetworkComponent.DoH.cs`：`UniTask DNSQuery(...)` 等调用点。
- PAT-146：完成结果缓存与重试语义。
