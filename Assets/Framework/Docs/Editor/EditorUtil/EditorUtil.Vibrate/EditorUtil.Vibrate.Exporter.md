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
  -> 仅代码导出时种入正式表格数据
  -> Luban Pipeline 按 VibrateSettings.DataFormat 生成并验证 JSON 或 Binary / C# / SchemaManifest
  -> Luban.GeneratedOutput 写入第一行单行所有权标记并登记安全过期删除
  -> FileSystem.OutputApplier 一次性发布本区域产物
  -> 持有工作区租约清理该区域 _temp 并刷新，最后释放租约
```

单文件类型找不到 Unit 时会记录错误并终止，不再退化为全量代码导出。Pipeline 或发布失败时，正式文件在 Apply 前保持原状，应用失败时尝试补偿恢复；Pipify 收到 `false` 时立即抛出并终止后续步骤。

Emphasis 与 Custom 共用一个格式选项，默认 JSON；数据发布时各自在自己的事务中删除同名反格式文件及其 `.meta`。

所选源目录的 `_temp` 已被其他导出持有时，本次调用返回失败，不删除其临时内容或 `.meta`，不调用 Pipeline 和刷新。取得租约后一直持有到本区域清场与刷新结束，刷新异常也通过 `finally` 释放租约。保护范围是当前 Editor 进程；两个区域若配置相同源目录，会共享该目录的租约并拒绝重入。

## 内部测试替换点

`ExportOperations` 只用于 Editor 定向测试替换 Pipeline 与 AssetDatabase 刷新，不改变公共调用契约。

## 关联文档

- [VibrateSettings.md](../../../Runtime/Modules/Vibrate/VibrateSettings.md)
- [VibrateUnitSetting.md](../../../Runtime/Modules/Vibrate/VibrateUnitSetting.md)
- [EditorUtil.Luban.Pipeline.md](../EditorUtil.Luban/EditorUtil.Luban.Pipeline.md)
- [EditorUtil.FileSystem.OutputApplier.md](../EditorUtil.FileSystem/EditorUtil.FileSystem.OutputApplier.md)
- [EditorUtil.Luban.GeneratedOutput.md](../EditorUtil.Luban/EditorUtil.Luban.GeneratedOutput.md)
- [DataPipeline.md](../../DataPipeline/DataPipeline.md)

## 区域对象与单文件参数

Area 和 StagedSettings 的只读属性仍持有可变单元列表/对象，不等于深快照；默认 Area 可含 null，getter 不校验。区域选择只区分 Emphasis/Custom，不跨区域合并；两区域同源时仍竞争同一租约。

数据入口的 dataExportPath 只做非空门禁，真实正式目标来自匹配 Unit.DatasExportPath；重复 SourcePath 取首项，找不到目标拒绝，不回退全量。代码入口可使用显式 classExportPath，不要求 Unit.ClassesExportPath 同值；全量取首个非空代码目录，多目录仅警告。代码模式用旧正式数据做 Map 种子，缺失不一定失败；空正式数据路径可跳过发布，未知格式除 Binary 外按 Json 处理。

配置检查不替代源/schema/template、数据地址或路径授权核验。GetFullPath 等前置异常可外抛，清场 Warning 可留残余、Refresh 可外抛；已获 lease 仍 finally 释放。公开 void 重载不提供成功收据，bool/异常也不证明没有已发生的配置或文件副作用；补偿限制见 OutputApplier。
