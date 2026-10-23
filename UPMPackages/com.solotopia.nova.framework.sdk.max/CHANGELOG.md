# Changelog

## [Unreleased]

## [0.2.1] - 2026-10-23

### Changed

- 依赖对齐至 Ad 1.2.1、Framework 0.7.3、MAX 8.6.6，并更新已声明的 mediation adapters 至官方 registry 最新正式版本。

- 兼容声明调整为 Unity 6000.4.2f1，配合 Nova Framework 的 Unity 6000.4.x 支持范围。

### Fixed

- MAX 8.6.6 原厂修正 iOS 奖励重载事件名的尾空格，消除其与 C# 回调匹配不一致的问题。

## [0.2.0] - 2026-10-23

### Fixed

- 新增 Android Quality Service 的临时构建修复：Unity 6000.4.4f1 及以上且开启 Quality Service 时，向生成的 launcher AndroidManifest.xml 补写 SafeDKApplication；已有其他自定义 Application 时保留并告警，不修改项目源 Manifest。官方修复后可移除此临时处理器。

## [0.1.14] - 2026-10-23

### Changed

- 全屏配置改为 PlacementGroups，支持 List 项内逗号/竖线分组，兼容旧字段和真实 ID 访问器；初始化注册前拒绝重复或无效组，构建处理器尚未接入此校验器。

- 恢复原初始化流程，按渠道单次初始化移除重复注册、结果缓存和等待身份；本地等待后主线程处理，保留真实成功判断、事件解绑及隐私等待清理。

- 全屏展示使用各格式 TCS 和真实广告位 ID，终态先清空再通知业务，保持重入、异常和释放时的等待收尾，移除重复展示上下文及状态。

- 常规日志保留全部原厂回调入口、实际 Show 和异常警告，含 Banner/过期重载及加载、收益、失败字段；渠道由 LogTag.Max 标识，移除重复 MAX 前缀与 Load、过滤、奖励采纳、TCS 和 Banner 刷新控制日志。

- 最低依赖对齐至Nova Framework 0.7.0、Ad 1.2.0，与本轮发布的框架及 SDK 接口配套。

### Fixed

- Unity Ads Android adapter 升为 4200101.0.0（4.20.1.1，SDK 4.20.1），Unity Ads iOS adapter 升为 4200001.0.0（4.20.0.1），LINE iOS adapter 升为 3010102.0.0（3.1.1.2）；移除 SPM 占位符并使用三段 tag，修复 EDM4U 解析及 Xcode 崩溃，iOS 原生 SDK 仍为 Unity Ads 4.20.0、LINE 3.1.1，全局 SPM 设置保持。

- iOS 顺序 44 后处理定向移除 LINE/Unity Ads 的 SPM repository/product 并按官方 XML 恢复 Pods，避免重复产出 AppLovinSDK.framework；保留 Firebase SPM、其他 Pods 和部署下限，未知合同失败，重复执行幂等。文档明确 Embed 名单、构建校验范围、SKAdNetwork 离线枚举再联网补齐及平台验收边界。

- 初始化保留准确回调委托，释放时注销自身订阅并清空国家及授权缓存，已排队回调遵守取消，避免旧会话回写。

### Breaking

- 最低支持 Unity 6000.6.7f1，与 Nova Framework 基线一致。

## [0.1.13] - 2026-10-10

### Changed

- MAX 全部 Banner 广告位通过广告基类 FileFragment 存储共享累计次数与收益，后台回调切回主线程；达到阈值后上传 ad_ilrd 并清空批次，广告信息取触发回调，ad_impression 仍逐次即时上报。

- 同步 MAX README 与接入文档，说明插屏和激励奖励资格、展示与关闭回调时点、全屏曝光 ID 关联，以及 Banner 聚合和插件缺失造成的收益事件条数差异。

- Ad 依赖下界提升至 1.1.16，匹配 Banner 汇总和展示、关闭附加属性重载；新增直接 Framework 0.6.38 依赖，匹配 Runtime / Editor asmdef 引用。

### Fixed

- MAX 全屏广告在播放接口前上报 nova_ad_show，成功与失败回调分别上报 nova_ad_show_result；尝试、结果、关闭及收益关联同一 nova_ad_impression_id，失败清理前保存关联属性，关闭后保留上下文供晚到收益使用，缺失时不伪造 ID。

- 收益派发按目标直接查询并缓存所需插件，修复慢初始化永久漏报；移除初始化预取、Volatile 和主线程补取队列，缺失或查询异常时逐目标告警并跳过本次派发，释放时清空缓存。

- 插屏展示成功回调的 AdResult.RewardGranted 设为 true，与插屏播放成功即满足奖励条件的业务规则一致，不再等待关闭或收益回调。

## [0.1.12] - 2026-09-23

### Added

- `ad_impression` 增加 `nova_ad_format` 属性，便于与其他 Nova 广告事件统一按广告格式筛选，同时保留原有 `ad_format`。
- Rewarded、Interstitial、AppOpen 每次展示尝试生成本地 `nova_ad_impression_id`，并在 `nova_ad_show`、`ad_ilrd`、`ad_impression` 间作尽力关联；收益事件同时记录回调序号和关联状态，暂不丢弃无法证明为重复的收益回调，也不把本地 ID 作为硬去重依据。

### Changed

- 最低 Ad 基础包依赖提升至 `1.1.14`，使用支持渠道附加属性的 `TrackAdShow` 重载。

### Fixed

- MAX 全局 SDK 回调注册增加幂等保护，避免异常重复初始化时叠加订阅。

## [0.1.11] - 2026-09-18

### Added

- 增加 AppLovin LINE Android `300001010.0.0` 与 iOS `3010101.0.0` Adapter 依赖。

### Fixed

- WebGL 下保留配置类型所在 Runtime 程序集，避免 Config 资产中的 MAX 渠道配置产生 Missing types 警告。

## [0.1.10] - 2026-09-14

### Added

- MAX Rewarded、Interstitial、AppOpen 原始 SDK 回调补充 Debug 日志，便于真机排查加载、展示、关闭、奖励和收益回调是否到达。

### Changed

- Unity Ads Android/iOS adapter 升级至 `4200000.0.0`（AppLovin adapter `4.20.0.0`，Unity Ads SDK `4.20.0`）。
- iOS 14.5 及以上不再由 MAX 初始化流程主动启用 Meta Advertiser Tracking，避免绕过业务侧 ATT 授权结果。

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
