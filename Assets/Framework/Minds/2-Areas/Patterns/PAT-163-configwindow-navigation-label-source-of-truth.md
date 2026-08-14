---
id: PAT-163
title: ConfigWindow 导航字段名必须复用实际显示名
type: pattern
status: active
date: 2026-08-13
summary: 导航字段名复用页面显示标签
category: editor
aliases:
  - PAT-163-configwindow-navigation-label-source-of-truth
  - ConfigWindow 字段导航文案单一来源
keywords:
  - PAT-163
  - ConfigWindow
  - 配置入口
  - SerializedProperty.displayName
  - ObjectNames.NicifyVariableName
  - AES-Key
  - AES-IV
tags:
  - pattern
  - editor
  - config
  - ux
related:
  - "[[PAT-09-inspector-config-i18n|PAT-09]]"
  - "[[PAT-18-editor-window-vs-util-split|PAT-18]]"
  - "[[PAT-161-configwindow-new-panel-dimension-header|PAT-161]]"
---

# PAT-163：ConfigWindow 导航字段名必须复用实际显示名

## 适用场景

- ProjectGuard、Config Validator、Runtime Error、HelpBox 或文档需要指出 `Nova/Open Config` 中的具体字段。
- ConfigWindow 通过 `SerializedProperty.displayName` 自动绘制字段，或通过显式 Label 绘制字段。

## 核心做法

1. 配置入口的面板层级必须使用 ConfigWindow 左树和右面板实际显示名称，禁止为字段另起“业务化”“友好化”别名。
2. 自动绘制字段的导航名称必须与 `SerializedProperty.displayName` 同源；可使用 `ObjectNames.NicifyVariableName(fieldName)`，不要手写第二份转换结果。
3. 显式绘制的字段必须逐项复用该面板的实际 Label。例如隐私配置的 `AESKey` 与 `AESIV` 分别显示为 `AES-Key` 与 `AES-IV`，错误入口也必须分别指向对应字段。
4. 无法确认具体字段显示名时，入口只写到已确认的面板层级，不得猜测或拼接字段别名。

## 原因

- 用户解决配置错误时以页面文字为检索锚点；字段别名会让用户在页面中找不到目标输入框。
- 代码字段名、运行时职责说明和页面 Label 可以不同，但错误提示的导航职责是准确带路，不是重新解释字段语义。
- 复用同一显示名生成规则可以避免 ConfigWindow 变更后 Guard、日志和文档静默漂移。

## 反模式

- 将 `AppAesKey / AppAesIV` 写成未在页面出现的“应用业务 AES-Key、AES-IV”。
- 对两个独立输入框给出一个合并字段名，导致用户无法判断应修改哪一个。
- 把代码成员名、Tooltip 语义或协议职责说明当成页面导航 Label。
- 不确定页面字段名时仍强行补全路径。

## 验证依据

- `ConfigWindow.RightPanel.cs` 的应用配置面板以 `SerializedProperty.displayName` 绘制字段。
- 同文件的隐私配置面板显式绘制 `AES-Key` 与 `AES-IV`。
- `EditorUtil.ProjectGuard.ConfigRule` 的入口文案应与上述两类绘制方式一一对应。

## 关联

- [[PAT-09-inspector-config-i18n|PAT-09]]：面向人的配置说明应紧贴真实字段。
- [[PAT-18-editor-window-vs-util-split|PAT-18]]：Window 负责展示，规则层提供稳定事实。
- [[PAT-161-configwindow-new-panel-dimension-header|PAT-161]]：ConfigWindow 面板的固定交互契约。
