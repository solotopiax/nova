# ThirdPayStore

`ThirdPayStore` 负责客户端造单、支付 URL、Google 外链政策、UniWebView 支付页、验单与本地补单。

账号 UID 真正变化并切换到对应存档后，Store 会自动预取一次当前国家或地区的商品。每次 `FetchProductListAsync` 调用都会独立请求，失败时由 Store 内部最多自动尝试三次；以后进入 ThirdPay Tab 或手动刷新仍会发起新请求，业务层无需维护请求状态。旧账号或旧国家请求完成时不会覆盖当前 Store 的商品快照。

## 接入

```csharp
if (iapPlugin.TryGetCapability<IIAPThirdPayCapable>(out var thirdPay))
{
    thirdPay.SetCountryCode("US");
    thirdPay.SetSkipPaymentInformationScreen(true);
    thirdPay.SetThirdPayWebViewTitleText("Payment");
    thirdPay.SetThirdPayWebViewCloseText("close");
    // 可选：业务已有 CID 时可手动注入；未设置时 Store 会在登录成功后按账号自动拉取一次。
    thirdPay.SetChannelParams(cid);
    await thirdPay.FetchProductListAsync(ct);

    IReadOnlyList<PbNetThirdProductInfo> products = thirdPay.GetProductList();
    bool hasProducts = thirdPay.HasProducts();
}

var request = new IAPThirdPayRequest
{
    TableId = 1001,
    CustomData = "business-data",
    ReceiptParam = "first-pay",
    AdaptRectTransform = webViewRect,
};

IAPResult result = await iapPlugin.PayAsync<IAPResult>(request, ct);
```

Android 与 Editor 中，`AdaptRectTransform` 非空时直接作为嵌入式 UniWebView 区域；为空时框架从 iap 模块 `IAPPluginConfig.LoadingPanelPrefab`（经 `IAPStoreContext.LoadingPanelPrefab` 注入，默认 `IAP/IAPLoadingPanel`）加载默认全屏面板。iOS 真机使用全屏 `UniWebViewSafeBrowsing`，适配区域仅作为框架面板生命周期锚点。支付成功、取消、加载失败或 Store 释放时，框架都会关闭原生支付页并销毁临时面板。

## 支付 URL

外层 Query 固定为：

`lang/params/app_id`

`params` 是以下 JSON 经项目 `AppAesKey/AppAesIV` 加密后的字符串：

`id/uid/table_id/currency/price/product_name/country/order_id/platform/is_external_browser/custom_param/payment_customer_ids/google_transaction_token`

其中 `is_external_browser` 固定为 `false`；`custom_param` 是 JSON 字符串，固定封装为 `{"receipt_param":"<IAPRequest.ReceiptParam>"}`，字符串长度不由客户端限制；`payment_customer_ids` 为空时省略；非 Android 或无需 Google token 时 `google_transaction_token` 为空字符串。`CustomData` 只保存在本地订单与支付结果中，不写入支付 URL。
支付 URL 基址通过 NetCmd `ThirdOpenURL` 解析，`app_id` 使用公共请求头中的全局应用 ID。

包固定依赖 UniWebView 5.11.1。Android 使用嵌入式 `UniWebView` 处理 `pay_callback`、`close_callback`、工具栏关闭、返回键、加载错误和内容进程终止；AlipayConnect Scheme 会保留原 Query 并重写为兼容 HTTPS 地址。iOS 使用 `UniWebViewSafeBrowsing`，通过 `Application.deepLinkActivated` 解析支付回调，并在收到终态、用户关闭、取消令牌或 Store 释放时注销回调和关闭浏览器。

## Android Google Policy

Android 真机上使用 Unity Purchasing 5.3.1 的 `ExternalBillingProgramClient`：

1. ThirdPay 优先从配置或业务侧使用 `CountryCode`；未提供时通过包内 Android Billing bridge 调用 `getBillingConfigAsync()` 读取 Google Play Billing 商店国家/地区代码。
2. 连接 Billing Client。
3. 检查 External Billing Program 可用性。
4. External Billing Program 不可用时，跳过 Google 信息页并直接用空 Google token 进入 ThirdPay 包内 UniWebView，不返回 Google 政策错误码。
5. External Billing Program 可用时，创建 reporting details 并取得 external transaction token。
6. `SkipPaymentInformationScreen` 或 `SetSkipPaymentInformationScreen(true)` 生效时，保留 token 并跳过 Google 信息页，直接进入 ThirdPay 包内 UniWebView。
7. 未跳过时，用包含 token 的最终支付 URL 调用 `LaunchExternalLink`，模式为 `CALLER_WILL_LAUNCH_LINK`。
8. Google 信息页成功后，由 ThirdPay 包内 UniWebView 服务打开应用内支付页。

渠道参数 `GetPayChannelParams` 失败（包括服务端错误码 `10707`）不会阻断支付；ThirdPay 会继续构造不含 `payment_customer_ids` 的支付 URL 并打开 UniWebView。商品列表、支付环境、URL 构造、WebView 打开和验单仍按各自错误语义处理。

## 本地订单与验单

订单在打开外部流程前保存，并按 `clientOrderId` 唯一索引。`IAPPlugin.CheckLocalOrdersAsync` 会先通过 `QueryPendingOrderCmdName` 查询服务端支付成功但客户端尚未校验的订单，再与本地订单按 `clientOrderId` 合并并批量验单。本地记录包含完整支付上下文和 `ReceiptParam`，重复时优先保留本地记录；服务端列表查询失败不会阻断本地订单验单。验单响应通过 `PbNetThirdVerifyOrderResult.receipt_param` 直接返回票据透传值，客户端写入 `IAPResult.ReceiptParam`，不再解析响应侧的 `custom_param` JSON。

协议源文件位于 `Nova/Protos/pb_net_third_pay.proto`，生成代码位于
`Nova/Scripts/Runtime/Protos/PbNetThirdPay.cs`。`ThirdIapNetService` 与 Mobile IAP 的
`MobileIapNetService` 保持同一职责：内部构造公共 Header 和 Protobuf 请求、解析 NetCmd、
调用 `NetService.SendAsync`，并记录请求/响应日志。Store 只传国家码或客户端订单号集合；应用 ID 由公共请求头统一提供。

InAppAuto 由客户端直接生成支付 URL，因此协议层不包含创建订单协议，保留以下四条协议：

协议路径统一使用 `/third_pay/*`，其中待校验订单与验单分别为 `/third_pay/query_pending_order` 和 `/third_pay/verify_iap`。

| 配置 | 作用 |
|---|---|
| `GetProductListCmdName` | 拉取第三方商品 |
| `PayChannelParamsCmdName` | 登录成功后按当前账号拉取一次支付页需要透传的 CID 等渠道参数 |
| `QueryPendingOrderCmdName` | 查询服务端支付成功但客户端尚未校验的订单 |
| `VerifyIapCmdName` | 按客户端订单号批量验单 |

| 服务端状态 | 处理 |
|---|---|
| 1 / 2 | 处理中，保留订单 |
| 3 | 支付成功，删除订单并广播可发货成功事件 |
| 4 | 支付失败或过期，删除订单并广播失败事件 |
| 5 | 已发货，删除订单但不再次广播发货成功事件 |
| 6 | 订单不存在，删除本地订单并广播验单失败事件 |
| 未知 / 网络失败 / 响应缺单 | 保留订单，等待下次检查 |

支付页关闭或打开失败也保留订单，避免用户已付款但客户端误删。
