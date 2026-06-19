# Changelog

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
