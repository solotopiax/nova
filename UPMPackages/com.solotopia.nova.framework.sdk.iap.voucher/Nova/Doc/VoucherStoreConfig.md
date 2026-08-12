# VoucherStoreConfig

`VoucherStoreConfig` 实现 `IIAPStoreConfig`，保存正式运行时协议和可选测试发放协议的 NetCmd 名称。

| 字段 | 说明 |
|---|---|
| `Enabled` | 默认是否启用 Voucher Store |
| `GetVoucherListCmdName` | 拉取礼券与赠币钱包的 NetCmd 名称 |
| `DeductVoucherCmdName` | 提交幂等扣减的 NetCmd 名称 |
| `TestGrantVoucherCmdName` | 测试发放礼券与赠币的 NetCmd 名称，如 `ThirdGiftVoucherTestGrant` |

`TestGrantVoucherCmdName` 为空时 `IIAPVoucherTestCapable.TestGrantAsync` 返回未配置错误且不发送请求。该接口只供测试环境使用，环境权限由服务端裁决。
