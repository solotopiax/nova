---
id: ADR-049
title: YooAsset Settings 路径 ConfigMaster 化（含 UPM 包本地 fork）
summary: YooAsset 设置路径收口 ConfigMaster
category: module
status: accepted
date: 2026-05-28
aliases:
  - ADR-049-yooasset-settings-via-configmaster
  - ADR-049
keywords:
  - ADR-049-yooasset-settings-via-configmaster
  - YooAsset Settings 路径 ConfigMaster 化（含 UPM 包本地 fork）
  - ADR-049
tags: [adr, nova, yooasset, configmaster, upm]
supersedes: []
superseded-by: []
related:
  - "[[ADR-047-editor-active-master-anchor]]"
  - "[[PAT-119-upm-private-fork-local-diff-marking|PAT-119]]"
  - "[[PAT-37-no-yooasset-outside-asset-module|PAT-37]]"
  - "[[ADR-019-yooasset-release-mandatory|ADR-019]]"
---

# ADR-049：YooAsset Settings 路径 ConfigMaster 化

## 背景

YooAssetSettings 和 BundleCollectorSetting 过去靠 Resources / FindAssets 选“哪一份”，多 sample 共存时会命中错误副本。

## 决策

- 把两份配置的实际路径收口到 `ConfigMasterSO`。
- 编辑器期通过注入拿到明确的 settings 实例，而不是全工程扫描。
- `YooAssetConfiguration` 和 `SettingLoader` 各补一个最小入口，方便按路径读取。
- 运行时发版链路保持兼容，只把正确副本带进产物。

## 影响

- 多 sample 工程不再依赖 GUID 顺序。
- ConfigMaster 成为 sample 配置中心，YooAsset 路径和其他编辑器配置统一管理。
- 引入本地 fork，后续同步上游时需要显式比对差异。

## 关联

- 相关 ADR：`ADR-047`、`ADR-019`
- 相关 Pattern：`PAT-119`、`PAT-37`
