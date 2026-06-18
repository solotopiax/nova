---
id: GLO-07
title: AssetLocation / Asset 地址
type: glossary
status: active
date: 2026-06-05
summary: AssetLocation 统一称为 Asset 地址
category: naming
tags: [glossary, nova, terminology, asset, yooasset]
aliases:
  - GLO-07-asset-location
  - AssetLocation
  - Asset 地址
keywords:
  - GLO-07
  - AssetLocation / Asset 地址
  - GLO-07-asset-location
  - AssetLocation
  - Asset 地址
related:
  - "[[PAT-36-git-tracked-paths-relative-to-project-root|PAT-36]]"
---

# AssetLocation / Asset 地址

## 一句话定义

`AssetLocation` 是 Nova 资源加载层使用的逻辑地址，中文统一称 **Asset 地址**。

## 边界

- 它不是文件系统路径
- 它和 `SourceLocation`、`TargetLocation` 不是一回事
- 在 DLL 场景里，它经常和程序集资源寻址直接关联

## 当前代码里的高频位置

- `DllAssetEntry`
- `DllMasterAssetEntry`
- `ConfigComponent` / `ConfigManagerConfig`
- UI、Table、Localization、Network 等模块中的表项或配置项

## 统一称呼规则

面向人的中文文本统一写：

- `Asset 地址`

不要混写成：

- `资产地址`
- `资源地址`
- `资产路径`
- `资产位置`

## 编程命名规则

- 中文文本统一为“Asset 地址”
- 代码标识符仍保持 `AssetLocation`

## 常见误解

- 把 `AssetLocation` 当成磁盘路径
- 以为 UI 上也应该直接显示 `AssetLocation` 英文
- 在同一文档里混用多套中文译法
