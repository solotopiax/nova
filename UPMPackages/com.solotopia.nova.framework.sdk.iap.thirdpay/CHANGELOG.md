# Changelog

## [Unreleased]

## [0.1.1] - 2026-10-23

### Fixed

- 修复 iOS 第三方支付收到成功回调后 Safe Browsing 支付页未自动关闭的问题，改用 C# 引用判空以确保执行关闭。

### Changed

- 补充平台支付政策核对及接入边界说明；上线前需按目标商店地区核实适用计划、选择与披露、平台 API 和交易上报，未满足时保持第三方支付入口关闭。

## [0.1.0] - 2026-10-23

### Fixed

- 修复订单、平台授权和支付 URL 尚未全部就绪时提前上报创建成功，以及支付页已有成功/关闭终态后重复记本地支付失败；后续验单失败仅保留验单事件。

- iOS Safe Browsing 和外部浏览器 Deep Link 必须匹配当前客户端订单号；Android 订阅 Application.deepLinkActivated，成功回调校验 orderid 后取消兜底倒计时并立即验单。

- 修复当前支付返回后 PendingPayment/Processing 只验一次及待支付状态误归类；当前返回链按场景短重试，历史订单和后台补单仍只验一次，待支付重试结束后删除本地订单。

- Store 初始化并行启动 Storefront、Billing、AD 国家查询，等待迟到的广告 SDK 初始化与首次国家发布；登录后首次业务读取锁定有效国家，后续来源和账号切换不重置 Store 期锁定，仅失效用户配置。无效国家不发送配置协议，预取失败输出网络码、信息或响应校验原因。

- Android 外部浏览器返回后的处理中、已发货、网络失败和缺单结果通过 PayFailed/PaySuccess(CanDeliver=false) 通知 UI，并回传本次 PayAsync 调用方。

- iOS 13 起读取 StoreKit storefront，低版本使用 Locale；三位 ISO 国家码统一转为 alpha-2，未知码返回空值。

- 支付 URL 不再校验或发送 PbNetThirdProductInfo 的旧数值 id，继续用字符串 product_id 匹配商品。

### Changed

- 最低 IAP Core 依赖提升至 0.2.0，与本轮 IAP 发布配对；沿用通用打点及 Store Pause/Focus 契约。

- 订单打点集中到 ThirdPayStore.Track.cs，使用 nova_client_order_id/nova_server_order_id 替代 nova_order_id，服务端号未取得时为空；pay_callback 成功仅报 nova_iap_local_pay_success，无成功回调的会话终态报 nova_iap_third_pay_close_order。独立记录关闭/失败/创建/状态/删除原因，nova_reason 保持粗粒度错误；验单事件补齐场景、协议码及原始/归一化状态，仅实际删单后报 nova_iap_third_pay_order_removed。

- 统一配置通过公开能力返回服务端关闭原因；client_config 仅在包内快照中转换为只读字典，支持 Test 和 TryGetValue，当前 Store 公开能力不提供该快照读取入口；快照完成后输出配置字段 Debug 日志。

- 外部浏览器返回复用 SDKComponent → IAPPlugin → Store 的 Pause/Focus 生命周期，不再创建隐藏监听对象。

- ThirdPayServiceHub 统一持有国家、商品、账号存档和浏览器会话，管理 Country/Checkout/Validation/Recovery 服务及后台取消、释放顺序；ThirdPayStore 保留公开入口和兼容适配，Runtime 分层并拆分 partial，公共支付语义保持。

- 商品、渠道参数、支付页地址和可用性统一为 ThirdGetPaymentConfig，ThirdIapNetService 构造公共 Header/Protobuf 并附国家码，由服务端判断名单及国家策略；Store 内按账号、国家、NetCmd 合并登录、Debug 切换和支付兜底请求，隔离旧响应，一次支付固定同一快照。

- 国家优先级为 Debug > Lock > Billing > iOS Storefront > AD，仅 SetDebugCountryCode 显式覆盖；不默认 US，IV/UNKNOWN/空值均无效，Billing/Storefront/AD 写入当前用户存档供后续兜底，商品响应按请求版本隔离，IAPDemo 不再写死 US。

- 验单重试新增 8 秒档：明确支付成功覆盖完整间隔序列，关闭和外部浏览器返回最多三次，支付前历史订单及后台补单一次。

- 验单直接使用 PbNetThirdVerifyOrderStatus：状态 2 保留、3 可发货、4 删除失败单、5 删除但不重复发货、6 删除并通知失败，网络或未知状态保留；状态 1 待支付按当前重试策略结束后删除，不再与处理中混用。

- 支付页由包内服务按平台固定打开：Android Auth Tab → AndroidX Browser 1.10.0 Custom Tabs → Application.OpenURL，iOS UniWebView Safe Browsing + Deep Link；服务端可区分承载方式，移除 UseExternalBrowserPayment/ExternalBrowserCountryCodes 配置。

- 本地订单改按 clientOrderId 保存，商品使用 IAPProductEntry.ThirdProductID；支付前按 TableId + ReceiptParam 复用未完成订单验单。仅存必要上下文，支付页明确未打开时删单，其他未终结单留待同业务或补单恢复。

- 支付 URL 保留 lang/params/app_id 外层 Query 与商品、账号、平台、CID、Google token 内层字段；每次生成 16 字节 Key/IV，AES 后将 key + iv + cipher 整体 Base64，不读取 App/Privacy 默认密钥。platform/channel 发送标准 Header 枚举整数，新增 client_version，并将 ReceiptParam 写入内层 JSON 顶层 receipt_param；验单响应继续回填 IAPResult.ReceiptParam。

- show_back_button 仅在 Android 最终 Application.OpenURL 兜底时为 true，Auth Tab、Custom Tabs、iOS 和 Editor 为 false。

- Google 外链政策使用 Unity Purchasing 5.3.1 ExternalBillingProgramClient，商店地区缺失时通过 getBillingConfigAsync 读取；信息页不可用、打开失败、授权服务缺失或设备/账号/地区不支持时继续默认 ThirdPay 支付，可不带 Google token，保留用户取消语义及建单到开页诊断。

- WebView 固定全屏，不再暴露 AdaptRectTransform 或借 LoadingPanelPrefab 承载；关闭时保留订单并验单，框架统一处理支付回调、message、加载失败、内容进程终止及 AlipayConnect URL 重写，保留终态日志。

- 商品/渠道兜底、本地建单、URL 构建及外部浏览器返回后的验单倒计时显示 IAP Loading；开页前释放准备阶段 Loading。

- 新增商品列表、存在性查询、导航栏文案及跳过 Google 信息页能力，跳过默认来自 ThirdPayStoreConfig，可由 IIAPThirdPayCheckoutCapable 覆盖；配置缺 payment_customer_ids 时继续不含渠道客户号的支付 URL。

- 协议路径迁为 /third_pay/*，待校验及验单使用 query_pending_order/verify_iap，同步 Proto、Net Service、Store 配置与 Demo NetCmd；移除支付方式列表、重复 payment_appid、首充标记及验单请求服务端订单号，统一领域前缀、order_list 和结果命名。

- 最低依赖对齐至Nova Framework 0.7.0、IAP Core 0.2.0，与本轮发布的框架及 SDK 接口配套。

### Removed

- 移除 IIAPThirdPayCapable、GetPayTypeList、PbNetPayTypeInfo 及上次支付方式状态，改用配置、商品和结算细分能力；移除 IThirdPayInAppLauncher/SetInAppLauncher 业务注入。

- 移除 ThirdPayExternalBrowserLifecycleProxy 静态监听链、旧 ThirdPayGoogleExpand 及自定义 Android 代理依赖。

- 移除 Store 国家码字段及默认值、ThirdGetProductList/ThirdGetPayChannelParams/ThirdOpenURL、创建订单协议及配置，以及渠道参数手工注入和分散缓存。

### Added

- 统一配置新增包内 internal ThirdPayClientConfig 模型，供内部快照读取 test 字符串；该模型不属于 Store 公开配置能力。

- 新增网页支付参数快照传递，Android 外部浏览器回退重建 URL 时保持同一请求；当前奖励参数类型与旧 CustomParam 迁移见 Breaking。

### Breaking

- 移除 IAPThirdPayRequest.CustomParam，改为可选的 FirstRechargeReward 与 ExtraBonusReward 强类型奖励；非空奖励以 item_id、quantity 对象写入加密支付 URL，并在 Android 外部浏览器回退重建 URL 时保持同一快照。

- 明确 PbNetThirdProductInfo 商品协议的字段编号重排不兼容：删除 id=1，并将 local_currency/local_price/product_id 从 2/3/4 改为 1/2/3；本地生成类一致不代表已部署服务端或旧客户端兼容，升级需配对协议版本并验证共存、灰度和回退。

- 最低支持 Unity 6000.6.7f1，与 Nova Framework 基线一致。

- 支付 URL 的内层 JSON 与旧格式不兼容：移除 id，platform/channel 从枚举名字符串改为标准 Header 枚举整数，ReceiptParam 从 custom_param 内移至顶层 receipt_param，并新增 client_version；升级须与支付网页及服务端解析规则配对。

### Important

完善第三方支付流程，统一处理支付页打开、支付回调、服务端验单和未完成订单补单。本次商品协议及网页参数存在不兼容调整，升级时需同步配对服务端和支付网页，并验证已有订单的恢复。

## [0.0.1] - 2026-06-03

### Added

- 首个 ThirdPay Store 版本。
