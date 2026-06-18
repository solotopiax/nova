# IPipifyProgressReporter

## §1 文件头

| 项 | 值 |
|---|---|
| 类签名 | `public interface IPipifyProgressReporter` |
| 命名空间 | `NovaFramework.Editor` |
| 功能描述 | Pipify 执行进度汇报接口，Window 实现为模态进度条，CLI 实现为纯日志 |

## §2 文件表

| 文件 | 类 | 说明 |
|---|---|---|
| `EditorUtil.Pipify/Definitions/IPipifyProgressReporter.cs` | `IPipifyProgressReporter` | 接口定义 |

## §5 公开 API

| 方法 | 说明 |
|---|---|
| `void BeginBatch(string batchName, int totalSteps)` | Batch 开始 |
| `bool ReportStep(int index, string stepDisplayName, float innerProgress)` | Step 进度汇报；返回 true 表示请求取消（CLI 恒返回 false） |
| `void EndStep(int index, bool success, TimeSpan elapsed, Exception error)` | Step 结束；error 失败时非 null |
| `void EndBatch(bool success, TimeSpan totalElapsed)` | Batch 结束 |

## §11 使用示例

```csharp
// Window 实现（T6 WindowReporter）
public sealed class WindowReporter : IPipifyProgressReporter
{
    public void BeginBatch(string batchName, int totalSteps) => EditorUtility.DisplayProgressBar(batchName, "", 0f);

    public bool ReportStep(int index, string stepDisplayName, float innerProgress)
    {
        float overall = (index + innerProgress) / _totalSteps;
        return EditorUtility.DisplayCancelableProgressBar(_batchName, stepDisplayName, overall);
    }

    public void EndStep(int index, bool success, TimeSpan elapsed, Exception error)
    {
        if (!success) Log.Error(error.ToString());
    }

    public void EndBatch(bool success, TimeSpan totalElapsed) => EditorUtility.ClearProgressBar();
}

// CLI 实现（T6 CliReporter）：ReportStep 恒返回 false，各方法委托 Log.Debug / Log.Error
```

## §13 关联文档

- [PipifyStepAttribute.md](./PipifyStepAttribute.md)
- [PipifyStepInfo.md](./PipifyStepInfo.md)
- [PipifyContext.md](./PipifyContext.md)
- [Editor.md](../../Editor.md)
