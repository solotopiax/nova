# Nova Framework - SDK - IAP - Voucher 文档索引

本包提供“服务端钱包 → 客户端不可变报价 → 单阶段幂等扣减”的 Voucher Store。

## 业务侧公开 API

| 类型 | 说明 |
|---|---|
| `IIAPVoucherCapable` | 获取钱包、刷新钱包和创建不可变报价 |
| `IIAPVoucherTestCapable` | 测试环境发放礼券和赠币，并刷新当前钱包 |
| `VoucherTestGrantRequest` | 不暴露 protobuf 的只读测试发放请求 |
| `VoucherTestGrantResult` | 测试发放结果与当前钱包快照 |
| `VoucherWalletSnapshot` | 不含券唯一码的只读聚合钱包 |
| `VoucherQuote` | 绑定账号 generation 与钱包版本的只读报价 |
| `VoucherRefreshResult` | 钱包刷新结果 |
| `IAPVoucherRequest` | 只能由 Ready 报价构造的支付请求 |

`VoucherStore` 必须保持 public 并保留 `[IAPStore]`，以满足 IAP 启动期反射扫描。它与 Mobile、ThirdPay Store 使用一致的 Attribute 表面；业务层仍通过 `IAPPlugin` 和 capability 使用。

## 包内结构与程序集边界

| 路径 | 职责 |
|---|---|
| `Nova/Scripts/Runtime/Data` | 公开只读模型、内部钱包数据和交易存档 DTO |
| `Nova/Scripts/Runtime/Services/Net` | protobuf 请求发送与领域映射 |
| `Nova/Scripts/Runtime/Services/Quote` | 无网络和持久化依赖的纯报价算法 |
| `Nova/Scripts/Runtime/Services/Transaction` | 稳定订单、journal、恢复和终态派发 |
| `Nova/Scripts/Runtime/Services/Wallet` | 账号 generation、取消令牌和钱包发布 |
| `Nova/Scripts/Runtime/Protos` | 由 protoc 生成的内部协议类型 |

`NovaFramework.SDK.IAP.Voucher.Runtime` 只依赖 `NovaFramework.Runtime`、`NovaFramework.SDK.IAP.Runtime` 和 `UniTask`。包不声明友元程序集；Editor 测试仅通过 public API 和反射验证程序集导出边界，不直接访问内部实现。

## 文档

- [VoucherStore.md](./VoucherStore.md) — 报价、支付、恢复与幂等语义
- [VoucherStoreConfig.md](./VoucherStoreConfig.md) — 运行时协议配置
