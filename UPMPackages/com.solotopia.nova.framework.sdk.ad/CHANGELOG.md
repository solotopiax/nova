# Changelog

## [Unreleased]

## [1.1.2] - 2026-08-03

### Changed

- AdDemo 同步 Localization 支持语言表与 JSON / Binary 数据格式能力，并将 Framework 最低依赖提升至 `0.6.3`。

## [1.1.1] - 2026-07-31

### Changed

- 将 `AdPlugin` 初始化优先级调整为 `80`，保持在收益打点与支付插件之后初始化。
- 明确 `ShowCompleted` 在渠道 SDK 的 displayed 回调时发布，不代表广告已关闭或激励已完成；最终结果通过 `ShowFailed` / `AdClosed` 获取。
- 明确加载、展示、关闭事件回到 Unity 主线程，收益事件保持即时分发。
- 补记非 Banner 广告在关闭或展示失败后自动续杯的现有行为。

## [1.1.0] - 2026-07-29

### Changed

- 将 Nova Framework 最低依赖版本提升至 `0.6.0`。
- AdDemo 同步启动应用配置网络命令、运行时配置与场景覆盖。

## [1.0.21] - 2026-07-28

### Fixed

- 调整广告聚合插件初始化顺序，使其在现有收益打点插件之后初始化，避免广告渠道初始化时缓存不到可用的打点实例。

## [1.0.20] - 2026-07-23

### Changed

- 收敛广告通道状态机与打点设计文档，以当前 `AdChannelPluginBase` 和统一埋点表作为包内事实入口。

## [1.0.19] - 2026-07-23

### Added

- 埋点表新增 `ad_ilrd` 广告收益明细与 `ad_impression` 单次曝光收益定义，明确 Banner 聚合和即时曝光上报口径。

## [1.0.18] - 2026-07-21

### Changed

- 广告加载、关闭与奖励回调统一切回主线程，收益回调保持即时分发。
- 新增 Banner ILRD 按配置间隔聚合上报能力，并补齐广告埋点表。

## [1.0.17] - 2026-07-13

### Changed

- 将 Nova Framework 的最低依赖版本提升至 `0.5.38`，使独立安装链通过 Framework 获取 `unitask@10.0.6`。

## [1.0.16] - 2026-07-13

### Changed

- 将 Nova Framework 的最低依赖版本提升至 `0.5.37`，避免安装时解析到仍使用旧契约的框架版本。

## [1.0.15] - 2026-07-08

### Fixed

- 清理 sample asmdef 克隆残留引用：删除代码未使用的 gamelogin / gamesave / tga / appsflyer 跨包程序集引用（从 MainDemo 模板克隆时带入，AdDemo 实际未调用），避免消费工程未安装这些无关包时编译报 CS0234。

## [1.0.14] - 2026-06-30

### Changed

- 依赖对齐：`com.solotopia.nova.framework`→`0.5.32`，修复公网 registry 仅有 0.5.32 而旧声明 0.5.31 缺失导致安装 404 的问题。

## [1.0.13] - 2026-06-19

### Changed

- 依赖对齐：`com.solotopia.nova.framework`→`0.5.31`。

## [1.0.12] - 2026-06-18

### Changed

- 例行版本升级。

本文件记录该 UPM 包各版本的变更内容，遵循 [Keep a Changelog](https://keepachangelog.com/) 格式。

---

## [1.0.11] - 2026-06-16

### Changed
- `dependencies` 中 `com.solotopia.nova.framework` 最低版本提升到 `0.5.28`（`AdLoadResult` 统一加载结果接口所在版本，确保下游装包时 framework 同步升级）。

## [1.0.10] - 2026-06-16

### Changed
- 广告插件适配统一加载结果模型：`AdChannelPluginBase` / `AdPlugin` 改用 `AdLoadResult`，`RequestAsync` 返回成功/失败统一结果。
- 广告奖励字段与打点（Track）流程优化。

## [1.0.9] - 2026-06-15

### Changed
- AdDemo sample 字体资源随包发布同步刷新。

## [1.0.8] - 2026-06-15

### Changed
- 补发上次 release 后的广告 SDK 包内变更，并对齐本轮 UPM 发布版本。

## [1.0.7] - 2026-06-10

### Changed
- 批量执行签名 UPM 重新发布，刷新包版本并对齐内网仓库分发批次。

---

## [1.0.6] - 2026-06-09

### Changed
- 刷新广告 SDK 包内设计文档、接口说明与索引，统一术语与章节结构，便于接入时检索。

## [1.0.5] - 2026-06-04

### Fixed
- 修复发布产物中 SamplePathManifest 未填充重写目标的问题：发布描述符 `nova-samples.json` 的 `sampleManifestRelative` 误指向 `Configs/`（实际在 `Editor/`），导致外部工程 import 后场景 / Prefab 内资产路径仍为开发工程目录 `Assets/Samples/<Demo>/...` 而未替换为真实 import 路径。

---

## [1.0.4] - 2026-06-04

### Added
- 新增 AdDemo 示例工程。

### Changed
- 重构广告渠道配置列表绘制器（AdChannelConfigListDrawer），大幅精简代码。

---

## [1.0.3] - 2026-05-28

### Changed
- 调整 UPM 包 displayName 为 "Nova Framework - SDK - AD"，与其它 SDK 子包命名风格统一。

---

## [1.0.2] - 2026-05-22

### Changed
- `AdChannelPluginBase` / `AdChannelPluginBase.Methods` / `AdChannelPluginBase.Track` 与 `AdPlugin` / `AdPlugin.Methods` 接口与内部实现同步刷新。

---

## [1.0.1] - 2026-05-21

### Changed
- 包内结构调整与冗余资源优化。

---

## [1.0.0] - 2026-05-21

### Added
- 接入 Nova Framework UPM 包结构，补齐 `CHANGELOG.md` / `LICENSE.md` / `README.md` 三件套，纳入发版强制校验。
