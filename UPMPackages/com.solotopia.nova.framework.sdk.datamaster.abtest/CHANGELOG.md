# Changelog

本文件记录 `com.solotopia.nova.framework.sdk.datamaster` 的版本变更。
格式遵循 [Keep a Changelog](https://keepachangelog.com/)，版本号遵循语义化版本。

## [0.0.7] - 2026-07-15

### Added

- `LogExperimentEvent(string, double, Dictionary<string, object>)`：项目可为单次事件传入 `ExtraContext`，字典不缓存、不合并、不修改。
- 每次事件上报前实时构造 `DMUserContext`，自动填充当前 UID、设备 ID、归因来源与 Campaign、渠道、安装时间、国家、语言、整数版本号及 Android/iOS 平台标识；数据不可用时对应字段留空。

### Changed

- 每次服务端刷新请求发出前强制更新 `m_UserProperties` 中的 `app_version` / `install_time`；`SetUserProperty` 仍允许项目设置任意分流属性。
- DataMasterDemo 改为演示单次 `extraContext`，并移除登录拉取前显式调用 `ApplyRequiredUserProperties()` 的要求。

### Removed

- 删除公开的 `LogExperimentEvent(string, double, DMUserContext)` 厂商类型重载，标准用户上下文统一由框架实时维护。

## [0.0.6] - 2026-07-13

### Changed

- 提升 Framework 与 GameLogin 的依赖下界，保证独立安装链最终解析到 `unitask@10.0.6`。

## [0.0.5] - 2026-07-09

### Added

- 新增可选依赖屏蔽宏 `NOVA_STARLUS_DATAMASTER`（Runtime asmdef `versionDefines`：`com.starlus.sdk.datamaster` 存在即定义，遵循 ADR-064「宏交 asmdef」与 `NOVA_NICEVIBRATIONS` 约定）。所有对厂商 `StarlusSDK.DataMaster` 的调用（`using` / `DataMaster.Instance.*` / `DMUserContext` / `DMGetParamsResponse` / 反射）均以 `#if NOVA_STARLUS_DATAMASTER` 包裹：未安装 DataMaster SDK 时插件优雅降级——读参返回兜底值、曝光 / 事件上报 / 服务端拉取为空操作、`OnInitializeAsync` 记 Warning 并跳过初始化，消费工程不再因缺库编译报 CS0246（补齐 ADR-064「绕过 PlugPals 手写 manifest 装包 UPM 直接报错」的编译期缺口）。

### Changed

- `LogExperimentEvent(string, double, DMUserContext)` 重载因签名含厂商类型 `DMUserContext`，仅在宏 `NOVA_STARLUS_DATAMASTER` 生效（已装 SDK）时暴露；未装 SDK 时请改用无 `userContext` 的简化重载 `LogExperimentEvent(string, double)`。
- 将 Nova Framework 的最低依赖版本提升至 `0.5.37`，避免安装时解析到仍使用旧契约的框架版本。

## [0.0.4] - 2026-07-08

### Added

- 新增 Editor 构建处理器 `DataMasterPluginBuildProcessor`（继承框架 `NovaSDKBuildProcessor`）：构建时按 `ConfigRuntimeSO.DevelopMode` 自动注入 / 移除厂商 SDK 的环境宏 `PRODUCTION_PACKAGE`（Release=正式域名、Debug=测试域名）。编译前记录工程原宏状态（SessionState）、构建完成后精确复原，不污染持久 PlayerSettings；仅在本插件已启用时处理。新增 `NovaFramework.SDK.StarlusDataMaster.ABTest.Editor` 程序集。

### Changed

- 重整 `Nova/Doc/`：`INDEX.md` 重写为清晰导航（主 API → ABTest 科普 → 官方底层文档 → 联调参考）；`DataMasterPlugin.md` 补「构建环境 / `PRODUCTION_PACKAGE` 宏」章节；新增 `官方SDK技术文档.md`（厂商 DataMaster SDK 官方技术文档原文，讲底层数据模型 / 初始化 / 拉取 / 存储与安全，供底层排查参考）。

## [0.0.3] - 2026-07-08

### Fixed

- 补全 sample 依赖声明：`package.json` dependencies 增加 `com.solotopia.nova.framework.kit.network.gamelogin`（DataMasterDemo 的「模拟登录并拉取」演示用登录 Kit，此前漏声明导致消费工程 import sample 后缺 `NovaFramework.Kit.Network.GameLogin` 程序集，编译报 CS0234 失败）。
- 清理 sample asmdef 克隆残留引用：`NovaFramework.Sdk.Datamaster.Samples.Runtime.asmdef` 删除代码未使用的 tga / appsflyer / ad / gamesave 程序集引用（DataMasterDemo 从 MainDemo 克隆时带入，实际未调用），避免消费工程缺这些无关包时编译失败。

## [0.0.2] - 2026-07-07

### Changed

- 拆分为「对接库 + 原版包」双包结构：DataMaster 原版迁至内部云仓库包 `com.starlus.sdk.datamaster`，本包更名为 `com.solotopia.nova.framework.sdk.datamaster.abtest`（对接层），`Core/` 转为空槽位、原版经 `dependencies` 从内部云仓库拉取。
- 对接程序集更名 `NovaFramework.SDK.DataMasterPlugin.Runtime` → `NovaFramework.SDK.StarlusDataMaster.ABTest.Runtime`（asmdef name 与顶层 namespace 一致）；GUID 与公开类型（`DataMasterPlugin` / `DataMasterPluginConfig`）保持不变。

## [0.0.1] - 2026-07-03

### Added

- 首次封装 Starlus DataMaster SDK 为 Nova SDK 插件包（远程配置 / ABTest / 事件上报）。
- 新增 `DataMasterPlugin`：继承 `SDKPluginBase`，由 `SDKManager` 统一编排初始化与生命周期；ABTest 能力经具体类型公开方法暴露，业务通过 `SDKManager.Get<DataMasterPlugin>()` 调用。
- 新增 `DataMasterPluginConfig`：承载 AppId / AesKey / 默认配置文本，供 ConfigMaster 静态配置并由 SDKManager 注入。
- 接入登录闭环：订阅 `SDKEventData.UserLogin`，登录后自动向服务端拉取实验配置。
- 公开 API：`GetParamValue<T>` / `GetParamValueJson`（读参）、`MarkExposure` / `SetExposureTimeMs`（曝光打点）、`LogExperimentEvent`（实验指标上报，含自动构造上下文的简化重载与携带 `DMUserContext` 的高级重载）、`SetUserProperty`（分流属性）、`OnConfigRefreshed`（配置刷新事件）。
- 设备 ID 取值对齐框架口径：优先 `Nova.SDK.TryGet<IDeviceIdProvider>()`，取不到时回退 `SystemInfo.deviceUniqueIdentifier`。
- 内置厂商依赖：vendored gilzoide sqlite-net-unity（含多平台原生库）与 BouncyCastle.Cryptography 托管库，随包分发。
- 附文档：`Nova/Doc/INDEX.md`、`DataMasterPlugin.md`（API 参考）、`ABTest扫盲.md`（原理科普）、`参考/`（服务端接口 swagger 与后台流程图）。
- 附示例：`DataMasterDemo` Sample（演示 `TryGet<DataMasterPlugin>` → 读参 → 曝光 → 上报的调用链，默认引导态，启用步骤见示例 README）。
