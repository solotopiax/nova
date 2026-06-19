# Changelog

## [0.0.11] - 2026-06-19

### Changed

- 依赖对齐：`com.solotopia.nova.framework`→`0.5.31`、`com.solotopia.unitask`→`10.0.5`。

## [0.0.10] - 2026-06-18

### Changed

- 例行版本升级。

本文件记录该 UPM 包各版本的变更内容，遵循 [Keep a Changelog](https://keepachangelog.com/) 格式。

---

## [0.0.9] - 2026-06-15

### Changed
- IAPDemo sample 字体资源随包发布同步刷新。

## [0.0.8] - 2026-06-15

### Changed
- Store 上下文新增当前 `DevelopMode`，支付打点 Debug 字段改为按 `DevelopMode.Debug` 判断，不再复用 `EnableAlwaysPaySucceed`。

---

## [0.0.7] - 2026-06-10

### Changed
- 批量执行签名 UPM 重新发布，刷新包版本并对齐内网仓库分发批次。

---

## [0.0.6] - 2026-06-09

### Changed
- 刷新 IAP 主包文档索引与接入说明，统一术语与使用描述，便于 store 侧能力检索。

## [0.0.5] - 2026-06-04

### Fixed
- 修复发布产物中 SamplePathManifest 未填充重写目标的问题：发布描述符 `nova-samples.json` 的 `sampleManifestRelative` 误指向 `Configs/`（实际在 `Editor/`），导致外部工程 import 后场景 / Prefab 内资产路径仍为开发工程目录 `Assets/Samples/<Demo>/...` 而未替换为真实 import 路径。

---

## [0.0.4] - 2026-06-04

### Changed
- 重构 IAP 多包结构：渠道专属 Request / Capability / DeductPlan 下沉至各子包，启用状态与懒初始化内聚至 IAPStoreBase。
- 所有商店存储统一为 baseStore；优化支付表配置页面卡顿。

### Removed
- 删除死代码 PendingOrderQueue 与 IAPPlugin 集中管控的 m_RuntimeDisabledStores。

### Fixed
- 修正若干支付逻辑与打点报错。

---

## [0.0.3] - 2026-05-21

### Changed
- 包内结构调整与冗余资源优化。

---

## [0.0.2] - 2026-05-21

### Added
- 补齐 `CHANGELOG.md` / `LICENSE.md` / `README.md` 三件套，纳入发版强制校验。

### Changed
- 跟随主框架 0.5.0 升版。
