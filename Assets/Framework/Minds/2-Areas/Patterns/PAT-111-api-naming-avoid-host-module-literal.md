---
id: PAT-111
title: 公开 API 命名避开宿主模块字面词重复
type: pattern
status: active
summary: API 名去模块字，类型名留模块字
category: naming
keywords:
  - api-naming
  - module-literal
  - host-module
  - load-asset
  - load-subs
tags:
  - nova
  - naming
  - api-design
date: 2026-05-26
aliases:
  - PAT-111-api-naming-avoid-host-module-literal
related:
  - "[[PAT-37-no-yooasset-outside-asset-module|PAT-37]]"
---

# PAT-111：公开 API 命名避开宿主模块字面词重复

## 适用场景

某模块公开 API 命名时，方法名与宿主模块字面词存在重复风险。

## 核心做法（动名分离铁律）

| 命名要素 | 字面词策略 | 例子 |
|---|---|---|
| **API 方法名（动作）** | **去掉**宿主模块字面词 | `LoadSync` / `LoadSubsAsync` / `LoadAllAsync` / `LoadRawSync` |
| **接口类型名（状语）** | **保留**宿主模块字面词 | `IAssetHandle<T>` / `ISubAssetsHandle<T>` / `IAllAssetsHandle<T>` / `IRawFileHandle` |
| **参数类型名（属性）** | **保留**宿主模块字面词 | `assetLocation` / `assetType` |

完整示例（AssetComponent 公开签名）：

```csharp
// ✅ 动名分离：方法名去 Asset，类型名留 Asset
public IAssetHandle<T> LoadSync<T>(string assetLocation);
public UniTask<IAssetHandle<T>> LoadAsync<T>(string assetLocation, CancellationToken ct = default);
public ISubAssetsHandle<T> LoadSubsSync<T>(string assetLocation);
public IAllAssetsHandle<T> LoadAllSync<T>(string assetLocation);
public IRawFileHandle LoadRawSync(string assetLocation);
public ISceneHandle LoadSceneSync(string assetLocation);
```

## 为什么这样设计

1. 调用上下文已经明示主体，方法名再重复模块字面词属于噪声。
2. 类型名跨模块流通时仍需要模块字面词锚定语义。
3. 参数名保留模块字面词，是为了让地址概念一眼可见。

## 反模式

- ❌ `AssetComponent.LoadAssetAsync<T>` —— `Asset.LoadAsset` 字面词二次重复
- ❌ `AssetComponent.LoadSubAssetsAsync<T>` —— 同上 + `SubAssets` 复数语义割裂
- ❌ 反向把模块字面词从类型名也拿掉：`ISubsHandle<T>`、`IRawHandle` —— 类型独立流通时丢失语义锚
- ❌ 给方法名加前缀 `As` / `From` 兜模块字面词（`LoadAsAsset`）—— 反而更绕

## 跨模块复用清单

| 宿主模块 | API 方法名前缀 | 接口类型名 |
|---|---|---|
| Asset | `Load*` / `LoadSubs*` / `LoadAll*` / `LoadRaw*` / `LoadScene*` | `IAssetHandle` / `ISubAssetsHandle` / `IAllAssetsHandle` / `IRawFileHandle` / `ISceneHandle` |
| Sound | `Play` / `Stop` / `Pause` / `Resume` | `ISoundAgentHandle` |
| Network | `Send` / `SendAsync` | `INetMessageHandle` |
| UI | `Open` / `Close` | `IUIFormHandle` |
