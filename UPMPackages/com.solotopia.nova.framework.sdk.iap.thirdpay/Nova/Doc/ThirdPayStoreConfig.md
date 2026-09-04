# ThirdPayStoreConfig

| 字段 | 说明 |
|---|---|
| `Enabled` | 是否启用 ThirdPay Store |
| `CountryCode` | Debug 覆盖用 ISO 3166-1 alpha-2 国家/地区代码；生产环境通常留空，让 Store 按 Billing / Native / AD / US 兜底解析 |
| `SkipPaymentInformationScreen` | 是否默认跳过 Google 第三方支付信息页；默认 `false`，业务侧 `SetSkipPaymentInformationScreen` 可在运行时覆盖 |
| `ExternalBrowserReturnValidateDelaySeconds` | 外部浏览器支付返回 App 后自动验单前的等待秒数；默认 `2.5`，非正数运行时回落到 `2.5` |
| `GetProductListCmdName` | 拉取第三方商品列表的 NetCmd 名称 |
| `QueryPendingOrderCmdName` | 查询支付成功但客户端尚未校验订单的 NetCmd 名称 |
| `PayChannelParamsCmdName` | 拉取当前账号第三方支付渠道参数的 NetCmd 名称 |
| `VerifyIapCmdName` | 批量验证第三方订单的 NetCmd 名称 |
| `GoogleApiTimeoutSeconds` | Google 外链结算网络类操作（连接/资格/生成 token）的超时秒数，默认 15，用户信息页不受此限制 |

四个协议字段默认分别为 `ThirdGetProductList`、`ThirdQueryPendingOrder`、`ThirdGetPayChannelParams` 和 `ThirdVerifyIap`，可按项目 NetCmd 表覆盖。旧序列化字段会通过 `FormerlySerializedAs` 自动迁移到新名称。

国家码规则与 Solar 保持一致：`CountryCode` 和业务侧 `SetDebugCountryCode` 都是 Debug 覆盖源，优先级最高；实际支付和商品列表使用的有效国家码按 `Debug > Lock > Billing > Native > AD > US` 解析。运行时会在各赋值入口对国家码执行 `Trim + ToUpperInvariant`，并将 `IV` 映射为 `US`。iOS 会在 ThirdPay 初始化时通过包内 StoreKit storefront bridge 读取 App Store 国家/地区代码。

支付页打开方式由平台固定决定，不再通过配置开关或国家列表切换。Android 默认使用外部支付页，打开前先执行 Google 外链信息页流程；当前系统不支持该信息页时继续进入浏览器打开规则。浏览器打开时优先使用 Auth Tab，不支持时回退 Custom Tabs，两者不可用或打开失败时由 C# `Application.OpenURL` 回退系统浏览器。iOS 使用 UniWebView Safe Browsing，Editor 使用嵌入式 UniWebView。

应用内 WebView 默认全屏显示，不再读取业务侧适配区域，也不复用 `IAPPluginConfig.LoadingPanelPrefab` 作为 WebView 承载面板；该配置只用于验单等待期的 IAP Loading。

支付 URL 参数加密不读取 Store 配置、`ConfigManager.AppConfigs.AppAesKey/AppAesIV` 或隐私配置默认 AES。ThirdPay 每次构造支付 URL 时生成 16 字节 Key 和 16 字节 IV，调用 `Util.Encrypt.AES.EncryptBytes(Encoding.UTF8.GetBytes(value), key, iv)` 得到密文，再按 `key + iv + cipher` 拼接字节并整体 Base64 写入 `params`。

支付 URL 基址固定通过 NetCmd `ThirdOpenURL` 解析；应用 ID 统一读取公共请求头的 `AppId`，不在 Store 配置中重复保存。

本配置不再包含打开模式和创建订单协议；ThirdPay 固定为客户端造单 + InAppAuto。
