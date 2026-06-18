# Changelog

## [0.0.19] - 2026-06-18

### Changed

- 例行版本升级。

本文件记录该 UPM 包各版本的变更内容，遵循 [Keep a Changelog](https://keepachangelog.com/) 格式。

---

## [0.0.18] - 2026-06-15

### Changed
- 补发上次 release 后的包内变更，并对齐本轮 UPM 发布版本。

## [0.0.17] - 2026-06-10

### Changed
- 批量执行签名 UPM 重新发布，刷新包版本并对齐内网仓库分发批次。

---

## [0.0.16] - 2026-06-09

### Changed
- 优化 `AppsFlyerPluginConfig` 的 Inspector 提示文案，并同步刷新包内文档说明，明确关键协议与参数字段的用途。

## [0.0.15] - 2026-06-04

### Fixed
- 修复发布产物中 SamplePathManifest 未填充重写目标的问题：发布描述符 `nova-samples.json` 的 `sampleManifestRelative` 误指向 `Configs/`（实际在 `Editor/`），导致外部工程 import 后场景 / Prefab 内资产路径仍为开发工程目录 `Assets/Samples/<Demo>/...` 而未替换为真实 import 路径。

---

## [0.0.14] - 2026-06-04

### Added
- 新增 AppsFlyer 第三方数据上报 cmd 与登录、注册接口对接。
- 新增 AppsFlyer 示例工程。

### Changed
- 登录成功后新增通知回调；更新 cmd 描述。

---

## [0.0.13] - 2026-05-22

### Changed
- `AppsFlyerPlugin` / `AppsFlyerPlugin.Methods` 接口与内部实现同步刷新。

---

## [0.0.12] - 2026-05-21

### Changed
- 包内结构调整与冗余资源优化。

---

## [0.0.11] - 2026-05-21

### Added
- 补齐 `CHANGELOG.md` / `LICENSE.md` / `README.md` 三件套，纳入发版强制校验。

### Changed
- 跟随主框架 0.5.0 升版，对齐 EDM 依赖版本。
