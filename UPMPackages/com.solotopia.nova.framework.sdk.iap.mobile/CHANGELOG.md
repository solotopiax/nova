# Changelog

## [Unreleased]

## [0.1.10] - 2026-08-26

### Fixed

- 验单队列处理期间同步占用订单键，避免同一订单被重复加入队列。
- 合并重复平台订单并限制仅对可恢复状态重新验单，避免重复回调覆盖有效订单状态。

### Changed

- `nova_iap_validate_success` 按当前 UID 持久化平台注册订单键去重，历史与运行期缓存最多保留 300 条。

## [0.1.9] - 2026-08-18

### Fixed

- `MobileStore.PayAsync` 返回失败 `IAPResult` 时统一补齐 `nova_iap_local_pay_fail`，覆盖商品未获取、商品不可用、透传参数非法、Store guard、平台失败与主动支付后的验单失败；平台失败回调不再直接上报该事件，避免同一次失败重复打点。

### Changed

- 将 Framework 与 IAP 最低依赖同步至 `0.6.15`、`0.1.5`，并将商品拉取重试默认值投影到 Sample ConfigMaster。

## [0.1.8] - 2026-08-14

### Fixed

- 启动期商品拉取成功后不再自动调用 `RestoreTransactions`，避免 iOS 进游戏弹出 Apple ID 验证框；订阅和非消耗品恢复仍通过 `FetchPurchases`、服务端查单、本地补单和权益刷新链路兜底。
- 订阅到期倒计时不再复用手动 `RestoreAsync`，改为先 `FetchPurchases` 刷新平台已有购买与票据缓存，再执行权益刷新，避免后台到期检查触发 iOS 恢复购买弹框。

## [0.1.7] - 2026-08-13

### Breaking

- 移除移动内购网络请求的明文调试开关；支付通信固定使用标准 Protobuf 与 AES 加密链路。

### Changed

- 将 Framework 与 IAP 最低依赖同步至本轮发布版本。

## [0.1.6] - 2026-08-12

### Fixed

- Google / Apple 验单请求的 `price` 改为支付表 `IAPProductEntry.Price`，并按不受地区影响的格式解析；不再使用 Unity IAP 的 Storefront 本地化价格，避免服务端验单金额漂移。

## [0.1.5] - 2026-08-12

### Fixed

- `ReceiptParam` 与 uid 在发起支付前改为校验可逆 GUID 槽位：拒绝非十六进制和前导 `0` 的非空值，避免 iOS 未写入 `AppAccountToken` 或回调解码后把业务订单误判为补单。
- 商品信息整体拉取失败后会自动按 2s / 5s / 10s 最多重试 3 次；任一轮收到成功商品，或失败数量小于请求数量时即停止重试，并在重试前清理上一轮失败 SKU 缓存，避免网络抖动导致商品永久不可用。
- 商品拉取成功后会清理旧失败 SKU，并按 StoreController 当前状态恢复仍缺失的 pending SKU；失败回调也只把当前仍缺失的 SKU 标记为不可用，避免迟到失败回调污染已成功商品。
- 未完成订单改以 `tableId + ReceiptParam` 识别并迁移旧存档，修复多个礼包共用 SKU 时错认订单。
- 释放 Store 时统一取消商品、补单、验单与权益刷新后台任务，避免迟到回调访问已释放对象。

### Changed

- 最低依赖提升至 Framework `0.6.9` 与 IAP `0.1.3`。

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
