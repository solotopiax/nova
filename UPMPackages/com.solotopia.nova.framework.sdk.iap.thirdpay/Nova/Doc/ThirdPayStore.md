# ThirdPayStore

`ThirdPayStore` 负责客户端造单、统一支付配置、支付 URL、Google 外链政策、支付页、验单与本地补单。

## 内部职责

`ThirdPayStore` 保留公开入口，并通过 IAP Store 生命周期接口接收 `IAPPlugin` 转发的 Pause/Focus；`ThirdPayServiceHub` 集中持有运行上下文、配置、商品表、共享状态及全部 internal 服务，并统一管理后台任务取消与释放顺序。国家码解析、支付主链、订单验单和补单恢复分别由专用服务处理，跨服务协调类只接收强类型 Hub，不使用字符串查询或 Callback 聚合对象，也不改变现有公共接入方式。

Hub 在 Store 构造时即创建国家码、账号存档和外部浏览器会话状态，保证未启用、尚未执行 `InitializeAsync` 的 Store 接收账号同步或调试国家码调用时仍然安全；具体运行服务只在 `InitializeAsync` 中绑定一次。释放时先取消 Hub 启动的后台任务，再清理支付会话和各平台服务。

## 统一配置

Store 初始化阶段会独立启动 iOS Storefront、Android Billing 和 AD 三路国家码查询，三者互不等待且不阻塞初始化；其中 AD 查询会在后台等待 SDK 统一初始化完成，再持续等待广告插件首次发布国家码，不受业务层 `GetCountryCodeAsync` 默认超时限制。国家码在当前 Store 生命周期内只查询一次并持续复用，此阶段不请求用户态支付配置，也不会提前锁定国家。登录 UID 就绪后，首次通过 `GetCountryCode()` 取得的有效自动解析结果会写入锁定值；已有结果时立即预取，否则首个有效来源返回后锁定并触发 `ThirdGetPaymentConfig`。后续更高优先级来源异步返回不会改变已锁定的业务国家，重新登录也只切换账号数据并刷新支付配置。配置协议在国家码为空或无效时禁止发送；请求使用标准 Header，并额外发送锁定的 ISO 3166-1 alpha-2 国家码。一次响应集中返回：

- `availability.enabled` 与整数 `disabled_reason`
- 当前国家的 `product_config.product_list`
- 可选的 `channel_config.payment_customer_ids`
- `payment_page_config.payment_page_url`

白名单、黑名单和国家开放策略全部由服务端判断，客户端不保存规则明细。配置按 `UID + Country + CmdName` 缓存；账号、国家或配置上下文变化时立即失效并重新预取。同上下文并发请求合并为一次网络请求，旧上下文响应不能覆盖当前快照。协议不包含版本和 TTL，因此快照在上下文失效前持续有效。

### 国家码时序

1. `InitializeAsync` 仅启动三路查询，任一路失败或耗时较长都不会阻塞 Store 初始化，也不会阻塞其他来源；AD 后台任务会退出当前 IAP 初始化调用栈并等待 SDK 管理器完成统一初始化，确保低优先级广告插件完成初始化后再读取数据槽。
2. 登录前的平台回调只写入候选来源；此时调用 `GetCountryCode()` 可以读取候选值，但不会写入 Lock。
3. `SetUserId` 收到非空 UID 后调用 `GetCountryCode()`；若已有候选值，立即锁定并预取配置。
4. 登录时仍无候选值则不发送协议并输出等待国家码的 Warning；首个有效异步结果返回后，国家变化回调会通过统一预取门禁调用 `GetCountryCode()` 完成锁定并触发预取。
5. Lock 建立后，后续 Billing、iOS Storefront 或 AD 回调只更新各自来源，不改变当前业务国家；账号切换也不清除 Lock。
6. Debug 国家码始终高于 Lock，但仅用于显式调试覆盖；清除 Debug 后恢复原 Lock。

`EnsurePaymentConfigAsync` 只读取一次 `GetCountryCode()` 并用该局部值创建配置上下文，避免门禁检查与请求构造之间读到不同国家。空值会在 Store 层直接结束；即使其他调用方绕过 Store，`ThirdIapNetService.GetPaymentConfigAsync` 也会在发送前归一化并拒绝空值、`IV` 与 `UNKNOWN`。

启用支付时，商品配置与 HTTPS 支付页地址必须有效；否则整份响应视为不可用。`payment_customer_ids` 为空不会阻断支付，URL 中省略该参数。

## 支付流程

1. 先检查同业务键的本地未完成订单并验单。
2. 获取统一配置快照，检查服务端 `enabled` 和目标商品。
3. 创建并保存本地订单。
4. 使用同一份固定快照完成 Google 授权和支付 URL 构建。
5. 打开支付页，随后验单；未终结订单由补单链继续处理。

固定快照保证商品、国家、渠道参数和支付页 URL 不会在一次支付中混用不同请求版本。
`nova_iap_create_order_success` 只在本地订单、平台授权和支付 URL 都准备完成后上报；上述准备阶段失败则上报 `nova_iap_create_order_fail`，同一次新订单流程不会同时上报两者。创建成功事件写入 `nova_client_order_id`，此时尚未取得的 `nova_server_order_id` 为空。

## ThirdPay 链路打点

`pay_callback` 的 `status=1`（成功）或 `status=3`（成功但渠道信息尚未同步）是唯一会发送 `nova_iap_local_pay_success` 的支付页终态；随后立即按 `DirectPay` 或 `ExternalBrowserCallback` 验单。Android 外部浏览器、iOS Safe Browsing 和嵌入式 WebView 都由 Unity 的 `Application.deepLinkActivated` 或 UniWebView message 进入同一回调解析，不需要 Java 层重复转发。`pay_callback` 的 `orderid` 必须匹配当前支付会话；参数不完整或订单不匹配的全局 Deep Link 只记录诊断日志，不会结束或推进当前会话。

已经建立支付会话、但没有收到成功 `pay_callback` 的关闭、失败、未知回调或异常会发送 `nova_iap_third_pay_close_order`。关闭事件通过 `nova_third_pay_close_reason` 区分 `close_callback`、嵌入式 WebView 关闭、Safe Browsing 关闭、失败/未知/非法 `pay_callback`、外部浏览器返回无成功回调、订单不匹配、验单开始后的迟到回调、页面加载失败、内容进程终止、取消、会话替换和 Store 释放。`nova_reason_detail` 是该关闭原因的稳定中文说明。

`nova_iap_local_pay_fail` 只在 `PayAsync` 返回边界兜住 Store 前置校验、创建/授权失败和支付页未建立等本地支付失败。支付页已经产生 `nova_iap_local_pay_success` 或 `nova_iap_third_pay_close_order` 后，后续验单失败只发送验单事件，不再补报 `nova_iap_local_pay_fail`。既有 `nova_reason` 保持 `IAPThirdPayErrorCode` 的粗粒度数值，细分原因写入 `nova_third_pay_failure_reason`，其中文说明写入 `nova_reason_detail`。`nova_iap_create_order_fail` 同样保持既有粗粒度 `nova_reason`，并用 `nova_third_pay_create_failure_reason` 区分 UID、统一配置、服务端关闭、商品缺失、支付页服务、Google 授权、URL 构建和授权返回空 URL 等创建阶段失败。

支付页相关事件按需携带以下字段：

| 字段 | 含义 |
|---|---|
| `nova_third_pay_open_mode` | 实际打开方式：Auth Tab、Custom Tabs、系统浏览器回退、嵌入式 WebView 或 iOS Safe Browsing。 |
| `nova_third_pay_callback_status` | `pay_callback` 的归一化状态；未收到回调为 `0`。 |
| `nova_third_pay_close_reason` | 仅关闭订单事件使用的细分关闭原因。 |
| `nova_third_pay_failure_reason` | 支付或验单的细分失败原因；成功时为 `0`。 |
| `nova_client_order_id` / `nova_server_order_id` | 客户端生成订单号与服务端确认订单号，未取得的一侧为空。 |
| `nova_native_error_code` / `nova_native_error_message` | 支付页、浏览器或原生能力的补充错误信息。 |

ThirdPay 的订单生命周期事件统一使用 `nova_client_order_id` 和 `nova_server_order_id`，不发送 `nova_order_id`。通用字段在不同阶段曾分别表示客户端订单号或服务端订单号，无法稳定关联同一笔订单；Mobile 继续保留既有 `nova_order_id` 语义，其他 Store 按自身事件契约定义订单字段。

订单创建、支付页终态、验单和删单事件的完整属性由 `ThirdPayStore.Track.cs` 构造；父包只提供各 Store 共用的商品字段、属性合并和事件发送能力。这样 ThirdPay 的双订单号和细分状态不会进入通用 IAP 基类，也不需要用渠道开关隐藏字段。

`nova_reason` 不承载上述细分枚举，避免同一属性混用多种枚举域。关闭订单和本地订单删除事件不写 `nova_reason`，只使用各自的专属原因字段和 `nova_reason_detail`。各专属属性的稳定枚举如下：

| 属性 | 枚举值 |
|---|---|
| `nova_third_pay_close_reason` | `0` 未知；`1` close_callback；`2` 嵌入式 WebView 关闭；`3` iOS Safe Browsing 关闭；`4` pay_callback 失败；`5` 未知 callback 状态；`6` callback 参数无效；`7` 外部浏览器返回无成功 callback；`8` callback 订单不匹配；`9` 验单开始后的迟到 callback；`10` 页面加载失败；`11` Web 内容进程终止；`12` 页面异常；`13` 操作取消；`14` 会话被替换；`15` Store 释放。 |
| `nova_third_pay_failure_reason` | `0` 未知；`1` Store 禁用；`2` Store 未初始化；`3` 重复支付；`4` 商品不存在；`5` UID 缺失；`6` 支付配置不可用；`7` 服务端关闭支付；`8` 服务端未配置商品；`9` WebView 服务不可用；`10` 外部浏览器服务不可用；`11` Google 连接失败；`12` Google token 创建失败；`13` Google 支付 URL 构建失败；`14` Google 用户取消；`15` 支付页打开失败；`16` 支付页打开异常；`17` 系统浏览器回退 URL 为空；`18` callback 支付失败；`19` callback 参数无效；`20` callback 状态未知；`21` callback 订单不匹配；`22` callback 到达时已开始验单；`23` 外部浏览器返回无成功 callback；`24` 验单服务不可用；`25` 验单网络失败；`26` 验单响应缺少订单；`27` 服务端待支付；`28` 服务端处理中；`29` 服务端失败或过期；`30` 服务端订单不存在；`31` 未知服务端订单状态；`32` 操作取消；`33` 会话被替换；`34` Store 释放；`35` 未分类异常；`36` 第三方支付 URL 构建失败；`37` 授权完成但支付 URL 缺失。 |
| `nova_third_pay_create_failure_reason` | `0` 未知；`1` UID 缺失；`2` 支付配置不可用；`3` 服务端关闭支付；`4` 服务端未配置商品；`5` 支付页服务不可用；`6` Google 连接失败；`7` Google token 创建失败；`8` Google 支付 URL 构建失败；`9` Google 用户取消；`10` 支付 URL 构建异常；`11` 支付 URL 为空。 |
| `nova_client_order_status` | `0` 未知；`1` 已支付可发货；`2` 已发货；`3` 待支付；`4` 处理中；`5` 失败或过期；`6` 不存在；`7` 响应缺单；`8` 网络失败；`9` 验单服务不可用；`10` 未知服务端状态。 |
| `nova_order_delete_reason` | `0` 未知；`1` 已支付；`2` 已发货；`3` 待支付达到上限；`4` 失败或过期；`5` 不存在；`6` 支付页未打开；`7` 授权失败；`8` URL 构建失败；`9` 用户或账号变化；`10` 显式清理；`11` 本地订单非法。 |
| `nova_validation_scene` | `0` 未知；`1` 应用内成功 callback；`2` 应用内支付页模糊返回；`3` 新支付前预检；`4` 启动或后台补单；`5` 外部浏览器返回兜底；`6` 外部浏览器成功 callback。 |

`nova_reason_detail` 按事件选用对应枚举的稳定中文说明：关闭订单对应 `nova_third_pay_close_reason`，创建失败对应 `nova_third_pay_create_failure_reason`，本地支付/验单失败对应 `nova_third_pay_failure_reason`，删单对应 `nova_order_delete_reason`。

## 验单状态与本地订单删除

`nova_iap_validate_fail`、`nova_iap_validate_fail_finish` 和 `nova_iap_validate_success` 都带有 `nova_validation_scene`、`nova_server_order_status`、`nova_client_order_status`、`nova_client_order_id`、`nova_server_order_id`、`nova_protocol_code` 和 `nova_protocol_message`。`nova_protocol_code` 仅表示网络/协议错误码，不能用于代替 `nova_server_order_status` 的 `PbNetThirdVerifyOrderStatus` 原始值。

| 服务端状态 | 客户端状态 | 本地订单处置 |
|---|---|---|
| `Paid` | `SuccessDeliverable` | 删除并允许发货。 |
| `Delivered` | `SuccessAlreadyDelivered` | 删除，不重复发货。 |
| `PendingPayment` | `PaymentPending` | 仅在当前场景验单上限耗尽后删除。 |
| `Processing` | `Processing` | 保留，等待后续验单。 |
| `FailedOrExpired` | `FailedOrExpired` | 删除。 |
| `NotFound` | `NotFound` | 删除。 |
| 响应缺单 / 网络失败 / 服务不可用 / 未知状态 | 对应 `ResponseOrderMissing`、`NetworkFailure`、`ValidationServiceUnavailable`、`UnknownServerStatus` | 保留。 |

本地订单只有在 `RemoveOrder(...)` 实际返回 `true` 后才发送 `nova_iap_third_pay_order_removed`。该事件使用 `nova_order_delete_reason` 区分已支付、已发货、待支付达到上限、失败或过期、不存在、支付页未成功打开、授权失败、URL 构建失败、账号切换、显式清理和本地数据非法；同时记录协议码、协议描述、服务端状态、客户端状态、验单场景和双订单号。没有服务端请求的删单路径使用协议码 `0` 和空协议描述。

## 验单策略

验单重试按触发场景区分。下表中的次数包含首次请求；成功响应只有返回 `PendingPayment` 或 `Processing` 时才会继续重试，其他明确状态立即结束。多次验单场景发生网络失败时，同样在对应次数上限内继续重试。

每次网络失败，以及每次仍需重试的 `PendingPayment` / `Processing` 响应，都会发送一条 `nova_iap_validate_fail`；`nova_validate_count` 包含当前请求，因此可完整还原单笔订单实际验单了几次。达到场景上限或收到终态响应后，再按最终结果发送 `nova_iap_validate_success`、`nova_iap_validate_fail` 或 `nova_iap_validate_fail_finish`。

| 场景 | 触发时机 | 最大验单次数 | 可重试的订单状态 |
|---|---|---:|---|
| `DirectPay` | 当前支付页明确回调支付成功 | `max(7, RetryValidateMaxNum)` | `PendingPayment`、`Processing` |
| `ExternalBrowserCallback` | 当前 Auth Tab、Custom Tabs 或系统浏览器明确回调支付成功 | `max(7, RetryValidateMaxNum)` | `PendingPayment`、`Processing` |
| `DirectPayAmbiguousReturn` | 当前应用内支付页关闭，支付结果不明确 | `min(3, RetryValidateMaxNum)` | `PendingPayment`、`Processing` |
| `ExternalBrowserReturn` | 外部支付没有收到成功回调，仅检测到 App 返回且倒计时结束 | `min(3, RetryValidateMaxNum)` | `PendingPayment`、`Processing` |
| `DirectPayPreflight` | 后续手动点击支付，命中相同 `TableId + ReceiptParam` 的历史订单 | 1 | 不重试 |
| `Recovered` | 后台补单 | 1 | 不重试 |

达到当前场景的次数上限后，客户端统一按最终响应处理：

- `Paid`：移除本地订单并允许发货。
- `Delivered`：移除本地订单，返回成功但不重复发货。
- `PendingPayment`、`FailedOrExpired`、`NotFound`：移除本地订单并返回失败。
- `Processing` 或未知状态：保留本地订单，等待之后手动支付检查或后台补单再次验单。
- 网络失败或验单响应缺少目标订单：保留本地订单。

重试过程中的中间响应不会提前执行上述最终处置，也不会提前删除订单。

## 接入

业务应按功能分别获取 `IIAPThirdPayConfigCapable`、`IIAPThirdPayProductCapable`、`IIAPThirdPayCheckoutCapable`，避免依赖无关能力。

```csharp
if (iapPlugin.TryGetCapability<IIAPThirdPayConfigCapable>(out var thirdPayConfig))
{
    // 仅用于调试或灰度固定国家；生产通常留空。
    // thirdPayConfig.SetDebugCountryCode("US");
    bool configReady = thirdPayConfig.IsPaymentConfigReady;
    bool paymentAvailable = thirdPayConfig.IsPaymentAvailable;
    int disabledReason = thirdPayConfig.PaymentDisabledReason;
}

if (iapPlugin.TryGetCapability<IIAPThirdPayProductCapable>(out var thirdPayProducts))
{
    IReadOnlyList<PbNetThirdProductInfo> products = thirdPayProducts.GetProductList();
}

var request = new IAPThirdPayRequest
{
    TableId = 1001,
    CustomData = "business-data",
    ReceiptParam = "first-pay",
};

IAPResult result = await iapPlugin.PayAsync<IAPResult>(request, ct);
```

`PaymentDisabledReason` 直接返回统一配置中的整数关闭原因码。配置尚未就绪时该属性返回 `0`，调用方必须先检查 `IsPaymentConfigReady`；只有配置已就绪且 `IsPaymentAvailable` 为 `false` 时，原因码才表示服务端关闭原因。

## 支付 URL

支付页基址直接来自统一配置的 HTTPS `payment_page_url`。外层 Query 为 `lang/params/app_id`；`params` 是 ThirdPay 动态 AES 加密后的 JSON，包含商品、用户、国家、订单、平台、渠道、打开方式、票据透传、可选 `payment_customer_ids` 和 Google token。

国家、商品和渠道参数均来自本次支付固定的统一配置快照。应用 ID、语言和渠道读取标准公共 Header，不在 Store 配置中重复保存。

## 协议

协议源位于 `Nova/Protos/pb_net_third_pay.proto`，生成代码位于 `Nova/Scripts/Runtime/Protos/PbNetThirdPay.cs`。

| NetCmd | 路径 | 用途 |
|---|---|---|
| `ThirdGetPaymentConfig` | `/v1/third_pay/payment_config` | 获取可用性、商品、渠道参数和支付页地址 |
| `ThirdQueryPendingOrder` | `/v1/third_pay/query_pending_order` | 查询支付成功但尚未校验的订单 |
| `ThirdVerifyIap` | `/v1/third_pay/verify_iap` | 批量验单 |

## 平台行为

Android 真机使用 Auth Tab → Custom Tabs → `Application.OpenURL` 的外部浏览器链；iOS 使用 UniWebView Safe Browsing；Editor 使用嵌入式 UniWebView。Android 外部支付会话直接订阅 Unity `Application.deepLinkActivated`，三种外部打开方式收到 `uniwebview://pay_callback?orderid=...&status=...` 后，都会先核对活动会话订单号，再取消返回倒计时并按 `ExternalBrowserCallback` 立即验单；没有收到成功回调时，App 返回后的倒计时继续按 `ExternalBrowserReturn` 兜底验单。具体请求次数和订单处置遵循“验单策略”。
