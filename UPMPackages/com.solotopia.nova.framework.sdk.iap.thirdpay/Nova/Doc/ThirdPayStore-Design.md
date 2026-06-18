# ThirdPayStore 当前实现设计

> 包名：`com.solotopia.nova.framework.sdk.iap.thirdpay`
> 对齐代码：2026-06 当前 `ThirdPayStore` / `ThirdPayStoreConfig`

## 1. 职责

`ThirdPayStore` 当前负责四件事：

1. 拉取第三方商品列表与支付渠道列表
2. 构造第三方支付 URL 并拉起 Browser / InAppAuto
3. 持久化进行中订单，支持跨会话补单
4. 调用服务端验单并回写支付结果

## 2. 初始化与账号切换

当前实现与旧设计不同：

- `InitializeAsync(...)`
  - 缓存 `ThirdPayStoreConfig`
  - 创建 `ThirdIapNetService`
  - 创建空的 `ThirdPayPersistData`
  - 初始化 `ThirdPayGoogleExpand`
- `SetAccountIDAsync(...)`
  - 才真正按账号读取本地存档
  - 触发补单
  - 拉取渠道参数
  - 预取 Google token

也就是说，读盘与补单已经从旧版的 `InitializeAsync` 挪到 `SetAccountIDAsync`。

## 3. 支付主链路

```
PayAsync
  -> PayGuardAsync
  -> ExecutePayFlowAsync
       1. 若已有未完成订单，先走 ValidateOrderAsync
       2. 客户端生成 clientOrderId
       3. 构造 ThirdOrderData + AES URL
       4. 先落盘，再 Open
       5. 根据 ThirdPayOpenResult 决定后续分支
       6. Success 路径进入 ValidateOrderAsync
```

## 4. 打开支付页

当前代码不再是旧版单一 `OpenWebViewAsync(...)` 设计，而是按 `IAPThirdOpenMode` 分两路：

- `Browser`
  - `Application.OpenURL(url)`
  - 等待 `OnBrowserDeepLinkResolved(orderId)` 命中本地订单
- `InAppAuto`
  - 由业务层覆写 `OpenAsync(...)` 接入 WebView/JS Bridge

打开结果统一收敛为：

- `Success`
- `Cancel`
- `Failed`

## 5. 持久化与补单

本地持久化容器是 `ThirdPayPersistData`：

- `OrderingStates`
- `LastPayMethod`
- `ChannelParams`

补单不再依赖旧版 `OrderId` 存档，而是按当前 `ThirdOrderData.ClientOrderId` 驱动。

## 6. 配置项

当前配置对象 `ThirdPayStoreConfig` 包含：

- `Enabled`
- `AndroidOpenMode`
- `IosOpenMode`
- `CountryCode`
- `AppId`
- `PayUrlBase`
- `GetProductListCmdName`
- `CreateOrderCmdName`
- `CheckOrderCmdName`

旧设计中的 `MaxValidateRetry` 等字段已不再存在。
