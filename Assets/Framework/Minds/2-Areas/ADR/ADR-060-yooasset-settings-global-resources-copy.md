---
id: ADR-060
title: 本工程直出包时放全局 YooAssetSettings 副本松绑 ADR-049
status: accepted
summary: 直出包放全局 YooAssetSettings 副本兜底
category: asset
date: 2026-06-02
aliases:
  - ADR-060-yooasset-settings-global-resources-copy
keywords:
  - ADR-060
  - 本工程直出包时放全局 YooAssetSettings 副本松绑 ADR-049
  - ADR-060-yooasset-settings-global-resources-copy
tags:
  - nova
  - asset
  - yooasset
  - config
---

# ADR-060：本工程直出包时放全局 YooAssetSettings 副本松绑 ADR-049

## 背景

编辑器期 settings 已从 Resources 迁出，但本工程直接打包运行时仍需要一个兜底副本，避免首包目录错位。

## 决策

- 在工程根 `Assets/Resources/` 放一份全局 `YooAssetSettings.asset`。
- 这份副本只用于本工程直出包场景，不替代编辑器注入链路。
- 每个 sample 各放一份 Resources 副本的做法不允许。

## 影响

- 直出包运行时能命中正确目录。
- 编辑器期仍以注入实例为准。
- ADR-049 需要补一句松绑说明。
