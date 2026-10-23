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

按上面顺序，源码默认分别为 Time=0、MuteInSoundGroup=false、Loop=false、Priority=0、VolumeInSoundGroup=1、FadeInSeconds=0、Pitch=1、PanStereo=0。无参按名称播放仅从表行覆盖 Loop/Priority/Volume；调用方显式提供 params 时不会自动再与行默认合并。

所有 setter 直接存值，不验证范围或浮点有限性，也不直接修改正在播放的 AudioSource。参数会在异步 clip 加载完成后才被消费，因此传入后既不能 Put，也不能继续修改或复用到另一条并发请求。

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
- 常规未知声音名、未知组或无代理的拒绝分支会回收参数；但空组名等校验异常或池/回调异常不具有统一 finally 兜底，不能把“任意抛出路径都会回收”当作保证。调用方应在转移前提供合法输入，避免用异常后再次 Put 去猜测当前所有权。
- 如果你自己 `new PlaySoundParams()` 绕过引用池，虽然不一定立刻坏，但会偏离这套生命周期约定。

## 继续阅读

关键源码：

- [PlaySoundParams.cs](../../../../Scripts/Runtime/Modules/Sound/Managers/Definitions/PlaySoundParams.cs)
- [SoundManager.PlaySoundInfo.cs](../../../../Scripts/Runtime/Modules/Sound/Managers/Implements/SoundManager.PlaySoundInfo.cs)

相关文档：

- [ISoundManager.md](ISoundManager.md)
- [SoundManager.md](SoundManager.md)
- [PlaySoundInfo.md](PlaySoundInfo.md)

