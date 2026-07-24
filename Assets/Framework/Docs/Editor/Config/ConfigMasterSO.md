# ConfigMasterSO

`ConfigMasterSO` 是 `NovaFramework.Editor` 下的设计态配置资产，只能由 Editor 程序集访问。ConfigWindow 编辑该资产，Exporter 将当前三维坐标裁剪为 `ConfigRuntimeSO`。

## Runtime 数据来源

- `PlatformChannelEntry.AppConfigsByMode` → `AppConfigs`
- `Namespace` 与 `NamespaceOverrides` → `Namespace`
- `HybridEditorConfigs.GameEntranceProcedureName / AotMetadataDlls / GameDlls` → 去除构建路径后生成 `HybridConfigs`
- Runtime SDK 与 Kit 配置矩阵
- `CustomConfigs` 当前导出空实例

## Editor-only 数据

- `YooAssetEditorConfigs` 与 `YooAssetEditorConfigsOverrides`
- `HybridEditorConfigs.LinkXmlTargetPath`、DLL 源/目标路径及对应 Overrides
- `CDNEditorConfigs` 与 `CDNEditorConfigsOverrides`
- 各 Editor 面板维度掩码和当前编辑坐标

这些数据保存在 `ConfigMaster.asset`，不会写入 `ConfigRuntime.asset`。

## 结构版本与旧资产迁移

- `ConfigSchemaVersion` 记录设计态资产已经完成的结构版本；当前版本为 `1`。
- 版本 `0` 是分组改造前的结构：`CommonByMode`、顶层 HybridCLR/YooAsset 字段以及旧面板掩码和 Override 字段。
- 框架在 Editor 脚本重载后自动扫描 `ConfigMasterSO`，将旧字段迁入新分组并重新导出绑定的 `ConfigRuntimeSO`；迁移成功时不显示菜单、弹窗或日志。
- 迁移先校验全部矩阵行及导出前置条件；失败时不会推进 `ConfigSchemaVersion`。
- 迁移是幂等的：版本已是当前值时不会再次复制旧字段，也不会覆盖项目组迁移后的新配置。

旧字段作为隐藏的序列化桥接输入仅存在于 Editor 层，并在迁移成功后清空。它们需保留到约定的兼容窗口结束；删除前必须确认所有项目已至少经过一次桥接版本迁移并保存资产。Runtime 不包含这些 Editor 迁移字段或迁移逻辑。

关键源码：[ConfigMasterSO.cs](../../../Scripts/Editor/Config/ConfigMasterSO.cs)、[SchemaMigration.md](../EditorUtil/EditorUtil.Config/EditorUtil.Config.SchemaMigration.md)、[ConfigWindow.md](../Windows/ConfigWindow.md)、[ConfigRuntimeSO.md](../../Runtime/Modules/Config/ConfigRuntimeSO.md)。
