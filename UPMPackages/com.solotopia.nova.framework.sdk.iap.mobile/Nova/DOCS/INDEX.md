# Nova Framework - SDK - IAP - Mobile 文档索引

> 本包是 Nova IAP 的移动端官方内购商店，实现 Google Play 与 iOS App Store 支付链路。
> 当前事实以 `Nova/Scripts/Runtime/**` 与本目录活跃文档为准。

## 业务侧公开 API

| 类型 | 命名空间 | 说明 | 文档 |
|---|---|---|---|
| `MobileStore` | `NovaFramework.SDK.IAP.Mobile.Runtime` | Mobile IAP Store，通过父包 `IAPPlugin` 反射发现并初始化 | [MobileStore.md](./MobileStore.md) |
| `IAPMobileRequest` | `NovaFramework.SDK.IAP.Runtime` | Mobile 渠道支付请求，继承 `IAPRequest` | [MobileStore.md](./MobileStore.md) |
| `MobileStoreConfig` | `NovaFramework.SDK.IAP.Mobile.Runtime` | 移动端官方内购商店专属配置，包含 Google / Apple 查单和验单 Cmd 名 | [MobileStore.md](./MobileStore.md) |
| `IIAPMobileQueryCapable` | `NovaFramework.SDK.IAP.Runtime` | 平台商品信息查询能力 | [MobileStore.md](./MobileStore.md) |
| `IIAPMobileSubscriptionCapable` | `NovaFramework.SDK.IAP.Runtime` | 订阅到期、非消耗品持有、订阅有效期查询能力 | [MobileStore.md](./MobileStore.md) |
| `IAPMobileErrorCode` | `NovaFramework.SDK.IAP.Mobile.Runtime` | 移动端官方内购商店支付过程失败原因；通过 `IAPResult.ErrorCode` 返回，也写入失败打点 `nova_reason` | [MobileStore.md](./MobileStore.md) |
| `MobileIapNetService` | `NovaFramework.SDK.IAP.Mobile.Runtime` | 查单与批量验单协议封装 | [MobileUtils.md](./MobileUtils.md) |

## 架构文档

- [MobileIAP-Architecture.md](./MobileIAP-Architecture.md) — Mobile IAP 服务拆分、Unity IAP 5.x 初始化、回调路由、存档与验单链路。

## 类规格文档

- [MobileStore.md](./MobileStore.md) — `MobileStore` 对外 API、配置、订单状态机、支付与补单流程。
- [MobileInitService.md](./MobileInitService.md) — Unity IAP 初始化服务和初始化失败原因。
- [MobileUtils.md](./MobileUtils.md) — 票据解析与购买透传参数编解码工具。

## 商品拉取与不可用 SKU 口径

- 移动端官方内购商店初始化只等待 Unity IAP 商店连接成功；商品信息在后台 `FetchProducts`，不会阻塞 `IAPInitResult.Success`。
- 商品拉取状态、自动重试、部分成功、迟到失败和不可用 SKU 校正收口在 `Services/Init` 内部 `MobileProductFetchCoordinator`，`MobileInitService` 只保留初始化生命周期和回调委托。
- `MobileProductFetchCoordinator` 是 Init 内部协调器，不是独立 service；相关成员级 XML 注释、运行期日志和测试断言消息遵循全仓 OPS 中文口径，简单表达式体、短三元表达式和短属性 getter 在不牺牲可读性时保持一行，文件头继续沿用仓库英文模板标签。
- 商品整体拉取失败后按 `MobileStoreConfig.ProductFetchRetryDelaysMs` 自动重试；默认值为 `2s / 5s / 10s` 共 3 次。配置为空或包含非正数时回落默认值并打印中文警告日志；成功、部分成功或 Dispose 会取消悬空重试并重置重试序号。
- 任一轮收到 `OnProductsFetched`，或 `OnProductsFetchFailed` 的失败数量小于本轮请求数量时，即认为至少存在成功商品，停止后续商品拉取重试。
- `OnProductsFetched` 会清理旧失败 SKU，并按 `StoreController` 当前状态恢复仍缺失的 pending SKU。
- `OnProductsFetchFailed` 先物化为内部失败快照，再只把 `StoreController` 当前仍查不到的 SKU 写入 `m_UnavailableSkus`；成功态下迟到失败不会回退状态、不会重试、不会重复触发商品成功后置流程。
- 14 个商品成功、1 个 SKU 找不到时，只保留该缺失 SKU 的查询/购买拦截，不会污染其他已成功商品。

## 当前契约重点

- `QueryPendingOrder` 响应里的 `table_id` 是 `long`，客户端优先用它确定商品行，再结合 `parameter` 解码出的 `ReceiptParam` 合并到 `OrderRecordsByKey`；`parameter` 解码不到 `ReceiptParam` 时按空透传兼容旧协议。
- `MobileOrderRecord.TransactionId` 是平台订单 ID；Android 可在运行期写入 Google `OrderId`，但通过条件编译禁止持久化；iOS 会写入本地存档供重启后补单验单使用。
- 订阅商品在自身有效期内重复支付会本地返回 `IAPMobileErrorCode.SubscriptionIsReady`，不会再发起 Unity IAP 平台购买。
- Google 验单与本地支付成功打点去重使用 `GoogleToken`，不是 `TransactionId`。
- `nova_iap_local_pay_success.nova_order_id` 优先使用 Unity IAP receipt 解析出的平台 `OrderId`；缺失时回退当前运行期 `TransactionId`。
- `nova_iap_validate_success.nova_order_id` 优先使用服务端验单响应 `OrderId`；缺失时回退当前运行期 `TransactionId`。
- `nova_iap_local_pay_fail` 覆盖所有 `MobileStore.PayAsync` 返回失败 `IAPResult` 的场景；Unity IAP `OnPurchaseFailed` 与 `OnPurchaseConfirmed(FailedOrder)` 也会直接上报本地支付失败点。失败打点不做运行期去重；`nova_reason` 统一写入 `IAPMobileErrorCode` 的 int 值，`PluginRouter` guard 失败会映射到 Mobile 错误码并在 `nova_reason_detail` 保留原始 `ErrorSource:ErrorCode`。
- `nova_iap_validate_fail` / `nova_iap_validate_fail_finish` 的 `nova_reason` 统一写入 `IAPMobileErrorCode` 的 int 值；补充描述写入 `nova_reason_detail`。
- Mobile 打点 `Debug` 字段来自父包注入的 `DevelopMode == Debug`，不再使用 `EnableAlwaysPaySucceed`；`EnableAlwaysPaySucceed` 只在 Editor 调试支付时生效。
- 商品拉取成功后只自动触发启动期平台 `FetchPurchases` 和延迟权益刷新；订阅倒计时到期会再次 `FetchPurchases` 刷新平台已有购买与票据缓存，再执行权益刷新；`RestoreTransactions` 仅由用户主动恢复购买入口调用，完整补单扫描仍由统一补单入口串行执行。
- Mobile 后台任务统一经 `MobileServiceHub.RunBackgroundTask` 启动并接入移动端官方内购商店运行期取消令牌；`DisposeAsync` 会先取消后台任务，再释放各内部服务。入口委托固定为 `Func<CancellationToken, UniTask>`，返回 `UniTask<T>` 的动作需要用 lambda 或包装方法显式 `await` 后丢弃返回值。支付验单桥接被取消时返回 `StoreNotAvailable` 失败结果，不向业务层抛取消异常。
- `MobileValidationService` 已把验单队列单次执行保护拆到 `MobileValidationQueueCoordinator`，本地订单扫描规则拆到 `MobileValidationLocalOrderScanner`；对外补单、支付和 Restore API 不变。

## 最新实现快照

- 初始化失败原因只有一套：`MobileStoreInitFailureReason`，用于 `IAPInitResult.FailReason` 和 `nova_iap_init.nova_init_failure_reason`。
- 支付过程失败原因只有一套：`IAPMobileErrorCode`，其中 0-9 是业务返回粗粒度错误，1000-1010 是 Unity IAP 本地购买失败映射，2000+ 是验单打点细分原因；`PluginRouter` 层 guard 失败在 Mobile 本地支付失败打点中映射到该枚举域，原始来源保留在 `nova_reason_detail`。
- 本地未完成订单仓库以 `tableId + ReceiptParam` 订单键合并订单；不传 `ReceiptParam` 时保持旧 tableId-only 语义。`TransactionId` 可承载平台订单号，但 Android 不持久化，iOS 随本地存档保留。
- Google 使用 `GoogleToken` 作为验单凭据和本地支付成功打点去重 key；Apple 使用 `TransactionId`。
- `TrackChannel` 按平台输出 `google` / `ios` / `mobile`，TGA 侧可用该值区分 `solar_channel`。

## 相关

- 父包文档索引：`../../../com.solotopia.nova.framework.sdk.iap/Nova/Doc/INDEX.md`
- 父包类规格：`../../../com.solotopia.nova.framework.sdk.iap/Nova/Doc/IAPPlugin.md`
- 父包架构：`../../../com.solotopia.nova.framework.sdk.iap/Nova/Doc/IAP-Architecture.md`
