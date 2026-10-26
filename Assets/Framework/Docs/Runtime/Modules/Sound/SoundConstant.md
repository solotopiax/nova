# SoundConstant

`SoundConstant` 是 Sound 模块的内部默认值集合。

它主要服务这些地方：

- `PlaySoundParams` 的初始值与 `Clear()` 重置值
- 停止 / 恢复等操作的默认淡入淡出时长
- 尚未播放或复位代理的内部初始状态

## 什么时候先看这页

- 你要确认 `PlaySoundParams.Create()` 之后各字段默认是什么。
- 你在排查未显式传入淡入淡出时间时的默认行为。
- 你想修改全模块统一的默认播放参数基线。

## 默认值语义

当前关键默认值包括：

- `c_DefaultTime = 0f`
- `c_DefaultMute = false`
- `c_DefaultLoop = false`
- `c_DefaultPriority = 0`
- `c_DefaultVolume = 1f`
- `c_DefaultFadeInSeconds = 0f`
- `c_DefaultFadeOutSeconds = 0f`
- `c_DefaultPitch = 1f`
- `c_DefaultPanStereo = 0f`

## 设计边界

- 它是模块内部常量，不是对业务公开的配置入口
- 修改这里会影响默认参数语义，而不是单次播放行为
- 实际播放的循环、优先级、音量与空间混合度只来自 `ISoundRow`；内部代理复位常量不充当缺失表行的回退配置
- `PlaySoundParams` 只提供表外运行时参数的默认值

## 风险点 / 易错点

- 把它当配置文件替代品不合适；这是一组编译期常量。
- 修改默认值会影响引用池对象复用后的重置结果，不只是新建时的初值。

## 继续阅读

关键源码：

- [SoundConstant.cs](../../../../Scripts/Runtime/Modules/Sound/Managers/Definitions/SoundConstant.cs)

相关文档：

- [PlaySoundParams.md](PlaySoundParams.md)
- [SoundManager.md](SoundManager.md)

