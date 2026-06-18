# EditorUtil.Pipify.Runner

## §1 文件头

| 项 | 值 |
|---|---|
| 类签名 | `internal static class EditorUtil.Pipify.Runner` |
| 命名空间 | `NovaFramework.Editor` |
| 全局访问 | 仅包内（`EditorUtil.Pipify` 命名空间），由 T7 public 入口间接暴露 |
| 功能描述 | Pipify 纯执行引擎：按 Batch Item 顺序执行，任一步 throw 即中断 |

## §2 文件表

| 文件 | 类 | 说明 |
|---|---|---|
| `EditorUtil.Pipify/EditorUtil.Pipify.Runner.cs` | `EditorUtil.Pipify.Runner` | RunBatchAsync 主循环 |
| `EditorUtil.Pipify/EditorUtil.Pipify.Methods.cs` | `EditorUtil.Pipify` | ApplyOverridesForItem / ConvertOverrideValue 私有工具方法 |

## §5 完整公开 API

### Runner（internal static class）

| 成员 | 签名 | 说明 |
|---|---|---|
| `RunBatchAsync` | `static async UniTask RunBatchAsync(Batch batch, IPipifyProgressReporter reporter, IReadOnlyDictionary<string, string> overrides, CancellationToken ct)` | 顺序执行 Batch 所有 Item；batch/reporter 为 null 抛 `ArgumentNullException`；任一步 throw 即中断 |

### 私有工具方法（Pipify 分部类，Methods.cs）

| 方法 | 说明 |
|---|---|
| `ApplyOverridesForItem(info, itemIndex, paramsInstance, overrides)` | 将 overrides 字典匹配当前 (stepId, itemIndex) 的键值写回 paramsInstance；支持精确索引格式与通配格式 |
| `ConvertOverrideValue(raw, targetType)` | string → string / enum / 数字 / bool 类型转换 |

## §9 关键算法

### RunBatchAsync 执行流程

1. batch / reporter 为 null 抛 `ArgumentNullException`
2. `Stopwatch` 全程计时；`reporter.BeginBatch` 通知开始
3. 按索引遍历 `batch.Items`：
   - `ct.ThrowIfCancellationRequested()` 响应外部取消
   - `Registry.FindById` 查元信息，未命中抛 `InvalidOperationException`
   - 有参 Step：ParamsJson 非空时 `Util.Json.Deserialize(json, type)` 反序列化，否则 `Activator.CreateInstance`；随后 `ApplyOverridesForItem` 覆盖 CLI 参数
   - 构造 `PipifyContext` 并下发
   - `reporter.ReportStep` 返回 true 抛 `OperationCanceledException`
   - 反射调用 `info.Method.Invoke`，强转 `UniTask` 后 `await`
   - `TargetInvocationException` 解包为 `InnerException`，经 `ExceptionDispatchInfo.Capture().Throw()` 保留原始栈后再抛
   - 任何异常先 `reporter.EndStep(false)` 再 throw
4. `finally` 确保 `reporter.EndBatch` 必被调用

### ApplyOverridesForItem Key 匹配优先级

精确格式 `stepId[itemIndex].fieldName` > 通配格式 `stepId.fieldName`（key 不含 `[`）。字段不存在抛 `InvalidOperationException`。

### ConvertOverrideValue 类型转换顺序

`string` → enum（`Enum.Parse` 大小写敏感）→ `Convert.ChangeType`（数字 / bool）。

## §11 使用示例

Runner 为 internal，外部通过 T7 public 入口调用（落地后示例）：

```csharp
// （T7 public 入口落地后）
using var cts = new CancellationTokenSource();
await EditorUtil.Pipify.RunBatchAsync(batch, reporter, overrides: null, cts.Token);
```

包内直接调用（如 T7 实现内部）：

```csharp
await EditorUtil.Pipify.Runner.RunBatchAsync(batch, reporter, overrides, ct);
```

## §13 关联文档

- [EditorUtil.Pipify.md](./EditorUtil.Pipify.md)
- [IPipifyProgressReporter.md](./IPipifyProgressReporter.md)
- [PipifyContext.md](./PipifyContext.md)
- [Batch.md](./Batch.md)
- [BatchItem.md](./BatchItem.md)
