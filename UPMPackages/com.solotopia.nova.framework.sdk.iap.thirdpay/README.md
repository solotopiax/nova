# Nova Framework - SDK - IAP - ThirdPay

> 包名：`com.solotopia.nova.framework.sdk.iap.thirdpay`
> 当前版本：`0.0.1`

Nova 的应用内第三方支付 Store。客户端生成订单号与兼容 Solar InAppAuto 的加密支付 URL，包内使用 UniWebView 5.11.1 或 Android Auth Tab / Custom Tabs 外部支付页完成支付；Android 通过 Unity Purchasing 5.3.1 的 `ExternalBillingProgramClient` 完成 Google 外链政策流程。

业务按需使用 `IIAPThirdPayConfigCapable`、`IIAPThirdPayProductCapable`、`IIAPThirdPayCheckoutCapable`，分别依赖配置状态与关闭原因、商品查询和结算展示能力。

## 安装

```json
{
  "dependencies": {
    "com.solotopia.nova.framework.sdk.iap.thirdpay": "0.0.1"
  }
}
```

## 设计边界

- 仅支持 InAppAuto，不对业务暴露支付页打开器或 DeepLink 模式；iOS 内部通过 Safe Browsing 与 Deep Link 完成支付回调。
- 支付页打开方式由平台固定决定，不再通过国家列表配置：Android 默认使用外部支付页，iOS 使用 UniWebView Safe Browsing，Editor 使用嵌入式 UniWebView。
- Android 打开支付页前会先执行 Google 外链信息页流程；当前系统不支持该信息页时继续进入浏览器打开规则。外部支付页通过包内 `Nova/Plugins/Android/ThirdPayAuthTab.androidlib` 优先打开 Auth Tab，不支持时回退 AndroidX Browser `1.10.0` Custom Tabs；两者不可用或打开失败时，由 C# 层调用 `Application.OpenURL` 回退系统浏览器。外部支付会话直接订阅 Unity `Application.deepLinkActivated`；三种方式收到 `pay_callback` 成功回调后都会核对活动订单并立即验单，App 返回倒计时仅用于没有收到成功回调时的兜底。
- Android 通过包内 `Nova/Plugins/Android/NovaThirdPayBillingBridge.java` 读取 Google Play Billing 商店地区代码；Google 外链政策仍使用 Unity Purchasing 的 `ExternalBillingProgramClient`。该裸 Java 插件依赖 Unity Purchasing 5.3.1 注入的 `com.android.billingclient:billing:8.3.0`，不需要额外的 `.androidlib`。
- 国家码不再从 `ThirdPayStoreConfig` 读取，也不提供配置默认值；Store 初始化时只并发启动 Billing、iOS Storefront、AD 三路查询，不等待结果，也不请求用户态配置。AD 查询会等待 SDK 统一初始化完成，并持续等待广告插件首次发布国家码，不受广告业务接口默认超时限制。登录后首次通过 `GetCountryCode()` 取得的有效自动解析结果会按 `Billing > iOS Storefront > AD` 锁定到当前 Store 生命周期，后续异步来源和重新登录不会改变业务国家；`SetDebugCountryCode` 仅作为显式 Debug 覆盖，不重写自动锁定值。所有来源执行 `Trim + ToUpperInvariant`，`IV` / `UNKNOWN` / 空值都视为无国家码。
- iOS 使用 UniWebView Safe Browsing；回调、关闭和异常清理由包内服务统一处理。
- 应用 Pause/Focus 生命周期由 `SDKComponent` 统一广播到 `IAPPlugin`，再转发给 `ThirdPayStore`；ThirdPay 不再创建独立隐藏 GameObject 监听生命周期。
- 应用内 WebView 不再接收业务侧适配区域，也不复用 `IAPPluginConfig.LoadingPanelPrefab` 作为承载面板；该配置用于点击支付后的准备阶段和验单等待期的 IAP Loading。
- 本地订单以 `clientOrderId` 为键，同一商品可保留多笔待处理订单。
- 第三方支付可用性、商品、`payment_customer_ids` 和 HTTPS 支付页地址由 `ThirdGetPaymentConfig` 一次返回；客户端额外发送登录后锁定的国家码，白名单、黑名单和国家策略由服务端统一判断。国家码为空或无效时客户端不会发送该协议，协议层也会返回 `PARAM_ERROR` 作为最终保护。
- 统一配置按账号、国家和 NetCmd 缓存，同上下文并发请求合并，账号或国家变化时旧响应不会覆盖当前快照；商品通过 `IAPProductEntry.ThirdProductID` 与服务端 `product_id` 匹配。
- `IAPThirdPayRequest.ReceiptParam` 不限客户端长度；发起支付时封装到 URL 内层 `custom_param` 的 `receipt_param` 字段，验单后由服务端 `receipt_param` 回填 `IAPResult.ReceiptParam`。
- `ThirdIapNetService` 负责统一支付配置、支付成功未校验订单和批量验单三条 Protobuf 协议；路径与命名统一采用 `/third_pay/*`、`query_pending_order` 和 `verify_iap`。
- Runtime 使用 `Services/Config/ThirdPayPaymentConfigService` 集中处理统一配置的请求、校验、缓存、并发合并和旧响应隔离；Store 只编排支付流程并消费固定快照。

详细接入方式见 [Nova/Doc/INDEX.md](./Nova/Doc/INDEX.md)。
