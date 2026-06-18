# ISoundRow

`ISoundRow` 是运行时声音数据行契约。

Luban 生成的声音 bean 只要实现这个接口，就能被 `SoundManager` 直接消费，不需要反射字段名或做运行时类型猜测。

## 什么时候先看这页

- 你要设计或检查声音表字段。
- 你在排查按名称播放为什么查不到目标行。
- 你要确认哪些字段会直接影响默认播放参数。

## 契约语义

这个接口定义了一条声音行最核心的运行时信息：

- `Name`
- `Desc`
- `AssetLocation`
- `GroupName`
- `Priority`
- `Loop`
- `Volume`
- `SpatialBlend`

其中最重要的是：

- `Name`：运行时主键
- `GroupName`：决定投递到哪个声音组
- `AssetLocation`：决定实际加载哪个 `AudioClip`

## 它如何进入播放链

- `SoundManager` 会把所有 `ITable<ISoundRow>` 扁平化进 `m_SoundRows`
- `PlaySound(name)` 先按 `Name` 找到一行
- 如果调用方没传 `PlaySoundParams`，会用这行的 `Loop / Priority / Volume` 组默认参数

## 设计边界

- 这不是编辑器字段全集，而是运行时真正消费的字段面
- 它不直接包含一次性参数如 `FadeInSeconds`、`Pitch`，那些属于调用时参数对象
- `SpatialBlend` 虽然是运行时字段，但默认参数构造目前并不会直接从这里拼出完整 `PlaySoundParams`

## 风险点 / 易错点

- `Name` 冲突时，后写入缓存的行会覆盖前者。
- `GroupName` 配错时，查表能成功，但真正播放仍会因为声音组不存在而失败。
- 如果生成的 Luban bean 没实现这个接口，运行时就无法进入统一播放链。

## 继续阅读

关键源码：

- [ISoundRow.cs](../../../../Scripts/Runtime/Modules/Sound/Managers/Definitions/ISoundRow.cs)
- [SoundManager.Methods.cs](../../../../Scripts/Runtime/Modules/Sound/Managers/Implements/SoundManager.Methods.cs)

相关文档：

- [SoundManager.md](SoundManager.md)
- [SoundSettings.md](SoundSettings.md)
- [PlaySoundParams.md](PlaySoundParams.md)
