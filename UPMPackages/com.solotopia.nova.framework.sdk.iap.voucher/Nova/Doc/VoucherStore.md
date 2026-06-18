# VoucherStore

## 1. 简介

`VoucherStore` 是代金券 / 金币抵扣渠道实现，继承 `IAPStoreBase` 并实现 `IIAPVoucherCapable`。

## 2. 公开 API

| 成员 | 说明 |
|---|---|
| `InitializeAsync(...)` | 初始化 store、快照与持久化容器 |
| `SetAccountID(string)` | 切换账号并触发补单与余额拉取 |
| `CanHandle(IAPRequest)` | 仅处理 `IAPVoucherRequest` |
| `PayAsync(IAPRequest, CancellationToken)` | 发起代金券 / 金币支付 |
| `GetCoinQuantity(int)` | 查询金币数量 |
| `GetVoucherQuantity(int)` | 查询代金券数量 |
| `CalcDeductPlan(long, long)` | 计算推荐抵扣方案 |
| `FetchBalanceAsync(CancellationToken)` | 主动拉取余额 |
| `DisposeAsync(CancellationToken)` | 清理运行时状态 |

## 3. 当前实现要点

- 支持 `VoucherCodes` 与 `CoinUsages` 混合抵扣
- 余额快照未就绪时，能力接口统一降级到 `0 / Cash`
- 支付前先写 `PendingDeduct`，避免取消或崩溃漏单

## 4. 使用示例

```csharp
voucherStore.SetAccountID(userId);
await voucherStore.FetchBalanceAsync(ct);

DeductPlan plan = voucherStore.CalcDeductPlan(tableId, priceMilliCents);

var request = new IAPVoucherRequest
{
    TableId = tableId,
    VoucherCodes = new List<string> { "A001", "A002" },
};

IAPResult result = await voucherStore.PayAsync(request, ct);
```

## 5. 关联

- 配置类型：[VoucherStoreConfig.md](./VoucherStoreConfig.md)
- 当前实现设计：[VoucherStore-Design.md](./VoucherStore-Design.md)
