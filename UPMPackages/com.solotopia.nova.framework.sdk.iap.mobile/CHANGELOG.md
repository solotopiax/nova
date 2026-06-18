# Changelog

## [0.0.5] - 2026-06-18

### Changed

- 例行版本升级。

本文件记录该 UPM 包各版本的变更内容，遵循 [Keep a Changelog](https://keepachangelog.com/) 格式。

---

## [0.0.4] - 2026-06-15

### Changed
- Mobile 打点 Debug 字段改为按父包注入的 `DevelopMode.Debug` 判断，不再使用 `EnableAlwaysPaySucceed`。

---

## [0.0.3] - 2026-06-10

### Changed
- 批量执行签名 UPM 重新发布，刷新包版本并对齐内网仓库分发批次。

---

## [0.0.2] - 2026-06-09

### Changed
- 订阅商品在自身有效期内重复支付时，本地直接返回 `IAPMobileErrorCode.SubscriptionIsReady`，不再继续调用 Unity IAP 平台购买。
- 调整启动补单顺序：首次 UID 会先等待服务端未完成订单查询返回，再统一执行本地补单扫描；远端成功但列表为空也会记录为已查询。
- Apple 订单的 `TransactionId` 现在会写入本地订单存档，重启后补单验单不再丢失商店订单号。

## [0.0.1] - 2026-06-03

### Added
- 首个版本：Google Play + iOS App Store 官方内购 store。
- 补齐 `CHANGELOG.md` / `LICENSE.md` / `README.md` 三件套，纳入发版强制校验。
