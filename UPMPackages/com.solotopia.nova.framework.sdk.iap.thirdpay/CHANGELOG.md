# Changelog

## [Unreleased]

### Changed

- ThirdPay 收敛为纯 C# 的 InAppAuto 单模式实现，移除 Browser、DeepLink 和等待器链路。
- 使用 Unity Purchasing 5.3.1 公开的 `ExternalBillingProgramClient` 实现 Google 外链政策流程。
- 支付 URL 恢复 Solar InAppAuto 的 `lang/params/app_id` 外层 Query 与完整商品、账号、平台、CID、Google token 内层 JSON 契约。
- 支付页改为由包内 UniWebView 5.11.1 统一管理：Android 使用嵌入式 WebView，iOS 使用 Safe Browsing 与 Deep Link 回调；请求未提供适配区域时自动加载包内默认面板。
- 支付回调、关闭、加载失败、内容进程终止和 AlipayConnect URL 重写统一收口到框架层。
- 本地订单改为按 `clientOrderId` 保存，并自动迁移旧版按 `tableId` 保存的数据。
- 商品映射改为使用 `IAPProductEntry.ThirdProductID`。
- 验单状态按 1/2 保留、3 可发货、4 删除失败单、5 删除但不重复发货处理；网络和未知状态保留。
- 商品列表、渠道参数、服务端待补发订单与批量验单统一由 `ThirdIapNetService` 构造公共 Header 和 Protobuf 请求，与 Mobile IAP 的协议服务保持同层职责。
- Runtime 按 Data、Net、Google、Order、WebView 与 Utils 分层，`ThirdPayStore` 拆分为公开入口、非公开方法、字段属性和打点四个 partial 文件。
- 第三方支付票据透传统一使用 `IAPRequest.ReceiptParam`：支付 URL 的 `custom_param` 封装 `receipt_param`，验单响应直接回填 `IAPResult.ReceiptParam`。
- 精简第三方支付协议，移除支付方式列表、重复的 `payment_appid`、首充标记和验单请求中的服务端订单号字段；删除旧字段占位，统一领域类型前缀、`order_list` 字段及验单结果命名。
- 第三方支付协议路径由 `/game_recharge/*` 统一为 `/third_pay/*`，待校验订单和验单协议分别统一为 `query_pending_order` 与 `verify_iap`，并同步重命名 Proto、Net Service、Store 配置和 Demo NetCmd。

### Removed

- 移除创建订单协议和配置。
- 移除旧 `ThirdPayGoogleExpand` 及自定义 Android 代理依赖。
- 移除业务侧 `IThirdPayInAppLauncher` 与 `SetInAppLauncher` 注入接口。
- 移除 `PbNetPayTypeInfo`、`IIAPThirdPayCapable.GetPayTypeList` 及上次支付方式持久化状态。

## [0.0.1] - 2026-06-03

### Added

- 首个 ThirdPay Store 版本。
