# Mobile IAP 内部架构文档

> 包名：`com.solotopia.nova.framework.sdk.iap.mobile`
> 最后更新：2026-08-17
> 适用版本：Unity IAP 5.x（`UnityEngine.Purchasing`）

## 1. 整体架构

`MobileStore` 是 Google Play 与 iOS App Store 官方内购商店，通过 `[IAPStore]` 被父包 `IAPPlugin` 反射发现。商店自身只保留对外接口和生命周期入口，具体职责拆到 `MobileServiceHub` 管理的服务中。

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
- `MobileOrderKey`：`tableId + ReceiptParam` 未完成订单键工具。
- `MobileRestoreCoordinator`：Restore 双路计数器。
- `MobileRuntimeContext`：初始化连接态与失败态。
- `MobileProductFetchCoordinator`：`Services/Init` 内部商品拉取状态机，不是独立 service。
- `MobileProductFetchFailureSnapshot` / `MobileProductFetchState`：商品拉取失败回调快照与状态枚举，随 Init 内部状态机使用。
- `MobileValidationQueueCoordinator`：`Services/Validation` 内部验单队列协调器，负责订单键入队去重和队列单次执行保护。
- `MobileValidationLocalOrderScanner`：`Services/Validation` 内部本地订单扫描器，负责把存档订单按状态筛选为待验单订单键。
- `MobileReceiptParser`：Unity IAP Receipt 解析缓存。
- `MobileStoreParameterCodec`：UID + tableId + receiptParam 编码为平台透传 UUID。

上述 Init 内部辅助结构不拆到 `Services/ProductFetch` 目录；源码成员级 XML 注释、运行期日志与测试断言消息遵循全仓 OPS 中文口径，简单表达式体、短三元表达式和短属性 getter 在不牺牲可读性时保持一行，C# 文件头继续沿用仓库英文模板标签。

## 2. MobileServiceHub

`MobileServiceHub` 构造时写入共享依赖：

| 属性 | 说明 |
|---|---|
| `Context` | 父包注入的 `IIAPStoreContext` |
| `Config` | `MobileStoreConfig` |
| `Table` | 父包构建的 `IIAPProductTable` |
| `Store` | 所属 `MobileStore` |
| `RuntimeTaskToken` | 移动端官方内购商店运行期后台任务取消令牌，Dispose 时统一取消 |
| `RunBackgroundTask` | 统一启动不等待完成的后台任务，并捕获取消与异常日志；入口委托固定为 `Func<CancellationToken, UniTask>` |

服务引用由 `MobileStore.InitializeAsync` 按序创建并写入，运行期通过 Hub 访问兄弟服务，避免构造期循环依赖。

## 3. 服务职责

| 服务 | 访问性 | 核心职责 |
|---|---|---|
| `MobileIapNetService` | `public sealed` | 查询服务端未完成订单、发送 Google / Apple 普通与订阅批量验单 |
| `MobileExtendedService` | `internal sealed partial` | `StoreController` 唯一持有者，封装平台调用和事件注册 |
| `MobileStoreService` | `internal sealed partial` | Unity IAP `On*` 回调统一入口，只做路由 |
| `MobileInitService` | `internal sealed partial` | Unity IAP 初始化、连接状态、初始化结果上报、商品拉取协调器委托入口 |
| `MobileProductService` | `internal sealed partial` | Receipt 缓存、Product 查询、平台商品信息查询、权益状态辅助 |
| `MobileSubscriptionService` | `internal sealed partial` | 订阅到期时间持久化、订阅倒计时、到期后触发 FetchPurchases 与权益刷新 |
| `MobileValidationService` | `internal sealed partial` | 本地订单状态机、服务端查单、批量验单、发货结果派发；验单队列与本地订单扫描规则委托给内部协调类 |
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
6. `OnProductsFetched` 后清理旧失败 SKU，并按 Controller 状态恢复仍缺失的 pending SKU，再调用 `FetchPurchases` 拉取平台已有购买；启动期不调用平台 `RestoreTransactions`
7. `OnPurchasesFetched` 路由到 RestoreService，缓存历史票据并恢复 PendingOrder

商品拉取成功或失败不再决定初始化结果。网络慢时初始化只等待商店连接，不等待商品信息返回；`MobileInitService` 只保留初始化生命周期，商品拉取状态、重试、部分成功、迟到失败和不可用 SKU 校正收口在 `Services/Init` 内部 `MobileProductFetchCoordinator`。商品整体拉取失败后会按 `MobileStoreConfig.ProductFetchRetryDelaysMs` 自动重试，默认 2s / 5s / 10s 共 3 次；配置为空或包含非正数时回落默认值并打印中文警告日志。重试发起前会清理上一轮失败 SKU 缓存；任一轮收到 `OnProductsFetched`，或失败数量小于请求数量时，即认为至少有商品信息已可用并停止后续重试。完整成功回调会清理旧失败 SKU，并按 StoreController 当前状态恢复仍缺失的 pending SKU；失败回调先物化为内部 snapshot，再只把 StoreController 当前仍缺失的 SKU 标记为不可用。因此 14 个商品成功、1 个 SKU 找不到时，只会保留该缺失 SKU 的拦截，不会污染已成功商品。启动期平台已有购买拉取在商品信息首次进入成功态后异步触发，但启动期不会调用 `RestoreTransactions`，避免 iOS 在无用户交互时弹出 Apple ID 验证框；`RestoreTransactions` 仅由用户主动恢复购买入口调用。`FetchPurchases` 回调缓存 receipt / PendingOrder 后，如果账号已登录，会通过 `MobileServiceHub.RunBackgroundTask` 合并触发一次完整补单扫描，由统一补单入口串行执行 QueryPendingOrder / 本地验单 / 权益刷新。订阅倒计时到期会再次调用 `FetchPurchases` 刷新平台已有购买与票据缓存，再执行 `RefreshEntitlementsAsync`，不复用手动 `RestoreAsync`。

商品拉取与补单的边界如下：

- `MobileProductFetchCoordinator.CancelRetry` 可以在成功、部分成功、成功态迟到失败、初始化失败和 Dispose 路径被多次调用；它是幂等清理，不会清空业务订单或触发补单。
- `FetchPurchases` 在商品链路首次进入成功态时自动触发；成功态迟到失败会直接返回，不重复触发补单后续流程。订阅倒计时到期也会触发一次 `FetchPurchases` 来刷新平台已有购买与票据缓存。`RestoreTransactions` 不在启动期或订阅倒计时触发，只保留给用户主动恢复购买。
- `m_UnavailableSkus` 是 `HashSet<string>`，重复写同一缺失 SKU 不会无限增长；写入前仍会检查 `StoreController.GetProductById`，避免已成功商品被失败列表污染。
- 查询、购买和 Restore 权益刷新都会尊重不可用 SKU 拦截；真实缺失 SKU 会被阻断，已进入 Controller 的商品继续可买可查。
- 后台补单扫描、权益刷新、订阅倒计时和支付验单桥接都经 Hub 后台任务入口启动；Dispose 会先取消这些任务，再释放服务，避免释放后回调继续访问旧 Hub。支付验单桥接被取消时返回 `StoreNotAvailable` 失败结果，不向业务层抛取消异常。
- Hub 后台任务入口只接受 `Func<CancellationToken, UniTask>`。`RefreshEntitlementsAsync` 这类返回 `UniTask<IReadOnlyList<IAPResult>>` 的方法作为后台补跑动作接入时，必须使用 lambda 或无返回包装方法显式 `await` 并丢弃结果，不能直接以方法组传入 `RunBackgroundTask`。

## 5. 初始化失败原因

`MobileStoreInitFailureReason` 是移动端官方内购商店自定义初始化失败码，并通过 `IAPInitResult.FailReason` 以 int 透传。

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
| 用户主动恢复购买 | `RestoreTransactions` |
| 商品拉取 | `FetchProducts` |
| 平台已有购买拉取 | `FetchPurchases`；用于启动期和订阅倒计时刷新平台已有购买与票据缓存，不触发 Apple ID 验证框 |
| Android / iOS 透传账号 | `SetObfuscatedAccountId`、`SetObfuscatedProfileId`、`SetAppAccountToken` |

## 8. 持久化模型

`MobileStorePersistData` 通过基类 `LoadPersistData<T>` / `SavePersistData<T>` 以 `classify=iap_mobile`、`item=data_{uid}` 持久化。

| 字段 | 说明 |
|---|---|
| `OrderRecordsByKey` | 待处理订单记录，key = `tableId + ReceiptParam`；Android 不持久化 `TransactionId`，iOS 持久化 `TransactionId` 供补单验单使用 |
| `OrderRecords` | 旧版 tableId 字典迁移字段，仅用于读取旧存档后迁移到 `OrderRecordsByKey` |
| `SubscriptionExpireMs` | 订阅到期 Unix 毫秒，key = tableId |
| `NonConsumeOwnership` | 非消耗品持有标记，key = tableId |
| `HasQueriedPendingFromServer` | 当前 UID 是否曾成功向服务端同步过未完成订单；仅作兼容和诊断标记，不阻止后续 QueryPendingOrder |

UID 切换由 `MobileStore.SetUserId` 触发，重新加载整包存档。

补单扫描只能在登录后执行。登录前平台回调先到达时，只将 PendingOrder 解析出的待验订单暂存在内存中，不读写账号存档，也不发送 QueryPendingOrder / Verify 协议。登录后业务调用 `CheckLocalOrdersAsync` 时，流程先合并登录前暂存订单，再请求服务端 QueryPendingOrder，优先使用返回项里的 `table_id`（long）确定商品行，并结合 `parameter` 解码出的 `ReceiptParam` merge 到本地 `OrderRecordsByKey`，随后扫描本地待验订单；`parameter` 缺失或无法解出 `ReceiptParam` 时按空透传兼容旧协议。完整补单流程使用单次执行保护；扫描中再次触发只标记当前轮结束后补跑一轮，避免服务端查单、存档合并、验单队列和权益刷新并发交错。

Google 订单必须具备 purchase token 才会发送验单协议；本地 `Purchasing` 占位记录缺少 token 时保留等待下次平台回调或服务端 QueryPendingOrder 补齐。`OrderRecordsByKey` 是未完成订单仓库，不是订单历史；正常支付在验单和平台确认完成后会删除记录。Restore / 权益刷新准备订单时会用最新 receipt 回填已有记录缺失的 token / orderId，避免 CheckEntitlement 早于 FetchPurchases 到达时把空凭据固化到本地记录。iOS Apple 验单协议必须具备 `order_id`（本地 `TransactionId`），缺失时不能发送空订单验单请求，客户端会删除本地待验订单记录并落盘，避免后续启动重复发送无效协议。验单请求中的 `price` 固定来自支付表 `IAPProductEntry.Price`，不使用 Unity IAP 平台本地化价格，避免 Storefront / 账号地区导致客户端验单金额漂移。`TransactionId` 承载平台订单 ID：Android 运行期可写入 Google `OrderId` 供结果和打点回填，但不写入本地存档；iOS 写入 Apple transaction id 并随本地存档保留。它不作为本地存档合并、验单响应匹配或 PaySuccess 去重判断。每次登录后的补单扫描结束后，还会触发一次 `CheckEntitlement` 权益刷新，刷新订阅和非消耗品权益，确保订阅状态不是只依赖倒计时触发；该刷新不重复触发平台 `RestoreTransactions`。订阅倒计时到期会先 `FetchPurchases` 刷新平台已有购买与票据缓存，再执行权益刷新，也不会调用 `RestoreTransactions`。Unity IAP 的 `FullyEntitled` 只说明平台侧仍返回持有记录；订阅权益回调会从 `Entitlement.Order.Info.PurchasedProductInfo[*]` 中筛选与当前 `Entitlement.Product` 匹配的条目，读取匹配项 `subscriptionInfo.GetExpireDate()` 的最晚到期时间。当当前商品到期时间明确已过期时，本次状态按 `NotEntitled` 缓存并跳过 Restore 验单；读取不到当前商品匹配的到期时间时仍交由服务端确认。如果商品信息尚未拉取成功，权益刷新会延后，商品成功回调后自动补跑，避免把“平台商品未进入 StoreController”误判为“没有待查询项”。

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

上表 `Verified` / `Reissued` / `Delivered` 的「删除订单」经 `FinalizeVerifiedOrderRecord` 收尾：验单成功即刻完成业务发货与订阅到期更新；若该订单仍持有平台 `PendingOrder`，先把本地记录置为 `AwaitingConfirm`（本地状态 4）并落盘，再发起 `ConfirmPurchase`，待平台 ack（`OnPurchaseConfirmed → ConfirmedOrder`）到达后由 `TryCompleteAwaitingConfirm` 删除记录；ack 失败（`FailedOrder`）则保留 `AwaitingConfirm` 记录，等待下次 `FetchPurchases` 重新拉取到 `PendingOrder`，经 `TryReconfirmAwaitingOrder` 直接重试确认、跳过重复验单。无待确认平台订单（token 补单 / 历史单）时验单成功后直接删除。启动本地扫描跳过 `AwaitingConfirm`，不重发服务端验单。

`PaySuccess` 只表达业务需要感知的本地发奖成功。普通商品与补单商品在 `CanDeliver=true` 且当前运行期未派发过同一 `tableId + ReceiptParam` 订单键时触发；订阅商品只有当前主动 `PayAsync` 对应的订单才触发，后台补单、Restore 和订阅刷新不走全局 `PaySuccess`。订阅 Restore 通知通过 `SubscriptionRestored` 表达：订阅订单服务端返回 `Verified` 或 `Delivered` 时会收集到 `SubscriptionRestored` 结果列表；`Reissued` 仅更新本地终态和完成 Restore 计数，不进入 `SubscriptionRestored`。如果服务端 QueryPendingOrder 先完成，随后 Unity IAP 又回调同一笔 PendingOrder，客户端按 `tableId + ReceiptParam` 合并本地订单并继续走验单终态处理，避免把平台交易号作为业务判断依据。当前服务端验单响应主要按 tableId 匹配；当同一批次存在重复 tableId 时，客户端会拆成单笔验单，避免同 SKU 多 ReceiptParam 订单响应错配。

## 10. 埋点边界

移动端官方内购商店通过 `MobileStore.Track.cs` 调用父包 `IAPStoreBase.Track*` 封装，覆盖初始化、用户发起购买、平台本地支付成功/失败、服务端验单失败/最终失败/成功，以及当前主动支付订单的首次验单失败。`nova_iap_local_pay_success` 的运行期打点去重按平台订单 key 执行：Apple 使用 `TransactionId`，Google 使用 `GoogleToken`；`nova_order_id` 优先使用 Unity IAP receipt 解析出的平台 `OrderId`，缺失时回退当前运行期 `TransactionId`。支付过程失败打点的 `nova_reason` 统一写入 `IAPMobileErrorCode` 的 int 值：本地支付失败使用 0-9 与 1000-1010 号段，验单失败使用 2000+ 细分号段，`nova_reason_detail` 记录网络错误、协议错误、订单状态或凭据缺失等可读描述。`MobileStore.PayAsync` 返回失败 `IAPResult` 时会在返回边界上报 `nova_iap_local_pay_fail`；Unity IAP `OnPurchaseFailed` 与 `OnPurchaseConfirmed(FailedOrder)` 也会直接上报本地支付失败点。失败打点不做运行期去重，同一次支付链路如果同时出现官方失败回调和 PayAsync 失败返回，两条失败点都会保留。`nova_iap_validate_success` 覆盖 `Verified`、`Delivered`、`Reissued` 三类服务端终态，其 `nova_order_id` 优先使用服务端验单响应 `OrderId`，缺失时回退当前运行期 `TransactionId`。

所有 Mobile IAP 打点的渠道字段 `nova_channel`（TGA 侧对应 `solar_channel`）按编译平台区分：Android 上报 `google`，iOS 上报 `ios`，其他平台或非移动环境兜底 `mobile`。

移动端官方内购没有第三方订单创建和第三方收银台关闭流程，因此不触发 `nova_iap_create_order_success`、`nova_iap_create_order_fail`、`nova_iap_third_pay_close_order`。业务发奖不由移动端官方内购商店执行，因此当前也不触发 `nova_iap_deliver_fail`。

## 11. IAP 4.x 过时概念

`IStoreController`、`IExtensionProvider`、`UnityPurchasing.Initialize(listener, builder)`、`IDetailedStoreListener`、`ProcessPurchase` 等 IAP 4.x 概念均不是当前实现事实。当前以 Unity IAP 5.x `StoreController` 事件模型为准。

## 12. 相关文档

- [MobileStore.md](./MobileStore.md)
- [MobileInitService.md](./MobileInitService.md)
- [MobileUtils.md](./MobileUtils.md)
- 父包架构：[IAP-Architecture.md](../../../com.solotopia.nova.framework.sdk.iap/Nova/Doc/IAP-Architecture.md)
