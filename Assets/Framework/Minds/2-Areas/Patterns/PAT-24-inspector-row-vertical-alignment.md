---
id: PAT-24
title: Inspector 同层级编辑区对齐规则
type: pattern
status: active
date: 2026-06-05
summary: Inspector 同层级编辑区垂直对齐
category: inspector
aliases:
  - PAT-24-inspector-row-vertical-alignment
keywords:
  - PAT-24
  - Inspector 对齐
tags:
  - pattern
  - editor
  - inspector
related:
  - "[[PAT-09-inspector-config-i18n|PAT-09]]"
  - "[[PAT-35-editor-draw-only|PAT-35]]"
---

# PAT-24：Inspector 同层级编辑区对齐规则

## 适用场景

- 同一个 Inspector 层级下出现多行带标签控件
- 既有 `TypesSelector`，也有 `Property`、`Toggle`、`EnumSelector` 等常规字段
- 肉眼能看出右侧编辑区起点参差不齐

## 核心规则

- 同层级的控件行应共享统一的 label 宽度。
- 顶层一套宽度，子层级可以用另一套，但不要在同层级内部混用。
- 对齐目标是“右侧可编辑区域的起点一致”，不是“左边中文长度看起来差不多”。
- 局部布局内控制宽度，避免污染整个 Inspector 的全局状态。

## 当前代码事实

- 当前多个 Inspector 已在使用固定宽度参数，常见宽度有 `180f` 与 `175f`。
- 代码库仍存在历史面板没有完全统一的情况，因此这条规则仍是增量收敛标准。

## 为什么这样定

- Label 长度一旦改成中文，自动宽度更容易造成控件列抖动。
- 对齐稳定后，字段扫描速度会明显快于“锯齿式”布局。
- 这也是 HelpBox、Foldout、子配置继续建立层级时的基础视觉前提。

## 反模式

- 同层级一部分行固定宽度，另一部分完全自适应
- 为了对齐去改全局 `labelWidth`，导致后续区域一起漂移
- 只看单行效果，不看整组行项的垂直一致性

## 关联

- [[PAT-09-inspector-config-i18n|PAT-09]]
- [[PAT-35-editor-draw-only|PAT-35]]
