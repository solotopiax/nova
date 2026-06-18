# PipifyDropdownAttribute

## §1 文件头

| 项 | 值 |
|---|---|
| 类签名 | `[AttributeUsage(Field)] public sealed class PipifyDropdownAttribute : Attribute` |
| 命名空间 | `NovaFramework.Editor` |
| 全局访问 | `PipifyDropdownAttribute` |
| 功能描述 | 标注 string 字段以接口实现类下拉框形式编辑（存储 FullName） |

## §2 文件表

| 文件 | 类 | 说明 |
|---|---|---|
| `EditorUtil.Pipify/Definitions/PipifyDropdownAttribute.cs` | `PipifyDropdownAttribute` | 唯一实现 |

## §5 完整公开 API

| 成员 | 类型 | 说明 |
|---|---|---|
| `InterfaceType` | `Type` | 用于收集实现类型的接口或基类 |
| `PipifyDropdownAttribute(Type interfaceType)` | 构造器 | 设定接口类型 |

PipifyWindow 解析规则：仅作用于 `string` 字段；下拉项 = `EditorUtil.TypeCache.GetTypeNames(InterfaceType)` 升序集合，首项固定 `(未配置 → Default)` 对应空串。

## §11 使用示例

```csharp
[PipifyDropdown(typeof(IBundleEncryptor))]
public string BundleEncryptorClassName = string.Empty;
```

## §13 关联文档

- [EditorUtil.Pipify.md](./EditorUtil.Pipify.md)
- [PipifySteps.md](./PipifySteps.md)
