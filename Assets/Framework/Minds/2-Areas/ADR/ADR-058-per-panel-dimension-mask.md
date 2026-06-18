---
id: ADR-058
title: ConfigWindow per-panel 可勾选维度（PanelDimensionMask + DimensionProjector + Override 旁路）
summary: 面板级维度掩码 + Override 旁路按需多维配置
category: module
status: accepted
date: 2026-06-02
aliases:
  - ADR-058-per-panel-dimension-mask
  - ADR-058
keywords:
  - ADR-058
  - ConfigWindow per-panel 可勾选维度（PanelDimensionMask + DimensionProjector + Override 旁路）
  - ADR-058-per-panel-dimension-mask
tags:
  - adr
  - nova
  - config
  - configwindow
  - dimension
  - configmaster
supersedes: []
superseded-by: []
related:
  - "[[ADR-054-kit-config-three-dim-matrix|ADR-054]]"
  - "[[ADR-053-kit-config-templating|ADR-053]]"
  - "[[ADR-005-hybridclr-namespace-single-write-path|ADR-005]]"
  - "[[ADR-047-editor-active-master-anchor|ADR-047]]"
  - "[[ADR-049-yooasset-settings-via-configmaster|ADR-049]]"
  - "[[ADR-022-sdk-plugin-architecture|ADR-022]]"
  - "[[MOC-Config|MOC-Config]]"
---

# ADR-058：ConfigWindow per-panel 可勾选维度

## 背景

ADR-054 建好了三维矩阵底座，但不是所有面板都需要按三维分别填写。有些字段应保持全局唯一，有些只需要按部分维度区分。

## 决策

- 给每个面板加 `PanelDimensionMask`，决定它是否按平台 / 渠道 / 开发模式分格。
- 顶层面板用 `XxxOverrides` 承载维度化后的差异值，矩阵面板直接沿用 `m_Entries`。
- 维度切换由 `DimensionProjector` 统一处理，包含分裂、合并和广播。
- 取数由 `DimensionalResolver` 统一处理，运行时 `ConfigRuntimeSO` 仍保持单格快照，不感知掩码。
- YooAsset 在切换维度后要重新注入，避免编辑期工具继续消费旧值。

## 影响

- 全局唯一和维度化配置可以并存，避免无意义的重复填写。
- 读写职责分离，Exporter / ConfigWindow 共享同一套取数规则。
- 运行时结构不变，风险主要集中在编辑器侧。

## 关联

- 相关 ADR：`ADR-054`、`ADR-053`、`ADR-047`、`ADR-049`
