# ILocalizationTextRow

`ILocalizationTextRow` 是运行时文本数据行契约。

Luban 文本行实现这个接口后，`LocalizationManager` 就能通过 `ITable<ILocalizationTextRow>` 直接扁平化文本表，保持 `GetText()` 的高频查找能力。

## 契约定位

它定义的是文本本地化最小字段集：

- `Name`
- `Value`

这也是运行时字符串查找真正依赖的两个字段。

## 调用方可依赖的语义

### 1. `Name`

表示本地化键。

`LocalizationManager.FlattenToLanguageTexts()` 会把它写进 `m_LanguageTexts` 字典，作为 `GetText(name)` 的查找键。

### 2. `Value`

表示当前语言下的文本值。

扁平化时如果值为 `null`，运行时会写成空字符串。

## 风险点 / 易错点

- 这个接口只服务运行时读取，不等于文本表的全部业务字段定义。
- `Name` 为空时，该行不会对运行时快速查找产生有效贡献。
- 如果文本行不实现这个接口，`LocalizationManager` 就无法把对应表扁平化进 `GetText()` 快速层。

## 继续阅读

关键源码：

- [ILocalizationTextRow.cs](../../../../Scripts/Runtime/Modules/Localization/Managers/Definitions/ILocalizationTextRow.cs)

相关文档：

- [LocalizationManager.md](LocalizationManager.md)
- [TextLocalizing.md](TextLocalizing.md)
- [../../Core/Table/ITable.md](../../Core/Table/ITable.md)
