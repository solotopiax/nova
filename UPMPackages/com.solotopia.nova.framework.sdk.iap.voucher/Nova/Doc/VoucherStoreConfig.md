# VoucherStoreConfig

`VoucherStoreConfig` 实现 `IIAPStoreConfig`，保存正式运行时协议和可选测试发放协议的 NetCmd 名称。

| 字段 | 默认值 | 说明 |
|---|---|---|
| `Enabled` | `true` | 默认是否启用 Voucher Store；关闭时 IAP 启动流程跳过初始化 |
| `GetVoucherListCmdName` | 空字符串 | 拉取礼券与赠币钱包的 NetCmd 名称，如 `GetGiftVoucherList` |
| `DeductVoucherCmdName` | 空字符串 | 提交幂等扣减的 NetCmd 名称，如 `DeductGiftVoucher` |
| `TestGrantVoucherCmdName` | 空字符串 | 测试发放礼券与赠币的 NetCmd 名称，如 `ThirdGiftVoucherTestGrant` |

`TestGrantVoucherCmdName` 为空时 `IIAPVoucherTestCapable.TestGrantAsync` 返回未配置错误且不发送请求。该接口只供测试环境使用，环境权限由服务端裁决。

三条命令名都没有自动默认路由；表中的名称是示例，须与当前项目已正式导出的 NetCmd 名一致，并配置正确的 Host/Path。钱包命令为空时刷新返回 `NetworkError`；扣减命令为空时网络层返回 `URL_NOT_FOUND (-4)`，网关将它归入可重试，支付返回 `TransactionPending` 并保留已创建的原单。先修复路由再恢复原单，不要反复新建订单。协议路径与服务端前置见 [VoucherStore](VoucherStore.md#5-服务端契约)。

配置在 `IAPPluginConfig.StoreConfigs` 的多态条目中维护。保存 ConfigMaster、正式导出当前平台 Runtime 后，在下次 Store 初始化时读入；网关初始化会复制命令名，编辑工作副本或已有配置对象不会实时替换当前网关。运行时启停走 `IAPPlugin.SetStoreEnabled`，不回写持久配置。
