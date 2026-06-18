# Batch

## §1 文件头

| 项 | 值 |
|---|---|
| 类签名 | `public sealed class Batch` |
| 命名空间 | `NovaFramework.Editor` |
| 功能描述 | 有序 BatchItem 列表，名字唯一，对应一条可执行流水线 |

## §2 文件表

| 文件 | 类 | 说明 |
|---|---|---|
| `EditorUtil.Pipify/Definitions/Batch.cs` | `Batch` | 完整实现 |

## §5 公开 API

| 成员 | 类型 | 说明 |
|---|---|---|
| `Name` | `string` { get; set; } | Batch 名称，在所属 PipifySettingsSO 中唯一 |
| `Description` | `string` { get; set; } | Batch 描述 |
| `Items` | `List<BatchItem>` { get } | Step 条目有序列表（只读暴露引用，可直接 Add/Remove） |

## §11 使用示例

```csharp
// 创建 Batch 并添加条目
var batch = new Batch { Name = "发布流程", Description = "导出 + AB 构建 + 打包" };
batch.Items.Add(new BatchItem { StepId = "export_config", ParamsJson = "" });
batch.Items.Add(new BatchItem { StepId = "build_ab", ParamsJson = "" });
settings.Batches.Add(batch);
```

## §13 关联文档

- [BatchItem.md](./BatchItem.md)
- [PipifySettingsSO.md](./PipifySettingsSO.md)
- [Editor.md](../../Editor.md)
