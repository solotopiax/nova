# Changelog

## [10.1.0] - 2026-08-18

### Changed

- 将 Nova UPM 分发版本升级至 `10.1.0`，并同步消费包的最低依赖。

---

## [10.0.7] - 2026-08-18

### Changed

- 官方 Luban 工具由 v4.7.0 升级至 v4.11.0。
- 保留 Nova 增量：Unity Runtime 使用的 `Google.Protobuf.dll`，以及 macOS / Windows `protoc` 与标准 Proto include 文件。

---

## [10.0.6] - 2026-06-18

### Changed

- 例行版本升级。

本文件记录该 UPM 包各版本的变更内容，遵循 [Keep a Changelog](https://keepachangelog.com/) 格式。

---

## [10.0.5] - 2026-06-15

### Changed
- 补发上次 release 后的包内变更，并对齐本轮 UPM 发布版本。

## [10.0.4] - 2026-06-10

### Changed
- 批量执行签名 UPM 重新发布，刷新包版本并对齐内网仓库分发批次。

---

## [10.0.3] - 2026-05-22

### Changed
- `Tools~/protoc/bin/` 二进制（macOS / Windows protoc 与对应 `.meta`）与 `.gitignore` 同步刷新。

---

## [10.0.2] - 2026-05-21

### Changed
- 包内结构调整与冗余资源优化。

---

## [10.0.1] - 2026-05-21

### Added
- 补齐 `CHANGELOG.md` / `LICENSE.md` / `README.md` 三件套，纳入发版强制校验。

### Changed
- 跟随主框架 0.5.0 升版。
