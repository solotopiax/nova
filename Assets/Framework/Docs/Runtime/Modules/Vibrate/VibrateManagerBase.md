# VibrateManagerBase

`VibrateManagerBase` 是振动管理器的抽象基类。

它做的事情非常少，但很关键：

- 把 `IVibrateManager` 接进 `FrameworkManager` 调度体系
- 固定 `Priority = 18`
- 强制具体实现补齐全部运行逻辑

## 什么时候先看这页

- 你要自定义振动管理器实现。
- 你要确认 Vibrate 模块在框架里的调度优先级。
- 你在区分接口契约和默认实现之间的边界。

## 核心语义

### 1. Priority 固定为 18

这决定了它在 Manager 调度中的更新与关闭顺序。

### 2. 它不包含业务默认逻辑

这里没有：

- 数据加载逻辑
- 振动分组构建逻辑
- 插件调用逻辑

这些都留给 `VibrateManager`。

### 3. 自定义实现优先继承它

如果你要实现新的振动后端，继承它比只实现 `IVibrateManager` 更符合当前框架结构，因为它已经接好了 `FrameworkManager` 的调度入口。

## 风险点 / 易错点

- 把它当成“半成品默认实现”会高估它提供的能力。
- 如果绕开它直接实现接口，就要自己处理框架调度契合问题。

## 继续阅读

关键源码：

- [VibrateManagerBase.cs](../../../../Scripts/Runtime/Modules/Vibrate/Managers/Implements/VibrateManagerBase.cs)

相关文档：

- [IVibrateManager.md](IVibrateManager.md)
- [VibrateManager.md](VibrateManager.md)
- [FrameworkManager.md](../FrameworkManager.md)
