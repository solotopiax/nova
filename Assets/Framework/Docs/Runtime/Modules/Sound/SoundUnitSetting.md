# SoundUnitSetting

`SoundUnitSetting` 描述单个声音数据单元的导表与运行时定位信息。

它继承自 `DataTableUnitSettingBase`，但固定采用：

- `DataTableMode.Map`
- `IndexField = "Name"`

## 什么时候先看这页

- 你要配置某个声音数据资产的位置。
- 你在比较它和 `VibrateUnitSetting` 的模式差异。
- 你要确认为什么声音表能直接按 `Name` 做运行时主键。

## 语义重点

### 1. 声音数据按 Map 模式加载

这和振动模块不同。声音数据天然适合按 `Name` 直接索引，所以单元设置固定是 `Map`。

### 2. 它只描述“数据单元入口”

这个类不定义组名、优先级、循环等播放语义；那些属于 `ISoundRow`。

### 3. 运行时真正关键的是 `AssetLocation`

Manager 会根据每个单元的 `AssetLocation` 读取数据资源，再反射构建声音表。

## 风险点 / 易错点

- 如果把它改成非 `Map` 模式，会直接冲击 `SoundManager` 当前按 `Name` 扁平缓存的假设。
- 它不是播放配置对象，也不是声音资源本体。
- 和 `SoundSettings` 一样，编辑器导出相关字段不应被误写成运行时播放参数。

## 继续阅读

关键源码：

- [SoundSettings.cs](../../../../Scripts/Runtime/Modules/Sound/Definitions/SoundSettings.cs)

相关文档：

- [SoundSettings.md](SoundSettings.md)
- [ISoundRow.md](ISoundRow.md)
- [SoundManager.md](SoundManager.md)
- [IDataTableUnitSetting.md](../../Core/Table/IDataTableUnitSetting.md)

