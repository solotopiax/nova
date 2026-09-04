# Changelog

## [Unreleased]

## [0.1.9] - 2026-09-04

### Fixed

- Android consumer ProGuard 增加 MAX/Adjust 相关类型的保留规则，避免开启 R8 后启动阶段被错误裁剪。

## [0.1.8] - 2026-09-02

### Changed

- MAX Runtime 与 Editor 程序集在 WebGL 下停止编译，避免引入不可用的原生广告 SDK。

## [0.1.7] - 2026-08-21

### Added

- MAX 初始化完成时缓存用户广告隐私授权状态，并通过广告聚合层的 `WaitForPrivacyFlowAsync()`、`IsUserConsentSet()` 与 `HasUserConsent()` 对外提供等待与组合查询。

## [0.1.6] - 2026-08-18

### Added

- `MaxAdPlugin` 新增 `GetCountryCode()` override，返回 MAX SDK 初始化回调 `SdkConfiguration.CountryCode` 缓存值。

### Changed

- 将 Ad SDK 最低依赖提升至 `1.1.4`，并将 Banner 自动刷新默认值投影到 Sample ConfigMaster。

## [0.1.5] - 2026-08-13

### Changed

- 将 Ad SDK 最低依赖提升至 `1.1.3`，使 MAX 安装链解析到本轮广告基础能力。

## [0.1.4] - 2026-08-12

### Added

- 新增 MAX Console / Readme 官方快捷入口。

## [0.1.3] - 2026-08-07

### Fixed

- iOS 构建后离线补全 `SKAdNetworkItems`，规避 AppLovin 在线网络列表请求超时导致 plist 条目数量不足，并保留已有第三方条目。

## [0.1.2] - 2026-08-06

### Changed

- `coreVersion` 同步为 AppLovin MAX `8.6.4`，Moloco Android/iOS adapter 提升至 `4110000.0.0` / `4090000.0.0`，Unity Ads Android/iOS adapter 提升至 `4190001.0.0`。
- 构建期注入 AppLovinSettings 后改为 `AssetDatabase.SaveAssetIfDirty`，避免全局 `SaveAssets` / `Refresh`。

## [0.1.1] - 2026-07-31

### Changed

- 将 AppLovin MAX Unity SDK 依赖恢复为 `8.6.4`，并对齐提交 `8f90b274f65cb56a3dc806805d88f6e0833e12c5` 中的 adapter 版本矩阵。
- 移除 BigoAds / PubMatic adapter，恢复 Yandex Android / iOS adapter；SDK Ad 依赖继续使用当前包要求的 `1.1.0`。
- 新增 Banner 自动刷新间隔配置，默认 `10` 秒并限制为 `5–120` 秒；启动自动刷新前写入 MAX `ad_refresh_seconds` 参数。

## [0.1.0] - 2026-07-29

### Changed

- 将 SDK Ad 最低依赖版本提升至 `1.1.0`，对齐本轮广告基础包发布。

## [0.0.18] - 2026-07-28

### Changed

- 将 SDK Ad 的最低依赖版本提升至 `1.0.21`，对齐广告聚合插件初始化顺序修复。

## [0.0.17] - 2026-07-21

### Changed

- MAX 广告加载、关闭与奖励回调接入 SDK Ad 主线程分发，收益事件保持即时上报。
- Banner ILRD 按 SDK Ad 配置间隔聚合上报，并将 SDK Ad 最低依赖提升至 `1.0.18`。

## [0.0.16] - 2026-07-16

### Changed

- 将 AppLovin MAX 的 ByteDance Android/iOS mediation adapter 最低版本分别提升至 `801000300.0.0` 与 `801000600.0.0`。

## [0.0.15] - 2026-07-13

### Changed

- 将 SDK Ad 的最低依赖版本提升至 `1.0.17`，使 MAX 独立安装链通过 SDK Ad 与 Framework 获取 `unitask@10.0.6`。

## [0.0.14] - 2026-07-09

### Added

- 新增可选依赖屏蔽宏 `NOVA_APPLOVIN_MAX`（Runtime 与 Editor 两个 asmdef 的 `versionDefines`：`com.applovin.mediation.ads` 存在即定义，遵循 ADR-064「宏交 asmdef」）。所有对外部 AppLovin MAX SDK（全局命名空间 `MaxSdk` / `MaxSdkBase` / `MaxSdkCallbacks` / `MaxSdkUtils` 及 Editor 的 `AppLovinSettings`）的引用均以 `#if NOVA_APPLOVIN_MAX` 包裹：MaxSdk 依赖的分部文件（RV/Inter/AppOpen/Banner/Callbacks/Methods/Track/UserId 及 FacebookAdSetting）整体条件编译，未安装 MAX 时随之移除，`MaxAdPlugin` 仅保留唯一抽象实现 `InitChannelSDKAsync`（降级为记 Warning + `RaiseInitResult(false)`）与 `Name`/`Channel`，其余广告能力回退基类 `AdChannelPluginBase` 的 virtual 空实现；Editor 构建处理器缺库时跳过 `AppLovinSettings` 注入。下游未安装 MAX 时不再编译报 CS0246。

## [0.0.13] - 2026-06-19

### Changed

- 依赖对齐：`com.solotopia.nova.framework.sdk.ad`→`1.0.13`。

## [0.0.12] - 2026-06-18

### Changed

- 例行版本升级。

本文件记录该 UPM 包各版本的变更内容，遵循 [Keep a Changelog](https://keepachangelog.com/) 格式。

---

## [0.0.11] - 2026-06-16

### Fixed
- `nova.scopedRegistries` 补回 OpenUPM（`package.openupm.com`，scope `com.google.external-dependency-manager`）：AppLovin MAX SDK 的传递依赖 EDM 由 OpenUPM 提供，现随 PlugPals 安装 max 时与 AppLovin registry 一起自动注册到工程 manifest，避免消费工程缺 OpenUPM 导致 `com.google.external-dependency-manager@1.2.182` 拉取失败。

## [0.0.10] - 2026-06-16

### Fixed
- 修正 AppLovin 官方作用域仓库声明：URL `unity.applovin.com` → `unity.packages.applovin.com/`，scope 细化为 `com.applovin.mediation.ads` / `com.applovin.mediation.adapters` / `com.applovin.mediation.dsp`（旧配置在 UPM 下拉不到包）。EDM 等传递依赖由消费工程自带的 OpenUPM registry 提供，本包不再声明 OpenUPM。

### Changed
- `dependencies` 中 `com.solotopia.nova.framework.sdk.ad` 最低版本提升到 `1.0.10`（对齐 AdLoadResult 接口，避免下游装包时 ad 未升级导致接口错）。

## [0.0.9] - 2026-06-16

### Added
- `package.json` 声明 AppLovin MAX Unity 官方作用域仓库（`nova.scopedRegistries`：`https://unity.applovin.com`，scope `com.applovin`）。安装/升级时由 PlugPals 自动注册到项目 manifest，使 `com.applovin.*` 依赖可由该私有云仓库解析；卸载时自动移除。

### Changed
- MaxAdPlugin 适配统一的 `AdLoadResult` 广告加载结果模型。

---

## [0.0.8] - 2026-06-15

### Changed
- MAX SDK 与 mediation adapter 依赖改为由本包 `package.json` 统一声明。

---

## [0.0.7] - 2026-06-10

### Changed
- 批量执行签名 UPM 重新发布，刷新包版本并对齐内网仓库分发批次。

---

## [0.0.6] - 2026-06-09

### Changed
- 刷新 MAX 子包文档索引与接入说明，统一广告插件与构建处理器相关描述。

## [0.0.5] - 2026-06-04

### Changed
- 更新文档索引（INDEX）。

---

## [0.0.4] - 2026-05-22

### Changed
- `MaxAdPlugin.Methods` / `MaxAdPlugin.Track` 接口与内部实现同步刷新。

---

## [0.0.3] - 2026-05-21

### Changed
- 包内结构调整与冗余资源优化。

---

## [0.0.2] - 2026-05-21

### Added
- 补齐 `CHANGELOG.md` / `LICENSE.md` / `README.md` 三件套，纳入发版强制校验。

### Changed
- 跟随主框架 0.5.0 升版，迁出 `Assets/Game/` 演示工程引用。
