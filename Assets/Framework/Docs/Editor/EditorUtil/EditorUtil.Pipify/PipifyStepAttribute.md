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
| `ParamsType` | `Type` { get; set; } | 保留的可选声明属性；Registry 实际类型从方法第二参数取得，不以该值决定有参或无参 |
| `PipifyStepAttribute(id, displayName, category)` | ctor | 构造标注特性 |

## §11 使用示例

```csharp
// 当前真实带参 Step 的声明；完整执行逻辑位于对应 PipifySteps 源码。
[PipifyStep("export.config", "导出 Config 资源", "导出资源/Config", ParamsType = typeof(ConfigExportParams))]
internal static UniTask RunExportConfig(PipifyContext ctx, ConfigExportParams parameters)
{
    // ...
}

// 当前 Bundle Step 同样以实际第二参数为准。
[PipifyStep("bundlebuilder.build", "Bundles", "构建资源", ParamsType = typeof(AssetBundleBuildArgs))]
internal static UniTask RunBuildAssetBundle(PipifyContext ctx, AssetBundleBuildArgs p)
{
    // ...
}
```

Registry 的 `ValidateSignature` 只接受 static + UniTask + 首参 PipifyContext；无第二参时实际 `PipifyStepInfo.ParamsType=null`，有第二参时必须为 `[Serializable]` class。特性的 `ParamsType` 省略或与签名不同不会改变这个推导，不能仅扫描命名参数判断当前 Step 是否带参。示例声明与源码扫描不代替本次 Unity TypeCache 注册、配置保存或运行验证。

## §13 关联文档

- [PipifyStepInfo.md](./PipifyStepInfo.md)
- [PipifyContext.md](./PipifyContext.md)
- [IPipifyProgressReporter.md](./IPipifyProgressReporter.md)
- [Editor.md](../../Editor.md)
