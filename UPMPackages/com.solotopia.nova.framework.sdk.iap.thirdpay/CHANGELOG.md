# Changelog

## [Unreleased]

### Fixed

- 修复支付页已经上报成功或关闭终态后，后续验单失败仍被 `PayAsync` 返回边界重复记为本地支付失败的问题；验单失败只保留验单事件。
- 修复 iOS Safe Browsing 及外部浏览器可能用无效或其他订单的 Deep Link 推进当前支付会话的问题；`pay_callback` 现在必须匹配当前客户端订单号。
- 修复 Android 外部支付会话未订阅 Unity `Application.deepLinkActivated`、支付成功后只能等待返回倒计时验单的问题；成功回调现在会校验 `orderid`、取消兜底倒计时并立即验单。
- 修复 ThirdPay 当前支付从外部浏览器返回后，服务端返回 `PendingPayment` 或 `Processing` 时只验单一次的问题；当前支付返回链按场景执行短重试，后续手动点击命中的历史订单与后台补单仍只验单一次。
- 修复 IAP 早于广告插件初始化时 ThirdPay 静默错过广告国家码、导致登录后不发送统一支付配置协议的问题；AD 后台任务改为等待 SDK 初始化及国家码数据槽首次发布，登录和迟到国家回调统一经过可诊断的预取门禁。
- 修复 ThirdPay 预取统一支付配置失败时 Warning 未输出网络错误码、错误信息或响应校验失败原因的问题；Store 初始化时独立启动 Storefront、Billing、AD 三路国家码查询且不等待结果，登录后首次业务读取锁定有效国家码，后续异步来源及重新登录均沿用锁定值，配置协议在国家码为空或无效时禁止发送。
- 修复 ThirdPay 在支付 URL 准备失败时先上报创建订单成功、随后又上报失败的矛盾打点；成功事件改为在订单、平台授权和支付 URL 均准备完成后发送。
- 修复账号切换时重复清理国家锁定、导致后续异步来源可能改变有效国家的问题；账号切换仅使用户态支付配置失效并复用 Store 生命周期内的锁定国家。
- 修复 ThirdPay 验单状态 `PendingPayment` 被误判为处理中并保留本地订单的问题。
- 修复 Android 外部浏览器支付页返回 App 后，异步验单得到处理中、已发货、网络失败或响应缺单等结果时未通过 `PayFailed` / `PaySuccess(CanDeliver=false)` 通知业务 UI 的问题。
- 修复 Android 外部浏览器返回 App 后验单失败只触发全局事件、不回传给本次 `PayAsync` 调用方的问题。

### Changed

- 最低 IAP Core 依赖提升至 `0.1.16`，适配新的通用打点入口与 Store Pause/Focus 生命周期契约；本包仍属于禁发包。
- ThirdPay 的订单创建、支付页终态、验单和删单事件改由 `ThirdPayStore.Track.cs` 构造完整属性，父包不再持有 ThirdPay 专属事件封装或订单号开关。
- ThirdPay 订单生命周期打点不再发送语义随阶段变化的 `nova_order_id`，统一使用 `nova_client_order_id` 和 `nova_server_order_id`；尚未取得服务端订单号时发送空字符串。
- 重构 ThirdPay 支付链路打点：`pay_callback` 成功仅上报 `nova_iap_local_pay_success`，已建立会话但未成功回调的终态统一上报 `nova_iap_third_pay_close_order`；关闭、支付失败、创建失败、客户端订单状态和本地删除原因均使用独立枚举字段，既有 `nova_reason` 继续保留 `IAPThirdPayErrorCode` 粗粒度语义。
- `nova_iap_validate_fail`、`nova_iap_validate_fail_finish` 与 `nova_iap_validate_success` 新增验单场景、协议码、服务端原始订单状态、客户端归一化状态及客户端/服务端订单号；新增 `nova_iap_third_pay_order_removed`，只在本地订单实际删除后上报删除原因、协议码和状态上下文。
- ThirdPay 配置能力新增服务端关闭原因码读取入口，调用方可在支付未开启时直接展示统一配置返回的原因。
- ThirdPay 外部浏览器返回检测改为复用 `SDKComponent → IAPPlugin → Store` 的统一 Pause/Focus 生命周期链，不再自行创建隐藏生命周期对象。
- 新增强类型 `ThirdPayServiceHub`，集中管理共享状态、内部服务、后台任务取消与释放顺序；Country、Checkout、Validation、Recovery 服务统一改为单一 Hub 依赖并移除 Callback 聚合对象，公共 API 与支付业务语义保持不变。
- ThirdPay 将国家码解析、支付主链、订单验单和补单恢复拆分为独立 internal 服务，`ThirdPayStore` 仅负责公开入口、生命周期组装与兼容适配；公共 API、协议及支付结果语义保持不变。
- ThirdPay 将商品列表、渠道参数、支付页地址和支付可用性合并为 `ThirdGetPaymentConfig` 单一协议；客户端随标准 Header 额外发送国家码，服务端统一判断黑白名单与国家策略。
- 新增集中式 `ThirdPayPaymentConfigService`，按账号、国家和 NetCmd 合并并发请求、隔离旧响应，并在一次支付内固定使用同一配置快照。
- ThirdPay 国家码不再默认回落到 `US`，`IV` / `UNKNOWN` / 空值统一视为无国家码；Billing、iOS Storefront 与 AD 国家码改为写入 ThirdPay 当前用户存档作为后续兜底，全部缺失时返回空字符串。
- ThirdPay 验单重试间隔新增 `8s` 档位，并按验单来源区分重试次数：明确支付成功默认覆盖完整间隔序列，关闭按钮和外部浏览器返回最多验单 3 次，发起支付前命中的历史订单与后台补单只验 1 次。
- ThirdPay 验单响应的 `status` 改为协议枚举 `PbNetThirdVerifyOrderStatus`，客户端直接按枚举处理订单状态。
- 移除 ThirdPay 冗余的本地订单分类枚举，验单处置只从 `PbNetThirdVerifyOrderStatus` 派生清理、发货和事件决策。
- ThirdPay 支付页会告诉服务端当前外部支付页是否由 Auth Tab / Custom Tabs 承载，便于区分系统浏览器兜底路径。
- ThirdPay Store 的国家码、商品快照、当前账号存档和外部浏览器 session 拆到专用状态对象，并由 `ThirdPayServiceHub` 统一持有与管理。
- ThirdPay 发起支付前会按 `TableId + ReceiptParam` 复用本地未完成订单直接验单，避免同一业务重复创建新支付订单。
- ThirdPay 本地订单仅记录必要支付上下文与本地状态；支付页明确未打开成功时删除本地订单，未终结订单等待后续同业务键支付或补单检查继续验单。
- ThirdPay 支付页打开方式改为平台固定策略：Android 默认外部浏览器并按 Auth Tab → Custom Tabs → `Application.OpenURL` 兜底，iOS 使用 UniWebView Safe Browsing；移除 `UseExternalBrowserPayment` 与 `ExternalBrowserCountryCodes` 配置。
- ThirdPay 支付 URL 参数改为动态 AES 封装：每次构造 URL 生成 16 字节 Key/IV，调用 `Util.Encrypt.AES.EncryptBytes` 后按 `key + iv + cipher` 整体 Base64，不再读取和缓存 `AppConfigs.AppAesKey/AppAesIV` 或隐私配置默认 AES。
- ThirdPay 支付 URL 内层 `params` JSON 新增 `show_back_button`，仅 Android 最终回退 `Application.OpenURL` 打开支付页时写入 `true`，Auth Tab、Custom Tabs、iOS 与 Editor 默认保持 `false`。
- Android Google 信息页不可用或打开失败时不再中断支付，保留用户取消语义并继续进入 ThirdPay 支付页打开流程。
- 补充 ThirdPay 订单创建后到支付页打开前的诊断日志；Android Google 外链授权服务为空时改为构建无 Google token 支付 URL 继续打开 ThirdPay，避免真机只看到建单打点后无后续反馈。
- 为嵌入式 UniWebView 显式设置全屏 Frame，避免支付页未铺满屏幕。
- 支持第三方验单状态 `6`（订单不存在）：删除本地订单并广播验单失败结果，避免无效订单持续补单。
- Android 外部支付页优先 Auth Tab，不支持时回退 AndroidX Browser `1.10.0` Custom Tabs，两者不可用或打开失败时由 C# `Application.OpenURL` 回退系统浏览器。
- Android 外部浏览器返回 App 后，自动验单倒计时等待期也会显示 IAP Loading，避免等待服务端确认前误触底层 UI。
- ThirdPay 配置拉取收口为 Store 内部流程，登录、Debug 国家切换和支付前兜底共用同账号、同协议名、同国家码的在途请求，业务侧只读取统一快照。
- ThirdPay 国家码不再默认回落到 `US`：仅 `SetDebugCountryCode` 作为显式 Debug 覆盖源，有效国家码按 `Debug > Lock > Billing > iOS Storefront > AD` 解析，`IV` / `UNKNOWN` / 空值视为无国家码，iOS 初始化时读取 StoreKit storefront，商品列表按请求版本忽略旧国家响应；IAPDemo 默认不再写死 `US`。
- Google Play Billing 商店地区未配置时，通过 `getBillingConfigAsync()` 自动读取国家/地区代码；External Billing Program 因设备、账号或地区不可用时直接进入平台默认 ThirdPay 支付页。
- 新增第三方商品列表读取、商品存在性查询、WebView 导航栏文案设置和跳过 Google 信息页能力；信息页跳过默认读取 `ThirdPayStoreConfig`，运行时可由 `IIAPThirdPayCheckoutCapable` 覆盖。
- 统一配置未返回 `payment_customer_ids` 时不阻断支付，继续使用不含渠道客户号的支付 URL。
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
- 统一支付配置、服务端待补发订单与批量验单由 `ThirdIapNetService` 构造公共 Header 和 Protobuf 请求，与 Mobile IAP 的协议服务保持同层职责。
- Runtime 按 Data、Channel、Net、Google、Order、WebView、ExternalBrowser、Native 与 Utils 分层，`ThirdPayStore` 拆分为公开入口、非公开方法、字段属性、外部浏览器会话和打点 partial 文件。
- 第三方支付票据透传统一使用 `IAPRequest.ReceiptParam`：支付 URL 的 `custom_param` 封装 `receipt_param`，验单响应直接回填 `IAPResult.ReceiptParam`。
- 精简第三方支付协议，移除支付方式列表、重复的 `payment_appid`、首充标记和验单请求中的服务端订单号字段；删除旧字段占位，统一领域类型前缀、`order_list` 字段及验单结果命名。
- 第三方支付协议路径由 `/game_recharge/*` 统一为 `/third_pay/*`，待校验订单和验单协议分别统一为 `query_pending_order` 与 `verify_iap`，并同步重命名 Proto、Net Service、Store 配置和 Demo NetCmd。

### Removed

- 移除冗余聚合接口 `IIAPThirdPayCapable`，调用方改为按需获取配置、商品和结算三个细分能力接口。
- 移除 `ThirdPayExternalBrowserLifecycleProxy` 及其静态事件注册链。
- 移除 `ThirdPayStoreConfig` 的国家码字段及配置默认值，国家码不再由 Store 配置注入。
- 移除 `ThirdGetProductList`、`ThirdGetPayChannelParams`、`ThirdOpenURL` 及对应 Store 配置、渠道参数手动注入和分散缓存实现。
- 移除创建订单协议和配置。
- 移除旧 `ThirdPayGoogleExpand` 及自定义 Android 代理依赖。
- 移除业务侧 `IThirdPayInAppLauncher` 与 `SetInAppLauncher` 注入接口。
- 移除 `PbNetPayTypeInfo`、`IIAPThirdPayCapable.GetPayTypeList` 及上次支付方式持久化状态。

## [0.0.1] - 2026-06-03

### Added

- 首个 ThirdPay Store 版本。
