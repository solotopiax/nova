# EditorUtil.UI.Exporter

**类签名**：`public static class Exporter`（嵌套于 `EditorUtil.UI`）

**命名空间**：`NovaFramework.Editor`

`EditorUtil.UI.Exporter` 是 UI 模块导出的公共门面。Inspector、Pipify 和其他 Editor 调用方只依赖这里的稳定 API；Excel 校验、Luban 流程、暂存发布与失败回滚统一交给 DataPipeline 中的 UI 专用实现。

## 调用关系

```text
Inspector / Pipify / UIExportAction
  -> EditorUtil.UI.Exporter（公共门面）
  -> UIExporter（内部校验与导出编排）
  -> Luban.GeneratedOutput（C# 所有权与安全清理）
  -> FileSystem.OutputApplier（正式产物应用与失败回滚）
```

公共门面不保存状态，也不解释 UI Excel 结构。

## 公开 API

```csharp
public static bool ExportAll(UISettings settings, string sourceDirPath);
public static bool ExportCode(UISettings settings, string sourceDirPath);
public static bool ExportData(UISettings settings, string sourceDirPath);
public static bool ExportCodeForFile(
    UISettings settings,
    string sourceDirPath,
    string filePath,
    string classExportPath);
public static bool ExportDataForFile(
    UISettings settings,
    string sourceDirPath,
    string filePath);
```

- `ExportAll`：导出代码和数据。
- `ExportCode`：仅更新代码；正式表格数据只作为 Map 生成输入，不会被修改。
- `ExportData`：仅更新所选 JSON/Binary 数据。

格式来自 `UISettings.DataFormat`，默认 JSON。Binary 使用 `.bytes`；数据发布时会在同一事务中删除同名反格式文件及其 `.meta`。
- `ExportCodeForFile` / `ExportDataForFile`：只发布精确匹配 Unit 的产物，不退化为全量发布。
- 所有方法返回是否成功；Pipify 收到 `false` 时中止当前流水线。

## 使用示例

```csharp
EditorUtil.UI.Exporter.ExportAll(settings, sourceDirPath);
EditorUtil.UI.Exporter.ExportData(settings, sourceDirPath);
EditorUtil.UI.Exporter.ExportCodeForFile(
    settings,
    sourceDirPath,
    changedFilePath,
    classExportPath);
```

## 边界

- UI 表结构与路径校验参见 `UIExporter`。
- `_temp/_publish`、正式文件替换和回滚参见 `FileSystem.OutputApplier`。
- 源目录 `_temp` 已被当前 Editor 中的其他导出持有时，返回失败且不清理其临时文件或刷新；成功取得的租约覆盖生成、发布、清场和刷新，最后才释放，刷新异常也会释放。
- UI 导出是模块深度定制流程，不作为 Table 通用表格导出的变体，也不要求其他模块接入相同分层。

## 受控 Agent 入口

`nova.project.ui.export` 复用上述五个公共 API，不增加新的 Skill。创建界面、更新界面和数据驱动界面 Workflow 共用此 Action。

请求包含必填 `scope`（`all/code/data`），可选 `sourcePath` 精确匹配一个 `UIUnitSetting.SourcePath`。省略 `sourcePath` 时走全量；单文件仅支持 `code/data`，因为公共领域入口没有原子单文件 `all`。不把两次调用拼成一个事务。

Plan 只读冻结已保存且无脏数据的活动场景、唯一 Nova/UIComponent 的 `m_UISettings`、持久 ConfigMaster Namespace、全部 UI Excel、模板文件、既有数据和输出状态。使用只读 `LubanSchemaManifestBuilder.Build` 计算准确文件集，不同步 `_configs`。写入范围包括：

- 所选数据，以及同名反格式文件与其 `.meta` 删除。
- 所选类型与共享 `UITables.cs`；全量代码导出还更新 `_configs/output-manifests/ui.json`，并删除同源且正文未改的过期生成代码及已存在 `.meta`。
- 所有模式的 `_configs/luban.conf`、`__tables__.xml`、`nova-export-manifest.json`；生成或替换文件对应 Unity `.meta`。

Action 仅允许业务 `Assets` 内来源与输出。它拒绝手改/无有效所有权的代码目标、非标准配置、既有临时目录、来源漂移和路径冲突。`code-only` JSON 的 Map 属性依赖正式数据；新注册必须先 data，再 code。限制仅作用于受控 Action，五个原有 API 的签名和语义保持原样。

Execute 在复核 Hash 后调用一次 Exporter，返回 `partial + Receipt`。`GeneratedExportCompilationState` 关联本次编译令牌、载荷和真实编译周期，并在代码导出后要求脚本重载。正式输出完成后先监听自动编译，分别记录程序集编译完成事件和 Unity 核验输入后判定输出未变化的 `assemblyCompilationNotRequired` 事件；后者证明本次输入已核验，不等同于物理重编译。跨重载恢复的 Editor update 轮询等待两轮空闲：未开始自动编译，或已结束周期没有这两类程序集事件时，同一令牌最多补一次 `CleanBuildCache` 编译请求。没有本次完整周期或程序集事件、尚未重载或存在错误时保持 `partial`，不借用旧编译成功。编译证据仅保存当前 Editor 会话最近一次，重启或后续导出覆盖后旧收据不能升级为成功。

稳定后用 Receipt 或 `recovery_token` Verify；验证只读取来源、准确产物、删除结果和范围外文件，绝不重放导出。Play、Prefab 绑定、Collector、资源加载和真机结果仍由调用 Skill 独立验证。Exporter 失败前可能已更新 `_configs`，Action 明确报告 partial；正式代码/数据发布仍使用既有 OutputApplier 事务。

## 关联文档

- [UIExporter.md](../../DataPipeline/Implements/UIs/UIExporter.md)
- [EditorUtil.FileSystem.OutputApplier.md](../EditorUtil.FileSystem/EditorUtil.FileSystem.OutputApplier.md)
- [EditorUtil.Luban.GeneratedOutput.md](../EditorUtil.Luban/EditorUtil.Luban.GeneratedOutput.md)
- [UISettings.md](../../../Runtime/Modules/UI/UIManager/Definitions/UISettings.md)
- [EditorUtil.Luban.Pipeline.md](../EditorUtil.Luban/EditorUtil.Luban.Pipeline.md)
- [PipifySteps.md](../EditorUtil.Pipify/PipifySteps.md)

## 门面参数来源

Inspector 在 Apply 后传当前 UISettings 和源根，Pipify 传解析后的配置，受控 Action 才使用重新核验的冻结快照。filePath 是权威单个 Excel，classExportPath 是显式代码目录；共享 UITables 管理代码仍可能受单文件代码导出影响。五个门面原样转发 bool 与异常，没有自身的路径、确认或恢复门，不能把 Action 的严格保护归给普通 API。
