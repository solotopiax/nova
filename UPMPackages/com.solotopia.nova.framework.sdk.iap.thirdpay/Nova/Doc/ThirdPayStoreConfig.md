# ThirdPayStoreConfig

| 字段 | 说明 |
|---|---|
| `Enabled` | 是否启用 ThirdPay Store |
| `CountryCode` | 默认 ISO 3166-1 alpha-2 国家/地区代码；Android 留空时自动读取 Google Play Billing 商店地区，业务侧 `SetCountryCode` 仍可覆盖 |
| `SkipPaymentInformationScreen` | 是否默认跳过 Google 第三方支付信息页；默认 `false`，业务侧 `SetSkipPaymentInformationScreen` 可在运行时覆盖 |
| `GetProductListCmdName` | 拉取第三方商品列表的 NetCmd 名称 |
| `QueryPendingOrderCmdName` | 查询支付成功但客户端尚未校验订单的 NetCmd 名称 |
| `PayChannelParamsCmdName` | 拉取当前账号第三方支付渠道参数的 NetCmd 名称 |
| `VerifyIapCmdName` | 批量验证第三方订单的 NetCmd 名称 |
| `GoogleApiTimeoutSeconds` | Google 外链结算网络类操作（连接/资格/生成 token）的超时秒数，默认 15，用户信息页不受此限制 |

四个协议字段默认分别为 `ThirdGetProductList`、`ThirdQueryPendingOrder`、`ThirdGetPayChannelParams` 和 `ThirdVerifyIap`，可按项目 NetCmd 表覆盖。旧序列化字段会通过 `FormerlySerializedAs` 自动迁移到新名称。

请求未提供 `AdaptRectTransform` 时加载的默认面板不再由本配置单独指定，统一复用 iap 模块 `IAPPluginConfig.LoadingPanelPrefab`（默认 `IAP/IAPLoadingPanel`），经 `IAPStoreContext.LoadingPanelPrefab` 注入。

AES Key/IV 继续读取 Nova 全局 `ConfigManager.AppConfigs.AppAesKey/AppAesIV`，不在 Store 配置中重复保存。构造支付 URL 前会确认 Config 已加载，并校验 Key 与 IV 均为 UTF-8 16 字节；缺失或非法时记录 Error、取消当前支付环境解析。请在 `Nova/Open Config → 通用配置 → 应用配置` 为当前 `Platform × Channel × DevelopMode` 配置 `AppAesKey / AppAesIV` 后重新导出 `ConfigRuntimeSO`。它不回退到 `PrivacyConfigs` 的默认 AES。

支付 URL 基址固定通过 NetCmd `ThirdOpenURL` 解析；应用 ID 统一读取公共请求头的 `AppId`，不在 Store 配置中重复保存。

本配置不再包含打开模式和创建订单协议；ThirdPay 固定为客户端造单 + InAppAuto。
