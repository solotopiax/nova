# Changelog

本文件记录 Nova Framework 各版本的团队聚合变更内容，遵循 [Keep a Changelog](https://keepachangelog.com/) 格式。

> 详细包级变更见 `Assets/Framework/CHANGELOG.md`，本文件仅作团队聚合视图，不进 npm tarball。

---

## [0.6.11] - 2026-08-13

汇总：见 [Assets/Framework/CHANGELOG.md](Assets/Framework/CHANGELOG.md) `[0.6.11]` 节。

### Changed

- 发布 Framework `0.6.11`、Alibaba Cloud OSS Editor Tool `0.0.2` 与 BestHTTP 适配包 `0.1.5`；内部 Best HTTP 升级至 `3.0.19`，Best TLS Security 保持 `3.0.5`。
- Framework 的 AES 缺配诊断指向隐私配置的当前坐标；启用 AES 的 Persist 存储实现会在加载前校验默认凭据，标准顺序为先加载 Config 再加载 Persist。
- Alibaba Cloud OSS 改为可选 Editor 工具包，不再进入 Player；缺少时 CDN 面板仅禁用 OSS 部署并提供安装入口。
- `NovaSpark.cs` 固定 Framework `0.6.11` 与 BestHTTP `0.1.5`；EDM `1.2.188` 与 unity-mcp `v10.1.2` 保持当前版本。

## [0.6.9] - 2026-08-12

汇总：见 [Assets/Framework/CHANGELOG.md](Assets/Framework/CHANGELOG.md) `[0.6.9]` 节。

### Breaking

- IAP 结果契约扩展 `IIAPResult` 字段，并将 `IAPResult.FailReason` 更名为 `ErrorDesc`；自定义结果实现和业务失败处理需按 `(ErrorSource, ErrorCode)` 适配。

### Added

- Framework 新增精准通知设置入口：Android API 26+、iOS 15.4+ 可直达当前应用的通知设置；不支持时明确返回 `false`，不回退到应用设置。
- AIHelp、AppsFlyer、Facebook、Firebase、MAX 与 TGA 新增官方 Console / Readme 菜单入口。

### Fixed

- IAP Mobile 修复未完成订单身份、商品拉取重试和迟到失败回调处理，避免共用 SKU 错认订单或网络抖动后商品永久不可用。
- Pipify / Build 与 HybridCLR Development ABI 校验对齐，避免构建参数和产物不一致。

### Changed

- 发布 Framework `0.6.9`、AIHelp `0.0.6`、AppsFlyer `0.1.3`、Facebook `0.1.4`、Firebase `0.1.3`、IAP `0.1.3`、IAP Mobile `0.1.5`、MAX `0.1.4` 与 TGA `0.1.5`。
- `NovaSpark.cs` 固定 Framework `0.6.9`；BestHTTP `0.1.3`、EDM `1.2.188` 与 unity-mcp `v10.1.2` 保持当前版本。

## [0.6.8] - 2026-08-07

汇总：见 [Assets/Framework/CHANGELOG.md](Assets/Framework/CHANGELOG.md) `[0.6.8]` 节。

### Added

- Framework 新增 Native 通知权限模块、CDN 部署前远端清理、最新 YooAsset 版本自动关联和构建期运行时配置临时投影。
- MAX 新增 iOS 构建后 SKAdNetworkItems 离线补全，规避 AppLovin 在线列表超时导致的条目缺失。

### Changed

- IAP Mobile 扩充平台订单成功与失败诊断日志，补充交易 ID、账号透传字段、失败详情和 tableId 解析来源。
- 发布 Framework `0.6.8`、IAP Mobile `0.1.4` 与 MAX `0.1.3`。
- `NovaSpark.cs` 固定 Framework `0.6.8` 与 BestHTTP `0.1.3`；EDM `1.2.188` 与 unity-mcp `v10.1.2` 保持当前最新版本。

## [0.6.7] - 2026-08-06

汇总：见 [Assets/Framework/CHANGELOG.md](Assets/Framework/CHANGELOG.md) `[0.6.7]` 节。

### Breaking

- Framework `IAssetManager` / `AssetManagerBase` 新增设备白名单与可启动版本提交方法，自定义 Asset Manager 实现需同步补齐契约。

### Added

- Framework 新增启动白名单、元数据根地址、URL 候选轮换、可启动版本记录、本地热更缓存清理与 CDN 白名单部署能力。
- GameBind 新增绑定状态查询协议/API 与 `ErrUIDAlreadyBoundOtherOpenID`(10408) 错误码。

### Fixed

- BestHTTP 修复 iOS IL2CPP 启动期 UniTask PlayerLoop 未就绪异常，并在下载回调释放前复制响应内容。

### Changed

- 发布 Framework `0.6.7`、BestHTTP `0.1.3`、GameBind `0.0.11`、GameLogin `0.1.5`、GameSave `0.1.3`、AIHelp `0.0.5`、DataMaster ABTest `0.0.11`、IAP Mobile `0.1.3` 与 MAX `0.1.2`。
- 更新各接入包 `coreVersion` / 原厂依赖元数据，MAX 同步 Moloco / Unity Ads adapter 版本并改为仅保存变更的 AppLovinSettings 资产。
- `NovaSpark.cs` 固定 Framework `0.6.7` 与 BestHTTP `0.1.3`；EDM `1.2.188` 与 unity-mcp `v10.1.2` 保持当前最新版本。

## [0.6.6] - 2026-08-05

汇总：见 [Assets/Framework/CHANGELOG.md](Assets/Framework/CHANGELOG.md) `[0.6.6]` 节。

### Fixed

- Config 导出新增 SDK / Kit 启用清单与当前坐标有效配置的完整性校验；已启用配置缺失或原类型无法确认时阻断，未启用失效残留降级为 Warning。
- ConfigWindow 清理失效引用后立即保存并重建 WorkingCopy，避免旧副本回写复活已清理空槽位。

### Changed

- 发布 Framework `0.6.6`；纯 Warning 导出改为显式确认后继续。
- `NovaSpark.cs` 固定 Framework `0.6.6`；BestHTTP `0.1.2`、EDM `1.2.188` 与 unity-mcp `v10.1.2` 保持当前最新版本。

## [0.6.5] - 2026-08-04

汇总：见 [Assets/Framework/CHANGELOG.md](Assets/Framework/CHANGELOG.md) `[0.6.5]` 节。

### Breaking

- Network Header Proto 字段统一为 `appid/devid`，Login、Bind 与 GameSave Proto 字段同步统一为 `openid/last_devid`；字段号与 wire 类型保持不变。
- `NetBuilder.BuildHeader` 不再接受候选 OpenID，Header 始终读取已确认身份快照。

### Added

- Framework 新增 UID/OpenID 原子身份读写、清理与非排队身份操作租约，Login、Delete、Bind、Resolve 共用同一并发边界。

### Changed

- 发布 Framework `0.6.5`、BestHTTP `0.1.2`、GameBind `0.0.10`、GameLogin `0.1.4` 与 GameSave `0.1.2`，按实际消费链提升最低依赖。
- `NovaSpark.cs` 固定 Framework `0.6.5` 与 BestHTTP 适配包 `0.1.2`；EDM `1.2.188` 与 unity-mcp `v10.1.2` 保持当前最新版本。

## [0.6.4] - 2026-08-03

汇总：见 [Assets/Framework/CHANGELOG.md](Assets/Framework/CHANGELOG.md) `[0.6.4]` 节。

### Added

- Best HTTP `3.0.18` 与 Best TLS Security `3.0.5` 新增不依赖 Nova 的结构化网络遥测、协议叶子错误与 TLS/OCSP 诊断。
- BestHTTP 适配包 `0.1.1` 自动注册 Nova 遥测接收器，并在 Network Inspector 提供可用性感知的上报开关。

### Changed

- 发布 Framework `0.6.4`、GameLogin `0.1.3`、GameBind `0.0.9`、Apple Sign-In `0.0.11`、Facebook `0.1.3`、Google Sign-In `0.0.12` 与 TGA `0.1.4`，内部依赖下界对齐本轮最新版本。
- `NovaSpark.cs` 固定 Framework `0.6.4` 与 BestHTTP 适配包 `0.1.1`；EDM 与 unity-mcp 保持当前版本。

### Breaking

- 第三方登录提供方、游戏运营渠道与 TGA 用户属性名称统一到新契约。

## [0.6.3] - 2026-08-03

汇总：见 [Assets/Framework/CHANGELOG.md](Assets/Framework/CHANGELOG.md) `[0.6.3]` 节。

### Added

- Framework 的 Localization、Network、Sound、UI 与 Vibrate 专用 Luban 链支持 JSON / Binary 格式选择及对应运行时加载。
- Localization 新增支持语言表导出与运行时行契约。

### Changed

- 发布 Framework `0.6.3` 与 13 个包含同步 Sample 的 Kit / SDK 包；对应包的 Framework 最低依赖统一提升至 `0.6.3`。
- `NovaSpark.cs` 固定 Framework `0.6.3`、unity-mcp `v10.1.2`；BestHTTP `0.1.0` 与 EDM `1.2.188` 保持当前最新版本。

## [0.6.2] - 2026-07-31

汇总：见 [Assets/Framework/CHANGELOG.md](Assets/Framework/CHANGELOG.md) `[0.6.2]` 节。

### Fixed

- 发布 Framework `0.6.2`，修复 ProjectGuard 将 Scene 目录误作配置搜索范围、导致外部项目无法定位 `ConfigRuntimeSO` 的问题。
- 发布 Mobile IAP `0.1.2`，商品信息已成功拉取后不再因商店连接回调重复请求。
- 发布 TGA `0.1.2`，统一等待 SDK 初始化完成后启动数据桥接，避免并发等待同一初始化任务。

### Changed

- `NovaSpark.cs` 固定 Framework `0.6.2`；BestHTTP `0.1.0`、EDM `1.2.188` 与 unity-mcp `v10.1.0` 保持当前最新版本。

## [0.6.1] - 2026-07-31

汇总：见 [Assets/Framework/CHANGELOG.md](Assets/Framework/CHANGELOG.md) `[0.6.1]` 节。

### Added

- 公共网络 Header 增加 OpenID，登录与绑定 Kit 按服务端权威结果统一同步 UID/OpenID。
- SDK MAX 增加 Banner 自动刷新间隔配置，默认 10 秒并限制为 5–120 秒。
- SDK TGA 增加时区与 DeviceId 作为 DistinctId 的可选配置。

### Changed

- 发布 Framework `0.6.1`、GameBind `0.0.7`、GameLogin `0.1.1`，以及 10 个 SDK 新版本。
- SDK 插件 Priority 调整为明确的初始化分桶；Ad 展示回调统一切回 Unity 主线程，收益回调保持即时分发。
- SDK MAX 对齐 AppLovin MAX `8.6.4` 与 adapter 版本矩阵，并依赖 SDK Ad `1.1.1`。
- 安装入口固定命名为 `NovaSpark.cs`，不再在文件名中追加版本号；Framework 固定为 `0.6.1`，BestHTTP 固定为 `0.1.0`。

### Breaking

- `NetService.Uid` / `SetUid` 更名为 `NetService.UID` / `SetUID`；`NetBuilder.BuildHeader` 增加可选 OpenID 参数。
- `TGAPluginConfig.Mode` 与构造参数由 `int` 改为 `TDMode`，并增加 `TDTimeZone`。

## [0.6.0] - 2026-07-29

汇总：见 [Assets/Framework/CHANGELOG.md](Assets/Framework/CHANGELOG.md) `[0.6.0]` 节。

### Added

- 发布 Framework `0.6.0`，新增启动应用 Custom 配置、完整远端 JSON 快照刷新、路径读取 API 与 AppCustomConfig 网络协议。
- Table 工具链新增多 Luban Project、导出描述、加载描述与显式数据资产地址模型。

### Changed

- 发布 BestHTTP `0.1.0`、GameBind `0.0.6`、GameLogin/GameSave `0.1.0`，以及 11 个 SDK 新版本；所有仓内包依赖对齐到本轮最新版本。
- 发布 SDK MAX `0.1.0` 并将 SDK Ad 依赖提升至 `1.1.0`。
- NovaSpark 升级为 `NovaSpark2.13.cs`，固定 Framework `0.6.0`；BestHTTP、EDM 与 unity-mcp 版本保持当前最新值。

### Breaking

- Config 的旧 `CustomConfigs` 与 Table 的旧 Project/Profile/Binding 契约由新 Custom、Project、Export Description、Load Description 模型替换。

## [0.5.46] - 2026-07-24

汇总：见 [Assets/Framework/CHANGELOG.md](Assets/Framework/CHANGELOG.md) `[0.5.46]` 节。

### Fixed

- 发布 `com.solotopia.nova.framework@0.5.46`，修复 ConfigMaster 新 CDN 配置结构在 npm Sample 中未完整脱敏的问题。
- 发布脚本新增 signed tgz 上传前复检，公开默认配置未全部替换为占位符时禁止 `npm publish`。
- NovaSpark 升级为 `NovaSpark2.12.cs`，固定 Framework `0.5.46`。

## [0.5.45] - 2026-07-24

汇总：见 [Assets/Framework/CHANGELOG.md](Assets/Framework/CHANGELOG.md) `[0.5.45]` 节。

### Added

- 发布 `com.solotopia.nova.framework@0.5.45`，新增全局占位符、动态 URL 模板、App 更新总开关与 CDN 缓存清理能力。

### Changed

- Config 编辑态与运行态完成分层重构和旧结构迁移，统一 CDN、HybridCLR、YooAsset 的顶层配置与维度覆盖模型。
- NovaSpark 升级为 `NovaSpark2.11.cs`，固定 Framework `0.5.45`；BestHTTP、EDM 与 unity-mcp 版本保持当前最新值。

## [0.5.44] - 2026-07-23

汇总：见 [Assets/Framework/CHANGELOG.md](Assets/Framework/CHANGELOG.md) `[0.5.44]` 节。

### Added

- 发布 `com.solotopia.nova.framework@0.5.44`，新增 Pipify CDN 部署、全量 Excel 导出、飞书通知、DoH 诊断树与 HostKey 预热能力。
- 发布 IAP `0.0.20`，新增商品工作簿导入、模板导出及编辑器校验能力。

### Changed

- 发布 SDK Ad `1.0.20`、Apple Sign-In `0.0.7`、AppsFlyer `0.0.27`、Google Sign-In `0.0.8` 与 IAP Mobile `0.0.14`，同步包内文档与配置展示说明。
- 公开发布副本对 Pipify 飞书参数及 CDN 顶层、维度覆盖配置执行字段专属占位符脱敏。
- NovaSpark 升级为 `NovaSpark2.10.cs`，固定 Framework `0.5.44`；BestHTTP、EDM 与 unity-mcp 版本保持当前最新值。

## [0.5.43] - 2026-07-23

汇总：见 [Assets/Framework/CHANGELOG.md](Assets/Framework/CHANGELOG.md) `[0.5.43]` 节。

### Added

- 发布 `com.solotopia.nova.framework@0.5.43` 与首个 `com.solotopia.alibabacloud.oss@0.0.1`，新增阿里云 OSS 上传、Cloudflare 缓存清理及多维 CDN 配置面板。
- 发布 SDK Ad `1.0.19`，补齐聚合收益与单次曝光收益的埋点定义。

### Changed

- 发布 IAP `0.0.19` 与 IAP Mobile `0.0.13`，将模拟支付成功严格限定在 Editor 编译态。
- NovaSpark 升级为 `NovaSpark2.9.cs`，固定 Framework `0.5.43`；BestHTTP、EDM 与 unity-mcp 版本保持当前最新值。

## [0.5.42] - 2026-07-21

汇总：见 [Assets/Framework/CHANGELOG.md](Assets/Framework/CHANGELOG.md) `[0.5.42]` 节。

### Added

- 发布 `com.solotopia.nova.framework@0.5.42`，新增埋点表聚合、跨插件 OpenID 数据槽与 DoH URL 候选规划。
- 发布 GameLogin `0.0.12` 与 GameBind `0.0.5`，补齐登录、删号、绑定和冲突裁决埋点。

### Changed

- 发布 Apple Sign-In `0.0.6`、Facebook `0.0.9`、Google Sign-In `0.0.7` 与 TGA `0.0.22`，打通 OpenID 用户属性自动同步链路。
- 发布 SDK Ad `1.0.18` 与 MAX `0.0.17`，收敛广告回调线程并新增 Banner ILRD 聚合上报。
- 发布 IAP `0.0.18` 与 IAP Mobile `0.0.12`，避让 registry 中未记录于 Git tag 的 `0.0.17` / `0.0.11`。
- 同步发布 BestHTTP `0.0.13` 与 DataMaster ABTest `0.0.8`。

## [0.5.41] - 2026-07-16

汇总：见 [Assets/Framework/CHANGELOG.md](Assets/Framework/CHANGELOG.md) `[0.5.41]` 节。

### Added

- 发布 `com.solotopia.nova.framework@0.5.41`，为 PlugPals“已安装”页新增一键批量升级入口，并更新服务中心标题与自适应布局。

## [0.5.40] - 2026-07-16

汇总：见 [Assets/Framework/CHANGELOG.md](Assets/Framework/CHANGELOG.md) `[0.5.40]` 节。

### Added

- 发布 `com.solotopia.nova.framework@0.5.40`，新增 ProjectGuard 项目结构校验、Play Mode 错误阻断与渐进式接入文档。

### Fixed

- 发布 `besthttp@0.0.12`、`sdk.appsflyer@0.0.26`、`sdk.firebase@0.0.24`，修复 TLS API、AGP 8 Gradle 属性与 Firebase Android namespace 兼容问题。
- 发布 `sdk.max@0.0.16`，更新 ByteDance Android/iOS mediation adapter 最低版本。

## [UPM 2026.07.14-01] - 2026-07-14

### Changed

- 发布 `com.solotopia.nova.framework.sdk.iap@0.0.16`，新增随平台票据往返的 `ReceiptParam`，并收敛补单扫描并发与登录前延迟触发。
- 发布 `com.solotopia.nova.framework.sdk.iap.mobile@0.0.10`，启用 `uid8 + tableId8 + receiptParam16` 透传布局，修复商品拉取、权益刷新、恢复与平台确认流程中的时序问题。
- NovaSpark 升级为 `NovaSpark2.4.cs`，固定 EDM `1.2.188` 与 unity-mcp `v10.1.0`。

## [0.5.38] - 2026-07-13

汇总：见 [Assets/Framework/CHANGELOG.md](Assets/Framework/CHANGELOG.md) `[0.5.38]` 节。

### Fixed

- 发布 `com.solotopia.unitask@10.0.6`（bundled core 2.5.11），并沿 NovaSpark → Framework → Kit/SDK 的真实安装链提升依赖下界，修复 Unity 6000.5 新工程仍解析旧 Tracker API 而触发 `CS0619` 的问题。
- NovaSpark 升级为 `NovaSpark2.3.cs`，固定 Framework `0.5.38` 与 BestHTTP `0.0.10`，确保新安装入口实际获得兼容版本。

## [0.5.37] - 2026-07-13

汇总：见 [Assets/Framework/CHANGELOG.md](Assets/Framework/CHANGELOG.md) `[0.5.37]` 节。

主要内容：
- 主包 framework 升级到 0.5.37：移除旧 Spine 槽位换肤与动画时长查询的四个公开扩展入口；Pipify 统一 Android keystore 与 key alias 命名，并让 Split Application Binary 可独立生效；PlugPals 不再把带 scoped registry 包的第三方传递依赖展开到项目顶层 manifest。
- 随 framework 依赖下界联动发布 Network Kit：`kit.network.gamebind@0.0.3`、`kit.network.gamelogin@0.0.10`、`kit.network.gamesave@0.0.17`。
- 随 framework 依赖下界联动发布 SDK：`sdk.ad@1.0.16`、`sdk.applesignin@0.0.4`、`sdk.appsflyer@0.0.24`、`sdk.facebook@0.0.7`、`sdk.firebase@0.0.22`、`sdk.googlesignin@0.0.5`、`sdk.iap@0.0.14`、`sdk.iap.mobile@0.0.8`、`sdk.tga@0.0.20`。
- 同轮发布 `besthttp@0.0.9`、`sdk.aihelp@0.0.1`、`sdk.datamaster.abtest@0.0.5`、`sdk.max@0.0.14`。

## [0.5.34] - 2026-07-03

汇总：见 [Assets/Framework/CHANGELOG.md](Assets/Framework/CHANGELOG.md) `[0.5.34]` 节。

主要内容：
- 主包 framework 升级到 0.5.34：`NOVA_NICEVIBRATIONS` / `NOVA_SIMPLEDISKUTILS` 宏定义从 `package.json` 的 `nova.requiredLibraries.defineSymbols` 迁移至 `NovaFramework.Runtime.asmdef` 的 `versionDefines`（装包自动跨平台定义，ADR-064 后半步）；删除 `NOVA_UNIWEBVIEW` / `NOVA_WEBGLSUPPORT` 死宏声明（孤儿宏根除，全仓无 `#if` 引用）；Debug `Settings.asset` 默认 `_isEnabled` 置为 0。

## [0.5.33] - 2026-07-03

汇总：见 [Assets/Framework/CHANGELOG.md](Assets/Framework/CHANGELOG.md) `[0.5.33]` 节。

主要内容：
- 主包 framework 升级到 0.5.33：`NetResponse<T>` 新增携带业务数据的失败工厂重载、`NetService` 业务错误体解析（ADR-068）；BuildProcessor `UnityManifest.xml` 模板路径三级定位重构；`WorkspaceActive.GetActiveRuntime` 增强 ExportTarget 优先定位（ADR-047）。
- 子包 `com.solotopia.nova.framework.kit.network.gamebind` 首次发布 0.0.1：账号绑定业务网络模块（ADR-067）。
- 子包 `com.solotopia.nova.framework.kit.network.gamelogin` 升级到 0.0.8。
- 子包 `com.solotopia.nova.framework.kit.network.gamesave` 升级到 0.0.15：`Save.GetFullAsync(targetUid)` 跨用户存档查询（ADR-069）。

## [0.5.26] - 2026-06-15

汇总：见 [Assets/Framework/CHANGELOG.md](Assets/Framework/CHANGELOG.md) `[0.5.26]` 节。

主要内容：
- 主包 framework 升级到 0.5.26：Asset 远端清单不可达时回退使用当前已激活清单或内置清单，PlugPals 缺失依赖检测跳过 Unity 包与已注册 scope。
- 子包 `com.solotopia.nova.framework.besthttp` 升级到 0.0.3：刷新 BestHTTP Runtime asmdef 配置。
- 子包 `com.solotopia.nova.framework.sdk.ad` 升级到 1.0.9：AdDemo sample 字体资源随包发布同步刷新。
- 子包 `com.solotopia.nova.framework.sdk.iap` 升级到 0.0.9：IAPDemo sample 字体资源随包发布同步刷新。

## [0.5.25] - 2026-06-15

汇总：见 [Assets/Framework/CHANGELOG.md](Assets/Framework/CHANGELOG.md) `[0.5.25]` 节。

主要内容：
- 主包 framework 升级到 0.5.25：进一步移除 PlugPals 安装按钮同步路径上的远端 package metadata 拉取，避免点击安装/升级时阻塞 Unity 主线程。

## [0.5.24] - 2026-06-15

汇总：见 [Assets/Framework/CHANGELOG.md](Assets/Framework/CHANGELOG.md) `[0.5.24]` 节。

主要内容：
- 主包 framework 升级到 0.5.24：修复 PlugPalsWindow 点击安装/升级后同步触发 UPM Resolve 导致消费端 Unity 卡顿/无响应的问题，改为下一帧合并解析。
- 子包 `com.solotopia.nova.framework.besthttp` 升级到 0.0.2：同步更新包级授权文件。

## [0.5.23] - 2026-06-15

汇总：见 [Assets/Framework/CHANGELOG.md](Assets/Framework/CHANGELOG.md) `[0.5.23]` 节。

主要内容：
- 主包 framework 升级到 0.5.23：补发 `upm-release-2026.06.10-01` 后累计变更，覆盖 BestHTTP 可选后端解耦、内部云仓库展示、缺失依赖提示与 Util.Json 依赖迁移等内容。
- 同步补发所有 eligible 且有变更的 UPM 子包；禁发名单内包仍保持不发布。

## [0.5.22] - 2026-06-10

汇总：见 [Assets/Framework/CHANGELOG.md](Assets/Framework/CHANGELOG.md) `[0.5.22]` 节。

主要内容：
- 主包 framework 升级到 0.5.22：Network / DoH 链路增强，新增预热后自动缓存 IP 直连与更清晰的模块文档说明；ConfigWindow 的 Luban 相关提示文案同步收敛。
- MainDemo 示例场景随主包同步刷新。

## [0.5.21] - 2026-06-09

汇总：见 [Assets/Framework/CHANGELOG.md](Assets/Framework/CHANGELOG.md) `[0.5.21]` 节。

主要内容：
- 主包 framework 升级到 0.5.21：新增场景级 `DevelopMode` 序列化回写与 Inspector 只读彩色展示；App / Asset 改为使用节点本地开发模式在 Debug / Release 主备地址间路由；版本检查增加主备地址失败回退并在双路都不可用时返回 `NoDownload`。
- 子包 `kit.network.gamelogin@0.0.2`、`kit.network.gamesave@0.0.9`、`sdk.appsflyer@0.0.16`、`sdk.firebase@0.0.14`、`sdk.tga@0.0.14`：同步收敛配置提示文案与包内文档说明。
- 子包 `sdk.ad@1.0.6`、`sdk.admob@0.0.3`、`sdk.iap@0.0.6`、`sdk.iap.mobile@0.0.2`、`sdk.max@0.0.6`：刷新包内文档索引与使用说明。

## [0.5.20] - 2026-06-05

汇总：见 [Assets/Framework/CHANGELOG.md](Assets/Framework/CHANGELOG.md) `[0.5.20]` 节。

主要内容：
- 主包 framework 升级到 0.5.20：优化启动流程，Splash / 进度面板的销毁时机由启动流程内部移交业务入口统一回收，避免首屏衔接闪帧；同步刷新框架 L0/L1/L2 文档与代码注释。

---

## [0.5.17] - 2026-06-01

汇总：见 [Assets/Framework/CHANGELOG.md](Assets/Framework/CHANGELOG.md) `[0.5.17]` 节。

主要内容：
- 主包 framework 升级到 0.5.17：新增 Kit 配置体系（`IKitConfig` + ConfigWindow「Kit 配置」面板 + `Nova.Config.GetKitConfig<T>()`），Asset 模块支持启动期按 tag 切片预热、运行期刷新清单与缓存治理接口，Hotfix 阶段新增热更提示弹窗与完成后自动清缓存开关。
- 子包 `kit.network.gamesave` 升级到 0.0.5：存取接口收口为 6 个零 cmd 参数极简入口，指令名由 ConfigWindow「Kit 配置」统一管理（新增 `GameSaveKitConfig`）。
- 子包 `kit.network.login` 升级到 0.0.16：登录入口简化为只传 `openId`，`Async` 签名新增首位 `uid` 参数，指令名与渠道由 ConfigWindow「Kit 配置」统一管理（新增 `LoginKitConfig`）。

---

## [0.5.16] - 2026-05-29

汇总：见 [Assets/Framework/CHANGELOG.md](Assets/Framework/CHANGELOG.md) `[0.5.16]` 节。

主要内容：
- 主包 framework 升级到 0.5.16：把对 `com.solotopia.yooasset` 的依赖从 `1.0.0` 提升到当前 Verdaccio 最新版 `1.0.3`，框架默认随附最新资源系统封装层。

---

## [0.5.15] - 2026-05-29

汇总：见 [Assets/Framework/CHANGELOG.md](Assets/Framework/CHANGELOG.md) `[0.5.15]` 节。

主要内容：
- 主包 framework 升级到 0.5.15、登录 Kit 升级到 0.0.15：两包均通过 `.npmignore` 排除 `nova-samples.json` 与 `.meta`，发版描述符仅留开发期源工程使用，不再随 npm tarball 落到外部工程的只读 `Packages/<pkg>/` 区，消除 `immutable folder` 警告与 `SamplePathRewriter` 的重复 RunRewrite。

---

## [0.5.14] - 2026-05-29

汇总：见 [Assets/Framework/CHANGELOG.md](Assets/Framework/CHANGELOG.md) `[0.5.14]` 节。

主要内容：
- 主包 framework 升级到 0.5.14：发版流水线 `publish_packages.py` 重构为统一流水线——主包 `MainDemo` 与所有子包 sample 走完全对称的 Stage 1 / Stage 3，删除主包专属分支，新增 `Assets/Framework/nova-samples.json` 描述符，所有 sample 等量复制 `Docs/Excels` + `Docs/Protos`、注入 `Nova.prefab` 的 `*SourceDirPath` PrefabInstance override、写入 `SamplePathManifest`；脚本对 `devPathPrefix` 末尾斜杠做防御性 `rstrip("/")` 归一。
- 登录 Kit（kit.network.login@0.0.14）：修正源 `nova-samples.json` 的 `devPathPrefix` 末尾斜杠 bug，外部工程 import LoginDemo 后 `SamplePathRewriter` 不再静默放弃路径重写，sample scene 与 SO 路径正确指向 import 后真实根目录。

---

## [0.5.13] - 2026-05-29

汇总：见 [Assets/Framework/CHANGELOG.md](Assets/Framework/CHANGELOG.md) `[0.5.13]` 节。

主要内容：
- 主包 framework 升级到 0.5.13：`WorkspaceActive` 增加多 sample 切换感知——当前活跃 scene 在 `Assets/Samples/<sampleRoot>/` 下且与 `Globals.json` 缓存的 ConfigMaster 所在 sample 根不一致时，自动按 scene 重新推断 ConfigMaster 并覆盖 `Globals.json`，根除外部工程同时 import 多个 sample 时打开次级 sample 却读到首个 sample 配置的玄学。
- 登录 Kit（kit.network.login@0.0.13）：发版脚本与主包对称——为子包 sample 复制 `Docs/Excels` + `Docs/Protos` 副本，注入 `Nova.prefab` 的 `*SourceDirPath` PrefabInstance override 到 sample scene；修正 `sampleManifestRelative` 路径错配；`package.json` 备份从包目录移到系统临时目录，消除外部工程 Console 持续刷的 `package.json.publish.bak` immutable folder 报错。

---

## [0.5.12] - 2026-05-29

汇总：见 [Assets/Framework/CHANGELOG.md](Assets/Framework/CHANGELOG.md) `[0.5.12]` 节。

主要内容：
- 主包 framework 升级到 0.5.12：修复 `WorkspaceActive` 的 sample scene 路径推断，外部工程 import sample 后打开 ConfigWindow 不再提示"未检测到激活的 ConfigMaster"——从 scene 所在目录起逐级向上递归扫 `Editor/ConfigMaster.asset`，同一逻辑兼容开发态扁平结构与 UPM 导入态三段嵌套结构。
- 登录 Kit（kit.network.login@0.0.12）：修正 `nova-samples.json` 的 `displayName` 为 `LoginDemo`（原为带空格的 `Login Demos`），与 `sampleName` / `sourceDir` 末段对齐，确保 Unity Package Manager 落盘的最末层文件夹名与开发态一致。

---

## [0.5.11] - 2026-05-29

汇总：见 [Assets/Framework/CHANGELOG.md](Assets/Framework/CHANGELOG.md) `[0.5.11]` 节。

主要内容：
- 主包 framework 升级到 0.5.11：ConfigMaster 新增 `YooAssetSettingsPath` / `BundleCollectorSettingPath` 显式路径字段，YooAsset 全局设置与 Bundle 收集器加载改由 ConfigWindow 配置驱动，避开 Editor 启动期 Resources 多副本与全工程 `AssetDatabase.FindAssets` 扫描；ConfigMasterSO 仅 Editor 期消费的字段补齐 `#if UNITY_EDITOR` 包围。
- yooasset 升级到 1.0.3：暴露 `YooAssetConfiguration.SetSettings`（internal）注入点与 `SettingLoader.LoadSettingDataAtPath<T>` 按路径加载重载，配合主包路径注入链路。
- 登录 Kit（kit.network.login@0.0.10）：新增 `nova-samples.json` 元数据，声明 `LoginDemo` 示例工程，预留 `/nova-create-sample` 与发版流水自动化能力。

---

## [0.5.10] - 2026-05-28

汇总：见 [Assets/Framework/CHANGELOG.md](Assets/Framework/CHANGELOG.md) `[0.5.10]` 节。

主要内容：
- 主包 framework 升级到 0.5.10：清理 `Assets/Framework/Tests/` 临时回归脚本、SDKInspector 插件条目绘制层调整、`Nova.Visitors.Version` 同步、`Assets/Samples/MainDemo/` 资源刷新。
- gamesave Kit 升级到 0.0.4：上传请求新增 `GameVersion / AppVersion / LastDeviceId` 三项元数据自动填充，新增 `SetGameVersion` 实例方法供业务侧注入；拉取响应新增最近一次存档版本 / 设备 / 时间戳元数据。
- sdk.ad 升级到 1.0.3：调整 UPM displayName 为 "Nova Framework - SDK - AD"，与其它 SDK 子包命名风格统一。

---

## [0.5.9] - 2026-05-27

汇总：见 [Assets/Framework/CHANGELOG.md](Assets/Framework/CHANGELOG.md) `[0.5.9]` 节。

主要内容：
- 声音 / 振动模块新增「按名称播放」入口，业务侧可直接传数据表 Name 字段触发播放。
- Editor 新增 PlugPals 私有 Verdaccio 仓库 UPM 包管理工具，支持远程包列表、安装/卸载与按版本查看更新日志。
- 网络 Kit（kit.network@0.0.10）：响应公共头新增 `app_id` 与 `uid` 字段，便于多产品/多账号场景识别。
- 登录 Kit（kit.network.login@0.0.9）：登录接口新增「按命令名调用」重载，并修复 `openId` 为 null 时底层异常。
- 存档 Kit（kit.network.gamesave@0.0.3）：拆分全量与非全量接口为独立入口，并新增「按命令名调用」重载（破坏性：旧版"keys 为空即全量"隐式回退取消）。

---

## [0.5.8] - 2026-05-27

汇总：见 [Assets/Framework/CHANGELOG.md](Assets/Framework/CHANGELOG.md) `[0.5.8]` 节。

主要内容：
- UI 视图新增「对象池开关」，可按视图选择关闭后缓存还是直接销毁（破坏性：UIView 子类覆写 OnInit 需补回参数）。
- UIView 默认不再带淡入淡出，淡入淡出能力移交业务自行实现。
- Asset / UI 模块 L2 文档同步刷新，强化 LoadXxx 必须经 Handle 释放铁律。
- Vault 沉淀本期 UI 深度因子、Asset Load API 等决策与通用模式。

---

## [0.5.7] - 2026-05-26

汇总：见 [Assets/Framework/CHANGELOG.md](Assets/Framework/CHANGELOG.md) `[0.5.7]` 节与各 UPM 子包 CHANGELOG。

主要内容：
- 启动期 UI 改为多语言驱动，弹窗显示接口签名简化（破坏性）。
- 启动期新增独立本地化能力，可在资源系统就绪前安全使用本地化文本。
- 网络 / 登录 / 数据上报三个 UPM 子包跟版升级。

---

## [0.5.6] - 2026-05-22

汇总：见 [Assets/Framework/CHANGELOG.md](Assets/Framework/CHANGELOG.md) `[0.5.6]` 节与各 UPM 子包 CHANGELOG。

主要内容：
- 网络 / 声音模块对外接口与 DTO 调整。
- MainDemo 演示工程切换为基于 Nova.UI 的树形导航 + TMP 文字渲染。
- 7 个 UPM 子包跟版升级。
- Vault 沉淀本期演示拓扑、UI 命名、Demo 覆盖标准、prefab 制作等多条规范。

---

## [0.5.5] - 2026-05-22

汇总：见 [Assets/Framework/CHANGELOG.md](Assets/Framework/CHANGELOG.md) `[0.5.5]` 节。

主要内容：
- 修复外部工程导入 sample 后 Inspector 业务字段为空与 SDK `[Missing]` 提示。
- 修复多版本 sample 共存时新旧版本识别不准的问题。

---

## [0.5.4] - 2026-05-22

汇总：见 [Assets/Framework/CHANGELOG.md](Assets/Framework/CHANGELOG.md) `[0.5.4]` 节。

主要内容：
- 放弃此前的桥接式生命周期方案，回归 HybridCLR 原生 MonoBehaviour + Prefab 直挂。
- 发版流程支持把项目根 Docs 资源（表格 / 协议）随 sample 一起打包，外部工程导入后 Inspector 路径自动对齐。
- HybridCLR 约束规则全面重写为原生方案口径。

---

## [0.5.3] - 2026-05-21

汇总：见 [Assets/Framework/CHANGELOG.md](Assets/Framework/CHANGELOG.md) `[0.5.3]` 节。

主要内容：
- 修复 0.5.2 演示工程改名后命名空间 / 配置残留导致的启动报错。

---

## [0.5.2] - 2026-05-21

汇总：见 [Assets/Framework/CHANGELOG.md](Assets/Framework/CHANGELOG.md) `[0.5.2]` 节。

主要内容：
- 演示工程更名 Demo → MainDemo，目录 / asmdef / 命名空间 / 子框架脚手架同步对齐。
- qa 测试改为按需就近建测试脚本，不再依赖固定测试入口。

---

## [0.5.1] - 2026-05-21

主要内容：
- 包内结构调整与冗余资源优化。

---

## [0.5.0] - 2026-05-21

汇总：见 [Assets/Framework/CHANGELOG.md](Assets/Framework/CHANGELOG.md) `[0.5.0]` 节。

主要内容：
- 接入 UPM 标准 Samples 机制，演示工程改作 sample 分发；导入后自动检测旧版本残留并询问设置启动场景。
- 各 UPM 包补齐 CHANGELOG / LICENSE / README 三件套，发版脚本强制校验。
- 主框架版本 0.4.2 → 0.5.0。
- 发布工具沉淀为 Claude Code skill。
- 废除 bootstrap.zip 机制。
