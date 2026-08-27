# ConfigMasterSO

`ConfigMasterSO` 是 `NovaFramework.Editor` 下的设计态配置资产，只能由 Editor 程序集访问。ConfigWindow 编辑该资产，Exporter 将当前三维坐标裁剪为 `ConfigRuntimeSO`。其中平台维度不是资产可编辑状态：`CurrentPlatform` 每次访问都实时映射 Unity 当前 `EditorUserBuildSettings.activeBuildTarget`；渠道与开发模式仍由资产保存和选择。

## Runtime 数据来源

- `PlatformChannelEntry.AppConfigsByMode` → `AppConfigs`
- `PlatformChannelEntry.PrivacyConfigsByMode` → `PrivacyConfigs`；使用独立 `PrivacyConfigsMask`
- `Namespace` 与 `NamespaceOverrides` → `Namespace`
- `HybridEditorConfigs.GameEntranceProcedureName / AotMetadataDlls / StartupGameDlls` → 去除构建路径后生成 `HybridConfigs`
- Runtime SDK 与 Kit 配置矩阵
- 顶层 `Custom` → 本地 JSONPath/string 默认值；云端完整 JSON 不受这些路径限制

## Editor-only 数据

- `YooAssetEditorConfigs` 与 `YooAssetEditorConfigsOverrides`
- `HybridEditorConfigs.RunningGameDlls`、`LinkXmlTargetPath`、DLL 源/目标路径及对应 Overrides
- `CDNEditorConfigs` 与 `CDNEditorConfigsOverrides`
- 各 Editor 面板维度掩码，以及当前编辑的 Channel / DevelopMode

这些数据保存在 `ConfigMaster.asset`，不会写入 `ConfigRuntime.asset`。

## Editor 平台真相源

`CurrentPlatform` 是只读计算属性，不序列化，也不能由 ConfigWindow、Pipify 或旧配置资产手动改写。它统一使用 `EditorUtil.Config.ActivePlatform` 映射当前 Unity Active BuildTarget：

| Unity `activeBuildTarget` | `CurrentPlatform` |
|---|---|
| `Android` | `PlatformType.Android` |
| `iOS` | `PlatformType.iOS` |
| `WebGL` | `PlatformType.WebGL` |
| 其他目标 | `PlatformType.None` |

映射为 `None` 时，ConfigWindow 导出、Pipify 的 Config / Bundle / Player 构建等生产入口会明确阻断，要求先切换到受支持的 Unity BuildTarget；不会静默回退到资产中的旧平台值。

升级前序列化的 `CurrentPlatform` 通过隐藏字段 `m_LegacyCurrentPlatform`（`FormerlySerializedAs("CurrentPlatform")`）无损读入。该字段只承担旧资产兼容，绝不参与当前坐标、占位符解析或导出决策。

## 结构版本与旧资产迁移

- `ConfigSchemaVersion` 记录设计态资产已经完成的结构版本；当前版本为 `1`。
- 版本 `0` 是分组改造前的结构：`CommonByMode`、顶层 HybridCLR/YooAsset 字段以及旧面板掩码和 Override 字段。
- 框架在 Editor 脚本重载后自动扫描 `ConfigMasterSO`，将旧字段迁入新分组并重新导出绑定的 `ConfigRuntimeSO`；迁移成功时不显示菜单、弹窗或日志。
- 迁移先校验全部矩阵行及导出前置条件；失败时不会推进 `ConfigSchemaVersion`。
- 迁移是幂等的：版本已是当前值时不会再次复制旧字段，也不会覆盖项目组迁移后的新配置。

旧字段作为隐藏的序列化桥接输入仅存在于 Editor 层，并在迁移成功后清空。它们需保留到约定的兼容窗口结束；删除前必须确认所有项目已至少经过一次桥接版本迁移并保存资产。Runtime 不包含这些 Editor 迁移字段或迁移逻辑。

关键源码：[ConfigMasterSO.cs](../../../Scripts/Editor/Config/ConfigMasterSO.cs)、[EditorUtil.Config.ActivePlatform.cs](../../../Scripts/Editor/EditorUtil/EditorUtil.Config/EditorUtil.Config.ActivePlatform.cs)、[SchemaMigration.md](../EditorUtil/EditorUtil.Config/EditorUtil.Config.SchemaMigration.md)、[ConfigWindow.md](../Windows/ConfigWindow.md)、[ConfigRuntimeSO.md](../../Runtime/Modules/Config/ConfigRuntimeSO.md)。
