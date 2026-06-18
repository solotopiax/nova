---
id: MOC-Debug
title: 调试系统图谱
summary: Debug 入口、RuntimeDebugger 激活与组件边界速查
category: module
status: active
date: 2026-06-05
aliases:
  - MOC-Debug
  - 调试系统图谱
  - GM工具图谱
tags: [moc, nova, debug, runtime-debugger, runtime]
keywords: [DebugComponent, IDebugManager, DebugManager, RuntimeDebugger, DebuggerActiveType, DiskCheckingConfig]
related:
  - "[[ADR-001-component-manager-three-layer|ADR-001]]"
  - "[[ADR-002-manager-priority-system|ADR-002]]"
  - "[[ADR-008-managerbase-internal-abstract|ADR-008]]"
  - "[[ADR-010-validation-on-consumer-side|ADR-010]]"
  - "[[ADR-023-no-editor-prefs-in-framework|ADR-023]]"
---

# MOC-Debug：调试系统图谱

## 一句话

Debug 模块负责调试入口和调试期附加能力；`Component` 负责入口与 MonoBehaviour 桥接，新增调试逻辑应优先下沉到 `Manager`。

## 何时查这页

- 要改调试面板激活条件
- 要理解 `RuntimeDebugger` 初始化和 `DiskChecking` 属于谁
- 要新增调试能力并判断该放 `Component` 还是 `Manager`

## 当前结构

```text
Nova.Debug
  -> DebugComponent
  -> IDebugManager
  -> DebugManagerBase
  -> DebugManager
```

关键定义：

- `DebuggerActiveType`
- `DiskCheckingConfig`
- `DiskCheckEventData`
- `DebugManagerConfig`

## 当前职责切分

### DebugComponent

- 在 `Awake()` 里创建 `IDebugManager`
- 按 `IsDebuggerActive()` 判断是否初始化 `RuntimeDebugger`
- 在 `Start()` 里把 `DiskCheckingConfigs` 交给 `DebugManager.Initialize`

### DebugManager

- 对外承担调试能力的初始化与关闭
- 新增调试逻辑时，应优先往 Manager 下沉，而不是继续把 Component 做厚

## 当前边界

- `RuntimeDebugger` 激活判断属于 Debug 模块入口逻辑
- `EditorPrefs` 不应成为框架调试状态的长期存储
- 这页描述的是 Nova 的调试模块边界，不是第三方调试工具的使用手册

## 导航提醒

- `RuntimeDebugger.Init(...)` 由入口条件触发，不应把调试器初始化散落到别的模块。
- 具体优先级、公开方法面与配置字段，以 `Docs` 和源码为准。
- 这页只回答模块边界，不承担第三方调试工具使用手册职责。

## 常见误区

- 继续把新功能堆进 `DebugComponent`
- 把第三方调试工具文档直接混进 MOC
- 用 `EditorPrefs` 记录框架调试状态
- 把“是否显示调试器”和“调试模块是否存在”理解成同一件事

## 先往哪看

- 改结构债务：[[ADR-001-component-manager-three-layer]]
- 改优先级：[[ADR-002-manager-priority-system]]
- 改框架存储边界：[[ADR-023-no-editor-prefs-in-framework]]

## 关联

- 图谱：[[MOC-Manager]]
