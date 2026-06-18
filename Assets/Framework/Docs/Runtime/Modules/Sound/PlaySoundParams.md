# PlaySoundParams

`PlaySoundParams` 是单次播放请求的参数对象。

它不是普通 DTO，而是一个 `IReference` 引用池对象。这个身份决定了它最重要的使用规则：

- 通过 `Create()` 获取
- 传给 `ISoundManager.PlaySound(...)` 后所有权转移
- 调用方禁止再手动 `ReferencePool.Put(...)`

## 什么时候先看这页

- 你要定制某次播放的循环、优先级、音量、淡入等行为。
- 你在排查引用池重复回收异常。
- 你要确认默认参数来自哪里。

## 参数面

它覆盖的核心播放参数包括：

- `Time`
- `MuteInSoundGroup`
- `Loop`
- `Priority`
- `VolumeInSoundGroup`
- `FadeInSeconds`
- `Pitch`
- `PanStereo`

这些默认值都来自 `SoundConstant`。

## 生命周期语义

### 1. 创建来自引用池

标准入口是：

- `PlaySoundParams.Create()`

这不是普通 `new` 出来的短命对象设计。

### 2. Manager 接管其生命周期

一旦传入 `PlaySound(...)`，它会被包进内部 `PlaySoundInfo`，并在后者 `Clear()` 时级联回收。

### 3. `Clear()` 会重置到默认值

这意味着引用池复用时，不应该假设上次参数残留还在。

## 风险点 / 易错点

- 这是整个 Sound 模块最容易踩的内存语义坑：传入后不要再持有。
- 如果播放请求因为声音名不存在、声音组不存在等原因提前失败，Manager 也会负责回收该对象。
- 如果你自己 `new PlaySoundParams()` 绕过引用池，虽然不一定立刻坏，但会偏离这套生命周期约定。

## 继续阅读

关键源码：

- [PlaySoundParams.cs](../../../../Scripts/Runtime/Modules/Sound/Managers/Definitions/PlaySoundParams.cs)
- [SoundManager.PlaySoundInfo.cs](../../../../Scripts/Runtime/Modules/Sound/Managers/Implements/SoundManager.PlaySoundInfo.cs)

相关文档：

- [ISoundManager.md](ISoundManager.md)
- [SoundManager.md](SoundManager.md)
- [PlaySoundInfo.md](PlaySoundInfo.md)

