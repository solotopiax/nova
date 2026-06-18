---
id: PAT-46
title: 多轮 Editor 迭代时每轮都做封装自检
type: pattern
status: active
date: 2026-06-05
summary: 每轮 Editor 迭代后都做封装自检
category: editor
aliases:
  - PAT-46-iteration-grep-self-check
keywords:
  - PAT-46
  - 迭代自检
tags:
  - pattern
  - editor
  - enforcement
related:
  - "[[PAT-35-editor-draw-only|PAT-35]]"
  - "[[PAT-39-editor-draw-discipline-enforcement|PAT-39]]"
---

# PAT-46：多轮 Editor 迭代时每轮都做封装自检

## 适用场景

- 同一个 Inspector / EditorWindow 连续多轮修布局、闪烁、抖动、错位、拖拽问题
- 一轮修完又出现下一轮 UI 问题
- 代码开始频繁接触 Rect、Color、Cursor、Scroll、Style 这类低层绘制细节

## 核心规则

- 不是只在“准备提交前”检查一次。
- 只要完成了一轮可测试的 Editor 改动，就应立即检查一次是否又回退到了原生绘制。
- 发现违规时，当轮就收口，不把问题滚到下一轮。

## 推荐检查方式

对本轮触碰到的业务侧 Editor 文件做关键字检查，重点看是否又新增了：

- `EditorGUI.*`
- `EditorGUILayout.*`
- `GUILayout.*`
- `GUILayoutUtility.*`
- `GUI.*`
- `GUIStyle.*`
- `EditorGUIUtility.*`

检查目标不是“全盘否定所有底层 API”，而是防止它们重新扩散到业务层文件里。

## 为什么这样定

- 多轮视觉修复时，注意力很容易只盯着眼前 bug，而忽略封装边界。
- 如果等到最后一轮再统一收口，通常已经把低层绘制细节扩散到多个位置。
- 每轮检查的成本很低，但能显著减少 Editor 风格再次分裂。

## 处理原则

- 如果这轮只是因为 `EditorUtil.Draw` 缺能力，就先补 Draw，再继续修业务层。
- 不接受“这轮先用原生 API 跑通，下轮再封装”的拖延做法。

## 关联

- [[PAT-35-editor-draw-only|PAT-35]]
- [[PAT-39-editor-draw-discipline-enforcement|PAT-39]]
