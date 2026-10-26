# ILocalizationFontRow

`ILocalizationFontRow` 是运行时字体数据行契约。

Luban 生成的字体表行只要实现这个接口，`LocalizationManager` 和 `TextLocalizing` 就能在不走反射的前提下直接读取字体配置。

## 契约定位

它定义的是“运行时字体适配最少需要哪些字段”，不是某个具体 JSON 或 Excel 的完整格式说明。

直接依赖它的通常是：

- `LocalizationManager`
- `TextLocalizing`
- Luban 生成的字体行类型

## 调用方可依赖的语义

### 1. `Language`

表示这条字体配置属于哪个语言。

`LocalizationManager` 会把它解析成 `Language` 枚举并归组到 `m_FontDatas`。

### 2. `Mark`

表示字体标记。

`Nova.Localization.GetFontData(mark)` 按当前语言和 `Mark` 查询字体配置，显式语言重载为 `GetFontData(language, mark)`；`TextLocalizing` 复用同一个查询。它们返回首个 Ordinal 精确匹配的原始配置行，空标记或未命中返回 `null`，不自动回退或加载字体资源。字体表没有通用 `Name` 字段。

### 3. `AssetLocation` / `MaterialName`

- `AssetLocation`：字体资源地址
- `MaterialName`：可选的字体材质地址

两者共同决定字体切换后实际加载什么资源。

### 4. `FontSizeScaleRatio`

用于在切语言后按比例缩放 TMP 字号。

这不是排版建议值，而是运行时直接参与字号调整的输入。

## 风险点 / 易错点

- `Language` 必须能解析成 `Language` 枚举，否则该行会被跳过。
- `Mark` 是 `TextLocalizing` 精确匹配的关键字段，不能随意改语义。
- `MaterialName` 为空时跳过自定义材质加载；当前实现没有显式释放或重置此前的自定义材质，不能承诺空值一定恢复默认材质。

## 继续阅读

关键源码：

- [ILocalizationFontRow.cs](../../../../Scripts/Runtime/Modules/Localization/Managers/Definitions/ILocalizationFontRow.cs)

相关文档：

- [LocalizationManager.md](LocalizationManager.md)
- [TextLocalizing.md](TextLocalizing.md)
- [LocalizationFontData.md](LocalizationFontData.md)

接口没有属性默认值或范围校验。Mark 使用 Ordinal 精确匹配首项，空 Mark 不加载；FontSizeScaleRatio 直接乘首次缓存的字号，不累计乘法，也不拒绝负值/NaN。Language 的 TryParse 不包含 IsDefined 校验。表行存在与正式导出通过，不代表字体/材质地址、资源类型、字符覆盖或实际排版已经验收。
