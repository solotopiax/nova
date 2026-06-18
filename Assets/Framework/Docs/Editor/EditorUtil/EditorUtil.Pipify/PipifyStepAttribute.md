# PipifyStepAttribute

## §1 文件头

| 项 | 值 |
|---|---|
| 类签名 | `public sealed class PipifyStepAttribute : Attribute` |
| 命名空间 | `NovaFramework.Editor` |
| 功能描述 | 标记静态方法为 Pipify Step，由 PipifyRegistry 通过 TypeCache 扫描注册 |

## §2 文件表

| 文件 | 类 | 说明 |
|---|---|---|
| `EditorUtil.Pipify/Definitions/PipifyStepAttribute.cs` | `PipifyStepAttribute` | 完整实现 |

## §5 公开 API

| 成员 | 类型 | 说明 |
|---|---|---|
| `Id` | `string` { get } | 稳定 Id，重命名 DisplayName 不影响存档；在整个工程中必须唯一 |
| `DisplayName` | `string` { get } | UI 展示名 |
| `Category` | `string` { get } | UI 分组名（如 "HybridCLR" / "导出" / "打包"） |
| `ParamsType` | `Type` { get; set; } | 参数类型（[Serializable] class），null 表示无参；通过命名参数赋值 |
| `PipifyStepAttribute(id, displayName, category)` | ctor | 构造标注特性 |

## §11 使用示例

```csharp
// 无参 Step
[PipifyStep("export_config", "导出配置", "导出")]
public static async UniTask ExportConfigAsync(PipifyContext ctx)
{
    // ...
}

// 带参 Step
[PipifyStep("build_ab", "AB 构建", "打包", ParamsType = typeof(BuildAbParams))]
public static async UniTask BuildAbAsync(PipifyContext ctx, BuildAbParams p)
{
    // ...
}
```

## §13 关联文档

- [PipifyStepInfo.md](./PipifyStepInfo.md)
- [PipifyContext.md](./PipifyContext.md)
- [IPipifyProgressReporter.md](./IPipifyProgressReporter.md)
- [Editor.md](../../Editor.md)
