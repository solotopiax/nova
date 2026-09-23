# BatchItem

## §1 文件头

| 项 | 值 |
|---|---|
| 类签名 | `public sealed class BatchItem` |
| 命名空间 | `NovaFramework.Editor` |
| 功能描述 | Batch 中的单个步骤条目，持有 StepId 与参数 JSON |

## §2 文件表

| 文件 | 类 | 说明 |
|---|---|---|
| `EditorUtil.Pipify/Definitions/BatchItem.cs` | `BatchItem` | 完整实现 |

## §5 公开 API

| 成员 | 类型 | 说明 |
|---|---|---|
| `StepId` | `string` { get; set; } | Step 稳定 ID，重命名 DisplayName 不影响存档 |
| `ParamsJson` | `string` { get; set; } | 参数序列化结果（Util.Json），无参 Step 为空字符串 |

## §9 可选 Package 卸载行为

`BatchItem` 不序列化 Step 实现类型，只保存 `StepId + ParamsJson`。提供该 Step 的可选 Package 被卸载或暂时编译失败时，条目与参数仍保留，不会变成 Unity Missing Script；PipifyWindow 会将其标为 `[Missing]` 并禁止运行整个 Batch。重新安装 Package 且 StepId 不变后，条目自动恢复为可配置、可执行状态。

## §11 使用示例

```csharp
// 构造一个绑定到特定 Step 的条目
var item = new BatchItem
{
    StepId = "export.config",
    ParamsJson = Util.Json.Serialize(myParams)
};
batch.Items.Add(item);
```

## §13 关联文档

- [Batch.md](./Batch.md)
- [PipifySettingsSO.md](./PipifySettingsSO.md)
- [Editor.md](../../Editor.md)
