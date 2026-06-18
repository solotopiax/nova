# Mobile IAP 内部架构文档

> 包名：`com.solotopia.nova.framework.sdk.iap.mobile`
> 最后更新：2026-06-12
> 适用版本：Unity IAP 5.x（`UnityEngine.Purchasing`）

## 1. 整体架构

`MobileStore` 是 Google Play + iOS App Store 官方内购 Store，通过 `[IAPStore]` 被父包 `IAPPlugin` 反射发现。Store 自身只保留对外接口和生命周期入口，具体职责拆到 `MobileServiceHub` 管理的服务中。

```
IAPPlugin（父包）
  └── MobileStore : IAPStoreBase,
                    IIAPMobileQueryCapable,
                    IIAPMobileSubscriptionCapable
        └── MobileServiceHub
              ├── MobileIapNetService
              ├── MobileExtendedService
              ├── MobileStoreService
              ├── MobileInitService
              ├── MobileProductService
              ├── MobileSubscriptionService
              ├── MobileValidationService
              ├── MobileRestoreService
              └── MobilePurchaseService
```

辅助结构：

- `MobileStorePersistData`：统一存档容器。
- `MobileOrderRecord` / `MobileOrderStatus`：本地订单状态机。
- `MobileRestoreCoordinator`：Restore 双路计数器。
- `MobileRuntimeContext`：初始化连接态与失败态。
- `MobileReceiptParser`：Unity IAP Receipt 解析缓存。
- `MobileStoreParameterCodec`：UID + tableId 编码为平台透传 UUID。

## 2. MobileServiceHub

`MobileServiceHub` 构造时写入共享依赖：

| 属性 | 说明 |
|---|---|
| `Context` | 父包注入的 `IIAPStoreContext` |
| `Config` | `MobileStoreConfig` |
| `Table` | 父包构建的 `IIAPProductTable` |
| `Store` | 所属 `MobileStore` |

服务引用由 `MobileStore.InitializeAsync` 按序创建并写入，运行期通过 Hub 访问兄弟服务，避免构造期循环依赖。

## 3. 服务职责

| 服务 | 访问性 | 核心职责 |
|---|---|---|
| `MobileIapNetService` | `public sealed` | 查询服务端未完成订单、发送 Google / Apple 普通与订阅批量验单 |
| `MobileExtendedService` | `internal sealed partial` | `StoreController` 唯一持有者，封装平台调用和事件注册 |
| `MobileStoreService` | `internal sealed partial` | Unity IAP `On*` 回调统一入口，只做路由 |
| `MobileInitService` | `internal sealed partial` | Unity IAP 初始化、连接状态、初始化结果上报、后台商品拉取触发 |
| `MobileProductService` | `internal sealed partial` | Receipt 缓存、Product 查询、平台商品信息查询、权益状态辅助 |
| `MobileSubscriptionService` | `internal sealed partial` | 订阅到期时间持久化、订阅倒计时、到期后触发 Restore |
| `MobileValidationService` | `internal sealed partial` | 本地订单状态机、服务端查单、验单队列、批量验单、发货结果派发 |
| `MobileRestoreService` | `internal sealed partial` | Restore 流程、权益检查聚合、Restore 结果事件 |
| `MobilePurchaseService` | `internal sealed partial` | 发起平台购买、订阅有效期拦截、订阅升降级、处理 Pending / Confirmed / Failed 回调 |

## 4. Unity IAP 5.x 初始化

```
MobileStore.InitializeAsync
  ├── base.InitializeAsync(table, config, ctx, ct)
  ├── 创建 MobileServiceHub 和各服务
  ├── m_PersistData = CreateEmptyPersistData()
  └── MobileInitService.InitializeAsync(table, ct)
        ├── UnityIAPServices.StoreController()
        ├── ExtendedService.SetController(controller)
        ├── ExtendedService.RegisterStoreCallbacks()
        ├── 构建 ProductDefinition 列表
        ├── await ExtendedService.Connect()
        └── 等待 OnStoreConnected / FailInitialization
```

连接成功后：

1. `MobileStoreService.OnStoreConnected`
2. `ExtendedService.RegisterProductCallbacks`
3. `MobileInitService.OnStoreConnected`
4. `MarkReady`、`IAPInitResult.Success`、`m_InitTcs.TrySetResult(true)`
5. `FetchProducts` 后台拉取商品
6. `OnProductsFetched` 后先触发一次平台 `RestoreTransactions`，再调用 `FetchPurchases` 拉取平台已有购买
7. `OnPurchasesFetched` 路由到 RestoreService，缓存历史票据并恢复 PendingOrder

商品拉取成功或失败不再决定初始化结果。网络慢时初始化只等待商店连接，不等待商品信息返回；启动期平台恢复交易和平台已有购买拉取在商品信息成功返回后异步触发。商品拉取后的 `RestoreTransactions` 只唤起平台侧订单补全，不直接发起服务端 QueryPendingOrder / Verify。

## 5. 初始化失败原因

`MobileStoreInitFailureReason` 是 Mobile Store 自定义初始化失败码，并通过 `IAPInitResult.FailReason` 以 int 透传。

| 值 | 名称 | 含义 |
|---|---|---|
| 0 | `None` | 未失败 |
| 1 | `PurchasingUnavailable` | 平台内购服务不可用的通用兜底 |
| 2 | `StoreControllerUnavailable` | Unity IAP `StoreController` 创建失败 |
| 3 | `StoreConnectException` | `StoreController.Connect` 抛出异常 |
| 4 | `StoreDisconnected` | 初始化期间商店连接断开 |
| 5 | `InitializationCanceled` | 初始化流程被取消 |

## 6. 回调路由

```
StoreController 事件
  └── MobileStoreService
        ├── OnStoreConnected         → ExtendedService.RegisterProductCallbacks + InitService.OnStoreConnected
        ├── OnStoreDisconnected      → InitService.OnStoreDisconnected
        ├── OnProductsFetched        → InitService.OnProductsFetched
        ├── OnProductsFetchFailed    → InitService.OnProductsFetchFailed
        ├── OnPurchasesFetched       → RestoreService.OnExistingPurchasesFetched
        ├── OnPurchasesFetchFailed   → RestoreService.OnExistingPurchasesFetchFailed
        ├── OnPurchasePending        → PurchaseService.OnPurchasePending
        ├── OnPurchaseDeferred       → PurchaseService.OnPurchaseDeferred
        ├── OnPurchaseConfirmed      → PurchaseService.OnPurchaseConfirmed
        ├── OnPurchaseFailed         → PurchaseService.OnPurchaseFailed
        └── OnCheckEntitlement       → RestoreService.OnCheckEntitlement
```

`MobileStoreService` 不做业务判断，业务处理集中在目标服务中。

## 7. StoreController 收口

`MobileExtendedService` 是唯一持有 `StoreController` 的服务。其他服务不得缓存或直接访问 Controller。

| 操作 | 方法 |
|---|---|
| 注入 / 清空 Controller | `SetController` / `DetachController` |
| 注册事件 | `RegisterStoreCallbacks` / `RegisterProductCallbacks` |
| 连接商店 | `Connect` |
| 发起购买 | `PurchaseProduct` |
| 查询商品 | `GetProductById` / `GetProducts` |
| 确认订单 | `ConfirmPurchase` |
| 权益检查 | `CheckEntitlement` |
| Restore | `RestoreTransactions` |
| 商品拉取 | `FetchProducts` |
| 平台已有购买拉取 | `FetchPurchases` |
| Android / iOS 透传账号 | `SetObfuscatedAccountId`、`SetObfuscatedProfileId`、`SetAppAccountToken` |

## 8. 持久化模型

`MobileStorePersistData` 通过基类 `LoadPersistData<T>` / `SavePersistData<T>` 以 `classify=iap_mobile`、`item=data_{uid}` 持久化。

| 字段 | 说明 |
|---|---|
| `OrderRecords` | 待处理订单记录，key = tableId；Android 不持久化 `TransactionId`，iOS 持久化 `TransactionId` 供补单验单使用 |
| `SubscriptionExpireMs` | 订阅到期 Unix 毫秒，key = tableId |
| `NonConsumeOwnership` | 非消耗品持有标记，key = tableId |
| `HasQueriedPendingFromServer` | 当前 UID 是否曾成功向服务端同步过未完成订单；仅作兼容和诊断标记，不阻止后续 QueryPendingOrder |

UID 切换由 `MobileStore.SetUserId` 触发，重新加载整包存档。

补单扫描只能在登录后执行。登录前平台回调先到达时，只将 PendingOrder 解析出的待验订单暂存在内存中，不读写账号存档，也不发送 QueryPendingOrder / Verify 协议。登录后业务调用 `CheckLocalOrdersAsync` 时，流程先合并登录前暂存订单，再请求服务端 QueryPendingOrder，优先使用返回项里的 `table_id`（long）merge 到本地 `OrderRecords`，随后扫描本地待验订单；`parameter` 解码仅作为旧协议兼容兜底。

Google 订单必须具备 purchase token 才会发送验单协议；本地 `Purchasing` 占位记录缺少 token 时保留等待下次平台回调或服务端 QueryPendingOrder 补齐。iOS Apple 验单协议必须具备 `order_id`（本地 `TransactionId`），缺失时不能发送空订单验单请求，客户端会删除本地待验订单记录并落盘，避免后续启动重复发送无效协议。`TransactionId` 承载平台订单 ID：Android 运行期可写入 Google `OrderId` 供结果和打点回填，但不写入本地存档；iOS 写入 Apple transaction id 并随本地存档保留。它不作为本地存档合并、验单响应匹配或 PaySuccess 去重判断。每次登录后的补单扫描结束后，还会触发一次 `CheckEntitlement` 权益刷新，刷新订阅和非消耗品权益，确保订阅状态不是只依赖倒计时触发；该刷新不重复触发平台 `RestoreTransactions`。

订阅商品发起购买前会先检查当前 tableId 是否仍在有效期内；命中时本地直接返回 `IAPMobileErrorCode.SubscriptionIsReady`，不写入 `Purchasing` 订单，也不再调用 Unity IAP 平台购买。只有当前商品未订阅时，才继续判断同订阅组内其他有效订阅并进入 Android 升降级或非 Android 已订阅失败分支。

## 9. 验单状态

服务端 `PbNetMobileVerifyOrderStatus` 当前含义：

| 值 | 名称 | 客户端处理 |
|---|---|---|
| 1 | `PendingVerify` | 客户端还未发过校验协议；保留订单等待重试 |
| 2 | `Verified` | 校验完毕；删除订单，按可发货成功处理 |
| 3 | `Reissued` | 奖励已通过其他渠道补发；删除订单，成功但 `CanDeliver=false` |
| 4 | `Delivered` | 服务端已处理过订单；删除订单，成功且 `CanDeliver=true`，客户端仍按本地幂等规则补发奖 |
| 5 | `Invalid` | 无效订单；删除订单并派发失败 |

`PaySuccess` 只表达业务需要感知的本地发奖成功。普通商品与补单商品在 `CanDeliver=true` 且当前运行期未派发过同一 tableId 时触发；订阅商品只有当前主动 `PayAsync` 对应的订单才触发，后台补单、Restore 和订阅刷新不走全局 `PaySuccess`。订阅 Restore 通知通过 `SubscriptionRestored` 表达：订阅订单服务端返回 `Verified` 或 `Delivered` 时会收集到 `SubscriptionRestored` 结果列表；`Reissued` 仅更新本地终态和完成 Restore 计数，不进入 `SubscriptionRestored`。如果服务端 QueryPendingOrder 先完成，随后 Unity IAP 又回调同一笔 PendingOrder，客户端按 tableId 合并本地订单并继续走验单终态处理，避免把平台交易号作为业务判断依据。

## 10. 埋点边界

Mobile Store 通过 `MobileStore.Track.cs` 调用父包 `IAPStoreBase.Track*` 封装，覆盖初始化、用户发起购买、平台本地支付成功/失败、服务端验单失败/最终失败/成功，以及当前主动支付订单的首次验单失败。`nova_iap_local_pay_success` 的运行期打点去重按平台订单 key 执行：Apple 使用 `TransactionId`，Google 使用 `GoogleToken`；`nova_order_id` 优先使用 Unity IAP receipt 解析出的平台 `OrderId`，缺失时回退当前运行期 `TransactionId`。支付过程失败打点的 `nova_reason` 统一写入 `IAPMobileErrorCode` 的 int 值：本地支付失败使用 0-8 与 1000-1010 号段，验单失败使用 2000+ 细分号段，`nova_reason_detail` 记录网络错误、协议错误、订单状态或凭据缺失等可读描述。`nova_iap_validate_success` 覆盖 `Verified`、`Delivered`、`Reissued` 三类服务端终态，其 `nova_order_id` 优先使用服务端验单响应 `OrderId`，缺失时回退当前运行期 `TransactionId`。

所有 Mobile IAP 打点的渠道字段 `nova_channel`（TGA 侧对应 `solar_channel`）按编译平台区分：Android 上报 `google`，iOS 上报 `ios`，其他平台或非移动环境兜底 `mobile`。

Mobile 官方内购没有第三方订单创建和第三方收银台关闭流程，因此不触发 `nova_iap_create_order_success`、`nova_iap_create_order_fail`、`nova_iap_third_pay_close_order`。业务发奖不由 Mobile Store 执行，因此当前也不触发 `nova_iap_deliver_fail`。

## 11. IAP 4.x 过时概念

旧 `MobileStore-Design.md` 中的 `IStoreController`、`IExtensionProvider`、`UnityPurchasing.Initialize(listener, builder)`、`IDetailedStoreListener`、`ProcessPurchase` 等 IAP 4.x 概念均不是当前实现事实。当前以 Unity IAP 5.x `StoreController` 事件模型为准。

## 12. 相关文档

- [MobileStore.md](./MobileStore.md)
- [MobileInitService.md](./MobileInitService.md)
- [MobileUtils.md](./MobileUtils.md)
- 父包架构：[IAP-Architecture.md](../../../com.solotopia.nova.framework.sdk.iap/Nova/Doc/IAP-Architecture.md)
