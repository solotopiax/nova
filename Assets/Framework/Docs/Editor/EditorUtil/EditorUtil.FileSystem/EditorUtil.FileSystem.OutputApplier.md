# EditorUtil.FileSystem.OutputApplier

**类签名**：`internal sealed class OutputApplier : IDisposable`（嵌套于 `EditorUtil.FileSystem`）

**命名空间**：`NovaFramework.Editor`

`OutputApplier` 是无业务语义的文件输出基础设施。它登记一批文件替换和精确删除操作，应用前备份已有目标；任一步失败时，按逆序恢复已经执行的操作。

## 调用关系

```text
模块专用 Exporter
  -> EditorUtil.FileSystem.OutputApplier
       ├─ Replace / Delete
       ├─ Backup
       └─ Rollback
```

模块 Exporter 决定更新哪些业务产物；`OutputApplier` 只处理文件路径和失败补偿。UI 与 Localization 不再保留只做转发的模块级 Applier。

## 内部 API

```csharp
using var applier = new OutputApplier(tempRoot);
applier.StagingRoot;
applier.AddReplacement(stagedPath, targetPath);
applier.AddDeletion(targetPath);
applier.Apply();
```

## 语义

- 暂存目录固定为 `{tempRoot}/_publish`，备份位于其 `backup` 子目录。
- 同一正式目标只能登记一次。
- 替换通过目标旁 `.tmp` 文件完成，结束后不保留 `.tmp`。
- 应用失败且回滚成功时重新抛出原异常。
- 回滚也失败时抛出 `AggregateException`，包含应用异常和全部恢复异常。
- `Dispose` 删除本事务的 `_publish` 目录。

这是“批量应用 + 补偿式回滚”，不是文件系统提供的跨文件原子事务；Editor 崩溃后的跨进程恢复不在其职责内。

## 关联文档

- [EditorUtil.FileSystem.md](EditorUtil.FileSystem.md)
- [EditorUtil.Luban.GeneratedOutput.md](../EditorUtil.Luban/EditorUtil.Luban.GeneratedOutput.md)
- [DataPipeline.md](../../DataPipeline/DataPipeline.md)
