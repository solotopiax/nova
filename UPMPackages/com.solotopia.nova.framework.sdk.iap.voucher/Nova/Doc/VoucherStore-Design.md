# VoucherStore 当前实现设计

> 包名：`com.solotopia.nova.framework.sdk.iap.voucher`
> 对齐代码：2026-06 当前 `VoucherStore` / `VoucherStoreConfig`

## 1. 职责

`VoucherStore` 当前负责：

1. 拉取礼券与金币余额
2. 维护本地余额快照
3. 计算扣费方案
4. 提交抵扣请求并支持补单

## 2. 初始化与账号切换

当前实现已经和旧版设计不同：

- `InitializeAsync(...)`
  - 创建 `VoucherBalanceSnapshot`
  - 创建空的 `VoucherStorePersistData`
  - 不立即拉余额
- `SetAccountID(string)`
  - 按账号加载持久化状态
  - 异步补单
  - 异步拉余额

也就是说，余额同步和补单已经迁移到账号切换时机，而不是单纯依赖初始化阶段。

## 3. 支付主链路

```
PayAsync
  -> PayGuardAsync
  -> BuildDeductDetail(...)
  -> AddPendingDeduct(...)
  -> SubmitDeductAsync(...)
```

当前请求体既支持：

- `VoucherCodes`
- `CoinUsages`

不再只是旧版设计中的“单纯金币扣减”路径。

## 4. 余额快照

`VoucherBalanceSnapshot` 维护两类数据：

- 礼券档位
- 金币余额

在当前代码里：

- `FetchBalanceAsync(...)` 成功后，`m_IsBalanceReady = true`
- `GetCoinQuantity(...)` / `GetVoucherQuantity(...)` 在余额未就绪时返回 `0`
- `CalcDeductPlan(...)` 在余额未就绪时返回 `Cash` 方案

## 5. 配置项

当前 `VoucherStoreConfig` 实际包含的是三个协议名，而不是旧设计里的重试次数类字段：

- `GetVoucherListCmdName`
- `DeductVoucherCmdName`
- `TestGrantVoucherCmdName`

## 6. 补单与持久化

本地存档容器是 `VoucherStorePersistData`，键是 `tableId`，值是 `VoucherPendingDeduct`。

补单时会重新调用 `SubmitDeductAsync(...)`，而不是沿用旧版设计里的“先验订单状态机”描述。
