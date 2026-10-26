# PlaySoundParams

`PlaySoundParams` 只保存单次播放的表外运行时参数。声音组、资源、循环、优先级、音量与空间混合度统一由声音表决定。

它不是普通 DTO，而是一个 `IReference` 引用池对象。这个身份决定了它最重要的使用规则：

- 通过 `Create()` 获取
- 传给 `ISoundManager.PlaySound(...)` 或 `PlaySoundByAsset(...)` 后所有权转移，即使同步拒绝也由 Manager 回收
- 调用方禁止再手动 `ReferencePool.Put(...)`

## 什么时候先看这页

- 你要定制某次播放的进度、单次静音、淡入、音调或声相。
- 你在排查引用池重复回收异常。
- 你要确认默认参数来自哪里。

## 参数面

它覆盖的核心播放参数包括：

- `Time`
- `MuteInSoundGroup`
- `FadeInSeconds`
- `Pitch`
- `PanStereo`

这些默认值都来自 `SoundConstant`。

默认值分别为 `Time=0`、`MuteInSoundGroup=false`、`FadeInSeconds=0`、`Pitch=1`、`PanStereo=0`。无论是否提供本对象，`Loop / Priority / Volume / SpatialBlend` 都直接消费目标 `ISoundRow`，不会被参数默认值覆盖。

升级时删除对本对象 `Loop / Priority / VolumeInSoundGroup` 的赋值，把对应配置写入声音源表的 `Loop / Priority / Volume` 并正式导出。需要另一组播放配置时使用另一个唯一 `Name` 的声音行；多个行可引用同一音频，此时按 `Name` 播放，按地址入口会拒绝歧义。声音组音量仍用独立 `SetSoundGroupVolume` 命令控制。

所有 setter 直接存值，不验证范围或浮点有限性，也不直接修改正在播放的 AudioSource。参数会在异步 clip 加载完成后才被消费，因此传入后既不能 Put，也不能继续修改或复用到另一条并发请求。

## 生命周期语义

### 1. 创建来自引用池

标准入口是：

- `PlaySoundParams.Create()`

这不是普通 `new` 出来的短命对象设计。

### 2. Manager 接管其生命周期

通过同步配置检查的请求会把参数放入内部 `PlaySoundInfo`，并在后者 `Clear()` 时级联回收；同步拒绝则立即归还参数。

### 3. `Clear()` 会重置到默认值

这意味着引用池复用时，不应该假设上次参数残留还在。

## 风险点 / 易错点

- 这是整个 Sound 模块最容易踩的内存语义坑：传入后不要再持有。
- 名称缺失、地址缺失或歧义、行缺资源或组、组未注册或无代理的拒绝分支均会回收参数。池操作或后续回调异常仍不具有统一 finally 兜底，调用方不要用异常后再次 Put 去猜测当前所有权。
- 如果你自己 `new PlaySoundParams()` 绕过引用池，虽然不一定立刻坏，但会偏离这套生命周期约定。

## 继续阅读

关键源码：

- [PlaySoundParams.cs](../../../../Scripts/Runtime/Modules/Sound/Managers/Definitions/PlaySoundParams.cs)
- [SoundManager.PlaySoundInfo.cs](../../../../Scripts/Runtime/Modules/Sound/Managers/Implements/SoundManager.PlaySoundInfo.cs)

相关文档：

- [ISoundManager.md](ISoundManager.md)
- [SoundManager.md](SoundManager.md)
- [PlaySoundInfo.md](PlaySoundInfo.md)

