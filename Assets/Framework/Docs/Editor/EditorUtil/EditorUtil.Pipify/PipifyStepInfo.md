# PipifyStepInfo

## §1 文件头

| 项 | 值 |
|---|---|
| 类签名 | `public sealed class PipifyStepInfo` |
| 命名空间 | `NovaFramework.Editor` |
| 功能描述 | PipifyRegistry 中一个 Step 的运行时元信息记录，供 Runner 反射调用 |

## §2 文件表

| 文件 | 类 | 说明 |
|---|---|---|
| `EditorUtil.Pipify/Definitions/PipifyStepInfo.cs` | `PipifyStepInfo` | 完整实现 |

## §5 公开 API

| 成员 | 类型 | 说明 |
|---|---|---|
| `Id` | `string` { get } | 稳定 Id |
| `DisplayName` | `string` { get } | UI 展示名 |
| `Category` | `string` { get } | UI 分组名 |
| `ParamsType` | `Type` { get } | 参数类型，null 表示无参 |
| `Method` | `MethodInfo` { get } | 反射调用入口（静态方法） |
| `PipifyStepInfo(id, displayName, category, paramsType, method)` | ctor | 构造记录 |

## §11 使用示例

```csharp
// PipifyRegistry 扫描后缓存
var info = new PipifyStepInfo(attr.Id, attr.DisplayName, attr.Category, attr.ParamsType, method);

// Runner 通过 Id 查找后调用
var found = registry.FindById("export_config");
found.Method.Invoke(null, new object[] { ctx });
```

## §13 关联文档

- [PipifyStepAttribute.md](./PipifyStepAttribute.md)
- [PipifyContext.md](./PipifyContext.md)
- [IPipifyProgressReporter.md](./IPipifyProgressReporter.md)
- [Editor.md](../../Editor.md)
