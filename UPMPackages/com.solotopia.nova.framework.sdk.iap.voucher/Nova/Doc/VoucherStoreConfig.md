# VoucherStoreConfig

## 1. 简介

`VoucherStoreConfig` 是 Voucher store 的配置对象，实现 `IIAPStoreConfig`。

## 2. 配置字段

| 字段 | 说明 |
|---|---|
| `Enabled` | 是否启用 Voucher store |
| `GetVoucherListCmdName` | 拉取礼券 / 金币余额的协议名 |
| `DeductVoucherCmdName` | 提交抵扣请求的协议名 |
| `TestGrantVoucherCmdName` | 测试发券协议名 |

## 3. 运行时使用点

- `VoucherStore.InitializeAsync(...)` 缓存配置
- `VoucherIapNetService` 发送协议时依赖这三个 `CmdName`
- `DeductVoucherCmdName` 未配置时，`SubmitDeductAsync(...)` 会直接返回失败

## 4. 关联

- Store 本体：[VoucherStore.md](./VoucherStore.md)
