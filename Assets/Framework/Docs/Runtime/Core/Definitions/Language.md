# Language / LanguageInfo / LanguageMetadata

**类签名**：`public enum Language : byte` / `public static class LanguageMetadata` / `public readonly struct LanguageInfo`
**命名空间**：`NovaFramework.Runtime`

游戏语言枚举及对应的中文名称（`LanguageMetadata.GetDesc`）、locale 标识码（`LanguageMetadata.GetFlag`）和书写方向（`LanguageMetadata.IsRightToLeft`）查询工具。使用 `Dictionary<Language, LanguageInfo>` 代替旧数组，保证枚举增删时不会导致索引错位。

## § 2 文件表

| 文件 | 类 | 说明 |
|---|---|---|
| `Runtime/Core/Definitions/Language.cs` | `Language`, `LanguageInfo`, `LanguageMetadata` | 语言枚举、信息结构体及元数据静态类 |

## § 3 继承关系

```text
Language        (enum, byte)       — 语言类型标识
LanguageInfo    (readonly struct)  — 中文名称 + BCP 47 locale 码
LanguageMetadata (static class)    — 字典查询工具，Key = Language
```

## § 4 关键字段表

| 字段 | 类型 | 说明 |
|---|---|---|
| `LanguageInfo.Desc` | `readonly string` | 中文语言名称，只能在构造时赋值 |
| `LanguageInfo.Flag` | `readonly string` | BCP 47 locale 标识码，只能在构造时赋值 |
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

public readonly struct LanguageInfo
{
    public readonly string Desc;   // 中文显示名称
    public readonly string Flag;   // BCP 47 locale 标识码
    public LanguageInfo(string desc, string flag);
}

public static class LanguageMetadata
{
    // 获取中文语言名称，未找到返回空字符串
    public static string GetDesc(Language language);

    // 获取 BCP 47 locale 标识码，未找到返回空字符串
    public static string GetFlag(Language language);

    // Arabic、Persian、Hebrew 返回 true，其余语言返回 false
    public static bool IsRightToLeft(Language language);
}
```

`LanguageInfo(string desc, string flag)` 的两个参数都必须显式提供，没有可省略的默认值。`desc` 保存中文显示名称，`flag` 保存 BCP 47 locale 标识码；构造函数直接赋值，不校验 `null`、空字符串或 locale 格式。`default(LanguageInfo)` 的两个字段都是 `null`。构造独立的 `LanguageInfo` 不会修改 `LanguageMetadata` 内部字典。

`GetDesc` 与 `GetFlag` 仅查询静态元数据：未知枚举值返回空字符串，`Unspecified` 分别返回“未指定”和 `unknown`，English 当前标识为 `en-US`。`IsRightToLeft` 仅对 Arabic、Persian、Hebrew 返回 `true`，未知值为 `false`。这些查询不会推断系统语言、切换当前语言、加载字体或执行双向排版；方向标志也不证明字体与目标平台显示正确。

## § 11 使用示例

```csharp
Language lang   = Nova.EditorLanguage;               // 从根组件读取
string name     = LanguageMetadata.GetDesc(lang);    // "简体中文"
string locale   = LanguageMetadata.GetFlag(lang);    // "zh-CN"
bool isRtl      = LanguageMetadata.IsRightToLeft(lang);

LanguageInfo info = new LanguageInfo("简体中文", "zh-CN");
// Desc / Flag 为 readonly 字段，构造后不能重新赋值。
```

## § 10 常见误区

| 误区 | 正确理解 |
|---|---|
| 放到 Localizations 模块下 | `Nova.Visitors.cs` 直接使用 `Language` 字段，放 Localizations 会产生根组件→子模块逆向依赖，正确位置是 `Runtime/Core/Definitions/` |
| 使用旧的 `LanguageTable.Desc[index]` 数组索引方式 | 已重构为 `LanguageMetadata.GetDesc(Language)` 字典查询，防止枚举增删导致的索引错位 |
| 用对象初始化器或后续赋值修改 `LanguageInfo.Desc` / `Flag` | 结构体和字段均为只读，使用 `LanguageInfo(desc, flag)` 构造 |

## § 13 关联文档

- [Definitions.md](Definitions.md)
- [LanguageSelectionWay.md](LanguageSelectionWay.md)（已移除，历史兼容页）
