# SoundGroupShell

`SoundGroupShell` 是 `SoundComponent` 在 Inspector 里配置单个声音组时使用的序列化外壳。

它不是运行时真正的声音组对象，而是“场景配置稿”。真正的 `SoundGroup` 会在 `SoundComponent.Start()` 里根据这些配置被创建出来。

## 什么时候先看这页

- 你要配置声音组初始音量、静音状态和代理数量。
- 你在排查为什么某个组能创建但容量不够。
- 你要理解 Inspector 配置是如何进入 `AddSoundGroup(...)` 的。

## 配置语义

每个 `SoundGroupShell` 只描述一个组的静态起始状态：

- `Name`
- `AvoidBeingReplacedBySamePriority`
- `Mute`
- `Volume`
- `AgentCount`

其中最关键的是：

- `Name`：运行时组名
- `AgentCount`：决定组内并发播放容量

## 与运行时对象的关系

- `Shell` 只是序列化数据壳
- `SoundManager.AddSoundGroup(...)` 才会创建真正的 Helper 和 Agent
- 因此改这里的字段，本质是在改启动时的建组参数

## 风险点 / 易错点

- 它不是运行时可变状态容器；运行时音量和静音真正由声音组对象维护。
- `AgentCount` 太小会直接抬高“组内代理不足”的失败概率。
- `Name` 冲突会导致建组失败，组件层只会记日志。

## 继续阅读

关键源码：

- [SoundGroupShell.cs](../../../../Scripts/Runtime/Modules/Sound/Definitions/SoundGroupShell.cs)
- [SoundComponent.cs](../../../../Scripts/Runtime/Modules/Sound/SoundComponent.cs)

相关文档：

- [SoundComponent.md](SoundComponent.md)
- [SoundManager.md](SoundManager.md)

