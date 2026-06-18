---
id: PAT-128
title: Inspector 持久化 UI 状态勿每帧强写
type: pattern
summary: 持久化 UI 状态只在 OnEnable 设一次，绘制回调禁每帧强写
category: inspector
status: active
date: 2026-05-30
aliases:
  - PAT-128
keywords:
  - PAT-128
  - Inspector 持久化 UI 状态勿每帧强写
tags:
  - pattern
  - nova
  - editor
  - inspector
  - imgui
---

# PAT-128：Inspector 持久化 UI 状态勿每帧强写

## 适用场景

编写自定义 Editor Inspector（`OnInspectorGUI` / IMGUI）时，需要控制 List/Foldout 等控件的持久化 UI 状态（如 `SerializedProperty.isExpanded`、折叠/展开、选中页签）。

规则：**这类持久化 UI 状态只应在 `OnEnable` 初始化一次**，初始化后交还给用户与 Unity 的状态记忆机制，不要在每帧绘制回调里再写。

## 核心做法

- 想要「首次默认展开」这种一次性默认值：放进 `OnEnable`，绑定 `SerializedProperty` 后设一次 `prop.isExpanded = true`。
- `OnInspectorGUI` 内只读取该状态用于绘制，**绝不赋值**。
- 让 Unity 自带的 isExpanded 持久化记住用户后续的折叠偏好。
- 如果确实需要“恢复默认展开状态”，应提供显式重置入口，而不是靠每帧强写偷偷回滚用户选择。

## 反模式

```csharp
// 反模式：每帧强制展开，折叠箭头点了没反应 → 控件「僵死」
if (!m_Packages.isExpanded) m_Packages.isExpanded = true;
```

放在 `OnInspectorGUI` 里时，用户点折叠箭头 → 状态瞬间被下一帧设回 true → 表现为「无法收起 / 僵死」。注释意图常是「省得每次手动点开」，但副作用是永久锁死、用户操作全部失效。判别信号：同类型控件（如另一个未加此逻辑的 List）能正常折叠，问题控件不能——锁定就在每帧强写那行。
