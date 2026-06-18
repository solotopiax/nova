# VibrateManagerConfig

`VibrateManagerConfig` 是 `VibrateComponent.Start()` 传给 `VibrateManager.Initialize(...)` 的配置包。

它只保留两类运行时输入：

- `EmphasisUnitsSettings`
- `CustomUnitsSettings`

## 什么时候先看这页

- 你要确认振动系统初始化到底传了哪些东西。
- 你在判断新配置项应该属于 Emphasis 区还是 Custom 区。
- 你在排查“组件里明明配了设置，为何 Manager 没吃到”的链路。

## 配置语义

### 1. 它只描述运行时数据来源

这不是播放参数对象，也不是设备能力配置对象。它只是告诉 Manager：

- 去哪里找 Emphasis 数据
- 去哪里找 Custom 数据

### 2. 两类数据源被故意分开

Emphasis 和 Custom 虽然都属于振动，但它们对应不同的数据行接口和不同的播放语义，所以配置层也保持双通道。

## 设计边界

- 这个配置对象不包含 `Enable`；启用状态属于运行时开关
- 这个配置对象不包含即时播放参数；即时参数通过 `PlayCustom(...)` / `PlayEmphasis(...)` 传入
- 它不关心编辑器源目录，编辑器路径仍留在 `VibrateSettings`

## 风险点 / 易错点

- 把 Emphasis / Custom 混成一张表或一份列表，会破坏当前模块的双区域设计。
- 如果后续新增字段其实只跟某一类振动有关，不要粗暴加成“全局振动设置”。

## 继续阅读

关键源码：

- [VibrateManagerConfig.cs](../../../../Scripts/Runtime/Modules/Vibrate/Managers/Definitions/VibrateManagerConfig.cs)
- [VibrateComponent.cs](../../../../Scripts/Runtime/Modules/Vibrate/VibrateComponent.cs)

相关文档：

- [VibrateComponent.md](VibrateComponent.md)
- [VibrateManager.md](VibrateManager.md)
- [VibrateSettings.md](VibrateSettings.md)

