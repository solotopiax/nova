# IVibrateRow

`IVibrateRow` 是振动表数据的运行时契约族。

它不是一个接口，而是一组分层接口：

- `IVibrateRow`
- `IVibrateEmphasisRow`
- `IVibrateCustomRow`

## 什么时候先看这页

- 你要设计振动表字段。
- 你在排查分组播放顺序和参数来源。
- 你要确认 Luban 生成类需要实现哪些接口。

## 契约结构

### 1. 基接口定义分组和排序

`IVibrateRow` 只要求三个最基础字段：

- `Name`
- `Order`
- `PreDuration`

这三项决定：

- 属于哪个分组
- 在组内第几步执行
- 这一步开始前先等待多久

### 2. Emphasis 行扩展强调振动字段

`IVibrateEmphasisRow` 增加：

- `Amplitude`
- `Frequency`
- `Interval`

### 3. Custom 行扩展持续振动字段

`IVibrateCustomRow` 增加：

- `Intensity`
- `Sharpness`
- `Duration`

## 它如何进入运行时

- Manager 按接口类型从表里读取数据
- 以 `Name` 分组
- 组内按 `Order` 升序排序
- 播放时逐步执行每一行

## 风险点 / 易错点

- 如果 Luban 生成类没有实现对应子接口，运行时就无法进入正确分组缓存。
- `Order` 是真正的执行顺序来源，不是装饰字段。
- `PreDuration` 属于“执行前等待”，不是“当前步骤持续时间”。

## 继续阅读

关键源码：

- [IVibrateRow.cs](../../../../Scripts/Runtime/Modules/Vibrate/Managers/Definitions/IVibrateRow.cs)
- [VibrateManager.Methods.cs](../../../../Scripts/Runtime/Modules/Vibrate/Managers/Implements/VibrateManager.Methods.cs)

相关文档：

- [VibrateManager.md](VibrateManager.md)
- [VibrateSettings.md](VibrateSettings.md)
- [VibrateType.md](VibrateType.md)

