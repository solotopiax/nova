# PipifyContext

## §1 文件头

| 项 | 值 |
|---|---|
| 类签名 | `public sealed class PipifyContext` |
| 命名空间 | `NovaFramework.Editor` |
| 功能描述 | Runner 下行给每个 Step 的运行时上下文，Step 只读取不修改 |

## §2 文件表

| 文件 | 类 | 说明 |
|---|---|---|
| `EditorUtil.Pipify/Definitions/PipifyContext.cs` | `PipifyContext` | 完整实现 |

## §5 公开 API

| 成员 | 类型 | 说明 |
|---|---|---|
| `BatchName` | `string` { get; internal set; } | 当前执行的 Batch 名称 |
| `CurrentStepIndex` | `int` { get; internal set; } | 当前 Step 在 Batch 中的索引（从 0 起） |
| `TotalSteps` | `int` { get; internal set; } | 当前 Batch 的步骤总数 |
| `Reporter` | `IPipifyProgressReporter` { get; internal set; } | 进度汇报接口（Window/CLI 各一实现） |
| `CancellationToken` | `CancellationToken` { get; internal set; } | 取消令牌，供长耗时 Step 内部响应取消 |

所有 setter 均为 `internal`，Step 实现侧不得修改上下文字段；Runner 在每次调用前赋值。

内部 `BuildState` 在同一 Batch 共享，Batch 之间独立，不属于新增公开 API。`build.package` 开始时清空旧产物，明确成功后记录报告路径、平台、DevelopmentBuild 与构建时包名，供 `googleplay.upload_aab` 使用。

窗口上传尚未开始时可将构建身份交接到同一次 Editor 会话重载后的 Runner；以原存档指纹、平台和产物文件指纹校验，不从当前 PlayerSettings 猜包名，不跨 Batch 复用。

## §11 使用示例

```csharp
// Step 实现内部使用 ctx
[PipifyStep("bundlebuilder.build", "Bundles", "构建资源")]
public static async UniTask BuildBundlesAsync(PipifyContext ctx)
{
    ctx.Reporter.ReportStep(ctx.CurrentStepIndex, "Bundles", 0f);
    // 响应取消
    ctx.CancellationToken.ThrowIfCancellationRequested();
    await DoHeavyWorkAsync(ctx.CancellationToken);
}
```

## §13 关联文档

- [PipifyStepAttribute.md](./PipifyStepAttribute.md)
- [PipifyStepInfo.md](./PipifyStepInfo.md)
- [IPipifyProgressReporter.md](./IPipifyProgressReporter.md)
- [Editor.md](../../Editor.md)
