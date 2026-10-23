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
| `ParamsType` | `Type` { get } | Registry 验证方法签名后取得的真实第二参数类型；只有 `PipifyContext` 一个参数时为 null |
| `Method` | `MethodInfo` { get } | 反射调用入口（静态方法） |
| `PipifyStepInfo(id, displayName, category, paramsType, method)` | ctor | 构造记录 |

## §11 使用示例

```csharp
// 只读查询已注册 Step；实际运行仍由既有 Runner 处理参数与生命周期。
PipifyStepInfo found = EditorUtil.Pipify.Registry.FindById("export.config");
if (found != null)
{
    // 当前 export.config 的真实第二参数是 ConfigExportParams，并非无参 Step。
    Type actualParamsType = found.ParamsType;
}
```

`Registry.Rebuild` 先用 `ValidateSignature(method, out paramsType)` 核对 static、返回 UniTask、首参 PipifyContext 和可选第二参 `[Serializable]` class，再将该 `paramsType` 传入构造器；不以 `PipifyStepAttribute.ParamsType` 的声明决定实际类型。按 ID 查找允许返回 null，查询或 MethodInfo 存在不表示 Step 已执行。

## §13 关联文档

- [PipifyStepAttribute.md](./PipifyStepAttribute.md)
- [PipifyContext.md](./PipifyContext.md)
- [IPipifyProgressReporter.md](./IPipifyProgressReporter.md)
- [Editor.md](../../Editor.md)
