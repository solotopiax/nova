# ThirdPay 文档索引

本包提供应用内第三方支付 Store，仅支持 InAppAuto。

| 文档 | 内容 |
|---|---|
| [ThirdPayStore.md](./ThirdPayStore.md) | 接入流程、国家码锁定时序、统一配置、订单状态和 Google Policy 行为 |
| [ThirdPayStoreConfig.md](./ThirdPayStoreConfig.md) | Store 配置字段 |

主要公开类型：

- `IIAPThirdPayConfigCapable`：国家码覆盖、当前国家码、支付配置可用状态及服务端关闭原因码。
- `IIAPThirdPayProductCapable`：第三方商品快照查询。
- `IIAPThirdPayCheckoutCapable`：Google 信息页策略与 WebView 文案控制。
- `IAPThirdPayRequest`：支付请求，WebView 展示由 ThirdPay 内部全屏处理。
- `ThirdIapNetService`：与 Mobile IAP 同层的商品列表、渠道参数、待补发订单和批量验单协议封装。

支付 URL 构造、UniWebView 5.11.1 生命周期、Android Auth Tab / Custom Tabs 外部支付页、支付回调和全屏支付页均由包内实现，业务无需注入支付页打开器。`ThirdPayStore` 仅持有 `ThirdPayServiceHub`，由 Hub 统一装配服务并维护国家码、商品快照、当前账号存档、外部浏览器 session 与后台任务生命周期。
