# Nova Framework - SDK - IAP - ThirdPay

> 包名：`com.solotopia.nova.framework.sdk.iap.thirdpay`
> 当前版本：`0.0.1`

Nova 的应用内第三方支付 Store。客户端生成订单号与兼容 Solar InAppAuto 的加密支付 URL，包内使用 UniWebView 5.11.1 或 Android Auth Tab / Custom Tabs 外部支付页完成支付；Android 通过 Unity Purchasing 5.3.1 的 `ExternalBillingProgramClient` 完成 Google 外链政策流程。

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
- Android 打开支付页前会先执行 Google 外链信息页流程；当前系统不支持该信息页时继续进入浏览器打开规则。外部支付页通过包内 `Nova/Plugins/Android/ThirdPayAuthTab.androidlib` 优先打开 Auth Tab，不支持时回退 AndroidX Browser `1.10.0` Custom Tabs；两者不可用或打开失败时，由 C# 层调用 `Application.OpenURL` 回退系统浏览器。
- Android 通过包内 `Nova/Plugins/Android/NovaThirdPayBillingBridge.java` 读取 Google Play Billing 商店地区代码；Google 外链政策仍使用 Unity Purchasing 的 `ExternalBillingProgramClient`。该裸 Java 插件依赖 Unity Purchasing 5.3.1 注入的 `com.android.billingclient:billing:8.3.0`，不需要额外的 `.androidlib`。
- 国家码解析与 Solar 保持一致：`ThirdPayStoreConfig.CountryCode` 和 `IIAPThirdPayCapable.SetDebugCountryCode` 只作为 Debug 覆盖；有效国家码按 `Debug > Lock > Billing > Native > AD > US` 解析，执行 `Trim + ToUpperInvariant`，并将 `IV` 映射为 `US`；iOS 初始化时通过 StoreKit storefront 读取 Native 国家码。
- iOS 使用 UniWebView Safe Browsing；回调、关闭和异常清理由包内服务统一处理。
- 应用内 WebView 不再接收业务侧适配区域，也不复用 `IAPPluginConfig.LoadingPanelPrefab` 作为承载面板；该配置用于点击支付后的准备阶段和验单等待期的 IAP Loading。
- 本地订单以 `clientOrderId` 为键，同一商品可保留多笔待处理订单。
- 商品列表通过 `IAPProductEntry.ThirdProductID` 与服务端 `product_id` 匹配。
- `IAPThirdPayRequest.ReceiptParam` 不限客户端长度；发起支付时封装到 URL 内层 `custom_param` 的 `receipt_param` 字段，验单后由服务端 `receipt_param` 回填 `IAPResult.ReceiptParam`。
- `ThirdIapNetService` 负责商品列表、渠道参数、支付成功未校验订单和批量验单四条 Protobuf 协议；路径与命名统一采用 `/third_pay/*`、`query_pending_order` 和 `verify_iap`，职责与 Mobile IAP 的协议服务一致。
- Runtime 按 `Data`、`Services/Net`、`Services/Google`、`Services/Order`、`Services/WebView`、`Utils` 分层；`ThirdPayStore` 按公开入口、非公开方法、字段属性和打点拆分 partial 文件。

详细接入方式见 [Nova/Doc/INDEX.md](./Nova/Doc/INDEX.md)。
