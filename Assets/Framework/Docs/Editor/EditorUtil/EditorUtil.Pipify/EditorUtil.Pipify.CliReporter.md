# EditorUtil.Pipify.CliReporter

## §1 文件头

| 项 | 值 |
|---|---|
| 类签名 | `internal sealed class CliReporter : IPipifyProgressReporter` |
| 命名空间 | `NovaFramework.Editor` |
| 宿主类 | `EditorUtil.Pipify`（嵌套） |
| 功能描述 | CLI 宿主进度 Reporter，纯日志输出，ReportStep 恒返回 false |

## §2 文件表

| 文件 | 类 | 说明 |
|---|---|---|
| `EditorUtil.Pipify/EditorUtil.Pipify.CliReporter.cs` | `EditorUtil.Pipify.CliReporter` | 完整实现 |

## §5 公开 API（实现 IPipifyProgressReporter）

| 方法 | 说明 |
|---|---|
| `void BeginBatch(string batchName, int totalSteps)` | 记录 Batch 名和步骤总数；打 Log.Debug `[CLI] Batch 开始` |
| `bool ReportStep(int index, string stepDisplayName, float innerProgress)` | 打 Log.Debug 含进度百分比；**恒返回 false**（CLI 不支持交互取消） |
| `void EndStep(int index, bool success, TimeSpan elapsed, Exception error)` | 成功 Log.Debug，失败 Log.Error |
| `void EndBatch(bool success, TimeSpan totalElapsed)` | 成功 Log.Debug，失败 Log.Error（无进度条可清除） |

### 关键约束

- `ReportStep` 返回值**永远是 false**，调用方不会因此触发取消逻辑
- 所有日志前缀带 `[CLI]` 标记，与 WindowReporter 日志可区分

## §11 使用示例

```csharp
// T8 CLI 入口内部组装
IPipifyProgressReporter reporter = new EditorUtil.Pipify.CliReporter();
await EditorUtil.Pipify.Runner.RunBatchAsync(batch, reporter, overrides, ct);
```

## §13 关联文档

- [IPipifyProgressReporter.md](./IPipifyProgressReporter.md)
- [EditorUtil.Pipify.md](./EditorUtil.Pipify.md)
- [EditorUtil.Pipify.WindowReporter.md](./EditorUtil.Pipify.WindowReporter.md)
