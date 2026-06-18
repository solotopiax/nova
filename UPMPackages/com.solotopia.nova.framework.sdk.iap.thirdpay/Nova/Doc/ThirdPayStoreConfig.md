# ThirdPayStoreConfig

## 1. 简介

`ThirdPayStoreConfig` 是第三方支付 store 的配置对象，实现 `IIAPStoreConfig`。

## 2. 配置字段

| 字段 | 说明 |
|---|---|
| `Enabled` | 是否启用 ThirdPay store |
| `AndroidOpenMode` | Android 平台打开方式 |
| `IosOpenMode` | iOS 平台打开方式 |
| `CountryCode` | 当前默认国家码 |
| `AppId` | 写入第三方支付请求体的应用 ID |
| `PayUrlBase` | 第三方支付页地址基址 |
| `GetProductListCmdName` | 拉商品列表协议名 |
| `CreateOrderCmdName` | 创建订单协议名 |
| `CheckOrderCmdName` | 验单协议名 |

## 3. 运行时使用点

- `ThirdPayStore.InitializeAsync(...)` 缓存本配置
- `GetCurrentOpenMode()` 根据平台选择 `AndroidOpenMode / IosOpenMode`
- `ThirdIapNetService` 使用三个 `CmdName` 发送协议
- `PayUrlBase` / `AppId` / `CountryCode` 参与支付 URL 构造

## 4. 关联

- Store 本体：[ThirdPayStore.md](./ThirdPayStore.md)
