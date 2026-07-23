# Changelog

## [Unreleased]

### Changed

- `EnableAlwaysPaySucceed` 调试支付成功分支仅在 Editor 编译态保留，移动端产物不再包含 `MOCK_ORDER_THIRDPAY` 路径；关闭订单打点 Debug 字段改为读取 `DevelopMode.Debug`。

本文件记录该 UPM 包各版本的变更内容，遵循 [Keep a Changelog](https://keepachangelog.com/) 格式。

---

## [0.0.1] - 2026-06-03

### Added
- 首个版本：第三方支付 store（WebView / 系统浏览器）。
- 补齐 `CHANGELOG.md` / `LICENSE.md` / `README.md` 三件套，纳入发版强制校验。
