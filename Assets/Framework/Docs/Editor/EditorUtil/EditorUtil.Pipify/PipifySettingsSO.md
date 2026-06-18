# PipifySettingsSO

## §1 文件头

| 项 | 值 |
|---|---|
| 类签名 | `public sealed class PipifySettingsSO : ScriptableObject` |
| 命名空间 | `NovaFramework.Editor` |
| 功能描述 | Pipify 流水线存档 SO，顶层持有所有 Batch |

## §2 文件表

| 文件 | 类 | 说明 |
|---|---|---|
| `EditorUtil.Pipify/Definitions/PipifySettingsSO.cs` | `PipifySettingsSO` | 完整实现 |

## §5 公开 API

| 成员 | 类型 | 说明 |
|---|---|---|
| `Batches` | `List<Batch>` { get } | 全部 Batch 列表（只读暴露引用，可直接 Add/Remove） |

**Asset 菜单：** `Nova/Pipify Settings`，文件名默认 `PipifySettings`。推荐通过 PipifyWindow 顶栏"新建"按钮（`AssetDatabase.CreateAsset`）创建，避免散落在项目随机目录。

## §11 使用示例

```csharp
// 通过 PipifyWindow 新建后，由代码读取
var settings = AssetDatabase.LoadAssetAtPath<PipifySettingsSO>(path);
foreach (var batch in settings.Batches)
{
    Debug.Log(batch.Name);
}
```

## §13 关联文档

- [Batch.md](./Batch.md)
- [BatchItem.md](./BatchItem.md)
- [Editor.md](../../Editor.md)
