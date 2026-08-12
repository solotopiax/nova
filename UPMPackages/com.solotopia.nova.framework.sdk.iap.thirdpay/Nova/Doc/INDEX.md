# ThirdPay 文档索引

本包提供应用内第三方支付 Store，仅支持 InAppAuto。

| 文档 | 内容 |
|---|---|
| [ThirdPayStore.md](./ThirdPayStore.md) | 接入流程、订单状态和 Google Policy 行为 |
| [ThirdPayStoreConfig.md](./ThirdPayStoreConfig.md) | Store 配置字段 |

主要公开类型：

- `IIAPThirdPayCapable`：业务侧能力入口。
- `IAPThirdPayRequest`：支付请求，包含可选 WebView 适配区域 `AdaptRectTransform`。
- `ThirdIapNetService`：与 Mobile IAP 同层的商品列表、渠道参数、待补发订单和批量验单协议封装。

支付 URL 构造、UniWebView 5.11.1 生命周期、支付回调和默认面板均由包内实现，业务无需注入支付页打开器。
