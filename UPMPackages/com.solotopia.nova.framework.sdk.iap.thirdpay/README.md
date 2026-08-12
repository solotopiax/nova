# Nova Framework - SDK - IAP - ThirdPay

> 包名：`com.solotopia.nova.framework.sdk.iap.thirdpay`
> 当前版本：`0.0.1`

Nova 的应用内第三方支付 Store。客户端生成订单号与兼容 Solar InAppAuto 的加密支付 URL，包内使用 UniWebView 5.11.1 完成支付页；Android 通过 Unity Purchasing 5.3.1 的 `ExternalBillingProgramClient` 完成 Google 外链政策流程。

## 安装

```json
{
  "dependencies": {
    "com.solotopia.nova.framework.sdk.iap.thirdpay": "0.0.1"
  }
}
```

## 设计边界

- 仅支持 InAppAuto，不对业务暴露系统浏览器或 DeepLink 模式；iOS 内部通过 Safe Browsing 与 Deep Link 完成支付回调。
- 不包含自定义 Android Java 插件或手写 BillingClient 代理。
- Android 使用嵌入式 UniWebView，iOS 使用 UniWebView Safe Browsing；回调、关闭和异常清理由包内服务统一处理。
- 请求未提供 `AdaptRectTransform` 时加载 iap 模块 `IAPPluginConfig.LoadingPanelPrefab`（经 `IAPStoreContext.LoadingPanelPrefab` 注入，默认 `IAP/IAPLoadingPanel`）作为默认面板，不再单独配置。
- 本地订单以 `clientOrderId` 为键，同一商品可保留多笔待处理订单。
- 商品列表通过 `IAPProductEntry.ThirdProductID` 与服务端 `product_id` 匹配。
- `IAPThirdPayRequest.ReceiptParam` 不限客户端长度；发起支付时封装到 URL 内层 `custom_param` 的 `receipt_param` 字段，验单后由服务端 `receipt_param` 回填 `IAPResult.ReceiptParam`。
- `ThirdIapNetService` 负责商品列表、渠道参数、支付成功未校验订单和批量验单四条 Protobuf 协议；路径与命名统一采用 `/third_pay/*`、`query_pending_order` 和 `verify_iap`，职责与 Mobile IAP 的协议服务一致。
- Runtime 按 `Data`、`Services/Net`、`Services/Google`、`Services/Order`、`Services/WebView`、`Utils` 分层；`ThirdPayStore` 按公开入口、非公开方法、字段属性和打点拆分 partial 文件。

详细接入方式见 [Nova/Doc/INDEX.md](./Nova/Doc/INDEX.md)。
