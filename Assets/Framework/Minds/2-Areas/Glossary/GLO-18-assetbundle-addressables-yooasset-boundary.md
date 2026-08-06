---
id: GLO-18
title: AssetBundle、Addressables 与 YooAsset 边界
summary: Nova用YooAsset管理Bundle资源
category: asset
status: active
date: 2026-08-06
aliases:
  - GLO-18-assetbundle-addressables-yooasset-boundary
  - AssetBundle
  - Addressables
keywords:
  - GLO-18
  - AssetBundle
  - Addressables
  - YooAsset
  - 资源管理边界
tags: [glossary, nova, asset, yooasset, assetbundle, addressables]
related:
  - "[[GLO-10-yooasset-asset-management|GLO-10]]"
  - "[[PAT-37-no-yooasset-outside-asset-module|PAT-37]]"
---

# GLO-18：AssetBundle、Addressables 与 YooAsset 边界

## 定义

- **AssetBundle**：Unity 原生资源打包格式与运行时加载基础设施，描述资源二进制包本身。
- **Addressables**：Unity 官方建立在 AssetBundle 上的地址、构建、目录和下载管理方案。
- **YooAsset**：Nova 选用的资源管理层，同样建立在 AssetBundle 之上，负责 Manifest、寻址、下载、缓存和 Handle 生命周期。

## Nova 边界

- Nova 当前不使用 Addressables 作为运行时资源系统；提到“地址”“远端资源”或“热更新”时，默认指 YooAsset 与 Asset 模块抽象，除非上下文明示 Addressables 调研。
- AssetBundle 是底层产物，不应让业务模块直接拼接 Bundle 路径或管理依赖。
- Asset 模块以外的 Runtime 消费方只使用 `IAssetManager`、`IAssetHandle` 与 Nova 的 AssetLocation 语义，不直接依赖 YooAsset API。
- YooAsset Manifest 决定 Bundle hash 和依赖；Tag 只是 Downloader 的筛选条件，不是另一套持久化资源目录。

## 易混淆项

- “使用 AssetBundle”不代表“使用 Addressables”；YooAsset 也以 AssetBundle 为底层格式。
- Addressables 教程或 API 不能直接作为 Nova 当前实现依据。
- Bundle 文件存在不等于某版本可启动；仍需对应 Manifest 与启动下载范围完整。

## 示例

启动白名单只切换 YooAsset `.version/.hash/.bytes` 元数据候选地址，Bundle 仍走常规 CDN，并继续由 YooAsset 根据 Manifest hash 判断本地缓存命中。

## 来源

- [[GLO-10-yooasset-asset-management|GLO-10]]：YooAsset 资源管理层。
- [[PAT-37-no-yooasset-outside-asset-module|PAT-37]]：Runtime 封装边界。

---
