# Language / LanguageInfo / LanguageMetadata

**类签名**：`public enum Language : byte` / `public static class LanguageMetadata` / `public struct LanguageInfo`
**命名空间**：`NovaFramework.Runtime`

游戏语言枚举及对应的中文名称（`LanguageMetadata.GetDesc`）与 locale 标识码（`LanguageMetadata.GetFlag`）查询工具。使用 `Dictionary<Language, LanguageInfo>` 代替旧数组，保证枚举增删时不会导致索引错位。

## § 2 文件表

| 文件 | 类 | 说明 |
|---|---|---|
| `Runtime/Core/Definitions/Language.cs` | `Language`, `LanguageInfo`, `LanguageMetadata` | 语言枚举、信息结构体及元数据静态类 |

## § 3 继承关系

```text
Language        (enum, byte)       — 语言类型标识
LanguageInfo    (struct)           — 中文名称 + BCP 47 locale 码
LanguageMetadata (static class)    — 字典查询工具，Key = Language
```

## § 4 关键字段表

| 字段 | 类型 | 说明 |
|---|---|---|
| `LanguageInfo.Desc` | `string` | 中文语言名称 |
| `LanguageInfo.Flag` | `string` | BCP 47 locale 标识码 |
| `s_LanguageInfos` | `private static readonly Dictionary<Language, LanguageInfo>` | 语言信息字典（含全部枚举成员） |

## § 5 完整公开 API

```csharp
public enum Language : byte
{
    Unspecified = 0,
    Afrikaans, Albanian, Arabic, Basque, Belarusian, Bulgarian, Catalan,
    ChineseSimplified, ChineseTraditional, Croatian, Czech, Danish, Dutch,
    English, Estonian, Faroese, Finnish, French, Georgian, German, Greek,
    Hebrew, Hungarian, Icelandic, Indonesian, Italian, Japanese, Korean,
    Latvian, Lithuanian, Macedonian, Malayalam, Norwegian, Persian, Polish,
    PortugueseBrazil, PortuguesePortugal, Romanian, Russian, SerboCroatian,
    SerbianCyrillic, SerbianLatin, Slovak, Slovenian, Spanish, Swedish,
    Thai, Turkish, Ukrainian, Vietnamese, Hindi, Malay, Filipino
}

public struct LanguageInfo
{
    public string Desc;   // 中文显示名称
    public string Flag;   // BCP 47 locale 标识码
}

public static class LanguageMetadata
{
    // 获取中文语言名称，未找到返回空字符串
    public static string GetDesc(Language language);

    // 获取 BCP 47 locale 标识码，未找到返回空字符串
    public static string GetFlag(Language language);
}
```

## § 11 使用示例

```csharp
Language lang   = Nova.EditorLanguage;               // 从根组件读取
string name     = LanguageMetadata.GetDesc(lang);    // "简体中文"
string locale   = LanguageMetadata.GetFlag(lang);    // "zh-CN"
```

## § 10 常见误区

| 误区 | 正确理解 |
|---|---|
| 放到 Localizations 模块下 | `Nova.Visitors.cs` 直接使用 `Language` 字段，放 Localizations 会产生根组件→子模块逆向依赖，正确位置是 `Runtime/Core/Definitions/` |
| 使用旧的 `LanguageTable.Desc[index]` 数组索引方式 | 已重构为 `LanguageMetadata.GetDesc(Language)` 字典查询，防止枚举增删导致的索引错位 |

## § 13 关联文档

- [Definitions.md](Definitions.md)
- [LanguageSelectionWay.md](LanguageSelectionWay.md)（已移除，历史兼容页）
