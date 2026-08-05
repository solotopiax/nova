---
id: GLO-10
title: YooAsset 资源管理层
type: glossary
status: active
date: 2026-08-05
summary: YooAsset 是 Nova 的资源管理层
category: asset
source: docs-and-source-verification
aliases:
  - GLO-10-yooasset-asset-management
  - YooAsset
  - yooasset
keywords: [GLO-10, YooAsset, AssetHandle, IAssetHandle, manifest, ResourceDownloaderOperation, AssetBundle, Addressables]
tags: [glossary, nova, terminology, asset, yooasset, addressables]
related:
  - "[[ADR-025-yooasset-url-template-placeholders|ADR-025]]"
  - "[[ADR-042-assetmanager-load-api-all-return-handle|ADR-042]]"
  - "[[ADR-049-yooasset-settings-via-configmaster|ADR-049]]"
  - "[[ADR-060-yooasset-settings-global-resources-copy|ADR-060]]"
  - "[[ADR-065-asset-manifest-three-tier-offline-fallback|ADR-065]]"
  - "[[MOC-Asset|MOC-Asset]]"
  - "[[RES-003-unity-yooasset-tutorial|RES-003]]"
---

# GLO-10：YooAsset 资源管理层

## 定义

YooAsset 是 Nova 选定的资源管理方案，以本地 UPM 包 `com.solotopia.yooasset`（`Packages/manifest.json` 中 `file:../UPMPackages/com.solotopia.yooasset`）引入，承担 AssetBundle 构建产物（`Assets/StreamingAssets/yooasset/`）的 manifest 寻址、下载、依赖解析与 handle 化加载。

## 边界

- Nova 业务层及 Asset 模块以外的 Framework 消费方不直接调用 YooAsset 原生 API，统一走 `IAssetManager` / `IAssetHandle` 抽象（ADR-042：所有 Load API 返回 handle，调用方持有并 `Release()`）；AssetManager 内部负责调用并适配 YooAsset。
- YooAsset 细节封装在 AssetManager 的适配器中：`YooAssetHandleAdapter`、`YooAssetSceneHandleAdapter`、`YooAssetSubAssetsHandleAdapter`、`YooAssetRawFileHandleAdapter`、`AssetDownloader`（包装 `ResourceDownloaderOperation`）、`AssetRemoteService`（远端寻址桥接）。
- YooAsset 的运行时设置经 ConfigMaster/ConfigRuntime 注入（ADR-049、ADR-060），不在代码里散落配置。
- manifest 采用三层离线兜底（ADR-065），URL 模板占位符遵循 ADR-025。

## 易混淆项

- YooAsset ≠ Unity Addressables：Nova 不使用 Addressables 寻址体系；RES-002 仅是外部教程资料，不代表 Nova 现状。
- YooAsset ≠ AssetBundle：AssetBundle 是 Unity 原生包体格式，YooAsset 是建立在其上的寻址/下载/引用管理层。
- `IAssetHandle.Release()` 是 Nova 层释放入口，不要直接操作 YooAsset 的 `AssetHandle.Release()`。
- YooAsset 句柄在 Nova 中以 `ReferencePool` 适配器承载（见 GLO-14）。

## 示例

```csharp
// 统一经 Nova 抽象加载，handle 由调用方 Release。
IAssetHandle<GameObject> handle = await assetManager.LoadAsync<GameObject>(location);
try { /* 使用 handle.Asset */ }
finally { handle.Release(); }
```

## 来源与验证

- `Packages/manifest.json`：`com.solotopia.yooasset` 以本地 UPM 包引入。
- `Assets/Framework/Scripts/Runtime/Modules/Asset/Managers/AssetManager/Definitions/` 下 YooAsset 适配器源码。
- `Assets/Framework/Docs/INDEX.md`：BundleBuilder（YooAsset SBP 构建封装）、AssetDownloader、AssetRemoteService、YooAssetHandleAdapter 条目。
