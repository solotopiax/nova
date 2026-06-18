# PlaySoundInfo

`PlaySoundInfo` 不是公开 API，而是 `SoundManager` 的内部播放上下文对象。

它把一次播放请求在“进入异步资源加载后”仍然需要携带的三样信息绑在一起：

- `SerialID`
- `SoundGroup`
- `PlaySoundParams`

## 什么时候先看这页

- 你在排查为什么 `PlaySoundParams` 是谁回收的。
- 你要理解声音请求从“提交播放”到“资源加载完成”之间是怎么传递上下文的。
- 你在读 `LoadAndPlaySoundAsync(...)` 这段代码。

## 角色定位

- 它是一次播放请求的临时信封对象
- 它只存在于 Manager 内部异步链路
- 它的主要价值不是数据结构复杂，而是把引用池生命周期压平

## 生命周期

### 1. 由 `Create(...)` 从引用池拿出

`SoundManager.PlaySound(...)` 在确认要进入异步加载后，会创建 `PlaySoundInfo`。

### 2. 加载成功或失败后都会回收

无论结果如何，`LoadAndPlaySoundAsync(...)` 最终都会把它 `ReferencePool.Put(...)`。

### 3. 回收时会级联回收 `PlaySoundParams`

`Clear()` 里最关键的一步是：

- 如果持有 `m_PlaySoundParams`，则一并 `ReferencePool.Put(...)`

这就是播放参数所有权转移机制成立的原因。

## 风险点 / 易错点

- 它是私有嵌套类，不应该被外部系统当扩展点。
- 如果你改坏了它的 `Clear()`，最先出问题的通常不是播放逻辑，而是引用池生命周期。
- 文档上把它写成“普通 DTO”会误导阅读者忽略其真正职责。

## 继续阅读

关键源码：

- [SoundManager.PlaySoundInfo.cs](../../../../Scripts/Runtime/Modules/Sound/Managers/Implements/SoundManager.PlaySoundInfo.cs)
- [SoundManager.Methods.cs](../../../../Scripts/Runtime/Modules/Sound/Managers/Implements/SoundManager.Methods.cs)

相关文档：

- [PlaySoundParams.md](PlaySoundParams.md)
- [SoundManager.md](SoundManager.md)
- [ISoundManager.md](ISoundManager.md)

