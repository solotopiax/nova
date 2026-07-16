# Changelog — com.solotopia.nova.framework

## [Unreleased]

## [0.5.41] - 2026-07-16

### Added

- PlugPals“已安装”页新增横向“一键升级”按钮，可一次确认后批量升级当前列表中的全部可升级包；没有可升级项时按钮自动禁用。

### Changed

- PlugPals 窗口主标题更名为“PlugPals 云插件服务中心”，并让标题布局自适应窗口宽度，避免较长标题被裁切。

## [0.5.40] - 2026-07-16

### Added

- 新增 `EditorUtil.ProjectGuard` 与 `ProjectGuardWindow`，集中校验场景入口、资源目录和项目结构，并在进入 Play Mode 前阻断错误级诊断项。
- 新增 `START_HERE.md` 及项目结构、资源工作流、验证流程等渐进式接入文档。

### Fixed

- `Nova.OnDestroy` 仅允许当前活动实例执行 Manager 关闭与静态引用清理，避免旧实例销毁时误清理新实例状态。

## [0.5.39] - 2026-07-15

### Added

- `Samples~/MainDemo` 震动 Demo（`DemoVibrateView`）新增强度、尖锐度、预持续时间、持续时间的自定义输入框，并对输入参数做范围校验。

## [0.5.38] - 2026-07-13

### Fixed

- 将 `com.solotopia.unitask` 最低依赖版本提升至 `10.0.6`，确保 NovaSpark 在 Unity 6000.5 新工程中解析到 bundled UniTask core 2.5.11，避免 Tracker 使用废弃 `TreeViewItem` API 导致 `CS0619`。

## [0.5.37] - 2026-07-13

### Changed

- Pipify 的 Android 签名配置统一为 keystore path 与 key alias 命名，已有配置升级后不会自动迁移，需重新保存对应值，否则构建前校验会报配置缺失；Split Application Binary 也可脱离 App Bundle 独立生效。
- PlugPals 安装带 scoped registry 的包时，第三方依赖保持为 UPM 传递依赖，不再展开到项目顶层 manifest；卸载仍会清理旧版留下的同类条目。

### Removed

- 旧 Spine 槽位换肤与动画时长查询的四个公开扩展入口已删除且不会恢复；外部 Spine 使用方需在升级前迁移到自身适配层或 Spine 官方 API。

## [0.5.36] - 2026-07-08

### Fixed

- 清理 MainDemo 示例 asmdef 克隆残留引用：`NovaFramework.Samples.Runtime` 删除代码未使用的 gamelogin / gamesave / tga / appsflyer / ad 跨包程序集引用（MainDemo Runtime 实际仅依赖框架自身），避免消费工程仅安装框架、import MainDemo 后因缺这些子包程序集编译报 CS0234。此为 sample 模板根因清理——`nova-create-sample` 由 MainDemo 克隆子 sample 时不再继承这批残留引用。

## [0.5.35] - 2026-07-08

### Changed

- SDK 模块重构：`SDKManager` 运行时启用统一以 `ConfigMaster.EnabledSDKs` 为唯一源，排序改用 `ISDKPlugin.Priority`（接口属性）；`SDKComponentInspector` 订阅 `ConfigMaster` 保存事件，`ConfigWindow` 保存后即时刷新 SDK 面板。
- 明确 DataMaster 接口规范：`topicId` 传 `Params` 字典 key（`topic_name`），非 `experiment.topicId`；补必传分流用户属性 `app_version` / `install_time` 处理。

### Removed

- 删除 `SDKPluginEntry` 的 `[SerializeField] public int Priority` 字段（运行时排序已改用 `ISDKPlugin.Priority`，该序列化字段为冗余残留）。**注意**：旧 `ConfigMaster.asset` 中该字段的存值在反序列化时丢失，属序列化行为变更（不影响跨包编译，无消费方引用该字段）。

## [0.5.34] - 2026-07-03

### Changed

- 宏定义迁移至 asmdef versionDefines（ADR-064 后半步）：`NOVA_NICEVIBRATIONS`（`com.solotopia.nicevibrations` ≥ 10.0.5）、`NOVA_SIMPLEDISKUTILS`（`com.solotopia.simplediskutils` ≥ 1.0.7）改由 `NovaFramework.Runtime.asmdef` 的 `versionDefines` 声明，装包即自动跨平台定义；`package.json` 的 `nova.requiredLibraries` 移除对应 `defineSymbols` 展示字段（requiredLibraries 仅作 PlugPals 展示，宏定义权威改由 asmdef 承载）。

### Removed

- 删除 `NOVA_UNIWEBVIEW`、`NOVA_WEBGLSUPPORT` 死宏声明（孤儿宏根除）：`package.json` 的 `nova.requiredLibraries` 中 `com.onevcat.uniwebview`、`com.solotopia.webglsupport` 移除 `defineSymbols`，asmdef 不再重新定义（全仓无 `#if` 引用，确认为死宏）。

### Fixed

- Debug 模块 `Settings.asset` 默认 `_isEnabled` 置为 0（关闭），避免开发态 Debug 面板误启用。

## [0.5.33] - 2026-07-03

### Added

- `NetResponse<T>` 新增 `Fail(int errorCode, string errorMessage, T data)` 工厂重载，支持失败响应携带业务数据（ADR-068）：服务端在业务错误码下仍返回业务体（如绑定冲突返回 existing_uid）时，业务侧可在 `IsSuccess=false` 时读取 `Data` 获取附带信息。

### Changed

- `NetService.SendAsync` 业务错误码分支增强：服务端返回业务错误且携带业务体时，尝试解析并随失败响应带回；解析失败或无业务体则降级为不带 data 的失败响应，不影响错误码 / 描述透传。
- BuildProcessor 的 `UnityManifest.xml` 模板路径解析重构：新增 `NovaBuildShared.ResolveUnityManifestTemplatePath()`，开发态优先 `Assets/Framework` 路径，UPM 引用态回退包 `resolvedPath` / `assetPath` / `AssetDatabase.FindAssets` 三级定位，修复 UPM 引用态模板缺失导致 Android 构建漏复制 UnityManifest 的问题。
- `EditorUtil.Config.WorkspaceActive.GetActiveRuntime` 定位策略增强（ADR-047）：首选 `ConfigMasterSO.ExportTarget` 序列化引用（GUID 追踪，资产可置于任意位置），`ExportTarget` 为 null 时回退 ADR-033 布局约定兜底，覆盖未配 ExportTarget 的老工程与新 sample。
- 同步刷新 Minds / Docs / AGENTS.md 文档（ADR-067 登录 / 绑定 / 云存档三端分离、ADR-068 网络失败携带数据、ADR-069 云存档跨用户查询、PAT-140 UPM 包与 sample 依赖关系）。

## [0.5.32] - 2026-06-19

### Changed

- PlugPals 公共仓库默认地址恢复为公网 `https://upm.solotopiax.com`（`c_DefaultExternalUrl` 常量与 nova-publish 技能默认值同步切回公网；内网云 `4874` 不受影响）。
- 随上轮 Debug 面板、BuildProcessor、EditorUtil.Luban/Network 等已提交改动一并发版。

## [0.5.31] - 2026-06-18

### Changed

- PlugPals 公共仓库默认地址恢复为公网 `https://upm.solotopiax.com`。

## [0.5.30] - 2026-06-18

### Changed

- 例行版本升级。

本文件记录 Nova Framework 主包各版本的变更内容，遵循 [Keep a Changelog](https://keepachangelog.com/) 格式。

---

## [0.5.29] - 2026-06-16

### Added
- PlugPals 安装时把被声明 registry scope 覆盖的依赖显式写入项目 `manifest.dependencies` 顶层，确保 UPM 作为直接依赖解析安装（仅作主包传递依赖时 UPM 不保证拉取 scoped-registry 包）；卸载时一并移除。
- PlugPals 卸载按钮增加确认弹窗。

### Changed
- `EditorUtil.PlugPals.UninstallPackage` 增加 `registryUrlsNeededByOthers` 参数，移除私有仓库前对仍被其它已安装包共用的 url 做保留保护（public 签名变更，仅 PlugPals 窗口内部调用）。

## [0.5.28] - 2026-06-16

### Added
- PlugPals 支持包自带声明的作用域仓库（`package.json` 的 `nova.scopedRegistries`）：安装/升级时按 URL 注册或更新到项目 `manifest.json`，卸载时按 URL 移除；被声明 scope 前缀覆盖的依赖在缺库检测中放行，交由该私有仓库 + UPM 传递解析自动拉取（典型场景：MAX 子包依赖的 AppLovin 官方私有云仓库）。
- PlugPals 卸载时对仍被其它已安装包共用的私有仓库做保留保护，避免误删；安装/升级前增加确认弹窗。

### Changed
- SDK 广告接口统一加载结果模型：`AdLoadEvent` 重命名为 `AdLoadResult` 并合并失败载荷，`IAdPlugin.RequestAsync` 返回 `UniTask<AdLoadResult>`，以 `Success` 标识成功/失败并携带错误码与描述。
- CheckUpdate 编辑器工具随 PlugPals registry 配置调整同步更新。

### Removed
- 删除 `AdLoadFailEvent`（失败信息合并入 `AdLoadResult`）。

## [0.5.27] - 2026-06-16

### Changed
- PlugPals 依赖检测重构：遍历包 `dependencies`，以 PlugPalsWindow 内存中已拉取的外网/内部云 registry 包列表判命中；命中依赖按来源自动配 scope 随主包安装，未命中（本地无 + 非 `com.unity.`/`com.solotopia.` 前缀 + registry 无）弹购买/内部云引导并中止安装。
- AssetManager 远端清单不可达时改为三级离线回退编排：沿用当前已激活清单 → 本地缓存版本清单 → 内置首包清单。

### Removed
- 移除 PlugPals 运行时宏注入机制（`requiredLibraries.defineSymbols` 注入、`PlugPalsInjectedDefines.json` 账本、后台审计弹窗、会话级抑制、scope 已注册判据）；可选库的宏改由各 asmdef 的 `versionDefines` / `defineConstraints` 自行处理。

## [0.5.26] - 2026-06-15

### Changed
- Asset 远端清单不可达时回退使用当前已激活清单或内置清单，避免 DNS / 网络异常导致启动流程卡死。
- PlugPals 缺失依赖检测跳过 Unity 包与已注册 scope，并对重复缺失提示做签名去重。
- MainDemo 字体资源随本轮主框架 sample 发布同步刷新。

## [0.5.25] - 2026-06-15

### Fixed
- 修复 PlugPalsWindow 点击安装/升级时仍可能在同步路径上拉取远端 package 元数据导致编辑器卡顿的问题；缺失三方库预检现在只使用包列表阶段已经写入条目的 `dependencies` / `nova.requiredLibraries` 元数据。

## [0.5.24] - 2026-06-15

### Fixed
- 修复 PlugPalsWindow 点击安装/升级时在 IMGUI 按钮同步栈内直接触发 UPM Resolve，导致消费端 Unity 编辑器长时间无响应的问题；现在 manifest 写入后将 Package Manager Resolve 排队到下一帧并合并重复请求。
- 收敛 PlugPalsWindow 远程包请求的 CancellationTokenSource 释放路径，避免 domain reload 前后重复 Dispose 的日志噪声。

### Changed
- 同步刷新 PlugPals 与 EditorUtil.PlugPals 文档，明确安装/升级/卸载后的 UPM Resolve 延迟执行边界。

## [0.5.23] - 2026-06-15

### Changed
- 补发 `upm-release-2026.06.10-01` 后主框架累计变更，包含 BestHTTP 可选后端解耦、内部云仓库展示与缺失依赖提示链路、Util.Json 依赖迁移等内容。
- MainDemo 示例与框架文档随本轮主包发版同步刷新。

## [0.5.22] - 2026-06-10

### Changed
- 网络模块增强 DoH 预热与缓存直连链路：数据加载完成后可自动触发预热，HTTP 请求优先复用缓存 IP 直连以提升请求稳定性，并同步收敛相关文档说明。
- ConfigWindow 的 Luban 相关面板与 Network Inspector 提示文案同步刷新，降低配置入口的理解成本。
- MainDemo 示例场景随主包同步更新。

## [0.5.21] - 2026-06-09

### Changed
- Config 导出时会将当前选中的 `DevelopMode` 回写到激活场景中的全部 `FrameworkComponent`，并在 Inspector 顶部以只读彩色文案显示当前开发模式。
- App / Asset 面板拆分为 Debug / Release 的主备地址配置，启动阶段改为读取节点本地序列化的 `DevelopMode` 选择路由，解除对 Config 加载时序的依赖。
- App 版本检查新增主备地址回退语义：主地址失败、超时、返回空内容或地址为空时自动切备用，备用同样不可用时统一返回 `NoDownload`。

## [0.5.20] - 2026-06-05

### Changed
- 启动流程优化：Splash / 进度面板的销毁时机由启动流程内部（ProcedureLoadDll）移交业务入口统一回收，避免 LoadDll → 业务流程衔接时提前回收启动 UI 导致首屏闪帧。
- 同步刷新框架 L0/L1/L2 文档（Editor 工具、Config 维度体系、Asset 热更接口等）与代码注释。

---

## [0.5.19] - 2026-06-04

### Added
- Config 维度体系增强：支持按面板维度掩码（PanelDimensionMask / TypedDimensionMask）与 HybridCLR / Namespace / YooAsset 维度 Override，配置导出按维度投影取数。

### Changed
- 网络 Kit 层（NetService / NetBuilder / NetResponse / NetParser 等）配合登录、存档、支付 Kit 接入做配套增强。
- 优化 Pipify 流程，场景切换时自动绑定相关设置。

### Fixed
- 修正示例工程中的若干运行期报错与冗余资源。

---

## [0.5.18] - 2026-06-01

### Fixed
- 修复 iOS 打包时 Pods 目录路径拼接在部分环境下异常的问题，xcframework 自动 Embed 更稳定。

---

## [0.5.17] - 2026-06-01

### Added
- 新增 Kit 配置体系：Kit 包（如登录、存档）可通过实现 `IKitConfig` 声明自己所需的配置，框架在 ConfigWindow 新增「Kit 配置」面板自动扫描并列出所有 Kit 配置项，按平台与渠道分别填写后随框架配置一起导出；业务侧通过 `Nova.Config.GetKitConfig<T>()` 取用，无需各 Kit 自建配置入口。
- Asset 模块支持启动期按 tag 切片预热：可在 AssetComponent Inspector 配置启动热更的 tag 列表，仅下载本次启动真正需要的资源分片，缩短首次进入耗时。
- Asset 模块新增按 tag / 按地址创建下载器、运行期刷新资源清单、清理无用缓存等接口，支持并发数与重试次数配置，便于业务侧做分阶段下载与缓存治理。
- Hotfix 阶段新增热更提示弹窗，向玩家展示更新进度与下载体量。

### Changed
- 热更流程支持热更完成后自动清理无用缓存，避免旧版本资源长期占用磁盘（可在 Inspector 开关）。

---

## [0.5.16] - 2026-05-29

### Changed
- 主包对 `com.solotopia.yooasset` 的依赖从 `1.0.0` 提升到当前最新版 `1.0.3`，使框架默认随附最新的资源系统封装层。

---

## [0.5.15] - 2026-05-29

### Fixed
- 修复外部工程 import sample 后 Console 持续刷 `Asset Packages/<pkg>/nova-samples.json has no meta file, but it's in an immutable folder` 警告并触发 `SamplePathRewriter.RunRewrite` 重跑的问题：`nova-samples.json` 是发版描述符，仅开发期使用，不应进入 npm tarball；现通过 `.npmignore` 显式排除该文件及其 .meta，外部工程不再看到它落在只读 `Packages/` 区。

---

## [0.5.14] - 2026-05-29

### Changed
- 发版流水线统一：`publish_packages.py` 重构后主包 MainDemo 与所有子包 sample 走完全对称的 Stage 1 / Stage 3，主包不再走专属分支。所有 sample 等量复制 `Docs/Excels` + `Docs/Protos`、注入 `Nova.prefab` 全套 `*SourceDirPath` PrefabInstance override、写入 `SamplePathManifest`、以 `Samples~` 形式被 npm pack 收录；Stage 3 finally 反向还原 `Samples~ → Samples~.dev` 后整体清空，临时态零残留。
- 新增 `Assets/Framework/nova-samples.json` 作为主包 sample 描述符，与子包对齐；发版脚本从描述符读取 `sampleName / sourceDir / expectedNamespace / sampleManifestRelative / devPathPrefix`，主包子包不再分两套硬编码常量。

### Fixed
- 修复发版脚本对 `nova-samples.json` 中 `devPathPrefix` 末尾斜杠的脆弱性：现在统一在描述符加载阶段做 `rstrip("/")` 防御性归一，避免 C# `SamplePathRewriter.LocateSampleRoot` 因 `Path.GetFileName(devRoot)` 返回空字符串而静默放弃路径重写——之前 LoginDemo 因源 `nova-samples.json` 误带尾斜杠出现该 bug。

---

## [0.5.13] - 2026-05-29

### Fixed
- `EditorUtil.Config.WorkspaceActive` 增加多 sample 切换感知：当前活跃 scene 在 `Assets/Samples/<sampleRoot>/` 下且与 `Globals.json` 缓存的 ConfigMaster 不在同一 sample 根时，自动按 scene 重新推断 ConfigMaster 并覆盖 `Globals.json`，避免外部工程同时 import 多个 sample 时打开 LoginDemo 场景却仍读 MainDemo 的 ConfigMaster / YooAssetSettings / ConfigRuntime 资产。

---

## [0.5.12] - 2026-05-29

### Fixed
- `EditorUtil.Config.WorkspaceActive` 第③段路径推断升级为「从 scene 所在目录起逐级向上递归找 `Editor/ConfigMaster.asset`」：开发态扁平结构 `Assets/Samples/{Demo}/{Scene}.unity` 与 UPM 导入态嵌套结构 `Assets/Samples/{PackageDisplayName}/{Version}/{SampleDisplayName}/{Scene}.unity` 共用同一逻辑，外部工程 import sample 后打开 ConfigWindow 立即识别激活 ConfigMaster，不再提示「未检测到激活的 ConfigMaster」。

---

## [0.5.11] - 2026-05-29

### Added
- ConfigMaster 新增 `YooAssetSettingsPath` / `BundleCollectorSettingPath` 字段，YooAsset 全局设置与 Bundle 收集器配置改由项目根相对路径显式声明，替代 `AssetDatabase.FindAssets` 全工程扫描。
- ConfigWindow 新增「YooAsset」配置面板与「BindGuide」绑定指引面板，可视化维护两条路径与对应资产引用。
- 新增 `EditorUtil.Config.WorkspaceActive` / `EditorUtil.Config.YooAssetInjector`，将路径注入收口在 ConfigMaster，避开 Editor 启动期 Resources 多副本玄学。

### Changed
- `Nova.Visitors` 框架版本号同步升至 0.5.11。
- ConfigMasterSO 中仅供 Editor 期消费的字段（YooAsset/BundleCollector 路径、AOT/Game DLL 列表、`EditorEntries` 视图、`GetCommon` / `EditorAddEntry` / `EditorRemoveEntryAt`）补齐 `#if UNITY_EDITOR` 包围与注释，运行时表面收紧。

### Demo
- `Assets/Samples/MainDemo/` 配置资产、字体 SDF、各 Demo View prefab 同步刷新；新增 `MainDemo/Editor/YooAssetSettings.asset` 作为 sample 自带 YooAsset 全局配置。

### Obs Vault
- 沉淀 ADR-DRAFT「YooAsset 设置经 ConfigMaster 注入」「Editor active master anchor」「Nova.prefab 跟随 framework」与多条 PAT（atomic write json via rename / upm private fork / publish sample rewrite symmetric / create sample user decides dirname）。

---

## [0.5.10] - 2026-05-28

### Changed
- `Nova.Visitors` 框架版本号同步升至 0.5.10。
- `SDKComponentInspector` 插件条目绘制层调整，编辑器交互体验保持原有契约。

### Removed
- 清理 `Assets/Tests/` 下两份临时回归测试脚本（AssetHandle / KitNetwork 重构验证用例），相关回归已并入对应模块自带测试。

### Demo
- `Assets/Samples/MainDemo/` 配置资产、本地化文本、Launcher 弹窗 prefab、字体 SDF 等资源同步刷新，跟随主包 Samples~ 一起发布。

---

## [0.5.9] - 2026-05-27

### Added
- 声音模块新增「按名称播放」入口，业务侧只需传 `ISoundRow.Name` 主键即可触发播放，无需再手动取行。
- 振动模块新增「按名称播放」入口，自定义振动与强调振动均可按数据表 Name 字段直接播放。

### Editor
- 新增 PlugPals 私有 Verdaccio 仓库 UPM 包管理工具，支持远程包列表拉取、安装/卸载、按版本查看更新日志。

### Docs
- 声音 / 振动 / Procedure 等模块 L2 文档同步刷新对外接口现状。

### Obs Vault
- 沉淀本期声音/振动 API 设计、UPM 包文档强制三件套、CHANGELOG 行文规范、XML 注释禁 HTML 转义、cs↔doc 同步铁律等多条规范。

---

## [0.5.8] - 2026-05-27

### Breaking Changes
- UI 模块新增「对象池开关」能力，关闭后视图直接销毁、不再走池缓存；UIView 子类如有覆写 OnInit 需按新签名补回参数。
- UIView 默认不再带淡入淡出，旧版自动挂 CanvasGroup 与 4 个淡入淡出字段已移除；如需淡入淡出请由业务侧自行实现。

### Added
- UI 视图打开 API 新增「是否走对象池」入参，业务可按视图按需选择缓存或直接销毁。

### Changed
- 资源加载文档全面强化「LoadXxx 必须经 Handle 释放」铁律，避免引用计数泄漏。
- 演示 prefab 同步清理已废弃的淡入淡出残留字段。

### Docs
- UI 模块与 Asset 模块的 L2 文档按本版本接口同步刷新。

### Obs Vault
- 沉淀 UI 深度因子重平衡 / Inspector 下沉、Asset Load API 统一回 Handle 等本期决策；并补全 API 命名、ManagerConfig 透传、UPM 版本管理等通用模式。

---

## [0.5.7] - 2026-05-26

### Breaking Changes
- 启动期 UI（进度面板 / 弹窗）改为多语言驱动，旧的单语言标题 / 内容字段已移除，文本统一由面板内多语言数组按类型显示。
- 弹窗显示接口签名调整，业务侧只需传按钮回调，文案由面板自身按弹窗类型选取。
- 移除一个无引用的 UI 加载抽象基类。

### Added
- 启动期新增独立本地化能力，可在资源系统就绪前的全链路（启动闪屏 → 版本检查 → 热更新 → 应用下载 → DLL 加载）安全使用本地化文本。
- 启动期内置中英文文案与字体资源，本地化资源路径模板可通过 ProcedureComponent Inspector 配置。

### Docs
- 启动期 UI 与本地化相关 L2 文档同步刷新。

---

## [0.5.6] - 2026-05-22

### Changed
- 网络 / 声音模块对外接口与 DTO 调整，调用方需按新签名迁移。
- 框架核心若干内部细节打磨。
- MainDemo 演示工程切换为基于 Nova.UI 的树形导航 + TMP 文字渲染。

### Added
- Vault 沉淀本期演示拓扑、UI 命名、Demo 覆盖标准、prefab 制作、文本组件、池化辐射等多条规范。

## [0.5.5] - 2026-05-22

### Fixed
- 外部工程导入 sample 后 Inspector 业务字段为空与 SDK `[Missing]` 提示问题已解决。
- 多版本 sample 共存时新旧版本识别不准的问题已修复（按 semver 数值排序）。

### Changed
- 发版脚本统一支持 docs / 业务字段两类自动注入，新增字段后无需改脚本。

---

## [0.5.4] - 2026-05-22

### Removed
- 放弃此前的桥接式生命周期方案，回归 HybridCLR 原生 MonoBehaviour + Prefab 直挂；相关脚本与 Inspector 一并清理。

### Added
- 发版流程支持把项目根 Docs 资源（表格 / 协议）随 sample 一起打包，外部工程导入后 Inspector 路径自动对齐 sample 内副本，无需手动改路径。

### Changed
- HybridCLR 约束规则全面重写为原生方案口径，dll 加载唯一入口与版本一致性等约束保留。

---

## [0.5.3] - 2026-05-21

### Fixed
- 修复 0.5.2 演示工程改名后命名空间 / 配置残留导致的「业务程序集未加载」与「入口 Procedure 未找到」启动报错。

---

## [0.5.2] - 2026-05-21

### Changed
- 演示工程更名 Demo → MainDemo，目录 / asmdef / 命名空间 / 发布脚本与子框架 sample 脚手架模板同步对齐。
- qa 测试改为按需就近建测试脚本（命名带「关键词+YYYYMMDD」），不再依赖固定测试入口。

### Removed
- 演示工程废除固定 Test 入口目录。

---

## [0.5.1] - 2026-05-21

### Changed
- 包内结构调整与冗余资源优化。

---

## [0.5.0] - 2026-05-21

### Added
- 接入 UPM 标准 Samples 机制，演示工程改作 sample 分发；导入后自动检测旧版本残留并询问设置启动场景。
- 各 UPM 包补齐 CHANGELOG / LICENSE / README 三件套，发版脚本强制校验。
- 提供子框架 sample 脚手架模板。

### Changed
- 主框架版本 0.4.2 → 0.5.0。
- 演示工程目录从项目根迁入 sample 子树。
- 发布工具沉淀为 Claude Code skill，发版命令统一入口。

### Removed
- 废除 bootstrap.zip 机制，相关脚本与 Editor 工具一并移除。
