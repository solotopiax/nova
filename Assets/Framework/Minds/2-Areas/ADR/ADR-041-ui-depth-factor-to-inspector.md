---
id: ADR-041
title: UIDepthConfig 静态常量下线，深度换算系数散到 UIComponent Inspector
summary: 深度因子静态常量改 Inspector 字段
category: runtime
status: accepted
date: 2026-05-26
aliases:
  - ADR-041-ui-depth-factor-to-inspector
keywords:
  - ADR-041
  - UIDepthConfig 静态常量下线，深度换算系数散到 UIComponent Inspector
  - ADR-041-ui-depth-factor-to-inspector
tags: [adr, runtime, ui, inspector, config]
supersedes:
  - "[[ADR-038-ui-depth-factor-rebalance|ADR-038]]"
superseded-by: []
related:
  - "[[PAT-83-canvas-sortingorder-overflow-clamp|PAT-83]]"
  - "[[PAT-27-config-no-serialize|PAT-27]]"
  - "[[PAT-112-managerconfig-host-to-leaf-passthrough|PAT-112]]"
---

# ADR-041：UIDepthConfig 静态常量下线，深度换算系数散到 UIComponent Inspector

## 背景

组深度换算系数不适合锁在静态常量里，不同项目的 UI 层级规模差异太大。

## 决策

- 删除 `UIDepthConfig` 静态常量类。
- 把 `GroupDepthFactor` / `ViewDepthFactor` 散到 `UIComponent` Inspector。
- 透传链保持在 `UIComponent -> UIManagerConfig -> UIManager -> UIGroup`。

## 影响

- 数值回到项目自决。
- 删除伪配置类，减少静态硬编码。
- 旧 prefab 需要补两个新字段。
