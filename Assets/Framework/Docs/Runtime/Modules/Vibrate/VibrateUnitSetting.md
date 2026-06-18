# VibrateUnitSetting

`VibrateUnitSetting` 描述单个振动数据单元的导表与运行时定位信息。

它继承自 `DataTableUnitSettingBase`，但固定采用一套非常明确的模式：

- `DataTableMode.List`
- `IndexField = ""`

## 什么时候先看这页

- 你要理解振动表为什么不是按主键 Map 加载。
- 你要配置或排查某个振动数据资产地址。
- 你在比较它和 `SoundUnitSetting` 的差异。

## 语义重点

### 1. 振动数据按列表读入

和声音数据不同，振动数据单元固定使用 `List` 模式，而不是 `Map` 模式。

原因很直接：运行时最终要做的是“按 `Name` 分组、按 `Order` 排序”，而不是直接按单主键随机访问。

### 2. 分组和排序在运行时二次建立

`VibrateUnitSetting` 自身不携带分组逻辑；它只是告诉系统从哪里加载数据。真正的分组缓存构建发生在 `VibrateManager`。

## 风险点 / 易错点

- 不要把它改成 `Map` 模式，否则会和当前 `IVibrateRow` 的组装路径冲突。
- 它不是播放参数对象，只负责数据单元入口。
- 如果和 `SoundUnitSetting` 混淆，会错误地期待 `Name` 直接成为底层表索引键。

## 继续阅读

关键源码：

- [VibrateUnitSetting.cs](../../../../Scripts/Runtime/Modules/Vibrate/Managers/Definitions/VibrateUnitSetting.cs)

相关文档：

- [VibrateSettings.md](VibrateSettings.md)
- [VibrateManager.md](VibrateManager.md)
- [DataTableUnitSettingBase.md](../../Core/Table/DataTableUnitSettingBase.md)

