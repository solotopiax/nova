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
- 普通字段编辑只以当前坐标为输入：已勾选轴保持独立，未勾选轴立即广播到同一逻辑组的全部物理格。
- 维度勾选或取消由 `DimensionProjector` 对整个矩阵原子投影：勾选时拆分全部旧逻辑组；取消时每个剩余逻辑组分别保留顶部当前轴取值，不能用一个完整坐标覆盖全矩阵。
- 保存只校验 WorkingCopy 的维度不变量，再用 `CopySerialized` 完整写回真实资产；保存阶段禁止重新广播或猜测权威来源。Exporter 使用同一只读门禁，阻止同一逻辑组的新旧值混合进入运行时快照。
- 取数由 `DimensionalResolver` 统一处理，运行时 `ConfigRuntimeSO` 仍保持单格快照，不感知掩码。
- YooAsset 与其它面板一样只编辑 WorkingCopy；路径或维度变化后用 WorkingCopy 刷新预览注入，点击保存后才写入 ConfigMaster 资产。
- CDN 部署面板（2026-07-22 接入）：`CdnMask + CdnOverrides`，整套 `CDNEditorConfigs` 为一份快照，切坐标即整套切换；走 WorkingCopy 延迟落盘，仅 Editor 期消费，不导出 Runtime。

## 影响

- 全局唯一和维度化配置可以并存，避免无意义的重复填写。
- 读写职责分离，Exporter / ConfigWindow 共享同一套取数规则。
- 运行时结构不变，风险主要集中在编辑器侧。
- 用户确认维度切换后，内存副本会一次完成全矩阵转换；用户取消或投影失败时保持原副本不变。

## 关联

- 相关 ADR：`ADR-054`、`ADR-053`、`ADR-047`、`ADR-049`
