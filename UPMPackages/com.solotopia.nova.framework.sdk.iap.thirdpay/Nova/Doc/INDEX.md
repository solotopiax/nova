# Nova Framework - SDK - IAP - ThirdPay 文档索引

> 本包提供第三方支付 store，实现浏览器 / 应用内 WebView 两类拉起路径。
> 当前代码已经从旧版“创建订单后再打开 WebView”的设计，演进为“本地造单号 + AES URL + 三态 OpenResult + 补单”的实现。

## 业务侧公开 API

| 类型 | 说明 | 文档 |
|---|---|---|
| `ThirdPayStore` | 第三方支付 store，负责拉商品、拉起支付页、补单与验单 | [ThirdPayStore.md](./ThirdPayStore.md) |
| `ThirdPayStoreConfig` | 第三方支付配置，承载打开模式、支付页地址与三个协议名 | [ThirdPayStoreConfig.md](./ThirdPayStoreConfig.md) |

## 关键数据类型

- `IAPThirdPayRequest`：第三方支付请求，包含 `PayTypeId`、`PayMethod`、`AdaptRectTransform`
- `ThirdPayOpenResult`：支付页打开结果三态（`Success / Cancel / Failed`）

## 设计文档

- [ThirdPayStore-Design.md](./ThirdPayStore-Design.md) — 当前实现设计快照

## 相关

- [ThirdPayStore.md](./ThirdPayStore.md) — 第三方支付 store
- [ThirdPayStoreConfig.md](./ThirdPayStoreConfig.md) — 第三方支付配置
- [ThirdPayStore-Design.md](./ThirdPayStore-Design.md) — 当前实现设计快照
