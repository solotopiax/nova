# Nova Framework - SDK - IAP - Voucher 文档索引

> 本包提供代金券 / 金币抵扣 store。
> 当前代码已经从旧版“固定 Cash 兜底设计”演进为：可拉余额、可计算抵扣方案、可按账号补单的实现。

## 业务侧公开 API

| 类型 | 说明 | 文档 |
|---|---|---|
| `VoucherStore` | 代金券 / 金币抵扣 store，实现 `IIAPVoucherCapable` | [VoucherStore.md](./VoucherStore.md) |
| `VoucherStoreConfig` | Voucher store 配置，承载三条业务协议名 | [VoucherStoreConfig.md](./VoucherStoreConfig.md) |

## 关键数据类型

- `IIAPVoucherCapable`：余额查询与抵扣方案计算能力接口
- `DeductPlan`：推荐抵扣方案
- `IAPVoucherRequest`：代金券支付请求

## 设计文档

- [VoucherStore-Design.md](./VoucherStore-Design.md) — 当前实现设计快照

## 相关

- [VoucherStore.md](./VoucherStore.md) — 代金券支付 store
- [VoucherStoreConfig.md](./VoucherStoreConfig.md) — 代金券支付配置
- [VoucherStore-Design.md](./VoucherStore-Design.md) — 当前实现设计快照
