# Changelog

## [Unreleased]

### Changed

- ThirdPay 支付页打开方式改为平台固定策略：Android 默认外部浏览器并按 Auth Tab → Custom Tabs → `Application.OpenURL` 兜底，iOS 使用 UniWebView Safe Browsing；移除 `UseExternalBrowserPayment` 与 `ExternalBrowserCountryCodes` 配置。
- ThirdPay 支付 URL 参数改为动态 AES 封装：每次构造 URL 生成 16 字节 Key/IV，调用 `Util.Encrypt.AES.EncryptBytes` 后按 `key + iv + cipher` 整体 Base64，不再读取和缓存 `AppConfigs.AppAesKey/AppAesIV` 或隐私配置默认 AES。
- ThirdPay 支付 URL 内层 `params` JSON 新增 `show_back_button`，仅 Android 最终回退 `Application.OpenURL` 打开支付页时写入 `true`，Auth Tab、Custom Tabs、iOS 与 Editor 默认保持 `false`。
- Android Google 信息页不可用或打开失败时不再中断支付，保留用户取消语义并继续进入 ThirdPay 支付页打开流程。
- 补充 ThirdPay 订单创建后到支付页打开前的诊断日志；Android Google 外链授权服务为空时改为构建无 Google token 支付 URL 继续打开 ThirdPay，避免真机只看到建单打点后无后续反馈。
- 为嵌入式 UniWebView 显式设置全屏 Frame，避免支付页未铺满屏幕。
- 支持第三方验单状态 `6`（订单不存在）：删除本地订单并广播验单失败结果，避免无效订单持续补单。
- Android 外部支付页优先 Auth Tab，不支持时回退 AndroidX Browser `1.10.0` Custom Tabs，两者不可用或打开失败时由 C# `Application.OpenURL` 回退系统浏览器。
- ThirdPay 国家码规则对齐 Solar：`CountryCode` / `SetDebugCountryCode` 作为 Debug 覆盖源，有效国家码按 `Debug > Lock > Billing > Native > AD > US` 解析，`IV` 归一化为 `US`，iOS 初始化时读取 StoreKit storefront，商品列表按请求版本忽略旧国家响应；IAPDemo 默认不再写死 `US`。
- Google Play Billing 商店地区未配置时，通过 `getBillingConfigAsync()` 自动读取国家/地区代码；External Billing Program 因设备、账号或地区不可用时直接进入平台默认 ThirdPay 支付页。
- 新增第三方商品列表读取、商品存在性查询、WebView 导航栏文案设置和跳过 Google 信息页能力；信息页跳过默认读取 `ThirdPayStoreConfig`，运行时可由 `IIAPThirdPayCapable` 覆盖。
- `ThirdGetPayChannelParams` 返回 `10707` 等渠道参数错误时不再阻断支付，继续使用不含渠道客户号的支付 URL。
- ThirdPay 收敛为纯 C# 的 InAppAuto 单模式实现，移除 Browser、DeepLink 和等待器链路。
- 使用 Unity Purchasing 5.3.1 公开的 `ExternalBillingProgramClient` 实现 Google 外链政策流程。
- 支付 URL 恢复 Solar InAppAuto 的 `lang/params/app_id` 外层 Query 与完整商品、账号、平台、CID、Google token 内层 JSON 契约。
- 支付页改为由包内服务统一管理：Android 使用外部浏览器链路，iOS 使用 Safe Browsing 与 Deep Link 回调。
- 应用内 WebView 用户关闭支付页时不再直接返回取消失败，改为保留订单并立即进入一次验单；支付 URL、WebView 打开、message、关闭和 failed 终态补充 Info/Warning 日志；WebView 固定使用 UniWebView 默认全屏显示，不再暴露 `AdaptRectTransform` 或复用 `LoadingPanelPrefab` 承载面板。
- 点击第三方支付后的渠道参数拉取、商品兜底拉取、本地建单与支付 URL 构建阶段会显示 IAP Loading，并在打开 WebView 或系统外部浏览器前释放。
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
