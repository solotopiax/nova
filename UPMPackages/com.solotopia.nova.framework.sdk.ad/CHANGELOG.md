# Changelog

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
