---
id: PAT-80
title: Demo View 纯色块与 TMP 样式基线
summary: Demo 默认纯色块加 TMP
category: demo
type: pattern
status: active
date: 2026-06-05
aliases:
  - PAT-80-demo-view-pure-color-style
keywords:
  - PAT-80
  - Demo View 纯色块与 TMP 样式基线
  - PAT-80-demo-view-pure-color-style
tags:
  - pattern
  - sample
  - demo
  - ui
  - style
  - tmp
related:
  - "[[PAT-77-base-demo-view-three-zone-template|PAT-77]]"
  - "[[PAT-79-demo-view-portrait-layout|PAT-79]]"
---

# PAT-80：Demo View 纯色块与 TMP 样式基线

## 适用场景

- 制作或修改 `MainDemo` 体系下的 Demo View
- 给 Demo 增加按钮、标签、输入控件、信息卡片

## 核心规则

### 1. 视觉基线优先纯色块

- Demo UI 默认依赖纯色 Image 与颜色对比，而不是 SpriteAtlas
- 背景色、卡片色、按钮色直接由控件颜色控制

### 2. 文本统一走 TMP

- Demo 文本组件统一使用 `TMP_Text` / `TextMeshProUGUI`
- 不把旧 `UnityEngine.UI.Text` 当成新 Demo 的默认选项

### 3. 交互元素强调高对比

- 深色主背景上，交互元素优先走白底黑字或其它高对比组合
- 只读文本与可点击控件在视觉上要能快速区分

## 当前项目中的可见事实

- `BaseDemoView.prefab` 存在统一的 `RootBackground`
- MainDemo 的大量 Prefab 已使用 `TextMeshProUGUI`
- 当前 Demo 视觉主线更接近“纯色块 + TMP”的轻量样式，而不是贴图型样式

## 为什么这样定

- Demo 的重点是说明能力，不是堆叠美术资产
- 纯色块样式更轻、更稳，也更容易批量维护
- TMP 已经是当前 Demo 文本链路的主流实现

## 反模式

- 新 Demo 重新依赖一套 SpriteAtlas 贴图按钮体系
- 在同一批 Demo 中混用 TMP 与旧 Text 形成两套文本基线
- 交互元素与说明文字缺少明确视觉差异

## 关联

- [[PAT-77-base-demo-view-three-zone-template|PAT-77]]
- [[PAT-79-demo-view-portrait-layout|PAT-79]]
