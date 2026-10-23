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
  -> 持有工作区租约清理 _temp 并刷新 AssetDatabase，最后释放租约
```

全量导出不再预先删除正式目录。Pipeline 返回失败、暂存产物缺失或发布异常时，正式文件在 Apply 前保持原状，应用失败时尝试补偿恢复；IO 或回滚异常可能留下部分产物。

同一源目录的 `_temp` 已被其他导出持有时，本次调用返回失败，不清理现有临时目录或 `.meta`，也不调用 Pipeline 和刷新。成功取得租约后，暂存、发布、清场和刷新均处于同一租约内；清场或刷新异常仍会释放租约。此保护只覆盖当前 Editor 进程，不提供跨进程文件锁；IO 失败与文件补偿边界仍以 `OutputApplier` 为准。

多个 Unit 配置不同 `ClassesExportPath` 时，仍沿用旧契约：记录警告并使用首个非空路径。Pipify 收到批次返回 `false` 时立即抛出并终止后续步骤。

## 内部测试替换点

`ExportOperations` 只允许 Editor 定向测试替换 Luban Pipeline 和 AssetDatabase 刷新，不是新增公共 API，也不保存业务状态。

## 关联文档

- [EditorUtil.Luban.ExportProfile.md](../EditorUtil.Luban/EditorUtil.Luban.ExportProfile.md)
- [EditorUtil.Luban.Pipeline.md](../EditorUtil.Luban/EditorUtil.Luban.Pipeline.md)
- [EditorUtil.FileSystem.OutputApplier.md](../EditorUtil.FileSystem/EditorUtil.FileSystem.OutputApplier.md)
- [EditorUtil.Luban.GeneratedOutput.md](../EditorUtil.Luban/EditorUtil.Luban.GeneratedOutput.md)
- [DataPipeline.md](../../DataPipeline/DataPipeline.md)

## 范围、克隆与失败结果

公开 void 重载没有程序化成功结果；null unit 可选择全量，非 null 必须按引用属于当前列表。内部 bool 重载和 Action 的精确调用不能与 void 混用。显式 classExportPath 也不能绕过 TryValidate 对 settings 中至少一个有效代码目录的要求；多目录只警告并取首个，不证明所有 Unit 均发布到各自目录。

克隆只复制当前用到的字段，新增配置字段时须同步映射；重复 Unit 对象引用会使映射 Add 失败。代码模式复制正式数据作为 Map 输入，缺数据可留下空 Map，不保证与新 Excel 同步。空正式数据路径可能跳过登记而返回 true；反格式文件删除也不独立验证业务所有权。原始 API 不提供项目根、符号链接或源文件身份冻结。

路径规范化和部分校验在 Execute 的 catch 外，清场删除只告警可留残余，Refresh 异常可越过 bool；即使发生异常，已取得的 lease 仍在最外 finally 释放。输出补偿不是跨文件原子提交，错误文案不能证明回滚完整；详见 OutputApplier。
