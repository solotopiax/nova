---
id: PAT-35
title: Editor 绘制统一走 EditorUtil.Draw
type: pattern
status: active
date: 2026-06-05
summary: 业务侧 Editor 绘制统一走 Draw
category: editor
aliases:
  - PAT-35-editor-draw-only
keywords:
  - PAT-35
  - EditorUtil.Draw
tags:
  - pattern
  - editor
  - ui
related:
  - "[[PAT-18-editor-window-vs-util-split|PAT-18]]"
  - "[[PAT-24-inspector-row-vertical-alignment|PAT-24]]"
  - "[[PAT-39-editor-draw-discipline-enforcement|PAT-39]]"
---

# PAT-35：Editor 绘制统一走 EditorUtil.Draw

## 适用场景

- `Editor/Inspectors/**`
- `Editor/Windows/**`
- 任何 Nova 业务侧的 `OnGUI` / `OnInspectorGUI` 绘制逻辑

## 核心规则

- 业务绘制优先走 `EditorUtil.Draw`。
- `EditorUtil.Draw` 缺接口时，先补 Draw，再在业务侧使用。
- 只有 Draw 封装层自身才应该直接面对 Unity 原生绘制 API。

## 为什么这样定

- 统一 label 宽度、间距、HelpBox、按钮样式
- 降低 Inspector/Window 各自演化出私有画法的概率
- 让布局修正与视觉收敛可以集中发生在一层

## 当前落地状态

- 目前大量 Inspector 已经依赖 `EditorUtil.Draw`。
- 代码库仍存在历史残留和局部直连写法，因此这条规则仍是持续收敛基线，而不是“历史问题已清零”。

## 反模式

- 业务侧新代码直接写原生 IMGUI
- 因为 Draw 缺一个接口，就在业务文件里临时绕过
- 在多个 Inspector 中复制同一段低层绘制细节

## 关联

- [[PAT-18-editor-window-vs-util-split|PAT-18]]
- [[PAT-39-editor-draw-discipline-enforcement|PAT-39]]
