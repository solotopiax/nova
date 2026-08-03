# EditorUtil.Sound.Exporter

**类签名**：`public static class Exporter`（嵌套于 `EditorUtil.Sound`）

**命名空间**：`NovaFramework.Editor`

`Sound.Exporter` 是 Sound 模块的公共入口和专用导出编排器。公开 API 保持不变；数据和代码先生成到 Excel 源目录下的 `_temp/_publish`，验证完整后再通过 `FileSystem.OutputApplier` 发布。

## 公开 API

```csharp
public static void ExportAll(string sourceDirPath, SoundSettings settings);

public static void ExportData(
    string sourceDirPath,
    SoundSettings settings,
    SoundUnitSetting unitSetting);

public static void ExportCode(
    string sourceDirPath,
    SoundSettings settings,
    SoundUnitSetting unitSetting,
    string classExportPath,
    HashSet<string> relevantFileNames);
```

Inspector 的调用方式没有变化。`ExportData` 处理指定单元；`ExportCode` 的 `unitSetting == null` 表示全量代码；`ExportAll` 在一次事务中发布所选 JSON/Binary 数据与 C#。Pipify 的数据/类型 Step 分别调用内部全量批次入口，不再逐 Unit 启动独立事务。格式来自 `SoundSettings.DataFormat`，默认 JSON；发布数据时删除同名反格式文件及其 `.meta`。

## 导出流程

```text
校验 SoundSettings、源目录、Unit 和输出路径
  -> 克隆 Settings，把所选格式数据/C# 输出改写到 _temp/_publish
  -> 仅代码导出时，把正式表格数据复制到暂存区供 Map 属性生成
  -> Luban Pipeline 生成数据、类型和 SchemaManifest
  -> 验证目标数据与 SchemaManifest 对应的全部 C# 文件
  -> Luban.GeneratedOutput 写入第一行单行所有权标记并登记安全过期删除
  -> FileSystem.OutputApplier 一次性替换正式产物
  -> finally 清理 _temp 并刷新 AssetDatabase
```

全量导出不再预先删除正式目录。Pipeline 返回失败、暂存产物缺失或发布异常时，正式文件保持原状或回滚到导出前状态。

多个 Unit 配置不同 `ClassesExportPath` 时，仍沿用旧契约：记录警告并使用首个非空路径。Pipify 收到批次返回 `false` 时立即抛出并终止后续步骤。

## 内部测试替换点

`ExportOperations` 只允许 Editor 定向测试替换 Luban Pipeline 和 AssetDatabase 刷新，不是新增公共 API，也不保存业务状态。

## 关联文档

- [EditorUtil.Luban.ExportProfile.md](../EditorUtil.Luban/EditorUtil.Luban.ExportProfile.md)
- [EditorUtil.Luban.Pipeline.md](../EditorUtil.Luban/EditorUtil.Luban.Pipeline.md)
- [EditorUtil.FileSystem.OutputApplier.md](../EditorUtil.FileSystem/EditorUtil.FileSystem.OutputApplier.md)
- [EditorUtil.Luban.GeneratedOutput.md](../EditorUtil.Luban/EditorUtil.Luban.GeneratedOutput.md)
- [DataPipeline.md](../../DataPipeline/DataPipeline.md)
