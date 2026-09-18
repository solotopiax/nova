# ThirdPayStore

`ThirdPayStore` 负责客户端造单、支付 URL、Google 外链政策、应用内 UniWebView 或系统外部浏览器支付页、验单与本地补单。

账号 UID 真正变化并切换到对应存档后，Store 会自动预取一次当前国家或地区的商品；业务侧不再手动请求商品列表，只读取 Store 已缓存的商品快照。登录预取、Debug 国家切换后的重新预取、以及支付前快照缺失时的内部兜底都会使用同一套商品拉取链路，失败时最多尝试三次；同账号、同协议名、同国家码的在途请求会合并等待同一结果。旧账号或旧国家请求完成时不会覆盖当前 Store 的商品快照。

国家码解析不再使用默认国家：`ThirdPayStoreConfig.CountryCode` 和 `IIAPThirdPayCapable.SetDebugCountryCode` 都是 Debug 覆盖源，优先级最高；有效国家码按 `Debug > Lock > Billing > iOS Storefront > AD` 解析，全部缺失时返回空字符串。运行时在各赋值入口执行 `Trim + ToUpperInvariant`，并将 `IV` / `UNKNOWN` / 空值都视为无国家码。iOS 会在 ThirdPay 初始化时通过包内 StoreKit storefront bridge 读取 App Store 国家码，Android 会通过包内 Billing bridge 读取 Google Play Billing 国家码，Billing / iOS Storefront / AD 国家码会写入 ThirdPay 当前用户存档作为后续兜底。商品列表请求会记录请求版本、账号和请求国家码；旧账号或旧国家响应不会覆盖当前商品快照。

## 内部状态边界

`ThirdPayStore.Visitors` 只保留 Store 的核心协作者、配置状态和支付页基址缓存：`m_Config`、`m_ConfigReady`、`m_PayUrlBase`、`m_NetService`、`m_ChannelParamsLoader`、`m_WebViewService`、`m_ExternalBrowserService`、`m_GooglePolicy` 等。可独立演进的运行时状态拆到专用对象，避免字段继续散落在 Store partial 中。

| 类型 | 职责 |
|---|---|
| `ThirdPayCountryState` | 维护 Debug / Lock / Billing / iOS Storefront / AD 国家码来源、归一化、优先级和存档同步 |
| `ThirdPayProductCatalogState` | 维护商品快照、商品列表在途请求上下文、请求版本和过期响应判定 |
| `ThirdPayPersistContext` | 维护当前账号 ThirdPay 存档、渠道参数和订单仓储操作 |
| `ThirdPayExternalBrowserSessionState` | 维护当前外部浏览器支付 session 引用与订单/返回版本匹配 |

iOS 商店国家码桥接类命名为 `ThirdPayIosStorefrontCountryBridge`，只表达 StoreKit storefront 国家码读取，不再使用泛化的 Native 命名。该 bridge 的 Unity `.meta` GUID 沿用旧文件，避免 Unity 资产引用漂移。

## 接入

```csharp
if (iapPlugin.TryGetCapability<IIAPThirdPayCapable>(out var thirdPay))
{
    // 可选：仅在调试或灰度固定国家时设置；生产通常留空。
    // thirdPay.SetDebugCountryCode("US");
    string countryCode = thirdPay.GetCountryCode();
    thirdPay.SetSkipPaymentInformationScreen(true);
    thirdPay.SetThirdPayWebViewTitleText("Payment");
    thirdPay.SetThirdPayWebViewCloseText("close");
    // 可选：业务已有 CID 时可手动注入；未设置时 Store 会在登录成功后按账号自动拉取一次。
    thirdPay.SetChannelParams(cid);
    IReadOnlyList<PbNetThirdProductInfo> products = thirdPay.GetProductList();
    bool hasProducts = thirdPay.HasProducts();
}

var request = new IAPThirdPayRequest
{
    TableId = 1001,
    CustomData = "business-data",
    ReceiptParam = "first-pay",
};

IAPResult result = await iapPlugin.PayAsync<IAPResult>(request, ct);
```

Android 真机默认使用外部浏览器支付页，打开前先执行 Google 外链信息页流程；当前系统不支持该信息页时继续进入浏览器打开规则。iOS 真机使用全屏 `UniWebViewSafeBrowsing`，Editor 使用嵌入式 UniWebView 默认全屏显示，不再接收业务侧适配区域，也不复用 `IAPPluginConfig.LoadingPanelPrefab` 作为 WebView 承载面板。点击支付后的渠道参数拉取、商品兜底拉取、本地建单和支付 URL 构建阶段会显示 IAP Loading；打开 WebView 或系统外部浏览器前释放，外部浏览器支付页返回后的倒计时等待与验单阶段会再次显示 Loading。支付成功、取消、加载失败或 Store 释放时，框架都会关闭原生支付页。

## 支付 URL

外层 Query 固定为：

`lang/params/app_id`

`params` 是以下 JSON 经 ThirdPay 动态 AES 加密后的字符串：每次构造支付 URL 时生成 16 字节 Key 和 16 字节 IV，调用 `Util.Encrypt.AES.EncryptBytes(Encoding.UTF8.GetBytes(value), key, iv)` 得到密文，再按 `key + iv + cipher` 拼接字节并整体 Base64。Key/IV 已包含在 Base64 解码后的前 32 字节中，不依赖 Store 配置、`AppConfigs.AppAesKey/AppAesIV` 或隐私配置默认 AES。

`id/uid/table_id/currency/price/product_name/country/client_order_id/platform/channel/is_external_browser/is_custom_tab/show_back_button/custom_param/payment_customer_ids/google_transaction_token`

其中 `channel` 来源于 `Nova.Config.Channel`，通过 `NetBuilder.BuildHeader()` 映射后写入支付 URL，不在 `ThirdPayStoreConfig` 中重复保存。`is_external_browser` 由平台固定策略决定：Android 真机写入 `true` 并使用外部支付页打开方式，iOS 与 Editor 写入 `false` 并使用包内 WebView 服务。`is_custom_tab` 用于区分 Android 外部支付页的实际打开容器：Auth Tab 与 Custom Tabs 写入 `true`，`Application.OpenURL` 兜底、iOS 与 Editor WebView 写入 `false`。`show_back_button` 仅在 Android Auth Tab / Custom Tabs 均不可用并最终回退 `Application.OpenURL` 时写入 `true`，其他支付页 URL 均写入 `false`。`custom_param` 是 JSON 字符串，固定封装为 `{"receipt_param":"<IAPRequest.ReceiptParam>"}`，字符串长度不由客户端限制；`payment_customer_ids` 为空时省略；非 Android 或无需 Google token 时 `google_transaction_token` 为空字符串。`CustomData` 只保存在本地订单与支付结果中，不写入支付 URL。

## 外部浏览器支付

Android 真机固定使用外部支付页，ThirdPay 仍然先创建并保存本地订单，再构造支付 URL，然后按 Auth Tab、Custom Tabs、`Application.OpenURL` 的顺序提交打开请求。浏览器打开成功只表示跳转请求已提交，`PayAsync` 会继续等待返回 App 后的自动验单结果；验单成功、失败、处理中、网络失败或响应缺单都会通过同一份 `IAPResult` 回到本次 `PayAsync` 调用方，同时按结果派发 `PaySuccess` / `PayFailed` 全局事件。

Android 外部支付页会先查询 Custom Tabs provider，再用 `CustomTabsClient.isAuthTabSupported` 判断是否支持 Auth Tab。支持时通过包内 `ThirdPayAuthTab.androidlib` 的透明 Activity 启动 `AuthTabIntent`；不支持或启动失败时回退为 AndroidX Browser `1.10.0` 的 Custom Tabs，并关闭标题、分享、书签、下载、关闭按钮和“在浏览器中打开”入口；当前设备没有 Custom Tabs provider 或 AndroidX 打开链路失败时，由 C# 层调用 Unity `Application.OpenURL` 回退系统浏览器。非 Android 平台也继续使用 Unity `Application.OpenURL` 提交外部浏览器打开请求。

浏览器支付返回 App 后，ThirdPay 子包内部的隐藏生命周期代理会接收 `OnApplicationPause` / `OnApplicationFocus`。ThirdPay 仅在存在外部浏览器支付 session 时响应这些事件：离开 App 时取消旧倒计时；回到前台时重启 `ExternalBrowserReturnValidateDelaySeconds` 秒倒计时并显示 IAP Loading，防止倒计时等待期点击底层业务 UI；若用户在倒计时内反复切前后台，旧倒计时会因版本号失效，只保留最后一次稳定回前台后的验单。

生命周期代理在 `ThirdPayStore.InitializeAsync` 中注册一次，内部 GameObject 使用 `DontDestroyOnLoad`，因此同一次 App 启动内跨场景持续有效；`ThirdPayStore.DisposeAsync` 会注销回调，之后不再响应前后台事件。若该隐藏 GameObject 被外部逻辑误销毁，代理会在 `OnDestroy` 清空静态实例和事件链，但不会自动重新注册；这种情况下仅失去“返回 App 后自动加速验单”，本地订单仍保留，后续补单链路继续兜底。

浏览器返回验单只做一次加速确认。若验单成功或服务端返回终态失败，按现有成功/失败事件处理并清理 session；若服务端返回已发货，外部浏览器返回链路会通过 `PaySuccess` 派发 `CanDeliver=false` 的成功结果通知业务 UI，并把同一成功结果返回给仍在等待的 `PayAsync` 调用方，但业务不应重复发货；若返回处理中、网络失败或响应未包含订单，则通过 `PayFailed` 派发本次验单结果通知业务 UI，并把同一失败结果返回给仍在等待的 `PayAsync` 调用方，同时清理 session 但保留本地订单，后续由 `IAPPlugin.CheckLocalOrdersAsync` / `ThirdPayStore.CheckLocalOrdersAsync` 的补单链路继续兜底。session 清理后，后续普通前后台切换不会再触发这笔浏览器订单验单。

支付 URL 基址通过 `ThirdPayStoreConfig.OpenUrlCmdName` 指定的 NetCmd 解析，默认值为 `ThirdOpenURL`；`app_id` 使用公共请求头中的全局应用 ID。

包内 WebView 服务固定依赖 UniWebView 5.11.1。Editor 使用嵌入式 `UniWebView` 处理 `pay_callback`、`close_callback`、工具栏关闭、返回键、加载错误和内容进程终止；AlipayConnect Scheme 会保留原 Query 并重写为兼容 HTTPS 地址。iOS 使用 `UniWebViewSafeBrowsing`，通过 `Application.deepLinkActivated` 解析支付回调，并在收到终态、用户关闭、取消令牌或 Store 释放时注销回调和关闭浏览器。应用内 WebView 的用户关闭会保留本地订单并立即进入一次直接验单，不再直接按取消失败返回；加载失败和内容进程终止会返回支付页失败并删除本地订单，避免未实际打开成功的订单进入后续补单。

## Android Google Policy

Android 真机上使用 Unity Purchasing 5.3.1 的 `ExternalBillingProgramClient`：

1. ThirdPay 按 `Debug > Lock > Billing > iOS Storefront > AD` 解析有效国家码，全部缺失时返回空字符串；`CountryCode` / `SetDebugCountryCode` 只作为 Debug 覆盖，留空时通过包内 Android Billing bridge 调用 `getBillingConfigAsync()` 读取 Google Play Billing 商店国家/地区代码，并在 iOS 初始化时通过包内 StoreKit storefront bridge 读取 App Store 国家/地区代码。
2. 连接 Billing Client。
3. 检查 External Billing Program 可用性。
4. External Billing Program 不可用时，跳过 Google 信息页并直接用空 Google token 进入 Android 浏览器打开规则，不返回 Google 政策错误码。
5. External Billing Program 可用时，创建 reporting details 并取得 external transaction token。
6. `SkipPaymentInformationScreen` 或 `SetSkipPaymentInformationScreen(true)` 生效时，保留 token 并跳过 Google 信息页，直接进入 Android 浏览器打开规则。
7. 未跳过时，用包含 token 的最终支付 URL 调用 `LaunchExternalLink`，模式为 `CALLER_WILL_LAUNCH_LINK`。
8. Google 信息页成功、不可用或打开失败后，ThirdPay 都进入 Android 浏览器打开规则；用户取消仍返回取消。

渠道参数 `GetPayChannelParams` 失败（包括服务端错误码 `10707`）不会阻断支付；ThirdPay 会继续构造不含 `payment_customer_ids` 的支付 URL 并按平台默认策略打开支付页。商品列表、支付环境、URL 构造、支付页打开和验单仍按各自错误语义处理。

## 本地订单与验单

订单在打开支付页前保存，并按 `clientOrderId` 唯一索引；发起新支付前会先按 `TableId + ReceiptParam` 查找同业务键的本地未完成订单，命中时直接验单并把结果作为本次 `PayAsync` 返回，不再创建新的本地订单或再次打开支付页。`IAPPlugin.CheckLocalOrdersAsync` 会先通过 `QueryPendingOrderCmdName` 查询服务端支付成功但客户端尚未校验的订单，再与本地订单按 `clientOrderId` 合并并批量验单。本地记录包含完整支付上下文、`ReceiptParam` 和本地状态；与服务端补单列表重复时优先保留本地记录，服务端列表查询失败不会阻断本地订单验单。验单响应通过 `PbNetThirdVerifyOrderResult.receipt_param` 直接返回票据透传值，客户端写入 `IAPResult.ReceiptParam`，不再解析响应侧的 `custom_param` JSON。`PbNetThirdVerifyOrderResult.status` 使用 `PbNetThirdVerifyOrderStatus` 枚举，客户端直接按协议状态决策，不再维护额外的数字状态映射或本地分类枚举。

协议源文件位于 `Nova/Protos/pb_net_third_pay.proto`，生成代码位于
`Nova/Scripts/Runtime/Protos/PbNetThirdPay.cs`。`ThirdIapNetService` 与 Mobile IAP 的
`MobileIapNetService` 保持同一职责：内部构造公共 Header 和 Protobuf 请求、解析 NetCmd、
调用 `NetService.SendAsync`，并记录请求/响应日志。Store 只传国家码或客户端订单号集合；应用 ID 由公共请求头统一提供。

InAppAuto 由客户端直接生成支付 URL，因此协议层不包含创建订单协议，保留商品列表、渠道参数、待校验订单查询和批量验单四条业务协议。支付页基址不是 Protobuf 业务协议，通过 `OpenUrlCmdName` 指定的 NetCmd 解析，默认值为 `ThirdOpenURL`。

协议路径统一使用 `/third_pay/*`，其中待校验订单与验单分别为 `/third_pay/query_pending_order` 和 `/third_pay/verify_iap`。

| 配置 | 作用 |
|---|---|
| `OpenUrlCmdName` | 解析第三方支付页基址 |
| `GetProductListCmdName` | 拉取第三方商品 |
| `PayChannelParamsCmdName` | 登录成功后按当前账号拉取一次支付页需要透传的 CID 等渠道参数 |
| `QueryPendingOrderCmdName` | 查询服务端支付成功但客户端尚未校验的订单 |
| `VerifyIapCmdName` | 按客户端订单号批量验单 |

| 服务端状态 | 处理 |
|---|---|
| `PendingPayment` | 用户未支付，删除订单并广播失败事件 |
| `Processing` | 支付处理中，保留订单；直接支付和外部浏览器返回验单会广播 `OrderPending` 失败事件 |
| `Paid` | 支付成功，删除订单并广播可发货成功事件 |
| `FailedOrExpired` | 支付失败或过期，删除订单并广播失败事件 |
| `Delivered` | 已发货，删除订单；外部浏览器返回验单广播 `CanDeliver=false` 的成功通知但不重复发货 |
| `NotFound` | 订单不存在，删除本地订单并广播验单失败事件 |
| `Unspecified` / 未知枚举值 / 网络失败 / 响应缺单 | 保留订单；直接支付和外部浏览器返回验单会广播失败事件，等待下次检查 |

支付页已打开后由用户关闭时会保留订单并立即验单，避免用户已付款但客户端误删；支付页明确未打开成功（WebView 打开异常、`Failed`、外部浏览器服务不可用或打开失败）时会删除本地订单，不进入后续补单。明确收到支付成功回调的直接支付验单默认覆盖完整重试间隔序列（含 `8s` 档），关闭按钮、外部浏览器返回和发起支付前补单属于结果不明确场景，最多验单 3 次；后台补单只验 1 次。仍未终结的本地订单会记录本地状态，等待后续按同业务键发起支付或补单检查继续验单。
