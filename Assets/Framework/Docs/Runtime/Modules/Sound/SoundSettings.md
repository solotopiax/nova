# SoundSettings

`SoundSettings` 是声音数据源的配置容器。

它同时服务两条链路：

- 编辑器侧：告诉导表工具数据源目录和模板位置
- 运行时侧：把 `SoundUnitSetting` 列表交给 `SoundManager`

## 什么时候先看这页

- 你要改声音表的数据来源。
- 你要理解 `SoundComponent` 里那个 `m_Settings` 到底承载什么。
- 你在排查声音表加载为空，想确认是不是单元设置列表没配对。

## 结构语义

### 1. 它直接实现了 `IDataTableSettings`

这说明它不是“纯 Inspector 容器”，而是标准数据表设置对象的一种具体实现。

### 2. 运行时真正关键的是 `SoundUnitsSettings`

每个 `SoundUnitSetting` 都代表一个独立数据单元，包含：

- 导出位置相关信息
- 运行时 `AssetLocation`

`SoundManager` 最终只消费这份列表。

### 3. 编辑器字段只在 `UNITY_EDITOR` 下存在

- `SourceDirPath`
- `TemplatePath`

这两项不应被当成运行时必备字段。

## 数据模型特征

- 声音表单元固定使用 `Map` 模式
- 索引字段固定是 `Name`

这也是为什么运行时能把所有数据扁平化成 `Name -> ISoundRow`。

## 风险点 / 易错点

- 如果 `SoundUnitsSettings` 为空，声音加载会被直接跳过。
- 重复的 `Name` 主键会在扁平缓存阶段互相覆盖，因此多表分配时要提前规避主键冲突。
- 编辑器模板路径不是运行时配置，不要把它写进系统启动说明里。

## 继续阅读

关键源码：

- [SoundSettings.cs](../../../../Scripts/Runtime/Modules/Sound/Definitions/SoundSettings.cs)

相关文档：

- [SoundComponent.md](SoundComponent.md)
- [SoundManager.md](SoundManager.md)
- [SoundUnitSetting.md](SoundUnitSetting.md)
- [IDataTableSettings.md](../../Core/Table/IDataTableSettings.md)

