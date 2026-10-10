# ConfigMasterSO

`ConfigMasterSO` 是 `NovaFramework.Editor` 下的设计态配置资产，只能由 Editor 程序集访问。ConfigWindow 编辑该资产，Exporter 将当前三维坐标裁剪为 `ConfigRuntimeSO`。平台维度不是 ConfigMaster 资产状态：`CurrentPlatform` 每次访问都实时映射 Unity 当前 `EditorUserBuildSettings.activeBuildTarget`；ConfigWindow 另以不写入 ConfigMaster 的窗口状态选择编辑平台，渠道与开发模式仍由资产保存和选择。

## Runtime 数据来源

- `PlatformChannelEntry.AppConfigsByMode` → `AppConfigs`
- `PlatformChannelEntry.PrivacyConfigsByMode` → `PrivacyConfigs`；使用独立 `PrivacyConfigsMask`
- `Namespace` 与 `NamespaceOverrides` → `Namespace`
- `HybridEditorConfigs.GameEntranceProcedureName / AotMetadataDlls / StartupGameDlls` → 去除构建路径后生成 `HybridConfigs`
- Runtime SDK 与 Kit 配置矩阵
- 顶层 `Custom` → 本地 JSONPath/string 默认值；云端完整 JSON 不受这些路径限制

旧资产首次由 ConfigWindow 建立工作副本时，会一次性为尚未填写的 `PrivacyInfoConfig` 补入 Solar 参考模板；保存后记录迁移标记，用户后续主动留空不会被再次覆盖。

SDK / Kit 配置 DTO 会跨平台保留，以便同一份 ConfigMaster 安全编辑 Android、iOS 与 WebGL 矩阵。某个平台不支持厂商原生能力时，只禁用该平台实现，不删除 DTO 类型，也不清理其他平台已经保存的配置。

## Editor-only 数据

- `YooAssetEditorConfigs` 与 `YooAssetEditorConfigsOverrides`
- `HybridEditorConfigs.RunningGameDlls`、`LinkXmlTargetPath`、DLL 源/目标路径及对应 Overrides
- `CDNEditorConfigs` 与 `CDNEditorConfigsOverrides`
- `ResourceEncryption`：资源密钥、独立维度掩码、当前编号与客户端源码导出目录；顶部保存只写资产，导出时生成源码，不写入 ConfigRuntime
- 各 Editor 面板维度掩码，以及当前编辑的 Channel / DevelopMode

这些数据保存在 `ConfigMaster.asset`，不会写入 `ConfigRuntime.asset`。

## 保存与导出

“保存”提交 ConfigWindow 的工作副本到 ConfigMaster；“导出”读取已保存的配置，按明确三维坐标生成 ConfigRuntime，并单独生成客户端密钥源码。保存不生成源码，导出不替用户保存或修正 Master。

保存依据各面板**当前勾选的维度**自动整理数据，不因历史无效维度残留要求用户确认：

- 未勾选轴以顶部当前编辑坐标为来源合并，同一逻辑组共用值；已勾选轴保留各组的独立配置。
- App、Privacy、SDK、Kit 保留完整物理矩阵，将共用组内的各格同步为同值，不删除其他有效平台或渠道的格；暂未启用的 SDK/Kit 存量也参与整理。
- Namespace、HybridCLR、YooAsset、CDN 的失效 Override 自动清理；全局配置保留顶层值并清空旧分维分支。
- ResourceEncryption 只整理各包的 `ActiveKeyOverrides`，不删除 `Keys`；旧资源需要的密钥不会因减维丢失。

整理在工作副本上原子执行，并在校验通过后落盘。遇到重复有效记录、无法确定来源等真实结构损坏时回滚，不猜测数据。导出继续执行只读校验，采用仍勾选的维度解析当前切片。窗口编辑平台与 Unity Active BuildTarget 是两种用途：保存取前者，生产导出必须符合后者。

实现边界见 [DimensionProjector](../EditorUtil/EditorUtil.Config/EditorUtil.Config.DimensionProjector.md)，密钥配置与导出见 [资源加解密](../../Runtime/Modules/Asset/ResourceEncryption.md)。

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
