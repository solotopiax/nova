# ConfigMasterSO

`ConfigMasterSO` 是 `NovaFramework.Editor` 下的设计态配置资产，只能由 Editor 程序集访问。ConfigWindow 编辑该资产，Exporter 将当前三维坐标裁剪为 `ConfigRuntimeSO`。平台维度不是 ConfigMaster 资产状态：`CurrentPlatform` 每次访问都实时映射 Unity 当前 `EditorUserBuildSettings.activeBuildTarget`；ConfigWindow 另以不写入 ConfigMaster 的窗口状态选择编辑平台，渠道与开发模式仍由资产保存和选择。

## Runtime 数据来源

- `PlatformChannelEntry.AppConfigsByMode` → `AppConfigs`
- `PlatformChannelEntry.PrivacyConfigsByMode` → `PrivacyConfigs`；使用独立 `PrivacyConfigsMask`
- `Namespace` 与 `NamespaceOverrides` → `Namespace`
- `HybridEditorConfigs.GameEntranceProcedureName / AotMetadataDlls / StartupGameDlls` → 去除构建路径后生成 `HybridConfigs`
- Runtime SDK 与 Kit 配置矩阵
- 顶层 `Custom` → 本地 JSONPath/string 默认值；云端完整 JSON 不受这些路径限制

SDK / Kit 配置 DTO 会跨平台保留，以便同一份 ConfigMaster 安全编辑 Android、iOS 与 WebGL 矩阵。某个平台不支持厂商原生能力时，只禁用该平台实现，不删除 DTO 类型，也不清理其他平台已经保存的配置。

## Editor-only 数据

- `YooAssetEditorConfigs` 与 `YooAssetEditorConfigsOverrides`
- `HybridEditorConfigs.RunningGameDlls`、`LinkXmlTargetPath`、DLL 源/目标路径及对应 Overrides
- `CDNEditorConfigs` 与 `CDNEditorConfigsOverrides`
- 各 Editor 面板维度掩码，以及当前编辑的 Channel / DevelopMode

这些数据保存在 `ConfigMaster.asset`，不会写入 `ConfigRuntime.asset`。

Config 矩阵只生成非 `None` 的 Platform × Channel 组合。`PlatformType.None` 与 `ChannelType.None` 仅供框架表示未识别或尚未加载状态，不能通过 `GetAppConfigs`、`GetPrivacyConfigs`、`TryGetEntry` 或 `EditorAddEntry` 作为实际配置坐标使用。旧资产中的 Platform None 行会删除；Channel None 行在同平台尚无 Official 行时迁移为 Official，否则作为重复行删除；旧的当前渠道 None 同样归一为 `Official`。

## Active BuildTarget 平台真相源

`CurrentPlatform` 是只读计算属性，不序列化，也不能由 ConfigWindow、Pipify 或旧配置资产手动改写。它统一使用 `EditorUtil.Config.ActivePlatform` 映射当前 Unity Active BuildTarget。ConfigWindow 顶栏的编辑平台是独立窗口状态，仅用于选择要编辑的矩阵份，不改变本属性：

| Unity `activeBuildTarget` | `CurrentPlatform` |
|---|---|
| `Android` | `PlatformType.Android` |
| `iOS` | `PlatformType.iOS` |
| `WebGL` | `PlatformType.WebGL` |
| 其他目标 | `PlatformType.None` |

映射为 `None` 时，ConfigWindow 仍可选择平台编辑并保存，但 ConfigRuntime 导出和所选 YooAsset 配置生效会被阻断；Pipify 的 Config / Bundle / Player 构建等生产入口仍要求先切换到受支持的 Unity BuildTarget。ConfigWindow 的 CDN 部署与清理由自身明确选择的编辑平台解析配置和路径，不依赖 Active BuildTarget。

## 结构版本保护

- `ConfigSchemaVersion` 标识设计态资产结构版本，当前固定为 `1`。
- 新建资产直接写入当前版本；Editor 脚本重载后只做版本一致性检查，不修改资产。
- 低于或高于当前版本的资产都会报错，必须先使用对应历史版本完成升级或重新生成，最新版不再内置旧结构迁移。

关键源码：[ConfigMasterSO.cs](../../../Scripts/Editor/Config/ConfigMasterSO.cs)、[EditorUtil.Config.ActivePlatform.cs](../../../Scripts/Editor/EditorUtil/EditorUtil.Config/EditorUtil.Config.ActivePlatform.cs)、[SchemaGuard.md](../EditorUtil/EditorUtil.Config/EditorUtil.Config.SchemaGuard.md)、[ConfigWindow.md](../Windows/ConfigWindow.md)、[ConfigRuntimeSO.md](../../Runtime/Modules/Config/ConfigRuntimeSO.md)。
