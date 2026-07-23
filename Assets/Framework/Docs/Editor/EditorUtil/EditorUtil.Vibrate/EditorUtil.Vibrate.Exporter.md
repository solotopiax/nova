# EditorUtil.Vibrate.Exporter

**类签名**：导出方法直接定义在 `EditorUtil.Vibrate` partial class 中

**命名空间**：`NovaFramework.Editor`

`EditorUtil.Vibrate` 保持 Emphasis 与 Custom 两条独立导出链。公开 API 不变；每个区域使用自己的源目录、Profile、Settings 适配视图和 `_temp/_publish` 事务。Pipify 的数据/类型 Step 各调用一次对应区域批次入口，不再逐 Unit 发布。

## 公开 API

```csharp
public static void ExportEmphasisData(string filePath, string dataExportPath, VibrateSettings settings);
public static void ExportEmphasisCode(string filePath, string classExportPath, VibrateSettings settings);
public static void ExportEmphasisAll(VibrateSettings settings);

public static void ExportCustomData(string filePath, string dataExportPath, VibrateSettings settings);
public static void ExportCustomCode(string filePath, string classExportPath, VibrateSettings settings);
public static void ExportCustomAll(VibrateSettings settings);
```

## 区域路由

| 区域 | 源目录 | Unit | Profile |
|---|---|---|---|
| Emphasis | `EmphasisSourceDirPath` | `EmphasisUnitsSettings` | `VibrateEmphasis` |
| Custom | `CustomSourceDirPath` | `CustomUnitsSettings` | `VibrateCustom` |

两个区域不会共享暂存目录或发布事务；一方导出失败不会修改另一方产物。

## 导出流程

```text
按区域定位源目录、Unit 与固定 Profile
  -> 单文件入口必须精确匹配 VibrateUnitSetting
  -> 克隆区域 Settings，把输出改写到该区域 _temp/_publish
  -> 仅代码导出时种入正式 JSON
  -> Luban Pipeline 生成并验证 JSON / C# / SchemaManifest
  -> Luban.GeneratedOutput 写入第一行单行所有权标记并登记安全过期删除
  -> FileSystem.OutputApplier 一次性发布本区域产物
  -> finally 清理该区域 _temp
```

单文件类型找不到 Unit 时会记录错误并终止，不再退化为全量代码导出。Pipeline 或发布失败时，正式文件保持或回滚到导出前状态；Pipify 收到 `false` 时立即抛出并终止后续步骤。

## 内部测试替换点

`ExportOperations` 只用于 Editor 定向测试替换 Pipeline 与 AssetDatabase 刷新，不改变公共调用契约。

## 关联文档

- [VibrateSettings.md](../../../Runtime/Modules/Vibrate/VibrateSettings.md)
- [VibrateUnitSetting.md](../../../Runtime/Modules/Vibrate/VibrateUnitSetting.md)
- [EditorUtil.Luban.Pipeline.md](../EditorUtil.Luban/EditorUtil.Luban.Pipeline.md)
- [EditorUtil.FileSystem.OutputApplier.md](../EditorUtil.FileSystem/EditorUtil.FileSystem.OutputApplier.md)
- [EditorUtil.Luban.GeneratedOutput.md](../EditorUtil.Luban/EditorUtil.Luban.GeneratedOutput.md)
- [DataPipeline.md](../../DataPipeline/DataPipeline.md)
