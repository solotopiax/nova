# Nova Framework - SDK - IAP - ThirdPay

> 包名：`com.solotopia.nova.framework.sdk.iap.thirdpay`
> 当前版本：`0.1.5`

Nova 的应用内第三方支付 Store。客户端生成订单号与加密支付 URL，包内使用 UniWebView 5.11.1 或 Android Auth Tab / Custom Tabs 外部支付页完成支付；Android 当前调用 Unity Purchasing 5.4.4 的 `ExternalBillingProgramClient` 外部结算接口。平台计划资格和现行选择、披露及上报要求须另行核对；接入时须核对当前商品协议、支付网页参数和商户映射。

业务按需使用 `IIAPThirdPayConfigCapable`、`IIAPThirdPayProductCapable`、`IIAPThirdPayCheckoutCapable`，分别依赖配置状态与关闭原因、商品查询和结算展示能力；购买前补单查询的运行时开关与窗口由独立的 `IIAPThirdPayOrderRecoveryCapable` 提供。

Android 重复点击购买时，默认在首次建单后的 30 分钟内先查询已支付但尚未校验的订单；查询命中后只补单，查询失败不创建新订单。配置可关闭该查询或调整零至三十分钟窗口，运行时通过 `IIAPThirdPayOrderRecoveryCapable` 控制。多笔补单通过原订单事件逐笔反馈，单次购买返回的批次完成结果不授予额外发货资格；见 [详细规则](./Nova/Doc/ThirdPayStore.md#android-重复点击补单查询)。

## 线上行为配置

`client_config` 支持控制 Android Google 信息页、购买前补单开关与窗口、浏览器返回验单延迟和 Google 网络超时。五项字段统一使用 Android 前缀；线上明确值优先，缺失或 null 使用本地，false 与合法的窗口 0 均有效。iOS 可省略这些字段。完整字段、范围与示例见 [线上客户端行为配置](./Nova/Doc/ThirdPayStore.md#线上客户端行为配置)。

## 安装

在 Unity 中打开 `Nova/Open PlugPals`，从外网仓库查找并安装 `com.solotopia.nova.framework.sdk.iap.thirdpay`。外网仓库默认入口为 [upm.solotopiax.com](https://upm.solotopiax.com)，实际使用窗口中保存的仓库地址。

当前包声明依赖 Nova Framework `0.7.3`、IAP `0.2.6`、UniTask `10.0.6`、Unity Purchasing `5.4.4` 和 `com.onevcat.uniwebview` `5.11.1`。UniWebView 是商业依赖，须先取得有效授权；通过 PlugPals 的「内部云仓库」取得该依赖时还须有对应仓库访问权限。内部云仓库地址由项目在 PlugPals 工具栏配置，不以本包外网可见代替商业库授权。安装后核对 UPM 实际解析结果，再配置和导出 Runtime；下方仅是已配置相应 registry 后的依赖片段。

```json
{
  "dependencies": {
    "com.solotopia.nova.framework.sdk.iap.thirdpay": "0.1.5"
  }
}
```

## 设计边界

- 仅支持 InAppAuto，不对业务暴露支付页打开器或 DeepLink 模式；iOS 内部通过 Safe Browsing 与 Deep Link 完成支付回调。
- 支付页打开方式由平台固定决定，不通过国家列表配置：Android 默认使用外部支付页，iOS 使用 UniWebView Safe Browsing，Editor 使用嵌入式 UniWebView。
- Android 打开支付页前会先执行 Google 外链信息页流程；当前系统不支持该信息页时继续进入浏览器打开规则。外部支付页通过包内 [Nova/Plugins/Android/ThirdPayAuthTab.androidlib](Nova/Plugins/Android/ThirdPayAuthTab.androidlib/) 优先打开 Auth Tab，不支持时回退 AndroidX Browser `1.10.0` Custom Tabs；两者不可用或打开失败时，由 C# 层调用 `Application.OpenURL` 回退系统浏览器。外部支付会话直接订阅 Unity `Application.deepLinkActivated`；三种方式收到 `pay_callback` 成功回调后都会核对活动订单并立即验单，App 返回倒计时仅用于没有收到成功回调时的兜底。
- Android 通过包内 [Nova/Plugins/Android/NovaThirdPayBillingBridge.java](Nova/Plugins/Android/NovaThirdPayBillingBridge.java) 读取 Google Play Billing 商店地区代码；Google 外链政策仍使用 Unity Purchasing 的 `ExternalBillingProgramClient`。该裸 Java 插件依赖 Unity Purchasing 5.4.4 注入的 `com.android.billingclient:billing:9.0.0`，不需要额外的 `.androidlib`。
- 国家码不从 `ThirdPayStoreConfig` 读取，也不提供配置默认值；Store 初始化时只并发启动 Billing、iOS Storefront、AD 三路查询，不等待结果，也不请求用户态配置。AD 查询会等待 SDK 统一初始化完成，并持续等待广告插件首次发布国家码，不受广告业务接口默认超时限制。登录后首次通过 `GetCountryCode()` 取得的有效自动解析结果会按 `Billing > iOS Storefront > AD` 锁定到当前 Store 生命周期，后续异步来源和重新登录不会改变业务国家；`SetDebugCountryCode` 仅作为显式 Debug 覆盖，不重写自动锁定值。所有来源执行 `Trim + ToUpperInvariant`，`IV` / `UNKNOWN` / 空值都视为无国家码。
- iOS 使用 UniWebView Safe Browsing；回调、关闭和异常清理由包内服务统一处理。
- 应用 Pause/Focus 生命周期由 `SDKComponent` 统一广播到 `IAPPlugin`，再转发给 `ThirdPayStore`；ThirdPay 不创建独立隐藏 GameObject 监听生命周期。
- 应用内 WebView 不接收业务侧适配区域，也不复用 `IAPPluginConfig.LoadingPanelPrefab` 作为承载面板；该配置用于点击支付后的准备阶段和验单等待期的 IAP Loading。
- 本地订单以 `clientOrderId` 为键，同一商品可保留多笔待处理订单。
- 第三方支付可用性、商品、`payment_customer_ids` 和 HTTPS 支付页地址由 `ThirdGetPaymentConfig` 一次返回；客户端额外发送登录后锁定的国家码，白名单、黑名单和国家策略由服务端统一判断。国家码为空或无效时客户端不会发送该协议，协议层也会返回 `PARAM_ERROR` 作为最终保护。
- 统一配置按账号、国家和 NetCmd 缓存，同上下文并发请求合并，账号或国家变化时旧响应不会覆盖当前快照；商品通过 `IAPProductEntry.ThirdProductID` 与服务端 `product_id` 匹配。
- `IAPThirdPayRequest.ReceiptParam` 不限客户端长度；发起支付时写入 URL 内层顶层 `receipt_param` 字段，验单后由服务端 `receipt_param` 回填 `IAPResult.ReceiptParam`。
- 支付 URL 内层 JSON 的 `platform` 与 `channel` 直接使用标准请求 Header 的枚举整数值，不发送枚举名称字符串。
- `IAPThirdPayRequest.FirstRechargeReward` 与 `ExtraBonusReward` 是可选网页奖励参数；每项通过 `IAPThirdPayRewardItem` 声明，构造参数 `itemId` 与 `quantity` 必须为正数，存在时分别写入加密 JSON 的 `first_recharge_reward` 与 `extra_bonus_reward`，缺失时省略节点。奖励不写入订单存档、不参与验单，也不回填支付结果。
- `ThirdIapNetService` 负责统一支付配置、支付成功未校验订单和批量验单三条 Protobuf 协议；路径与命名统一采用 `/third_pay/*`、`query_pending_order` 和 `verify_iap`。
- Runtime 使用 `Services/Config/ThirdPayPaymentConfigService` 集中处理统一配置的请求、校验、缓存、并发合并和旧响应隔离；Store 只编排支付流程并消费固定快照。

详细接入方式见 [Nova/Doc/INDEX.md](./Nova/Doc/INDEX.md)。

面向 Google Play 或 Apple App Store 上架前，先按 [平台支付政策核对](./Nova/Doc/PlatformPaymentPolicy.md) 确认目标商店地区和计划；当前客户端不自动完成所有新计划所需的平台 API、选择/披露或交易上报。

接入时先在 `IAPPluginConfig` 中登记 `ThirdPayStoreConfig` 和带 `ThirdProductID` 的商品，保存 ConfigMaster 并导出当前平台的 Runtime 配置；再核对 `ThirdGetPaymentConfig`、`ThirdQueryPendingOrder`、`ThirdVerifyIap` 三条 NetCmd 的实际路由与后端实现。登录后须取得有效国家码，服务端统一配置须允许支付并提供目标商品及 HTTPS 支付页地址，才能发起新支付。完整步骤与验证边界见 [ThirdPayStore.md](./Nova/Doc/ThirdPayStore.md#接入与验收顺序)。

## 跨包文档定位

本页链接相对于当前文件；独立包之间不假定相邻安装。先用 `UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages()` 按下表精确包名查找，以该包的 `resolvedPath` 为根读取文档入口，再按包内相对链接进入目标。无 Editor 通道时使用当前有效 manifest 或解析工具给出的实际包位置。

| 包名 | 包内文档入口 | 本文引用的包内位置 |
|---|---|---|
| `com.solotopia.nova.framework.sdk.iap` | `Nova/Doc/INDEX.md` | `Nova/Doc/IAP-Architecture.md#支付打点三维属性`<br>`Sample IAPDemo / README.md` |

`Sample 名称 / 相对路径` 表示该包导入的示例内容；通过 Package Manager 和实际导入清单定位当前副本，再从其 README 进入。不要把开发仓源目录或某个版本的导入目录当作固定位置。
