# EditorUtil.Config.SchemaMigration

`EditorUtil.Config.SchemaMigration` 是 `ConfigMasterSO` 的 Editor-only 结构升级入口。它不进入 Player，也不向 `ConfigRuntimeSO` 写入任何 Editor 配置。

## 公开入口

```csharp
public static bool TryMigrate(
    ConfigMasterSO master,
    out bool changed,
    out string error);

public static bool TryMigrateAndReexport(
    ConfigMasterSO master,
    out bool changed,
    out bool exported,
    out string error);
```

- `TryMigrate`：只迁移内存中的 Master 结构。
- `TryMigrateAndReexport`：先验证当前导出坐标和目标路径，再迁移、保存 Master，并覆盖关联的 Runtime 快照。
- Editor 脚本重载后会通过延迟回调自动扫描工程内全部 Master；成功时保持静默，仅单个资产迁移失败时输出错误日志。

## 版本 0 → 1

迁移内容包括：

- `CommonMask` → `AppConfigsMask`
- `PlatformChannelEntry.CommonByMode` → `AppConfigsByMode`
- `HybridCLRMask`、顶层 HybridCLR 字段及 Overrides → `HybridEditorConfigs*`
- `YooAssetMask`、顶层 YooAsset 路径及 Overrides → `YooAssetEditorConfigs*`
- `CdnMask / CdnDeployment / CdnOverrides` → `CDNEditorConfigs*`

桥接字段保留旧序列化名称并隐藏显示。Unity 可能为资产中不存在的引用字段实例化空对象或空列表，因此迁移采用“有效旧值优先；已存在的新分组非默认值不被空桥接值覆盖”的规则。

## 安全约束

- 全部矩阵行验证通过后才写入新结构并推进版本。
- 迁移成功后清空旧字段，避免形成双真相源。
- 已完成当前版本的资产重复执行时直接返回，不覆盖新数据。
- Runtime 重导出失败会返回错误；导出前置条件在推进 Master 版本前检查。
- 兼容字段只在过渡窗口保留，后续大版本可删除对应版本步骤；若仍允许从版本 0 直升最新版，则必须继续保留本步骤或提供独立桥接工具。

关联文档：[ConfigMasterSO.md](../../Config/ConfigMasterSO.md)、[EditorUtil.Config.Exporter.md](EditorUtil.Config.Exporter.md)、[ConfigRuntimeSO.md](../../../Runtime/Modules/Config/ConfigRuntimeSO.md)。
