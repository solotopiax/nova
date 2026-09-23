# ThirdPayStoreConfig

| 字段 | 说明 |
|---|---|
| `Enabled` | 是否启用 ThirdPay Store |
| `SkipPaymentInformationScreen` | 是否默认跳过 Google 第三方支付信息页 |
| `ExternalBrowserReturnValidateDelaySeconds` | 外部浏览器未收到成功回调时，返回 App 后兜底验单前的等待秒数，默认 `2.5` |
| `PaymentConfigCmdName` | 获取第三方支付配置信息的 NetCmd，默认 `ThirdGetPaymentConfig` |
| `QueryPendingOrderCmdName` | 查询支付成功但客户端尚未校验订单的 NetCmd |
| `VerifyIapCmdName` | 批量验证第三方订单的 NetCmd |
| `GoogleApiTimeoutSeconds` | Google 外链结算网络类操作的超时秒数，默认 `15` |

第三方支付可用性、商品、`payment_customer_ids` 和 HTTPS 支付页地址统一由 `PaymentConfigCmdName` 对应协议返回。客户端不再分别配置商品、渠道参数和支付页 URL 的 NetCmd。

国家码不从 `ThirdPayStoreConfig` 读取。Store 初始化时并发查询 Billing、iOS Storefront 和 AD 国家码，但登录前不会锁定结果或发送用户态配置协议；登录后首次有效自动解析结果会写入 Lock，并在当前 Store 生命周期及重新登录后持续复用。运行时可通过 `SetDebugCountryCode` 显式覆盖当前读取结果，但不会重写既有 Lock。全部来源均无有效值时 `ThirdGetPaymentConfig` 不会发送，网络协议层也会返回 `PARAM_ERROR`。服务端负责白名单、黑名单和国家策略判断，客户端只消费 `enabled` 与整数 `disabled_reason`，不保存策略明细。

支付页打开方式由平台固定决定：Android 使用外部浏览器链，iOS 使用 UniWebView Safe Browsing，Editor 使用嵌入式 UniWebView。
