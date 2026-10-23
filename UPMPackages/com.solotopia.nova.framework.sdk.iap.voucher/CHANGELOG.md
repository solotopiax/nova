# Changelog

## [Unreleased]

### Added

- 新增不可变 VoucherWalletSnapshot、VoucherQuote、VoucherRefreshResult 及展示模型；有界精确组合报价按券金额、件数、面值和稳定 ID 确定优先级。

- 新增按 game_order_id 索引的完整命令 journal、账号 generation 隔离和可恢复交易状态机。

- 新增 IIAPVoucherTestCapable，接通 ThirdGiftVoucherTestGrant 测试发放并在成功后刷新钱包。

### Changed

- 最低 IAP Core 依赖提升至 0.2.0，与旧 protected Store 打点契约删除保持边界；本包仍为禁发包。

- IIAPVoucherCapable 收口为 Wallet/RefreshWalletAsync/Quote，IAPVoucherRequest 仅由 Ready VoucherQuote 构造，业务不再拼装协议 payload。

- Voucher Protobuf 使用公共 Header，C# 与 .proto 一致且生成类型仅程序集内部可见；状态 1/3 均视为成功。

- 同账号 generation 的并发钱包刷新合并为一次网络加载。

- EnableAlwaysPaySucceed 仅在 Editor 编译，移动端不再包含 MOCK_ORDER_VOUCHER 路径。

- Runtime 重排为 Data 和 Net/Quote/Transaction/Wallet 服务，复杂服务拆为 partial；程序集仅依赖 Nova Runtime、IAP Runtime、UniTask，测试限于 public API 和导出边界。VoucherStore 保留 IAPStore 标记并移除 EditorBrowsable(Never)。

- 最低依赖对齐至Nova Framework 0.7.0、IAP Core 0.2.0，与本轮发布的框架及 SDK 接口配套。

- 对齐依赖版本：com.solotopia.nova.framework 0.7.0 → 0.7.3、com.solotopia.nova.framework.sdk.iap 0.2.0 → 0.2.2。

- 兼容声明调整为 Unity 6000.4.2f1，配合 Nova Framework 的 Unity 6000.4.x 支持范围。

### Removed

- 删除 DeductPlan、VoucherDeductMode、可变 VoucherBalanceSnapshot、旧查询、VoucherCodes/CoinUsages/AddOrder 请求字段及 tableId-keyed pending 存档。

- 删除友元程序集和直接访问内部交易类型的白盒测试。

### Fixed

- 钱包共享刷新使用 AsyncLazy，支持尚未完成时的多个等待者，保留调用方取消隔离、账号代际及完成后重新刷新语义。

### Breaking

- 最低支持 Unity 6000.6.7f1，与 Nova Framework 基线一致。

## [0.0.1] - 2026-06-03

### Added
- 首个版本：代金券/金币虚拟货币 store。
- 补齐 `CHANGELOG.md` / `LICENSE.md` / `README.md` 三件套，纳入发版强制校验。
