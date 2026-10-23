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
| `ApplyOverridesForItem(info, itemIndex, paramsInstance, overrides)` | 将 overrides 字典匹配当前 (stepId, itemIndex) 的键值写回 paramsInstance；支持精确索引格式与通配格式。随后统一同步平台字段到 Unity Active BuildTarget |
| `ConvertOverrideValue(raw, targetType)` | string → string / enum / 数字 / bool 类型转换 |

## §9 关键算法

### RunBatchAsync 执行流程

1. batch / reporter 为 null 抛 `ArgumentNullException`
2. 在读取工作区和执行任何 Step 前预检整个 Batch；存在未注册 Step 时一次性列出缺失 StepId 并抛 `InvalidOperationException`
3. `Stopwatch` 全程计时；`reporter.BeginBatch` 通知开始
4. 按索引遍历 `batch.Items`：
   - `ct.ThrowIfCancellationRequested()` 响应外部取消
   - `Registry.FindById` 查元信息，未命中抛 `InvalidOperationException`
   - 有参 Step：ParamsJson 非空时 `Util.Json.Deserialize(json, type)` 反序列化，否则创建默认实例；随后 `ApplyOverridesForItem` 覆盖 CLI 参数，并以 Unity Active BuildTarget 覆盖所有平台字段
   - 构造 `PipifyContext` 并下发
   - `reporter.ReportStep` 返回 true 抛 `OperationCanceledException`
   - 反射调用 `info.Method.Invoke`，强转 `UniTask` 后 `await`
   - `TargetInvocationException` 解包为 `InnerException`，经 `ExceptionDispatchInfo.Capture().Throw()` 保留原始栈后再抛
   - Step 调用保护块内异常先 `reporter.EndStep(false)` 再 throw
5. 正常结束、取消或异常时由 `finally` 调用 `reporter.EndBatch`；Domain Reload 不保证执行旧异步链的 `finally`，窗口 Reporter 通过独立重载钩子清理进度并记录中断。

窗口构建后、上传开始前单独执行一次编译与重载交接，恢复仍走 `RunBatchCoreAsync` 共用循环。检查点只保存原存档位置、参数指纹和确切构建身份与文件指纹，恢复时拒绝工作区、参数、平台或文件变化；不重复执行前序步骤，已经开始的上传也不重放。CLI / Job 不进入该窗口交接。Runner 与 Step 均不使用程序集重载锁或 Asset Editing 批锁。

### ApplyOverridesForItem Key 匹配优先级

精确格式 `stepId[itemIndex].fieldName` > 通配格式 `stepId.fieldName`（key 不含 `[`）。字段不存在抛 `InvalidOperationException`。

`export.config.Platform`、`build.package.Target`、`bundlebuilder.build.Target`、`bundlebuilder.build_raw_file.Target` 属于只读同步字段：CLI 中即使保留这些旧 key 也会跳过值解析，并在参数解析的最后一步同步为 Unity 当前 Active BuildTarget。

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

## 运行态、覆盖与取消边界

Runner 只按启动时 Master/Pipify GUID 在每步前后复核工作区，同 GUID 内容、Batch 参数和产物仍可能变化；BeginBatch 在执行 try 前，参数解析及部分取消检查在 EndStep 的调用保护外，不能保证任意失败都先 EndStep。Reporter 的结束/通知异常也可能遮原错。旧域重载不保证异步 finally，窗口重载钩子只清进度并记录中断，不撤销已完成副作用。

CLI plain 与 indexed override 都匹配时按字典遍历顺序写，不能依标题“优先级”推断固定赢家；转换用当前 Culture。平台字段跳过覆盖解析后强制当前 ActiveBuildTarget，WebGL 不支持首包模式在 Pipify 归一为 ClearAndCopyAll。字符串占位只递归公开字段和 IList，不覆盖属性或 Dictionary 值，未知 token 保留；旧 Config 空 ParamsJson 是明确例外，会初始化并可能保存所属 Settings。

HybridCLR 步骤只镜像第一个后续 build.package 的 DevelopmentBuild，finally 恢复；普通同步 Step 多数不消费 token，取消发生在 Runner/配合的异步点，不能强制中止引擎或回滚。AllExcel 固定19项且不含 Config、Proto、包扩展，前成功产物保留。Helpers FindAnyObjectByType 包含 inactive，未限制 Active Scene 或唯一 Nova，不能推广为严格源绑定。

窗口上传交接仅在请求发出前消费 checkpoint，核 GUID、Batch内容、平台和 AAB/输入文件 Hash；不核所有 Master 值、外部凭据正文或远端状态。恢复等稳定后先清 checkpoint 再继续，失败不重传，不跨 Editor 重启恢复。
