# Changelog

## [Unreleased]

### Added

- 新增不可变 `VoucherWalletSnapshot`、`VoucherQuote`、`VoucherRefreshResult` 和展示明细模型。
- 新增有界精确组合报价引擎，确定性遵循券金额、件数、面值和稳定 ID 优先级。
- 新增按 `game_order_id` 索引的完整命令 journal、账号 generation 隔离和可恢复交易状态机。
- 新增独立 `IIAPVoucherTestCapable`，接通 `ThirdGiftVoucherTestGrant` 测试发放并在成功后刷新钱包。

### Changed

- `IIAPVoucherCapable` 改为 `Wallet / RefreshWalletAsync / Quote` 最小能力接口。
- `IAPVoucherRequest` 只能由 Ready `VoucherQuote` 构造，业务层不再拼装协议 payload。
- Voucher protobuf 统一使用公共 Header，并正式生成与 `.proto` 一致的 C#；状态 1/3 均按成功处理。
- Voucher protobuf 生成类型改为程序集内部可见，避免协议 DTO 泄漏到业务公共 API。
- 同一账号 generation 内的并发钱包刷新合并为一次网络加载。
- `EnableAlwaysPaySucceed` 调试支付成功分支仅在 Editor 编译态保留，移动端产物不再包含 `MOCK_ORDER_VOUCHER` 路径。
- Runtime 按 `Data` 与 `Services/Net|Quote|Transaction|Wallet` 重排，复杂服务统一拆分入口、非公开方法和字段属性分部文件。
- 程序集依赖收口为 Nova Runtime、IAP Runtime 和 UniTask，Editor 测试改为只验证 public API 与导出边界。
- `VoucherStore` 保留 `[IAPStore]` 并移除仅影响 IDE 补全的 `EditorBrowsable(Never)`，与其他 Store 保持一致。

### Removed

- 删除 `DeductPlan`、`VoucherDeductMode`、可变 `VoucherBalanceSnapshot` 和旧查询方法。
- 删除 `VoucherCodes / CoinUsages / AddOrder` 请求字段和 tableId-keyed pending 存档。
- 删除友元程序集声明和直接访问内部交易类型的白盒测试。

本文件记录该 UPM 包各版本的变更内容，遵循 [Keep a Changelog](https://keepachangelog.com/) 格式。

---

## [0.0.1] - 2026-06-03

### Added
- 首个版本：代金券/金币虚拟货币 store。
- 补齐 `CHANGELOG.md` / `LICENSE.md` / `README.md` 三件套，纳入发版强制校验。
