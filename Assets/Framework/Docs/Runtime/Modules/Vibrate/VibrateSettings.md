# VibrateSettings

`VibrateSettings` 是振动数据源的总配置容器。

它和 `SoundSettings` 不同：并不直接实现 `IDataTableSettings`，而是内部维护两套区域数据，再通过适配器按需转换成标准数据表设置。

## 什么时候先看这页

- 你要理解为什么振动配置不是一张统一表。
- 你要确认编辑器侧和运行时侧分别从这里取什么。
- 你在排查 Emphasis / Custom 某一路数据没有加载进来。

## 结构语义

### 1. 它天然分成两个区域

- `EmphasisUnitsSettings`
- `CustomUnitsSettings`

这是模块设计本身的一部分，不是临时实现技巧。

### 2. 编辑器路径也是双区域

在 `UNITY_EDITOR` 下分别有：

- `EmphasisSourceDirPath`
- `CustomSourceDirPath`

这说明两类数据从源头上就是独立维护的。

### 3. 运行时通过适配器暴露标准接口

`GetEmphasisAsSettings()` / `GetCustomAsSettings()` 会返回 `VibrateAreaSettingsAdapter`，把单个区域包装成 `IDataTableSettings`。

这样做的意义是：

- 保留振动模块自己的双区域语义
- 同时复用通用数据表工具链

## 风险点 / 易错点

- 它不是 `IDataTableSettings` 本体；文档如果写成“直接实现”就是错的。
- 把 Emphasis / Custom 配成同一组数据，不符合当前运行时分流设计。
- 适配器只用于传参，不参与序列化；不要把它当持久对象来理解。

## 继续阅读

关键源码：

- [VibrateSettings.cs](../../../../Scripts/Runtime/Modules/Vibrate/Managers/Definitions/VibrateSettings.cs)

相关文档：

- [VibrateComponent.md](VibrateComponent.md)
- [VibrateManager.md](VibrateManager.md)
- [VibrateManagerConfig.md](VibrateManagerConfig.md)
- [VibrateUnitSetting.md](VibrateUnitSetting.md)
- [IDataTableSettings.md](../../Core/Table/IDataTableSettings.md)

