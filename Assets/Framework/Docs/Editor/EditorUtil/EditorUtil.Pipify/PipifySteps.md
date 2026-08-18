# PipifySteps

`PipifySteps` 是 Pipify 的内置步骤目录。  
这页不再试图充当“所有 Step 参数字典总表”，而是回答三个更关键的问题：

- Pipify 现在到底能编排哪些流程？
- 常见批处理应该怎么组合这些 Step？
- 每一类 Step 的前置条件和失败点在哪里？

## 什么时候先看这页

优先看这页的场景：

- 你准备组一个新的 Batch，但不知道先后顺序。
- 你要判断某个能力应该走 `Pipify`，还是直接走菜单 / 独立工具。
- 你要查某个 Step 的职责，而不是想看所有参数细节。

如果你已经确定具体参数怎么写，继续看：

- [PipifyStepAttribute.md](./PipifyStepAttribute.md)
- [PipifyContext.md](./PipifyContext.md)
- 具体工具页，比如 `EditorUtil.BundleBuilder`、`EditorUtil.HybridCLR`、各导出器文档

## Pipify 当前覆盖的能力

### 1. HybridCLR 流程

目标：

- 生成或补齐 HybridCLR 产物
- 编译业务 DLL
- 拷贝 AOT / Game DLL

高频 Step：

- `hybridclr.validate_linkxml`
- `hybridclr.compile_dll_active_build_target`
- `hybridclr.generate_all`
- `hybridclr.copy_aot_dll`
- `hybridclr.copy_game_dll`

适合场景：

- 热更 DLL 产物刷新
- HybridCLR 完整预构建

### 2. Android 依赖准备

目标：

- 在 HybridCLR 或打包前先跑一次 EDM4U Resolve

高频 Step：

- `edm4u.android_resolve`

适合场景：

- Android 构建链起点
- 避免 `GeneratedLocalRepo` 缺失导致后续构建失败

### 3. 资源导出

目标：

- 从当前工程状态导出 Config / Table / UI / Localization / Network / Sound / Vibrate

高频 Step 族：

- `export.config`
- `export.table.data` / `export.table.code`
- `export.ui.data` / `export.ui.code`
- `export.localization.*`
- `export.network.*`
- `export.sound.*`
- `export.vibrate.*`

适合场景：

- 构建前统一刷新资源导出物
- 只重导某一模块的数据或代码

`export.config` 的参数区依次显示三个枚举下拉框：`Platform`、`Channel`、`DevelopMode`。新建条目时立即读取当前激活
`ConfigMasterSO` 的三项当前值并写入 `ParamsJson`，因此参数不会为空。历史空参数条目在第一次执行前执行同样的初始化，
并立即保存所属 `PipifySettingsSO`；后续执行只使用已经固化的值，不再跟随 ConfigMaster 当前选择变化。

执行时 Step 将三项显式传给 `EditorUtil.Config.Exporter.Export`，只更新目标 `ConfigRuntimeSO`，不会修改、标脏或保存
`ConfigMasterSO`。CLI 参数覆盖在历史条目固化之后应用，只对本次执行生效，不回写已经保存的 Step 参数。

### 4. Bundle 构建

目标：

- 按目标资源类型选择标准 AssetBundle 或 RawFile 构建

高频 Step：

- `bundlebuilder.build`：`ScriptableBuildPipeline` + `AssetBundle`
- `bundlebuilder.build_raw_file`：仅针对 `PackRawFile` 资源，使用 `RawFileBuildPipeline` + `RawBundle`

适合场景：

- 资源包构建阶段

两个 Step 是独立的可选构建入口，不要求在同一 Batch 中连续运行。

### 5. Player 打包

目标：

- 生成安装包或工程导出产物

高频 Step：

- `build.package`

适合场景：

- 资源导出与 Bundle 构建之后的最终打包

### 6. 系统外壳辅助

目标：

- 在批处理中打开目标目录，便于人工接力

高频 Step：

- `shell.open_folder`

适合场景：

- 构建产物落地后直接打开目录
- 导出完成后快速跳到目标位置检查结果

### 7. 批量 Excel 导出

目标：

- 用单个 `export.excel.all` Step 刷新所有 Excel 派生的数据、类型和辅助清单

固定顺序：

1. Table 数据 / 类型
2. UI 数据 / 类型
3. Localization 文本数据 / 类型、语言列表、字体数据 / 类型
4. Network HostKey 数据 / 类型、NetCmd 数据 / 类型
5. Sound 数据 / 类型
6. Vibrate Emphasis 数据 / 类型、Custom 数据 / 类型

该 Step 明确不执行 `export.config` 与 `export.network.proto`。它直接复用现有原子 Step，每个原子步骤前响应取消，首个失败即中断。

### 8. 飞书机器人通知

目标：

- 通过 `notification.feishu_webhook` 在 Batch 任意位置发送自定义文本通知

参数：

- `WebhookUrl`：窗口标签为 `Webhook URL`，使用密码框遮罩，但仍以明文保存在 PipifySettingsSO
- `MessageText`：窗口标签为“文案”，使用 3–8 行自适应 TextArea，可直接输入并保留换行排版

文案支持 `{Platform}` / `{Channel}` / `{Package}` / `{Version}` / `{Time}`。发送前从当前激活的
`ConfigMasterSO` 读取 Platform 与 Channel，从 canonical `Nova.prefab` 读取 YooAsset 默认资源包名，
Version 使用 `Application.version`，Time 使用实际发送时刻并格式化为 `yyyy-MM-dd-HH-mm-ss`。
参数区 HelpBox 会直接展示这些规则；未知占位符保持原样。

Step 发送飞书标准 `msg_type=text` 请求。参数为空、URL 无效、HTTP 失败、响应缺少业务码或业务码非 0 时均抛错中断；日志不会输出完整 Webhook URL。

### 9. CDN 资源部署

目标：

- 使用当前激活 `ConfigMasterSO` 已配置的 OSS Endpoint、密钥与固定路径前缀
- 在 Pipify Batch 中单独指定版本检查文件位置与热更资源目录位置

Step：

- `cdn.deploy`：显示名“批量部署资源到 CDN”，分类“CDN”

参数：

- `VersionCheckLocalFilePath`：窗口标签为“版本检查-本地文件位置”
- `VersionCheckRemoteFilePath`：窗口标签为“版本检查-云端文件位置”；当前维度 Config 的 `PresetOSSPath` 以前缀只读框显示
- `AutoLinkLatestVersion`：窗口标签为“自动关联最新版本”，默认 `true`；开启时 `LocalDirectory` 作为包根或版本目录锚点，参数面板实时显示最新完整版本目录并将输入框置为只读，执行前仍按 Config 相同的完整性与写入时间规则重新解析；失败时显示红色路径和错误说明
- `LocalDirectory`：窗口标签为“热更资源-本地目录位置”；自动关联开启时作为可编辑锚点，关闭时必须手工指向待部署目录
- `RemoteDirectory`：窗口标签为“热更资源-云端目录位置”；当前维度 Config 的 `PresetOSSPath` 以前缀只读框显示
- `CleanRemoteFilesAndDirectories`：窗口标签为“清理云端文件和目录”，默认 `false`；字段下方 HelpBox 与参数整行左边缘对齐，说明先清理再上传、清理范围和失败停止行为

四个路径都支持大小写敏感的 `{Platform}` / `{Channel}` / `{Package}` / `{Version}`。执行时按当前
`Platform / Channel / DevelopMode` Resolve CDN 配置快照，仅在快照中覆盖这四个路径和自动关联开关，不回写
`ConfigMasterSO`。自动关联的 `PackageFilePrefix` 取当前 ConfigMaster 当前维度的 YooAsset 配置，不读取 `YooAssetSettings.asset`。开启清理时先删除本次版本检查文件并清理本次热更资源目录，再调用上传；清理失败时不上传并中断 Batch。
随后直接调用 `EditorUtil.CDN.DeployAsync`；配置、目录、清理或上传失败时抛错并中断 Batch。
版本检查本地与云端文件位置都非空时，该单文件会与热更资源目录合并进入同一上传计划。
参数区不提供独立部署按钮，部署只由 Pipify Runner 执行该 Step 时触发。

### 10. CDN 白名单部署

目标：

- 使用当前激活 `ConfigMasterSO` 已配置的 OSS Endpoint、密钥与固定路径前缀
- 在 Pipify Batch 中配置设备 ID、配置文件云端文件位置、三个 YooAsset 版本文件及其远端目录

Step：

- `cdn.whitelist.deploy`：显示名“白名单批量部署到 CDN”，分类“CDN”

参数：

- `DeviceIDs`：窗口标签为“配置文件-设备ID（每行一个设备ID）”，使用 3–8 行自适应 TextArea
- `WhitelistRemoteFilePath`：窗口标签为“配置文件云端文件位置”；填写包含 `.json` 文件名的完整对象位置
- `AutoLinkLatestVersion`：窗口标签为“自动关联最新版本”，默认 `true`；开启时以 `ManifestBytesLocalFilePath` 为锚点，参数面板实时显示同一最新完整版本的 `.bytes/.hash/.version` 并将三项置为只读，执行前再次解析；失败时显示红色路径和错误说明
- `ManifestBytesLocalFilePath`：窗口标签为“版本文件(.bytes)-本地文件位置”；自动关联开启时作为可编辑锚点
- `ManifestHashLocalFilePath`：窗口标签为“版本文件(.hash)-本地文件位置”
- `PackageVersionLocalFilePath`：窗口标签为“版本文件(.version)-本地文件位置”
- `RemoteDirectory`：窗口标签为“版本文件云端目录位置”；当前维度 Config 的 `PresetOSSPath` 以前缀只读框显示
- `CleanRemoteFilesAndDirectories`：窗口标签为“清理云端文件和目录”，默认 `false`；字段下方 HelpBox 与参数整行左边缘对齐，说明先清理再上传、清理范围和失败停止行为

执行时按当前 `Platform / Channel / DevelopMode` Resolve CDN 配置快照，仅覆盖上述七个内容参数并读取本次清理开关，不回写
`ConfigMasterSO`。自动关联的三个文件名取当前 ConfigMaster 当前维度的 YooAsset 配置，不读取 `YooAssetSettings.asset`。设备 ID 按行解析，生成 `VersionsCheckWhiteList.json` 时统一去空、Trim 和去重；配置文件上传到
`WhitelistRemoteFilePath` 指定的完整对象位置，`.bytes/.hash/.version` 三个文件上传到 `RemoteDirectory`。配置文件位置为空或非法时仅跳过
配置文件，不回退到版本文件目录。五个路径支持大小写敏感的 `{Platform}` / `{Channel}` / `{Package}` / `{Version}`
占位符。开启清理时先删除本次白名单配置文件并清理版本文件远端目录；远端目录为空、清理失败或任一实际上传失败时抛错并中断 Batch。

### 11. CDN 缓存清理

目标：

- 在 Pipify Batch 中按 Cloudflare Zone 批量清除指定 CDN 缓存 URL
- 参数只覆盖本次执行快照，不回写当前 `ConfigMasterSO`

Step：

- `cdn.purge`：显示名“批量清除 CDN 缓存”，分类“CDN”

参数：

- `ZoneID`：窗口标签为“Zone ID”
- `Token`：窗口标签为“API Token”，使用密码框遮罩，但仍以明文保存在 `PipifySettingsSO`
- `CachePaths`：窗口标签为“缓存路径”，使用 3–8 行自适应 TextArea；支持英文逗号、英文分号或换行分隔

执行时按当前 `Platform / Channel / DevelopMode` Resolve CDN 配置快照，仅覆盖上述三个 Cloudflare 字段，
随后调用 `EditorUtil.CDN.PurgeAsync`。URL 会去重并按每批最多 100 条顺序提交；参数非法、请求失败或
Cloudflare 返回业务失败时抛错并中断 Batch。

## 常见组合方式

### 1. HybridCLR 完整链

推荐顺序：

1. `edm4u.android_resolve`（Android 时）
2. `hybridclr.validate_linkxml`
3. `hybridclr.generate_all`
4. `hybridclr.copy_aot_dll`
5. `hybridclr.copy_game_dll`

适合：

- 要一次性拿到可用于运行时加载的完整 DLL 产物

### 2. 构建前资源刷新链

推荐顺序：

1. `export.config`
2. `export.table.data` / `export.table.code`
3. `export.ui.data` / `export.ui.code`
4. 按需追加 `localization / network / sound / vibrate`

适合：

- 正式构建前刷新所有高价值导出物

### 3. 完整打包链

典型顺序：

1. `export.config`
2. 按需追加其他资源导出 Step
3. 按目标资源选择 `bundlebuilder.build` 或 `bundlebuilder.build_raw_file`
4. `build.package`
5. `shell.open_folder`

适合：

- 一条龙产出资源包 + Player 包体

## 关键前置条件

### Config 导出

- 必须能通过 `EditorUtil.Config.WorkspaceActive` 定位当前激活 `ConfigMasterSO`
- `ConfigMasterSO.ExportTarget` 不能为空
- `Platform` / `Channel` 不可为 `None`，且 ConfigMaster 中必须存在对应矩阵配置

### 组件型导出 Step

`Table / UI / Localization / Network / Sound / Vibrate` 这些 Step 都依赖：

- 当前活动场景里能找到 `Nova`
- 对应组件挂在 `Nova` 层级上

这类 Step 不是纯资产导出器，它们依赖“当前场景上下文”。

### HybridCLR

- 相关菜单链和 `EditorUtil.HybridCLR` 必须可用
- `copy_aot_dll / copy_game_dll` 之前必须先有 DLL 产物

### Bundle / Build

- 构建类 Batch 应先跑 `export.config`，再按目标资源选择 `bundlebuilder.build`（标准 AssetBundle）或 `bundlebuilder.build_raw_file`（`PackRawFile`）；需要 Player 产物时再运行 `build.package`
- 其他前置导出物必须已就绪
- `build.package` 的产物命名还依赖当前激活 `ConfigRuntimeSO` 的 `DevelopMode`
- `build.package` 的 `OutputFolderPath` 不做特殊字符清洗；相对路径基于项目根解析，绝对路径直接使用

## 常见失败点

- 当前场景没有 `Nova`：组件型导出 Step 会直接失败。
- UI Excel 校验、Luban 或暂存发布失败：`export.ui.data` / `export.ui.code` 会抛出异常并中断流水线，不再以完成任务返回。
- Sound/Vibrate 数据或类型批次返回 `false`：对应 Step 立即抛出异常并中断流水线，不会继续执行后续 Step；每个 Sound 或 Vibrate 区域只建立一次发布事务。
- `ConfigMasterSO.ExportTarget` 没配：`export.config` 会中断流水线。
- `export.config` 参数为 `None` 或指定矩阵不存在：会携带 Platform / Channel / DevelopMode 坐标中断流水线。
- HybridCLR 只跑了拷贝，没先生成 DLL：拷贝类 Step 会失去输入产物。
- Android 没先 `edm4u.android_resolve`：后续构建链可能在依赖目录上失败。
- 手动移除或跳过 `export.config` 后直接跑 `build.package`：文件名开发模式段会降级为 `Debug`，并输出 Warning。
- `PlayerSettings.bundleVersion` 或 `OutputFolderPath` 写入路径敏感字符：`BuildPackage` 不会替它们清洗，可能生成不适合 Xcode / shell / 文件系统的路径。

## 关键源码入口

关键源码：

- [PipifySteps.HybridCLR.cs](../../../../Scripts/Editor/EditorUtil/EditorUtil.Pipify/Steps/PipifySteps.HybridCLR.cs)
- [PipifySteps.Export.cs](../../../../Scripts/Editor/EditorUtil/EditorUtil.Pipify/Steps/PipifySteps.Export.cs)
- [PipifySteps.Export.All.cs](../../../../Scripts/Editor/EditorUtil/EditorUtil.Pipify/Steps/PipifySteps.Export.All.cs)
- [PipifySteps.Export.Helpers.cs](../../../../Scripts/Editor/EditorUtil/EditorUtil.Pipify/Steps/PipifySteps.Export.Helpers.cs)
- [PipifySteps.BundleBuilder.cs](../../../../Scripts/Editor/EditorUtil/EditorUtil.Pipify/Steps/PipifySteps.BundleBuilder.cs)
- [PipifySteps.Build.cs](../../../../Scripts/Editor/EditorUtil/EditorUtil.Pipify/Steps/PipifySteps.Build.cs)
- [PipifySteps.Shell.cs](../../../../Scripts/Editor/EditorUtil/EditorUtil.Pipify/Steps/PipifySteps.Shell.cs)
- [PipifySteps.Notification.cs](../../../../Scripts/Editor/EditorUtil/EditorUtil.Pipify/Steps/PipifySteps.Notification.cs)
- [PipifySteps.CDN.cs](../../../../Scripts/Editor/EditorUtil/EditorUtil.Pipify/Steps/PipifySteps.CDN.cs)

关键入口：

- `[PipifyStep(...)]` 特性声明
- `Helpers.ResolveConfigMaster()`
- `Helpers.ResolveComponentOnNova<T>()`

## 相关文档

- [EditorUtil.Pipify.md](./EditorUtil.Pipify.md)
- [PipifyContext.md](./PipifyContext.md)
- [PipifyStepAttribute.md](./PipifyStepAttribute.md)
- [EditorUtil.BundleBuilder.md](../EditorUtil.BundleBuilder/EditorUtil.BundleBuilder.md)
- [Editor.md](../../Editor.md)

## Output Naming In `build.package`

`build.package` 调用 `EditorUtil.Build.BuildPackage` 自动生成产物名，格式与清洗规则以 [EditorUtil.Build.md](../EditorUtil.Build/EditorUtil.Build.md) 为准。

当前关键规则：

- `PlayerSettings.productName` 只保留英文字母和数字；冒号、空格、引号、浪线、中文等都会被删除。
- `ConfigRuntimeSO.DevelopMode` 决定文件名中的 `Debug` / `Release` 段；找不到激活 ConfigRuntimeSO 时降级为 `Debug`。
- `PlayerSettings.bundleVersion` 原样拼入产物名，不做特殊字符清洗。
- `PackageParams.OutputFolderPath` 只负责定位输出文件夹；相对路径基于项目根解析，绝对路径直接使用，`~` 不会展开为用户 Home。
- iOS 后处理里的 entitlements 文件名当前只对 `Application.productName` 去空格，不复用产物名的字母数字白名单；启用 iOS capability 的工程仍应避免在 productName 中使用 Xcode / 文件系统敏感字符。

## Android Output Options In `build.package`

`build.package` exposes Android output options on `PackageParams`:

- `SplitApplicationBinary`
- `BuildAppBundle`

`SplitApplicationBinary` is declared above `BuildAppBundle`, so PipifyWindow draws it above the AAB switch. Both fields are Android-only output options: `BuildAppBundle` controls `EditorUserBuildSettings.buildAppBundle`, while `SplitApplicationBinary` controls `PlayerSettings.Android.splitApplicationBinary`. The Step passes both values to `EditorUtil.Build.BuildPackage`, which temporarily writes both settings, then restores them in `finally`.

### Android DevelopmentBuild AAB Signing Risk

When the PipifyWindow parameter Drawer first enters the following `build.package` combination, it immediately writes a Warning and displays a confirmation dialog:

```text
Target == Android
&& DevelopmentBuild == true
&& BuildAppBundle == true
&& !EditorUserBuildSettings.exportAsGoogleAndroidProject
```

In the current Unity build chain, this combination produces an AAB without an upload signature even when a keystore is configured. Choosing **取消并恢复** restores the entire `PackageParams` snapshot and does not write `ParamsJson` or mark the Batch dirty; choosing **仍要保留** persists the edited parameters normally. The dialog belongs to parameter editing and is not deferred until the Batch runs. To upload an AAB to Google Play Console, turn off `DevelopmentBuild` before packaging.

## Android Signing In `build.package`

`build.package` exposes Android signing parameters on `PackageParams`:

- `UseAndroidKeystore`
- `AndroidKeystorePath`
- `AndroidKeystorePass`
- `AndroidKeyalias`
- `AndroidKeyaliasPass`

These fields are visible only for Android builds, and the four value fields are visible only when `UseAndroidKeystore` is enabled. Password fields use `PipifyPasswordAttribute`, so `PipifyWindow` renders them with `PasswordField`; the stored value is still a normal string for `PipifySettingsSO` and CLI overrides.

When enabled, the Step temporarily writes `PlayerSettings.Android.useCustomKeystore`, `keystoreName`, `keystorePass`, `keyaliasName`, and `keyaliasPass`, calls `EditorUtil.Build.BuildPackage`, then restores the previous PlayerSettings values in `finally`. Missing signing fields fail before `BuildPlayer` runs.
