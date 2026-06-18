# LocalizationFontData

`LocalizationFontData` 是旧式字体配置 DTO。

当前运行时主链已经转向 `ILocalizationFontRow` + Luban 字体表；这个类型更多保留给编辑器工具和兼容性读取场景，而不是 `LocalizationManager` 当前的主存储结构。

## 什么时候先看这页

优先看这页的场景：

- 你在看编辑器工具如何读取或校验字体配置 JSON。
- 你要区分“旧 DTO”与“当前运行时接口行”的边界。
- 你在追查 `TextLocalizingInspector` 一类编辑器逻辑为什么仍引用这个类型。

## 当前定位

### 1. 运行时主链不再以它为核心

当前运行时里：

- `LocalizationManager` 持有的是 `IReadOnlyList<ILocalizationFontRow>`
- `TextLocalizing` 消费的也是 `ILocalizationFontRow`

所以不要把它当成当前运行时字体适配的主契约。

### 2. 它仍然描述了一条完整字体配置

字段包括：

- `Language`
- `Mark`
- `AssetLocation`
- `MaterialName`
- `FontSizeScaleRatio`

这使它仍然适合做编辑器侧解析、预览或兼容转换。

## 风险点 / 易错点

- 如果把它误当作运行时主契约，就会和 `ILocalizationFontRow` 的真实链路混淆。
- `FontSizeScaleRatio` 默认值是 `1.0f`，不是自动推导值。
- `Language` 只是字符串；是否能用于运行时归组，仍取决于能否解析成 `Language` 枚举。

## 继续阅读

关键源码：

- [LocalizationFontData.cs](../../../../Scripts/Runtime/Modules/Localization/Managers/Definitions/LocalizationFontData.cs)

相关文档：

- [ILocalizationFontRow.md](ILocalizationFontRow.md)
- [LocalizationManager.md](LocalizationManager.md)
- [TextLocalizing.md](TextLocalizing.md)
