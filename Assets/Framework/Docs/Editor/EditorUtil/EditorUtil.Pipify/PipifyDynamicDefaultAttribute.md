# PipifyDynamicDefaultAttribute

## §1 文件头

| 项 | 值 |
|---|---|
| 类签名 | `[AttributeUsage(Field)] public sealed class PipifyDynamicDefaultAttribute : Attribute` |
| 命名空间 | `NovaFramework.Editor` |
| 全局访问 | `PipifyDynamicDefaultAttribute` |
| 功能描述 | 标注 string 字段：值为空时显示动态默认值占位（不写回字段） |

## §2 文件表

| 文件 | 类 | 说明 |
|---|---|---|
| `EditorUtil.Pipify/Definitions/PipifyDynamicDefaultAttribute.cs` | `PipifyDynamicDefaultAttribute` | 唯一实现 |

## §5 完整公开 API

| 成员 | 类型 | 说明 |
|---|---|---|
| `ProviderType` | `Type` | 默认值提供者所在类型 |
| `MethodName` | `string` | 无参 static 方法名（返回 string） |
| `PipifyDynamicDefaultAttribute(Type providerType, string methodName)` | 构造器 | 设定提供者方法 |

PipifyWindow 解析规则：仅作用于 `string` 字段；当字段值为 null/空串时，每帧调用 `ProviderType.MethodName()`（无参 static，公私皆可）取占位文本绘制在文本框上方；用户聚焦或键入即覆盖占位、写回字段。占位仅做显示，不会被持久化为字段值。调用异常不阻断流水线，仅 `Log.Warning`。

## §11 使用示例

```csharp
[PipifyDynamicDefault(typeof(EditorUtil.BundleBuilder), nameof(EditorUtil.BundleBuilder.GetDefaultPackageVersion))]
public string BuildVersion = string.Empty;
```

## §13 关联文档

- [EditorUtil.Pipify.md](./EditorUtil.Pipify.md)
- [PipifySteps.md](./PipifySteps.md)
