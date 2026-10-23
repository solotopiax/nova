# EditorUtil.Luban.SchemaManifest

**作用范围**：Unity Editor 内部的 Luban 导出链
**文件名**：`_configs/nova-export-manifest.json`

Schema manifest 是一次导出的 Excel 结构快照。每次调用 Luban 前，Pipeline 都会重新扫描当前 Profile 的 Excel 文件，先在内存中构建并验证完整快照，再保存到数据源目录的 `_configs/` 下。它不是手工配置，也不进入 Player。

---

## 为什么需要它

一个 Excel 可以包含多个有效 Sheet，后续步骤都必须对“本次导出有哪些表”得到同一个答案。manifest 把扫描结果集中起来，供以下步骤共享：

- `ConfigSyncer` 生成 `__tables__.xml`
- `JsonMerger` 按 Excel 合并 Luban 输出 JSON
- `MapPropGen` 为 Map 表生成属性
- `ExportHelper.BuildRelevantFileNames` 筛选单文件导出的生成代码
- Pipeline 保存并传递本次导出的同一个内存快照

因此，Inspector 不保存 Excel Sheet 名，也没有额外的刷新按钮或回调。

---

## 生成流程

```text
点击导出
  -> Pipeline.SyncSchema
  -> ConfigSyncer 先初始化/更新 luban.conf
  -> 扫描当前 Profile 的所有 Excel
  -> 构建并验证 LubanSchemaManifest
  -> 保存原 XML 字节，从同一快照以 .tmp + Replace/Move 发布 __tables__.xml
  -> 独立保存 _configs/nova-export-manifest.json；保存失败才尝试恢复 XML
  -> 调用 Luban CLI
  -> JsonMerger / MapPropGen / 文件筛选继续使用同一快照
```

扫描或验证失败会抛出异常，Pipeline 不会调用 Luban；此前 `luban.conf` 或初始化目录可能已经改变，不会整体回滚。XML 与 manifest 分别保存，manifest 保存失败才尝试恢复 XML，回滚失败会抛出 `AggregateException`。缺失、无法读取的 Excel 不会被当成空表静默跳过。

---

## JSON 字段

```json
{
  "schemaVersion": 1,
  "profileId": "sound",
  "units": [
    {
      "sourcePath": "Main/Hero.xlsx",
      "lubanInputPath": "Main/Hero.xlsx",
      "datasExportPath": "Assets/Game/Data/Hero.json",
      "classesExportPath": "Assets/Game/Scripts/Tables",
      "mode": "map",
      "indexField": "ID",
      "tables": [
        {
          "name": "TbHero",
          "valueType": "Hero"
        }
      ]
    }
  ]
}
```

| 字段 | 含义 |
|------|------|
| `schemaVersion` | manifest 格式版本；当前为 `1`，读取不支持的版本会失败 |
| `profileId` | 本次导出使用的固定 Profile 标识，如 `sound`、`network-cmd` |
| `units` | 本次导出的数据源单元列表；路径经过标准化并按 `sourcePath` 稳定排序 |
| `units[].sourcePath` | Excel 相对数据源根目录的路径，禁止绝对路径和 `..` 穿越 |
| `units[].lubanInputPath` | 写入 `__tables__.xml` 的 Luban 输入路径；预过滤模块可指向 `_temp` |
| `units[].datasExportPath` | 合并后的 JSON 输出路径 |
| `units[].classesExportPath` | C# 类型输出目录 |
| `units[].mode` | 小写的 `list`、`map` 或 `one` |
| `units[].indexField` | Map 的索引字段；非 Map 单元为空字符串 |
| `units[].tables` | Excel 扫描出的有效表，按表名稳定排序 |
| `tables[].name` | Luban 表容器名，例如 `TbHero` |
| `tables[].valueType` | Excel Sheet 对应的值类型短名，例如 `Hero` |

`DataTypeNameHelper` 忽略以 `#` 开头的 Sheet，并按 Sheet 总行数阈值筛选。当前九个内置 Profile 均默认 5；不存在 Table Profile，也不据此验证表头结构。

---

## 保存与重建

保存使用 UTF-8 无 BOM：先在同目录写入 `.tmp`，已有目标文件时通过 `File.Replace` 替换，新文件通过 `File.Move` 落盘。失败时只清理临时文件，不删除旧 manifest。

`_configs/` 当前由仓库忽略。manifest 是从 Excel 和当前单元设置重建的派生事实，不应描述为需要版本控制的源文件。删除它不会丢失业务源数据；下一次成功导出会重新生成。

所有 manifest 模型、校验、存储和构建类型都是 `internal`，只在 Editor 程序集中使用，不参与 Runtime 序列化或 Player 构建。

---

## 关联文档

- [EditorUtil.Luban.Pipeline.md](EditorUtil.Luban.Pipeline.md)
- [EditorUtil.Luban.ConfigSyncer.md](EditorUtil.Luban.ConfigSyncer.md)
- [EditorUtil.Luban.DataTypeNameHelper.md](EditorUtil.Luban.DataTypeNameHelper.md)
- [EditorUtil.Luban.ExportHelper.md](EditorUtil.Luban.ExportHelper.md)
- [EditorUtil.Luban.MapPropGen.md](EditorUtil.Luban.MapPropGen.md)
