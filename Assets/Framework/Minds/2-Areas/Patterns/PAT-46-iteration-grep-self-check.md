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
  - "[[PAT-09-inspector-config-i18n|PAT-09]]"
  - "[[PAT-21-inspector-helpbox-multiline|PAT-21]]"
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
- 改布局前先列出受影响的“字段、标题、专属 HelpBox”单元及启用/禁用状态；每轮改动后在实际窗口逐项核对顺序、对齐、长内容空间、说明编号和整组灰显。编译通过或序列化字段存在，不能替代视觉验收。

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
- 若把用户指出的局部现象当作唯一目标，容易依次漏掉分组、文本格式、禁用态背景等同一布局契约中的其他部分，造成多轮返工。
- 如果等到最后一轮再统一收口，通常已经把低层绘制细节扩散到多个位置。
- 每轮检查的成本很低，但能显著减少 Editor 风格再次分裂。

## 验证依据

- `ConfigWindow.RightPanel.cs` 的隐私配置面板同时包含 AES 与 iOS 隐私清单两类字段；只检查字段可序列化、窗口可打开，无法证明 HelpBox 顺序与平台禁用态符合要求。
- `EditorUtil.Draw.HelpBox.cs` 的背景和图标由自绘完成；外层禁用作用域可以使文字变灰，却不能据此推断背景、图标也已灰显。该情形必须实际查看禁用态，并按 [[PAT-35-editor-draw-only|PAT-35]] 收口绘制能力。

## 处理原则

- 如果这轮只是因为 `EditorUtil.Draw` 缺能力，就先补 Draw，再继续修业务层。
- 不接受“这轮先用原生 API 跑通，下轮再封装”的拖延做法。

## 关联

- [[PAT-35-editor-draw-only|PAT-35]]
- [[PAT-39-editor-draw-discipline-enforcement|PAT-39]]
- [[PAT-09-inspector-config-i18n|PAT-09]]
- [[PAT-21-inspector-helpbox-multiline|PAT-21]]
