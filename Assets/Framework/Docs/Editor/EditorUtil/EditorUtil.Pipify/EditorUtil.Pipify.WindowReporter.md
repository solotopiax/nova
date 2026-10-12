# EditorUtil.Pipify.WindowReporter

## §1 文件头

| 项 | 值 |
|---|---|
| 类签名 | `internal sealed class WindowReporter : IPipifyProgressReporter` |
| 命名空间 | `NovaFramework.Editor` |
| 宿主类 | `EditorUtil.Pipify`（嵌套） |
| 功能描述 | Window 宿主进度 Reporter：模态进度条 + Batch 末尾通过宿主窗口 ShowNotification 弹结果通知 |

## §2 文件表

| 文件 | 类 | 说明 |
|---|---|---|
| `EditorUtil.Pipify/EditorUtil.Pipify.WindowReporter.cs` | `EditorUtil.Pipify.WindowReporter` | 完整实现 |

## §5 公开 API（实现 IPipifyProgressReporter）

| 方法 | 说明 |
|---|---|
| `WindowReporter(EditorWindow host)` | 构造时绑定宿主窗口；host 为 null 时 EndBatch 不弹通知，仅写日志 |
| `void BeginBatch(string batchName, int totalSteps)` | 记录 Batch 名和步骤总数；注册重载与退出收尾；打 Log.Debug |
| `bool ReportStep(int index, string stepDisplayName, float innerProgress)` | 调用 `DisplayCancelableProgressBar`；返回用户是否点击取消 |
| `void EndStep(int index, bool success, TimeSpan elapsed, Exception error)` | 成功 Log.Debug，失败 Log.Error |
| `void EndBatch(bool success, TimeSpan totalElapsed)` | 调用 `ClearProgressBar`；成功 Log.Debug，失败 Log.Error；最后通过宿主窗口 `ShowNotification("{BatchName}  ✓/✗  {Elapsed:0.0}s")` 弹右下角浮窗 |

### 进度计算

```
outer = (index + Mathf.Clamp01(innerProgress)) / totalSteps
```

模态进度条标题格式：`"{BatchName} - {StepName}"`，副标题：`"第 {i+1}/{total} 步：{StepName}"`

进度条不冻结 Editor 或阻止程序集重载。异步上传期间 Unity 仍可更新，能操作界面不能单独证明上传失败。正常结束时注销生命周期事件；重载或退出时立即清除进度。窗口上传前检查点会在重载后继续，其余重载中断记录 Batch / 步骤并在新域再次打印；已启动的 Google Play 上传提示远端状态需要核对。收尾幂等，中断后不会再显示成功或重新出现进度条。

## §11 使用示例

```csharp
// public 入口内部组装：调用方传入宿主窗口，结束通知由宿主 ShowNotification 承载
IPipifyProgressReporter reporter = new EditorUtil.Pipify.WindowReporter(hostWindow);
await EditorUtil.Pipify.Runner.RunBatchAsync(batch, reporter, overrides: null, ct);
```

## §13 关联文档

- [IPipifyProgressReporter.md](./IPipifyProgressReporter.md)
- [EditorUtil.Pipify.md](./EditorUtil.Pipify.md)
- [EditorUtil.Pipify.CliReporter.md](./EditorUtil.Pipify.CliReporter.md)
