# MobileStore

> 最后更新：2026-09-21
> 当前代码事实：`UPMPackages/com.solotopia.nova.framework.sdk.iap.mobile/Nova/Scripts/Runtime/**`

**类签名**：`public sealed partial class MobileStore : IAPStoreBase, IIAPMobileQueryCapable, IIAPMobileSubscriptionCapable`
**命名空间**：`NovaFramework.SDK.IAP.Mobile.Runtime`
**发现方式**：通过 `[IAPStore]` 由父包 `IAPPlugin` 反射发现并初始化。业务侧通常通过 `IAPPlugin.PayAsync<T>` 和 `TryGetCapability<T>` 使用。

`MobileStore` 对接 Unity IAP 5.x，覆盖 Google Play 与 iOS App Store 官方内购的购买、Restore、服务端验单、补单、订阅到期和非消耗品持有状态。

## 1. 文件表

| 文件 | 类型 | 说明 |
|---|---|---|
| `MobileStore.cs` | `MobileStore` | Store 对外入口、生命周期和 public/override API |
| `MobileStore.Visitors.cs` | `MobileStore` partial | 字段、状态属性和运行期缓存 |
| `MobileStore.Methods.cs` | `MobileStore` partial | internal/protected/private 辅助方法；包括 PayAsync 返回失败打点映射 |
| `MobileStore.Track.cs` | `MobileStore` partial | Mobile 打点转发入口 |
| `MobileStoreConfig.cs` | `MobileStoreConfig` | 移动端官方内购商店专属配置 |
| `IAPMobileRequest.cs` | `IAPMobileRequest` | Mobile 支付请求 |
| `IAPMobileErrorCode.cs` | `IAPMobileErrorCode` | Mobile 支付过程失败原因与错误码 |
| `IIAPMobileQueryCapable.cs` | `IIAPMobileQueryCapable` | 商品信息查询能力 |
| `IIAPMobileSubscriptionCapable.cs` | `IIAPMobileSubscriptionCapable` | 订阅和非消耗品查询能力 |
| `Data/*.cs` | `MobileStorePersistData`、`MobileOrderRecord`、`MobileOrderKey`、`MobileOrderStatus`、`MobileCheckEntitlementInfo` | 存档、订单键、订单状态、权益检查数据 |
| `Services/MobileServiceHub.cs` | `MobileServiceHub` | 服务聚合容器 |
| `Services/Net/MobileIapNetService.cs` | `MobileIapNetService` | 查单和验单协议 |
| `Services/Extended/MobileExtendedService.cs` | `MobileExtendedService` | `StoreController` 收口 |
| `Services/Store/MobileStoreService.cs` | `MobileStoreService` | Unity IAP 回调路由 |
| `Services/Init/*.cs` | `MobileInitService`、`MobileRuntimeContext` | 初始化状态机 |
| `Services/Purchase/*.cs` | `MobilePurchaseService` | 平台购买和购买回调 |
| `Services/Validation/*.cs` | `MobileValidationService` | 订单状态机与验单队列 |
| `Services/Validation/MobileValidationQueueCoordinator.cs` | `MobileValidationQueueCoordinator` | 验单订单键队列去重与单次执行保护 |
| `Services/Validation/MobileValidationLocalOrderScanner.cs` | `MobileValidationLocalOrderScanner` | 本地订单状态扫描与待验订单键收集 |
| `Services/Restore/*.cs` | `MobileRestoreService`、`MobileRestoreCoordinator` | Restore 流程 |
| `Services/Product/*.cs` | `MobileProductService` | Product / Receipt 查询缓存 |
| `Services/Subscription/*.cs` | `MobileSubscriptionService` | 订阅到期和倒计时 |
| `Utils/*.cs` | `MobileReceiptParser`、`MobileStoreParameterCodec` | 票据解析和透传编码 |

## 2. 配置

`MobileStoreConfig` 通过父包 `IAPPluginConfig.StoreConfigs` 的 `[SerializeReference]` 多态列表配置。

| 属性 | 默认值 | 说明 |
|---|---|---|
| `Enabled` | `true` | 是否启用移动端官方内购商店 |
| `GoogleQueryPendingOrderCmdName` | `IAPGoogleQueryPendingOrder` | Google 查询未完成订单 Cmd |
| `GoogleVerifyCmdName` | `IAPGoogleVerify` | Google 普通内购验单 Cmd |
| `GoogleVerifySubscriptionCmdName` | `IAPGoogleVerifySubscription` | Google 订阅验单 Cmd |
| `AppleQueryPendingOrderCmdName` | `IAPAppleQueryPendingOrder` | Apple 查询未完成订单 Cmd |
| `AppleVerifyCmdName` | `IAPAppleVerify` | Apple 普通内购验单 Cmd |
| `AppleVerifySubscriptionCmdName` | `IAPAppleVerifySubscription` | Apple 订阅验单 Cmd |
| `ProductFetchRetryDelaysMs` | `2000 / 5000 / 10000` | 商品拉取整体失败后的自动重试延迟，单位毫秒；空列表或非正数回落默认值 |

## 3. MobileStore 关键属性

| 属性 / 字段 | 说明 |
|---|---|
| `StoreType` | 固定 `IAPStoreType.Mobile` |
| `TrackChannel` | Android = `google`，iOS = `ios`，其他平台兜底 `mobile` |
| `LogTag` | 固定 `LogTag.IAPMobile` |
| `GameUID` | 当前用户 UID，供内部服务读取 |
| `InPayTableId` | 当前支付中的 tableId；0 表示空闲 |
| `IsStoreReady` | `InitService.IsReady && ExtendedService.IsAttached` |
| `m_RuntimeHandledTransactionIds` | 当前运行期本地支付成功打点去重缓存；Apple 使用 `TransactionId`，Google 使用 `GoogleToken` |
| `m_Hub` | `MobileServiceHub` 服务聚合容器 |
| `m_PersistData` / `PersistData` | 当前 UID 的统一存档容器 |

## 4. 公开 API

```csharp
public override IAPStoreType StoreType => IAPStoreType.Mobile
public override bool CanHandle(IAPRequest request)
public override UniTask InitializeAsync(IIAPProductTable table, IIAPStoreConfig config, IIAPStoreContext ctx, CancellationToken ct)
public override UniTask<IAPResult> PayAsync(IAPRequest request, CancellationToken ct)
public override UniTask<IReadOnlyList<IAPResult>> RestorePurchasesAsync(CancellationToken ct)
public override UniTask CheckLocalOrdersAsync(CancellationToken ct)
public override UniTask DisposeAsync(CancellationToken ct)
public override void SetUserId(string uid)

public void SetSubscriptionReplaceMode(int replaceMode)
public UniTask<IReadOnlyList<ProductInfo>> QueryProductsAsync(IReadOnlyList<string> productIds, CancellationToken ct)
public long GetSubscriptionExpireTime(long tableId)
public bool HasNonConsumeProduct(long tableId)
public ProductInfo GetProductInfo(long tableId)
```

能力接口：

```csharp
public interface IIAPMobileQueryCapable : IIAPCapable
{
    UniTask<IReadOnlyList<ProductInfo>> QueryProductsAsync(IReadOnlyList<string> productIds, CancellationToken ct);
    ProductInfo GetProductInfo(long tableId);
}

public interface IIAPMobileSubscriptionCapable : IIAPCapable
{
    long GetSubscriptionExpireTime(long tableId);
    bool HasNonConsumeProduct(long tableId);
    bool InSubscriptionPeriod(long tableId);
}
```

## 5. 初始化流程

```
MobileStore.InitializeAsync(table, config, ctx, ct)
  ├── base.InitializeAsync(table, config, ctx, ct)
  ├── config as MobileStoreConfig，否则 new MobileStoreConfig()
  ├── 创建 MobileServiceHub
  ├── 创建 Pay / Extended / Store / Init / Product / Subscription / Validation / Restore / Purchase 服务
  ├── m_PersistData = CreateEmptyPersistData()
  ├── RunBackgroundTask(runtimeCt => InitService.InitializeAsync(table, runtimeCt))
  └── 返回，游戏主 Loading 继续
        └── 后台连接与初始化
              ├── 商店连接成功即 Ready
              ├── 商品信息后台 FetchProducts；失败后按 ProductFetchRetryDelaysMs 自动重试，默认 2s/5s/10s，只标记 Controller 缺失 SKU 为不可用
              └── 商品拉取成功后自动 FetchPurchases，恢复平台 PendingOrder 票据；若已登录则合并触发一次补单扫描；RestoreTransactions 仅由用户主动恢复购买触发
```

`MobileStore.InitializeAsync` 不等待 Unity IAP 商店连接，因此不会因平台连接长期无回调而卡住游戏 Loading。连接成功前以及初始化失败后，支付都会被基类 `PayGuardAsync` 的 `IsStoreReady` 检查拦截；初始化失败时 `MobileInitService` 通过 `IAPInitResult.Fail((int)MobileStoreInitFailureReason, detail)` 上报。初始化成功和失败复用父包的通用初始化事件入口上报 `nova_iap_init`。

### 5.1 商品拉取与不可用 SKU

商品拉取是商店连接成功后的后台流程，不改变初始化结果：

| 场景 | 当前处理 |
|---|---|
| 商店连接成功 | `OnStoreConnected` 标记 Ready，随后调用 `FetchProducts` |
| 正在拉取或已成功 | `FetchProducts` 直接跳过，避免 Unity IAP 并发商品请求 |
| 整体失败 | 标记 `ProductFetchState=Failed`，按 `ProductFetchRetryDelaysMs` 自动重试；默认 `2s / 5s / 10s` 共 3 次 |
| 成功回调 | 取消重试、清理旧失败 SKU、按 Controller 状态恢复仍缺失的 pending SKU、置 `Succeeded`，再触发 FetchPurchases 和延迟权益刷新 |
| 失败数量小于请求数量 | 视为至少有商品成功，置 `Succeeded` 并停止重试；仍缺失 SKU 继续保留拦截 |
| 成功态迟到失败 | 只补记录 Controller 当前仍缺失的 SKU，不回退 `Failed`，不重试，不重复触发后续流程 |

`m_UnavailableSkus` 只应表达“当前 `StoreController` 查不到”的 SKU。购买和商品查询会通过该集合拦截不可用商品；因此失败回调不能无条件写入失败列表，否则 Unity IAP 的迟到失败会把已成功商品误标记为不可买。成功回调清理旧失败 SKU 后会立即根据当前 Controller 重建仍缺失 SKU，覆盖“14 个商品成功、1 个 SKU 找不到”的部分成功场景。

## 6. 支付流程

```
IAPPlugin.PayAsync<IAPResult>(IAPMobileRequest)
  └── MobileStore.PayAsync
        ├── ct.ThrowIfCancellationRequested()
        ├── m_LoadingGuard.HasUserInteracted = true
        ├── UNITY_EDITOR && Context.EnableAlwaysPaySucceed → 返回 MOCK_ORDER_MOBILE
        └── PayGuardAsync(request, ct, PurchaseService.PayAsync)
              ├── store 禁用 / 未就绪 / 已有支付 / 商品表缺失 → 失败
              └── MobilePurchaseService.PayAsync
                    ├── 当前 `tableId + ReceiptParam` 订单键已有待验订单 → 优先补验
                    ├── 订阅商品自身仍在有效期内 → 返回 SubscriptionIsReady，不发起平台购买
                    ├── 写 Purchasing 占位订单
                    ├── 发起平台购买
                    ├── 平台回调后写 PendingValidate 并入验单队列
                    ├── MobileValidationService 批量验单后派发结果
                    └── 验单通过且持有平台 PendingOrder → 置 AwaitingConfirm + ConfirmPurchase，ack 回调后删除记录
```

`MobileStore.PayAsync` 只要拿到失败 `IAPResult`，都会在返回给业务层前调用 `TrackReturnedPayFailureInternal` 上报一次 `nova_iap_local_pay_fail`。这包括 `PayGuardAsync` 产生的 Store 禁用、未就绪、重入和商品表缺失，也包括 `MobilePurchaseService` 返回的商品未获取、商品不可用、透传参数非法、平台购买发起异常、主动支付验单失败等结果。Mobile 覆写 `ShouldTrackPayGuardFailure` 返回 `false`，避免 guard 内直接按父包枚举域上报；最终由 PayAsync 返回边界映射到 `IAPMobileErrorCode`。

## 6.1 补单扫描流程

```
IAPPlugin.CheckLocalOrdersAsync
  └── MobileStore.CheckLocalOrdersAsync
        └── MobileValidationService.CheckLocalOrdersAsync
              ├── 未登录时直接跳过，不发起 QueryPendingOrder / Verify 协议
              ├── 启动期 OnProductsFetched 会自动 FetchPurchases 恢复平台 PendingOrder，不调用 RestoreTransactions
              ├── 登录前收到的 PendingOrder 只暂存在内存，登录后先合并到当前 UID 存档
              ├── 每次扫描都向服务端发送 QueryPendingOrder
              ├── 优先按服务端返回的 `table_id`（long）确定商品行，并结合 `parameter` 解码出的 `ReceiptParam` merge 到本地 `OrderRecordsByKey`
              ├── 服务端返回的 Google token 可补齐本地 Purchasing 占位记录
              ├── `MobileValidationLocalOrderScanner` 本地扫描 PendingValidate / ValidateFailed / 具备凭据的 Purchasing
              ├── `MobileValidationQueueCoordinator` 统一处理订单键入队去重和队列单次执行保护
              ├── AwaitingConfirm 跳过，不重发验单；交由本次 FetchPurchases 重新拉取到 PendingOrder 后重试确认
              ├── Google 订单缺少 purchase token 时保留记录，不发送 VerifyGoogleIap
              └── 本地补单扫描结束后触发一次 CheckEntitlement；商店、商品或平台已有购买未就绪时登记延后请求，在票据缓存完成后补跑
```

登录前平台回调可能先于业务 `SetUserId` 到达。此时 Mobile 只收集待验单数据，不读写账号存档，也不发起服务端协议；商品拉取后只自动 `FetchPurchases` 恢复平台已有购买，不调用 `RestoreTransactions`。业务登录后调用 `CheckLocalOrdersAsync`，会按“合并暂存订单 → 拉取服务端未完成订单 → 本地验单 → 权益查询”的顺序执行。若登录补单早于商店连接、商品或平台已有购买拉取完成，服务端查单和具备凭据的本地订单仍立即处理，权益刷新登记为延后请求。`FetchPurchases` 成功回调先缓存 ConfirmedOrder receipt 并处理 PendingOrder，再补跑延后权益刷新；因此不会先生成缺少 Google token / Apple order id 的 Restore 占位记录，也不会提前派发空恢复事件。失败回调优先消费已登记的权益刷新；没有延后请求时再执行服务端、本地和权益兜底。完整补单流程串行执行；订阅倒计时也先经 RestoreService 发起已有购买拉取，票据缓存完成后再刷新权益。

`CheckEntitlement` 的 `FullyEntitled` 只代表平台侧仍返回持有记录，不直接等价于订阅仍有效。权益回调会先缓存 `Entitlement.Order.Info.Receipt`，为 Google token 或 Apple order id 提供就地兜底；随后订阅流程从 `PurchasedProductInfo[*]` 中筛选当前商品并读取最晚到期时间。明确过期时按 `NotEntitled` 处理；读不到匹配到期时间时保留平台状态并交由服务端验单。非消耗品不受订阅到期过滤影响。

## 7. 订单状态机

### 本地订单状态

| 值 | 名称 | 含义 |
|---|---|---|
| 0 | `Purchasing` | 已发起平台购买，等待平台回调；Google 订单未取得 purchase token 前不会进入验单 |
| 1 | `PendingValidate` | 平台回调成功，待服务端验单 |
| 2 | `ValidateFailed` | 验单网络或 HTTP 失败，保留记录等待下次补单 |
| 3 | `LocalPayFailed` | 平台本地支付失败，启动扫描时直接删除 |
| 4 | `AwaitingConfirm` | 服务端验单已通过、业务已发货，等待平台 `ConfirmPurchase` 的 ack 回调；收到 `OnPurchaseConfirmed(ConfirmedOrder)` 后删除，ack 失败则保留，等待下次 `FetchPurchases` 重新拉取到 `PendingOrder` 后重试确认（不重发服务端验单）。仅对持有平台 `PendingOrder` 的订单进入该状态；token 补单 / 历史单无 ack 可等，验单成功后直接删除 |

### 服务端验单状态

| 值 | 名称 | 客户端处理 |
|---|---|---|
| 1 | `PendingVerify` | 客户端还未发过校验协议；保留订单等待重试 |
| 2 | `Verified` | 校验完毕；删除记录并派发可发货成功 |
| 3 | `Reissued` | 奖励已通过其他渠道补发；删除记录并派发成功，`CanDeliver=false` |
| 4 | `Delivered` | 服务端已处理过订单；删除记录并派发成功，`CanDeliver=true`，客户端仍按本地幂等规则补发奖 |
| 5 | `Invalid` | 无效订单；删除记录并派发失败 |

上表「删除记录」经 `FinalizeVerifiedOrderRecord` 收尾：若该订单仍持有平台 `PendingOrder`，先置 `AwaitingConfirm` 并落盘、发起 `ConfirmPurchase`，待 ack 回调（`OnPurchaseConfirmed`）到达后再删除；无待确认平台订单时才立即删除。业务发货（`PaySuccess` / 订阅到期更新）在验单成功即刻完成，不等待平台 ack。

`PaySuccess` 派发按 `tableId + ReceiptParam` 组成的订单键做运行期去重，不使用平台 `TransactionId` 作为业务判断依据。`MobileStore` 仍维护当前运行期平台订单打点 key 缓存，但只用于平台 Pending / Confirmed 双回调的本地支付成功打点去重：Apple 使用 `TransactionId`，Google 使用 `GoogleToken`。验单成功打点另按当前 UID 持久化平台订单号去重：Apple 使用 transaction id，Google 优先使用服务端验单响应 `OrderId`、缺失时回退当前运行期 Google `OrderId`；持久化列表和运行期兜底缓存最多各保留 300 条，新增超限时淘汰最老记录。旧版 `google:{purchaseToken}` 会在同一订单再次验单成功时迁移为订单号键并跳过重复打点。订阅商品只有当前主动 `PayAsync` 对应的订单才走 `PaySuccess`；后台补单、Restore 和订阅刷新只更新订阅到期时间。

### 埋点事件

Mobile 在 `MobileStore.Track.cs` 中构造订单号、验单状态和失败原因等渠道字段，并复用父包的通用属性构造与发送能力，当前接入事件如下：

| 事件 | 触发时机 |
|---|---|
| `nova_iap_init` | 商店连接成功或初始化失败 |
| `nova_iap_buy` | 用户发起真实平台购买前；Editor 下 `EnableAlwaysPaySucceed` 调试支付也会上报 |
| `nova_iap_local_pay_success` | Unity IAP 返回 Pending / Confirmed 并登记本地订单后；同一订单号在当前运行期去重 |
| `nova_iap_local_pay_fail` | `MobileStore.PayAsync` 返回失败 `IAPResult`；没有 `PayAsync` 返回链路的迟到 `OnPurchaseFailed`；`OnPurchaseConfirmed(FailedOrder)` |
| `nova_iap_validate_fail` | 单轮验单失败但订单仍可能重试或补单 |
| `nova_iap_first_pay_order_validate` | 当前主动支付订单第一次验单失败 |
| `nova_iap_validate_fail_finish` | 验单最终失败、无效订单或超出重试后进入 `ValidateFailed` |
| `nova_iap_validate_success` | 服务端返回 `Verified`、`Delivered` 或 `Reissued` 并终结订单；按当前 UID 的平台订单键持久化去重，最多保留 300 条 |

`nova_iap_create_order_success`、`nova_iap_create_order_fail`、`nova_iap_third_pay_close_order` 是第三方支付链路事件，移动端官方内购不触发。`nova_iap_deliver_fail` 目前不触发，因为业务发奖不由移动端官方内购商店执行。

`OnPurchaseFailed` 会把 Unity IAP `PurchaseFailureReason` 映射到 `IAPMobileErrorCode` 的 1000-1010 号段，并把 `FailedOrder.Details` 写入返回结果。当前存在活跃 `PayAsync` 时，回调只完成失败结果，由 `MobileStore.PayAsync` 返回边界统一上报一次；只有没有返回链路但仍关联有效本地订单的迟到回调才直接兜底上报。`OnPurchaseConfirmed(FailedOrder)` 属于独立的平台确认失败通知，仍直接记录对应失败详情。

关键字段口径：

| 字段 | 口径 |
|---|---|
| `nova_iap_local_pay_success.nova_order_id` | 优先使用 Unity IAP receipt 解析出的平台 `OrderId`；缺失时回退 Apple `TransactionId` |
| `nova_iap_validate_success.nova_order_id` | 优先使用服务端验单响应 `OrderId`；缺失时回退当前运行期 `TransactionId` |
| `nova_iap_validate_success` 去重 key | Apple 使用 transaction id；Google 使用该事件的 `nova_order_id`，即优先取服务端验单响应 `OrderId`、缺失时回退当前运行期 Google `OrderId` |
| `nova_iap_local_pay_fail.nova_reason` | `IAPMobileErrorCode` 的 int 值；`PluginRouter` guard 失败会映射到对应 Mobile 错误码，Unity IAP `PurchaseFailureReason` 映射到 1000-1010 |
| `nova_iap_validate_fail(.finish).nova_reason` | `IAPMobileErrorCode` 的 int 值；验单网络、响应缺失、待完成、凭据缺失和无效订单使用 2000+ 号段 |
| `nova_reason_detail` | 失败原因的可读补充描述，例如协议错误信息、服务端状态或缺失凭据说明；从 `PluginRouter` 映射而来的失败会保留原始 `ErrorSource:ErrorCode` |
| `Debug` | 来自父包注入的 `IIAPStoreContext.DevelopMode == DevelopMode.Debug`；不再取 `EnableAlwaysPaySucceed` |
| 本地业务去重 | `PaySuccess` 按 `tableId + ReceiptParam` 订单键去重，不按平台订单号或 purchase token 去重 |

## 8. 存档

`MobileStorePersistData` 是当前 UID 的统一存档容器。

`OrderRecordsByKey` 只保存未完成订单，不是订单历史。正常支付在平台回调、服务端验单和平台确认链路全部完成后会删除记录；只有支付中断、网络验单失败、缺少凭据、平台确认失败或服务端未完成订单补回时，记录才会跨启动保留用于补单。

| 字段 | 说明 |
|---|---|
| `OrderRecordsByKey` | `Dictionary<string, MobileOrderRecord>`，key = `tableId + ReceiptParam` 订单键；Android 不持久化 `TransactionId`，iOS 持久化 `TransactionId` |
| `OrderRecords` | 旧版 `Dictionary<long, MobileOrderRecord>` 迁移字段，仅用于把旧存档迁移到 `OrderRecordsByKey`，新写入不再使用 |
| `SubscriptionExpireMs` | 订阅到期 Unix 毫秒 |
| `NonConsumeOwnership` | 非消耗品持有标记 |
| `ValidateSuccessOrderKeys` | 当前 UID 已上报验单成功的平台订单号键；Apple 使用 transaction id，Google 使用订单号；最多 300 条，新增超限时淘汰最老记录；命中旧版 Google token 键时按当前订单迁移 |
| `HasQueriedPendingFromServer` | 当前 UID 是否曾成功向服务端同步过未完成订单；不用于阻止后续 QueryPendingOrder |

`MobileOrderRecord` 字段：

| 字段 | 说明 |
|---|---|
| `TransactionId` | 平台订单 ID；Android 运行期可写入 Google `OrderId` 但不落本地存档，iOS 写入 Apple transaction id 并持久化供补单验单使用 |
| `TableId` | 商品配置表行 ID |
| `GoogleToken` | Google Play purchase token；iOS 为空；用于 Google 验单与本地支付成功打点的运行期去重，不再用于验单成功打点去重 |
| `Status` | 当前订单状态 |
| `IsReplenish` | 是否为补单路径 |
| `CustomDataParam` | 业务透传字符串 |

## 9. 错误码

`IAPMobileErrorCode` 是 Mobile 支付过程统一失败原因。0-9 可通过 `IAPResult.ErrorCode` 以 int 返回给业务层；`MobileStore.Track.cs` 将枚举的 int 值写入失败打点：

| 值 | 名称 | 含义 |
|---|---|---|
| 0 | `None` | 无错误 |
| 1 | `ProductNotFound` | 配置表或平台商品中未找到目标商品 |
| 2 | `SubscriptionIsReady` | 当前订阅商品已处于有效期，或同订阅组已有有效订阅且当前平台不走升降级 |
| 3 | `UserCancelled` | 用户取消支付 |
| 4 | `StoreNotAvailable` | 平台商店不可用或无法发起支付 |
| 5 | `AlreadyPurchasing` | 当前已有支付或验单流程 |
| 6 | `NetworkError` | 网络不可用或请求失败 |
| 7 | `ServerValidationFailed` | 服务端验单失败或拒绝订单 |
| 8 | `StoreInitFailed` | MobileStore 初始化失败 |
| 9 | `InvalidPassthroughParam` | tableId、ReceiptParam 或 uid 超出长度限制，或 uid / ReceiptParam 不是不以 `0` 开头的十六进制值 |
| 1000 | `PurchaseFailurePurchasingUnavailable` | Unity IAP 当前不可购买 |
| 1001 | `PurchaseFailureExistingPurchasePending` | Unity IAP 已有待处理购买 |
| 1002 | `PurchaseFailureProductUnavailable` | Unity IAP 平台商品不可用 |
| 1003 | `PurchaseFailureSignatureInvalid` | Unity IAP 签名校验失败 |
| 1004 | `PurchaseFailureUserCancelled` | Unity IAP 用户取消购买 |
| 1005 | `PurchaseFailurePaymentDeclined` | Unity IAP 支付被拒绝 |
| 1006 | `PurchaseFailureDuplicateTransaction` | Unity IAP 重复交易 |
| 1007 | `PurchaseFailureValidationFailure` | Unity IAP 交易校验失败 |
| 1008 | `PurchaseFailureStoreNotConnected` | Unity IAP 商店未连接 |
| 1009 | `PurchaseFailurePurchaseMissing` | Unity IAP 平台未返回购买数据 |
| 1010 | `PurchaseFailureUnknown` | Unity IAP 未知购买失败 |

0-9 是移动端官方内购商店自身流程错误；1000-1010 是 Unity IAP `PurchaseFailureReason` 的专用映射号段；2000+ 是验单失败打点细分号段。`MobileStore.PayAsync` 返回失败时会统一补齐 `nova_iap_local_pay_fail`，并把 `PluginRouter` 层 guard 失败映射到 `IAPMobileErrorCode` 后写入 `nova_reason`；原始 `ErrorSource:ErrorCode` 保留在 `nova_reason_detail`。活跃支付的 Unity IAP 失败回调不直接上报，由返回边界保证一次失败只产生一条事件；没有返回链路的迟到回调保留一次兜底上报。`TrackLocalPayFailInternal`、`TrackReturnedPayFailureInternal`、`TrackValidateFailInternal` 和 `TrackValidateFailFinishInternal` 都只接收 `IAPMobileErrorCode`，确保支付过程 `nova_reason` 的枚举域统一。

| 值 | 名称 | Mobile 使用场景 |
|---|---|---|
| 2000 | `ValidateNetworkUnavailable` | 验单前网络不可用 |
| 2001 | `ValidateNetworkRequestFailed` | 验单请求异常、HTTP 失败或重试耗尽 |
| 2002 | `ValidateResponseMissing` | 服务端响应未找到对应订单 |
| 2003 | `ValidatePending` | 服务端返回 `PendingVerify` / `Unspecified` 等未完成状态 |
| 2004 | `ValidateCredentialMissing` | Google 订单缺少 purchase token |
| 2005 | `ValidateInvalid` | 服务端判定订单无效 |
| 2999 | `ValidateUnknown` | 未知验单失败 |

## 10. 当前代码口径快照

后台任务生命周期：

| 任务 | 当前口径 |
|---|---|
| Unity IAP 商店连接 | 经 `MobileServiceHub.RunBackgroundTask` 启动，不阻塞游戏主 Loading；连接等待接入 Store 运行期取消令牌，连接成功前支付由 `IsStoreReady` 拦截 |
| 验单队列处理 | 经 `MobileServiceHub.RunBackgroundTask` 启动，接入移动端官方内购商店运行期取消令牌 |
| 商品成功后的权益刷新 | 先标记并发起 `FetchPurchases`；已有购买回调前只登记延后请求，成功缓存票据或失败收口后再经 `RunBackgroundTask` 补跑 |
| 平台已有购买后的补单扫描 | 成功回调先缓存平台票据和 PendingOrder，再补跑延后权益或进入统一补单；失败回调优先补跑延后权益，没有延后请求时执行完整补单兜底 |
| 订阅到期倒计时 | 自身 CTS 与 Hub 运行期取消令牌链接，Dispose 时统一取消；到期后经 RestoreService 发起 `FetchPurchases`，回调收口后继续权益刷新，不进入 `RestoreTransactions` |
| 支付验单结果桥接 | 经 `RunBackgroundTask` 等待验单 TCS；Dispose 时取消等待，并以 `StoreNotAvailable` 失败结果解除支付 await，不向业务抛取消异常 |

后台任务入口只接受 `Func<CancellationToken, UniTask>`。如果业务动作本身返回 `UniTask<T>`，不能直接作为方法组传入 `RunBackgroundTask`；当前商品成功后的权益刷新使用 `async token => { await RefreshEntitlementsAsync(token); }` 显式等待并丢弃返回列表，确保后台生命周期只承载取消与异常收口，不改变 Restore / 补单结果语义。

当前 Mobile 支付代码按两条失败原因线维护：

| 线 | 枚举 | 用途 |
|---|---|---|
| 初始化失败 | `MobileStoreInitFailureReason` | 初始化阶段失败分类，写入 `IAPInitResult.FailReason` 和 `nova_iap_init.nova_init_failure_reason` |
| 支付过程失败 | `IAPMobileErrorCode` | 支付、平台本地支付失败、验单失败分类，写入 `IAPResult.ErrorCode` 或支付失败打点 `nova_reason` |

支付过程失败统一落到 `IAPMobileErrorCode` 后，`TrackLocalPayFailInternal`、`TrackReturnedPayFailureInternal`、`TrackValidateFailInternal`、`TrackValidateFailFinishInternal` 都不再接受其他失败原因枚举。`nova_reason_detail` 只存可读补充描述，不参与主分类；当失败原始来源不是 `Mobile` 时，必须在该字段保留原始错误域，避免把不同枚举的相同整数误读为同一语义。活跃支付失败在 `PayAsync` 返回边界上报一次，不使用运行期失败 key 做事后去重；本地支付成功点按运行期平台订单 key 去重，验单成功点按当前 UID 持久化的平台订单 key 去重。

订单身份和存档口径：

| 场景 | 当前口径 |
|---|---|
| 本地存档合并 | 以 `tableId + ReceiptParam` 订单键为 key；服务端 QueryPendingOrder 优先使用 `table_id` 确定商品行，`parameter` 用于解出 `ReceiptParam`，缺失时按空透传兼容 |
| Apple 平台订单号 | 写入 `TransactionId` 并随本地存档保留 |
| Google 平台订单号 | 运行期写入 `TransactionId`，Android 存档时忽略该字段 |
| Google 验单凭据 | 写入 `GoogleToken` 并持久化 |
| 本地支付成功打点去重 | Apple 使用 `TransactionId`，Google 使用 `GoogleToken` |
| 业务 `PaySuccess` 去重 | 使用 `tableId + ReceiptParam` 订单键，不使用平台订单号或 purchase token |
| `nova_iap_validate_success.nova_order_id` | 优先服务端验单响应 `OrderId`；缺失时回退当前运行期 `TransactionId` |

渠道打点口径：

| 平台 | `nova_channel` / TGA `solar_channel` |
|---|---|
| Android | `google` |
| iOS | `ios` |
| 其他或编辑器兜底 | `mobile` |

## 11. 使用示例

```csharp
SDKComponent sdk = FrameworkComponentsGroup.GetComponent<SDKComponent>();
if (!sdk.TryGet<IAPPlugin>(out IAPPlugin iap))
    return;

var request = new IAPMobileRequest
{
    TableId = 10001,
    CustomData = "shop_entry",
};

IAPResult result = await iap.PayAsync<IAPResult>(request, ct);

if (iap.TryGetCapability<IIAPMobileQueryCapable>(out var query))
{
    IReadOnlyList<ProductInfo> products = await query.QueryProductsAsync(
        new[] { "com.game.coin100" }, ct);
}

if (iap.TryGetCapability<IIAPMobileSubscriptionCapable>(out var sub))
{
    bool active = sub.InSubscriptionPeriod(20001);
    long expireMs = sub.GetSubscriptionExpireTime(20001);
}
```

## 12. 常见误区

**误区 1：初始化会等待商品拉取完成。**
当前游戏主初始化不等待商店连接；连接和商品拉取都在 Store 运行期后台完成。连接成功前支付由 `IsStoreReady` 拦截。商品信息在 `OnStoreConnected` 后拉取，整体失败按配置重试，成功态不会被迟到失败回退。补单末尾的权益刷新同时检查商店、商品和平台已有购买拉取状态；任一项未就绪都登记延后请求，已有购买回调缓存票据后再补跑，避免把缺少验单凭据的 `FullyEntitled` 误当成一次空恢复完成。

**误区 2：直接访问 `StoreController`。**
所有平台调用必须经 `MobileExtendedService`，不要在其他服务中缓存或绕过它访问 Controller。

**误区 3：登录后不做补单扫描。**
`SetUserId` 只负责切换 UID 和加载对应存档；业务层仍需在合适时机调用 `IAPPlugin.CheckLocalOrdersAsync`。

**误区 4：看到 `CanDeliver=false` 仍直接发货。**
`Reissued` 会返回成功但 `CanDeliver=false`，表示奖励已通过其他渠道补发，业务层不应重复发货。`Delivered` 仍会按 `CanDeliver=true` 返回，用于覆盖客户端发出验单协议但未收到响应的补发奖场景。验单成功打点按当前 UID 持久化的平台订单号键去重：Apple 使用 transaction id，Google 使用订单号；重复平台回调仍由订单状态机和运行期队列去重控制。

**误区 5：用 Google purchase token 作为验单成功打点去重键。**
Android 运行期的 `TransactionId` 承载 Google `OrderId`，服务端验单响应也会返回 `OrderId`。验单成功打点应按该订单号持久化去重；`GoogleToken` 只继续承担验单凭据和本地支付成功打点的运行期去重。

**误区 6：重新引入独立的验单失败枚举。**
当前支付过程失败原因已经统一到 `IAPMobileErrorCode`。新增验单失败类型时，应扩展 2000+ 号段，而不是新建 `MobileStoreTrackFailureReason` 或父包级失败原因枚举。

**误区 7：认为 `EnableAlwaysPaySucceed` 可以在移动端正式包中生效。**
该开关只用于 Editor 调试。移动端编译产物不包含 `MOCK_ORDER_MOBILE` 支付成功分支，发货必须以服务端验单结果为准。
