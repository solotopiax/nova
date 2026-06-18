# LocalizationSettings

`LocalizationSettings` 是 `LocalizationComponent` 持有的序列化设置对象。

它管理的是“本地化数据源怎么组织”，而不是运行时语言切换本身。

## 什么时候先看这页

优先看这页的场景：

- 你要确认文本和字体数据源分别怎么配置。
- 你要排查为什么某个 Excel 没进入本地化构建链。
- 你要看 `LocalizationComponent` 是怎样把 Inspector 配置传给 Manager 的。

## 结构定位

### 1. `LocalizationSettings`

它本身只做两件事：

- 保存文本数据单元列表 `TextUnitsSettings`
- 保存字体数据单元列表 `FontUnitsSettings`

在 Editor 下还会额外记录：

- `TextSourceDirPath`
- `FontSourceDirPath`

### 2. `LocalizationTextUnitSetting`

文本单元的固定规则是：

- `DataTableMode.Map`
- `IndexField = "Name"`
- Editor 下 `GetLubanInputPath()` 使用 `_temp/` 前缀

这说明文本本地化表按键名聚合，适合切语言后重建整表。

### 3. `LocalizationFontUnitSetting`

字体单元的固定规则是：

- `DataTableMode.List`
- 不使用索引字段

这符合“一个语言对应多条字体配置”的读取方式。

## 调用方可依赖的边界

- 这层负责描述数据源组织方式，不负责语言解析或文本切换。
- `LocalizationComponent.Start()` 会把这里的两组单元列表塞进 `LocalizationManagerConfig`。

## 风险点 / 易错点

- 文本单元和字体单元是两条不同的数据链，不要把它们当成同一张表的两种视图。
- 文本单元固定走 `Map` 模式；如果底层生成链改成别的模式，会直接影响文本查找与扁平化逻辑。
- Editor 下的数据源目录字段只服务编辑期，不是运行时加载地址。

## 继续阅读

关键源码：

- [LocalizationSettings.cs](../../../../Scripts/Runtime/Modules/Localization/Managers/Definitions/LocalizationSettings.cs)

相关文档：

- [LocalizationComponent.md](LocalizationComponent.md)
- [LocalizationManagerConfig.md](LocalizationManagerConfig.md)
- [LocalizationManager.md](LocalizationManager.md)
