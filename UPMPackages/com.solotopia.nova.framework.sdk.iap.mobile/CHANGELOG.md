# Changelog

## [Unreleased]

## [0.1.4] - 2026-08-07

### Changed

- 平台订单成功、待确认与失败日志补充交易 ID、Apple Original Transaction ID、App Account Token、Google Obfuscated Account ID、透传 UUID、失败详情及 tableId 解析来源，便于定位订单归属与确认失败。

## [0.1.3] - 2026-08-06

### Changed

- `coreVersion` 与 `com.unity.purchasing` 依赖同步为 `5.3.1`。

## [0.1.2] - 2026-07-31

### Fixed

- 商品信息正在拉取或已成功拉取时忽略重复请求；失败状态仍允许后续商店连接回调重试。

## [0.1.1] - 2026-07-31

### Changed

- 更新 Mobile IAP 架构、商店接入与维护说明，使包内文档与当前实现保持一致。

## [0.1.0] - 2026-07-29

### Changed

- 将 Framework 与 IAP 最低依赖版本统一提升至 `0.6.0` 与 `0.1.0`。

## [0.0.14] - 2026-07-23

### Changed

- 合并并精简 Mobile IAP 架构、初始化与工具说明，以当前实现文档作为包内维护入口。

## [0.0.13] - 2026-07-23

### Changed

- `EnableAlwaysPaySucceed` 调试支付成功分支仅在 Editor 编译态保留，移动端产物不再包含 `MOCK_ORDER_MOBILE` 路径。

## [0.0.12] - 2026-07-21

### Fixed

- 注册 Unity IAP 商品前过滤空 `ProductID` 并去除重复平台商品 ID，避免无效商品导致商店初始化异常。

### Changed

- 商品拉取前输出实际注册数量，便于定位商店配置问题。
- Registry 中 `0.0.11` 已存在，本次发布直接升至 `0.0.12`。

## [0.0.10] - 2026-07-14

### Changed

- 平台账号字段改为 `uid8 + tableId8 + receiptParam16` 布局，支付前严格校验透传参数容量，并在支付、补单与恢复结果中回填 `ReceiptParam`。
- 非消耗品与订阅权益刷新改为等待商品拉取完成后执行，恢复购买期间统一接入 Loading 引用计数。

### Fixed

- 修复部分商品拉取失败后权益刷新被永久阻塞、补单扫描并发漏跑，以及服务端发货成功但平台确认失败时订单记录过早删除的问题。

## [0.0.9] - 2026-07-13

### Changed

- 提升 Framework、IAP 与 UniTask 的依赖下界，保证独立安装时完整解析到本轮 Unity 6000.5 兼容版本。

## [0.0.8] - 2026-07-13

### Changed

- 将 Nova Framework 的最低依赖版本提升至 `0.5.37`，避免安装时解析到仍使用旧契约的框架版本。

## [0.0.7] - 2026-06-30

### Changed

- 依赖对齐：`com.solotopia.nova.framework`→`0.5.32`、`com.solotopia.nova.framework.sdk.iap`→`0.0.12`（适配 iap 0.0.12 移除 `IIAPStoreContext.UIManager` 的破坏性变更），修复公网 registry 仅有 0.5.32 而旧声明 0.5.31 缺失导致安装 404 的问题。

## [0.0.6] - 2026-06-19

### Changed

- 依赖对齐：`com.solotopia.nova.framework`→`0.5.31`、`com.solotopia.nova.framework.sdk.iap`→`0.0.11`、`com.solotopia.unitask`→`10.0.5`。

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
