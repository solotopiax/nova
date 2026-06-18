# PipifyVisibleWhenAttribute

## §1 文件头

| 项 | 值 |
|---|---|
| 类签名 | `[AttributeUsage(Field)] public sealed class PipifyVisibleWhenAttribute : Attribute` |
| 命名空间 | `NovaFramework.Editor` |
| 全局访问 | `PipifyVisibleWhenAttribute` |
| 功能描述 | 字段显隐特性：依赖另一字段当前整型化值匹配 AnyOf 时才显示 |

## §2 文件表

| 文件 | 类 | 说明 |
|---|---|---|
| `EditorUtil.Pipify/Definitions/PipifyVisibleWhenAttribute.cs` | `PipifyVisibleWhenAttribute` | 唯一实现 |

## §5 完整公开 API

| 成员 | 类型 | 说明 |
|---|---|---|
| `DependsOn` | `string` | 同参数类内的依赖字段名 |
| `AnyOf` | `int[]` | 触发显示的整型化取值集合 |
| `PipifyVisibleWhenAttribute(string dependsOn, params int[] anyOf)` | 构造器 | 设定依赖与可见值集合 |

PipifyWindow 解析规则：每帧 reflectively 取依赖字段值，`Convert.ToInt32(depValue)` 命中 `AnyOf` 任一项才绘制；隐藏字段同时不计入参数区高度。仅支持枚举/整数依赖字段；非 int 兼容类型（如字符串）异常时返回 true（即不隐藏）。

## §11 使用示例

```csharp
[PipifyVisibleWhen(nameof(BundledCopyOption),
    (int)EBundledCopyOption.ClearAndCopyByTags,
    (int)EBundledCopyOption.OnlyCopyByTags)]
public string BundledCopyParams = string.Empty;
```

## §13 关联文档

- [EditorUtil.Pipify.md](./EditorUtil.Pipify.md)
- [PipifySteps.md](./PipifySteps.md)
