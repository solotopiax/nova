# ThirdPay 平台支付政策核对

> 核对日期：2026-10-23。本文是接入检查表，不是商户资格或上架批准。平台规则会变化；每次面向新商店地区、支付计划或审核提交时，须重新查看下方官方页面及商户后台的实际批准状态。

ThirdPay 实现支付订单、网页打开、回调和服务端验单。**支付能力可运行，不等于该应用、商品、商店地区和支付计划获准使用第三方支付。** 业务应先确定商品是应用内消耗的数字内容、应用外消费的实体商品或服务，再按发行渠道和商店地区选用适用计划。本文重点讨论应用内数字内容。

## 当前包的实际能力

- Android 使用 `com.unity.purchasing` 5.4.4 的 `ExternalBillingProgramClient` 查询其外部结算计划可用性、创建上报 token、显示信息页，随后用 Auth Tab / Custom Tabs / 系统浏览器打开支付页。`SkipPaymentInformationScreen` 可以跳过信息页；计划不可用或 Billing 服务不可用时，当前实现还可能用空 token 继续打开页面。以上结果均不证明商户已获准上线。
- iOS 使用 UniWebView Safe Browsing 打开支付页，并读取 Storefront 国家码；包内没有 StoreKit External Purchase Entitlement 配置、`ExternalPurchaseCustomLink`/披露页或外部交易上报实现。
- 包内 `ThirdGetPaymentConfig` 的 `enabled` 是业务服务端开关，不验证 Play Console / App Store Connect 的资格、商品类别、地区计划、外部链接目的地或交易上报。客户端国家码只用于配置上下文，不能替代平台资格判断。
- ThirdPay 的 `pay_callback` 只是页面信号；实际发货依赖服务端验单和幂等履约。Google / Apple 所要求的平台交易上报是另一条独立链路，不能用 ThirdPay 验单事件代替。

## Google Play 发布前核对

| 商店地区与计划 | 官方要求摘要 | 当前包边界 |
|---|---|---|
| 英国、澳大利亚、日本的 Billing Choice；EEA 的 Billing Choice | 须先按应用和地区在 Play Console 登记；向用户提供 Google Play Billing 与替代结算或外部网页链接的选择，并按所选展示方式接入对应 API、家长控制及信息页。澳大利亚和日本自 2026-09-30、英国和 EEA 自 2026-06-30 提供该计划。 | 当前固定的 ThirdPay 外部网页流程没有 Billing Choice 的选择屏、对应计划 API 和与 Play Billing 并列的结算路径；不能把旧 `ExternalBillingProgramClient` 调用当作 Billing Choice 集成完成。 |
| EEA External Offers | 须单独登记适用应用和地区、接入 External Offers API，并按官方要求上报适用交易；该计划对与 Google Play Billing / User Choice Billing 同时提供外链有单独限制。 | 当前包没有证明已登记或已接入该计划的 API 和上报链。 |
| 美国 | Google 将替代结算和外部内容链接列为独立现有计划；按实际选择的计划核对资格和接口。 | 不能由两位国家码为 `US` 或旧接口返回可用直接推断两个计划均适用。 |
| 其他商店地区 | 以 Google 当期公开的可用地区和商户后台批准为准。 | 未找到适用计划或证据不足时，不应向 Play 用户展示 ThirdPay 数字商品入口。 |

外部结算还需核对结算页、客户支持、退款、订单记录、未成年人保护及平台交易上报。处理银行卡信息时另须核对 PCI DSS 责任。不要通过 `SkipPaymentInformationScreen` 或空 token 回退绕过所选计划的强制披露、选择和上报步骤。

官方依据：[Google Play Billing Choice 登记与地区](https://support.google.com/googleplay/android-developer/answer/17161464?hl=en)、[Billing Choice 接入说明](https://developer.android.com/google/play/billing/billingchoice)、[EEA External Offers 登记与上报](https://support.google.com/googleplay/android-developer/answer/14372887?hl=en)、[Google Play 支付政策](https://support.google.com/googleplay/android-developer/answer/9858738?hl=en)。

## Apple App Store 发布前核对

| 商店地区 | 官方要求摘要 | 当前包边界 |
|---|---|---|
| 美国 | App Review Guidelines 3.1.1(a) 允许面向美国 storefront 的应用提供指向其他购买方式的按钮或链接而无需该条款所述 Entitlement；应用仍须满足其他审核、支付和消费者要求。 | Safe Browsing 能打开网页，不证明具体商品、嵌入式结算方式、链接文案或应用已通过审核；须按实际呈现方式核对 Apple 要求。 |
| 欧盟 | 提供替代支付或应用外优惠须使用 StoreKit External Purchases or Offers Entitlement；可操作外链或应用内替代支付还须调用 StoreKit External Purchase API 和系统披露页，并满足儿童保护与交易报告要求。官方当前 Entitlement Profile 有最低系统版本条件。 | 当前包没有 Entitlement、StoreKit 外部购买 API 和系统披露页；不能仅靠 UniWebView Safe Browsing 在欧盟销售应用内数字商品。 |
| 其他 storefront | 仅在 Apple 对该地区、平台、应用类别和目的地开放的 Entitlement 条件下提供外链；未适用时遵循 3.1.1 的应用内购买规则。 | 不能用国家码、后端 `enabled` 或网页可访问性替代 Apple 批准。 |

官方依据：[App Review Guidelines 3.1.1 与 3.1.1(a)](https://developer.apple.com/app-store/review/guidelines/)、[欧盟 App Store 替代支付选项](https://developer.apple.com/support/payment-options-on-the-app-store-in-the-eu/)。

## 上线验收证据

1. 冻结每个发行渠道、应用 ID、商店地区、商品类型、支付计划及外链目的地；记录 Play Console 或 Apple Developer / App Store Connect 的实际登记与批准证据。
2. 核对当前客户端是否实现该计划要求的选择、披露、资格检查、家长控制、平台 token 和交易上报。缺失时保持该地区 ThirdPay 业务入口关闭；`enabled=true` 不足以开放。
3. 按真实商户、真实商店账号和目标地区测试取消、支付成功、回 App、补单、退款与重复回调；分别核对业务验单/幂等发货和平台交易上报回执。
4. 上架前再次阅读所选计划的官方条款。政策或 SDK 版本变化需单独评估代码改造，不能仅修改本文即视为完成接入。
