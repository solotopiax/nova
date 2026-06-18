---
id: PAT-37
title: Runtime 侧禁止在 Asset 模块外直接依赖 YooAsset
type: pattern
status: active
date: 2026-06-05
summary: YooAsset 细节只留在 Asset 模块
category: asset
aliases:
  - PAT-37-no-yooasset-outside-asset-module
keywords:
  - PAT-37
  - YooAsset 边界
tags:
  - pattern
  - asset
  - runtime
  - boundary
related:
  - "[[GLO-07-asset-location|GLO-07]]"
  - "[[ADR-017-component-manager-isolation|ADR-017]]"
---

# PAT-37：Runtime 侧禁止在 Asset 模块外直接依赖 YooAsset

## 适用场景

- Runtime 新增资源加载、场景加载、RawFile 读取、预下载、标签加载等能力
- 其他模块打算直接引入 `YooAsset` 命名空间
- 评估某个模块应依赖“资源系统抽象”还是“资源底层实现”

## 核心规则

- Runtime 侧的 YooAsset 直接依赖只允许留在 Asset 模块内部。
- 其他模块通过 `AssetComponent` / `IAssetManager` / `AssetLocation` 与资源系统交互。
- 资源寻址对外暴露的是 Nova 语义，不是 YooAsset 语义。

## 当前代码事实

- Runtime 目录下的 YooAsset 直接引用集中在 `Assets/Framework/Scripts/Runtime/Modules/Asset/**`。
- 其他模块如 `Config`、`UI`、`Table`、`Localization`、`Sound`、`Network` 都主要消费 `AssetLocation` 与 Asset 模块抽象，不直接持有 YooAsset API。
- Editor 侧存在少量 YooAsset 集成代码，例如配置注入、Bundle 构建与相关窗口；这些不在本条禁止范围内。

## 为什么这样定

- 资源后端一旦变化，影响面只应收敛在 Asset 模块。
- 其他模块关心的是“加载某个逻辑资源”，不是“如何初始化 YooAsset 包”。
- 这能避免跨模块把底层资源方案固化成公共契约。

## 反模式

- UI、Config、Procedure、Network 等 Runtime 模块直接 `using YooAsset`
- 在业务模块里传播 `Package`、`Handle`、初始化参数等底层概念
- 为了图快，跨模块复制 Asset 模块内部的底层调用

## 关联

- [[GLO-07-asset-location|GLO-07]]
- [[ADR-017-component-manager-isolation|ADR-017]]
