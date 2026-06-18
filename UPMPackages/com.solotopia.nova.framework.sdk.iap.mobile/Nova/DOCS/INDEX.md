# Nova Framework - SDK - IAP - Mobile 文档索引

> 本包是 Nova IAP 的官方移动内购 Store，实现 Google Play + iOS App Store 支付链路。
> 当前事实以 `Nova/Scripts/Runtime/**` 与本目录活跃文档为准。

## 业务侧公开 API

| 类型 | 命名空间 | 说明 | 文档 |
|---|---|---|---|
| `MobileStore` | `NovaFramework.SDK.IAP.Mobile.Runtime` | Mobile IAP Store，通过父包 `IAPPlugin` 反射发现并初始化 | [MobileStore.md](./MobileStore.md) |
| `IAPMobileRequest` | `NovaFramework.SDK.IAP.Runtime` | Mobile 渠道支付请求，继承 `IAPRequest` | [MobileStore.md](./MobileStore.md) |
| `MobileStoreConfig` | `NovaFramework.SDK.IAP.Mobile.Runtime` | Mobile Store 专属配置，包含 Google / Apple 查单和验单 Cmd 名 | [MobileStore.md](./MobileStore.md) |
| `IIAPMobileQueryCapable` | `NovaFramework.SDK.IAP.Runtime` | 平台商品信息查询能力 | [MobileStore.md](./MobileStore.md) |
| `IIAPMobileSubscriptionCapable` | `NovaFramework.SDK.IAP.Runtime` | 订阅到期、非消耗品持有、订阅有效期查询能力 | [MobileStore.md](./MobileStore.md) |
| `IAPMobileErrorCode` | `NovaFramework.SDK.IAP.Mobile.Runtime` | Mobile Store 支付过程失败原因；通过 `IAPResult.ErrorCode` 返回，也写入失败打点 `nova_reason` | [MobileStore.md](./MobileStore.md) |
| `MobileIapNetService` | `NovaFramework.SDK.IAP.Mobile.Runtime` | 查单与批量验单协议封装 | [MobileUtils.md](./MobileUtils.md) |

## 架构文档

- [MobileIAP-Architecture.md](./MobileIAP-Architecture.md) — Mobile IAP 服务拆分、Unity IAP 5.x 初始化、回调路由、存档与验单链路。

## 类规格文档

- [MobileStore.md](./MobileStore.md) — `MobileStore` 对外 API、配置、订单状态机、支付与补单流程。
- [MobileInitService.md](./MobileInitService.md) — Unity IAP 初始化服务和初始化失败原因。
- [MobileUtils.md](./MobileUtils.md) — 票据解析与购买透传参数编解码工具。

## 当前契约重点

- `QueryPendingOrder` 响应里的 `table_id` 是 `long`，客户端优先用它合并 `OrderRecords`；`parameter` 解码只作为旧协议兜底。
- `MobileOrderRecord.TransactionId` 是平台订单 ID；Android 可在运行期写入 Google `OrderId`，但通过条件编译禁止持久化；iOS 会写入本地存档供重启后补单验单使用。
- 订阅商品在自身有效期内重复支付会本地返回 `IAPMobileErrorCode.SubscriptionIsReady`，不会再发起 Unity IAP 平台购买。
- Google 验单与本地支付成功打点去重使用 `GoogleToken`，不是 `TransactionId`。
- `nova_iap_local_pay_success.nova_order_id` 优先使用 Unity IAP receipt 解析出的平台 `OrderId`；缺失时回退当前运行期 `TransactionId`。
- `nova_iap_validate_success.nova_order_id` 优先使用服务端验单响应 `OrderId`；缺失时回退当前运行期 `TransactionId`。
- `nova_iap_local_pay_fail` / `nova_iap_validate_fail` / `nova_iap_validate_fail_finish` 的 `nova_reason` 统一写入 `IAPMobileErrorCode` 的 int 值；补充描述写入 `nova_reason_detail`。
- Mobile 打点 `Debug` 字段来自父包注入的 `DevelopMode == Debug`，不再使用 `EnableAlwaysPaySucceed`。

## 最新实现快照

- 初始化失败原因只有一套：`MobileStoreInitFailureReason`，用于 `IAPInitResult.FailReason` 和 `nova_iap_init.nova_init_failure_reason`。
- 支付过程失败原因只有一套：`IAPMobileErrorCode`，其中 0-8 是业务返回粗粒度错误，1000-1010 是 Unity IAP 本地购买失败映射，2000+ 是验单打点细分原因。
- 本地存档以 `tableId` 合并订单；`TransactionId` 可承载平台订单号，但 Android 不持久化，iOS 随本地存档保留。
- Google 使用 `GoogleToken` 作为验单凭据和本地支付成功打点去重 key；Apple 使用 `TransactionId`。
- `TrackChannel` 按平台输出 `google` / `ios` / `mobile`，TGA 侧可用该值区分 `solar_channel`。

## 归档文档

- [MobileStore-Design.md](./MobileStore-Design.md) — 旧版设计说明，已转换为归档入口；当前事实以 `MobileIAP-Architecture.md` 和 `MobileStore.md` 为准。

## 相关

- 父包文档索引：`../../../com.solotopia.nova.framework.sdk.iap/Nova/Doc/INDEX.md`
- 父包类规格：`../../../com.solotopia.nova.framework.sdk.iap/Nova/Doc/IAPPlugin.md`
- 父包架构：`../../../com.solotopia.nova.framework.sdk.iap/Nova/Doc/IAP-Architecture.md`
