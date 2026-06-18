# MobileStore

> 最后更新：2026-06-12
> 当前代码事实：`UPMPackages/com.solotopia.nova.framework.sdk.iap.mobile/Nova/Scripts/Runtime/**`

**类签名**：`public sealed partial class MobileStore : IAPStoreBase, IIAPMobileQueryCapable, IIAPMobileSubscriptionCapable`
**命名空间**：`NovaFramework.SDK.IAP.Mobile.Runtime`
**发现方式**：通过 `[IAPStore]` 由父包 `IAPPlugin` 反射发现并初始化。业务侧通常通过 `IAPPlugin.PayAsync<T>` 和 `TryGetCapability<T>` 使用。

`MobileStore` 对接 Unity IAP 5.x，覆盖 Google Play 与 iOS App Store 官方内购的购买、Restore、服务端验单、补单、订阅到期和非消耗品持有状态。

## 1. 文件表

| 文件 | 类型 | 说明 |
|---|---|---|
| `MobileStore.cs` / `.Visitors.cs` / `.Methods.cs` / `.Track.cs` | `MobileStore` | 对外入口、生命周期、持久化辅助、埋点转发 |
| `MobileStoreConfig.cs` | `MobileStoreConfig` | Mobile Store 专属配置 |
| `IAPMobileRequest.cs` | `IAPMobileRequest` | Mobile 支付请求 |
| `IAPMobileErrorCode.cs` | `IAPMobileErrorCode` | Mobile 支付过程失败原因与错误码 |
| `IIAPMobileQueryCapable.cs` | `IIAPMobileQueryCapable` | 商品信息查询能力 |
| `IIAPMobileSubscriptionCapable.cs` | `IIAPMobileSubscriptionCapable` | 订阅和非消耗品查询能力 |
| `Data/*.cs` | `MobileStorePersistData`、`MobileOrderRecord`、`MobileOrderStatus`、`MobileCheckEntitlementInfo` | 存档、订单、权益检查数据 |
| `Services/MobileServiceHub.cs` | `MobileServiceHub` | 服务聚合容器 |
| `Services/Net/MobileIapNetService.cs` | `MobileIapNetService` | 查单和验单协议 |
| `Services/Extended/MobileExtendedService.cs` | `MobileExtendedService` | `StoreController` 收口 |
| `Services/Store/MobileStoreService.cs` | `MobileStoreService` | Unity IAP 回调路由 |
| `Services/Init/*.cs` | `MobileInitService`、`MobileRuntimeContext` | 初始化状态机 |
| `Services/Purchase/*.cs` | `MobilePurchaseService` | 平台购买和购买回调 |
| `Services/Validation/*.cs` | `MobileValidationService` | 订单状态机与验单队列 |
| `Services/Restore/*.cs` | `MobileRestoreService`、`MobileRestoreCoordinator` | Restore 流程 |
| `Services/Product/*.cs` | `MobileProductService` | Product / Receipt 查询缓存 |
| `Services/Subscription/*.cs` | `MobileSubscriptionService` | 订阅到期和倒计时 |
| `Utils/*.cs` | `MobileReceiptParser`、`MobileStoreParameterCodec` | 票据解析和透传编码 |

## 2. 配置

`MobileStoreConfig` 通过父包 `IAPPluginConfig.StoreConfigs` 的 `[SerializeReference]` 多态列表配置。

| 属性 | 默认值 | 说明 |
|---|---|---|
| `Enabled` | `true` | 是否启用 Mobile Store |
| `GoogleQueryPendingOrderCmdName` | `IAPGoogleQueryPendingOrder` | Google 查询未完成订单 Cmd |
| `GoogleVerifyCmdName` | `IAPGoogleVerify` | Google 普通内购验单 Cmd |
| `GoogleVerifySubscriptionCmdName` | `IAPGoogleVerifySubscription` | Google 订阅验单 Cmd |
| `AppleQueryPendingOrderCmdName` | `IAPAppleQueryPendingOrder` | Apple 查询未完成订单 Cmd |
| `AppleVerifyCmdName` | `IAPAppleVerify` | Apple 普通内购验单 Cmd |
| `AppleVerifySubscriptionCmdName` | `IAPAppleVerifySubscription` | Apple 订阅验单 Cmd |

## 3. MobileStore 关键属性

| 属性 / 字段 | 说明 |
|---|---|
| `StoreType` | 固定 `IAPStoreType.Mobile` |
| `TrackChannel` | Android = `google`，iOS = `ios`，其他平台兜底 `mobile` |
| `StoreLogTag` | 固定 `LogTag.IAPMobile` |
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
  └── InitService.InitializeAsync(table, ct)
        ├── 商店连接成功即 Ready
        ├── 商品信息后台 FetchProducts，不阻塞初始化结果
        └── 商品拉取成功后自动 RestoreTransactions + FetchPurchases，恢复平台 PendingOrder 票据
```

初始化失败时 `MobileInitService` 通过 `IAPInitResult.Fail((int)MobileStoreInitFailureReason, detail)` 上报；支付会被基类 `PayGuardAsync` 的 `IsStoreReady` 检查拦截。初始化成功和失败都会通过父包 `IAPStoreBase.Track*` 封装上报 `nova_iap_init`。

## 6. 支付流程

```
IAPPlugin.PayAsync<IAPResult>(IAPMobileRequest)
  └── MobileStore.PayAsync
        ├── ct.ThrowIfCancellationRequested()
        ├── m_LoadingGuard.HasUserInteracted = true
        ├── Context.EnableAlwaysPaySucceed → 返回 MOCK_ORDER_MOBILE
        └── PayGuardAsync(request, ct, PurchaseService.PayAsync)
              ├── store 禁用 / 未就绪 / 已有支付 / 商品表缺失 → 失败
              └── MobilePurchaseService.PayAsync
                    ├── 当前 tableId 已有待验订单 → 优先补验
                    ├── 订阅商品自身仍在有效期内 → 返回 SubscriptionIsReady，不发起平台购买
                    ├── 写 Purchasing 占位订单
                    ├── 发起平台购买
                    ├── 平台回调后写 PendingValidate 并入验单队列
                    └── MobileValidationService 批量验单后派发结果
```

## 6.1 补单扫描流程

```
IAPPlugin.CheckLocalOrdersAsync
  └── MobileStore.CheckLocalOrdersAsync
        └── MobileValidationService.CheckLocalOrdersAsync
              ├── 未登录时直接跳过，不发起 QueryPendingOrder / Verify 协议
              ├── 启动期 OnProductsFetched 会先触发平台 RestoreTransactions，再自动 FetchPurchases 恢复平台 PendingOrder
              ├── 登录前收到的 PendingOrder 只暂存在内存，登录后先合并到当前 UID 存档
              ├── 每次扫描都向服务端发送 QueryPendingOrder
              ├── 优先按服务端返回的 `table_id`（long）将未完成订单 merge 到本地 OrderRecords，`parameter` 解码仅作旧协议兜底
              ├── 服务端返回的 Google token 可补齐本地 Purchasing 占位记录
              ├── 本地扫描 PendingValidate / ValidateFailed / 具备凭据的 Purchasing
              ├── Google 订单缺少 purchase token 时保留记录，不发送 VerifyGoogleIap
              └── 本地补单扫描结束后触发一次 CheckEntitlement，刷新订阅和非消耗品权益
```

登录前平台回调可能先于业务 `SetUserId` 到达。此时 Mobile 只收集待验单数据，不读写账号存档，也不发起服务端协议；商品拉取后的 `RestoreTransactions` 也只用于唤起平台侧订单补全。业务登录后调用 `CheckLocalOrdersAsync`，才会按“合并暂存订单 → 拉取服务端未完成订单 → 本地验单 → 订阅权益查询”的顺序执行。

## 7. 订单状态机

### 本地订单状态

| 值 | 名称 | 含义 |
|---|---|---|
| 0 | `Purchasing` | 已发起平台购买，等待平台回调；Google 订单未取得 purchase token 前不会进入验单 |
| 1 | `PendingValidate` | 平台回调成功，待服务端验单 |
| 2 | `ValidateFailed` | 验单网络或 HTTP 失败，保留记录等待下次补单 |
| 3 | `LocalPayFailed` | 平台本地支付失败，启动扫描时直接删除 |

### 服务端验单状态

| 值 | 名称 | 客户端处理 |
|---|---|---|
| 1 | `PendingVerify` | 客户端还未发过校验协议；保留订单等待重试 |
| 2 | `Verified` | 校验完毕；删除记录并派发可发货成功 |
| 3 | `Reissued` | 奖励已通过其他渠道补发；删除记录并派发成功，`CanDeliver=false` |
| 4 | `Delivered` | 服务端已处理过订单；删除记录并派发成功，`CanDeliver=true`，客户端仍按本地幂等规则补发奖 |
| 5 | `Invalid` | 无效订单；删除记录并派发失败 |

`PaySuccess` 派发按 tableId 做运行期去重，不使用平台 `TransactionId` 作为业务判断依据。`MobileStore` 仍维护当前运行期平台订单打点 key 缓存，但只用于平台 Pending / Confirmed 双回调的本地支付成功打点去重：Apple 使用 `TransactionId`，Google 使用 `GoogleToken`。订阅商品只有当前主动 `PayAsync` 对应的订单才走 `PaySuccess`；后台补单、Restore 和订阅刷新只更新订阅到期时间。

### 埋点事件

Mobile 通过 `MobileStore.Track.cs` 调用父包 `IAPStoreBase.Track*` 封装，当前接入事件如下：

| 事件 | 触发时机 |
|---|---|
| `nova_iap_init` | 商店连接成功或初始化失败 |
| `nova_iap_buy` | 用户发起真实平台购买前；`EnableAlwaysPaySucceed` 调试支付也会上报 |
| `nova_iap_local_pay_success` | Unity IAP 返回 Pending / Confirmed 并登记本地订单后；同一订单号在当前运行期去重 |
| `nova_iap_local_pay_fail` | 平台购买失败、平台购买发起异常或活跃支付被主动结束为失败 |
| `nova_iap_validate_fail` | 单轮验单失败但订单仍可能重试或补单 |
| `nova_iap_first_pay_order_validate` | 当前主动支付订单第一次验单失败 |
| `nova_iap_validate_fail_finish` | 验单最终失败、无效订单或超出重试后进入 `ValidateFailed` |
| `nova_iap_validate_success` | 服务端返回 `Verified`、`Delivered` 或 `Reissued` 并终结订单 |

`nova_iap_create_order_success`、`nova_iap_create_order_fail`、`nova_iap_third_pay_close_order` 是第三方支付链路事件，Mobile 官方内购不触发。`nova_iap_deliver_fail` 目前不触发，因为业务发奖不由 Mobile Store 执行。

关键字段口径：

| 字段 | 口径 |
|---|---|
| `nova_iap_local_pay_success.nova_order_id` | 优先使用 Unity IAP receipt 解析出的平台 `OrderId`；缺失时回退 Apple `TransactionId` |
| `nova_iap_validate_success.nova_order_id` | 优先使用服务端验单响应 `OrderId`；缺失时回退当前运行期 `TransactionId` |
| `nova_iap_local_pay_fail.nova_reason` | `IAPMobileErrorCode` 的 int 值；Unity IAP `PurchaseFailureReason` 映射到 1000-1010 |
| `nova_iap_validate_fail(.finish).nova_reason` | `IAPMobileErrorCode` 的 int 值；验单网络、响应缺失、待完成、凭据缺失和无效订单使用 2000+ 号段 |
| `nova_reason_detail` | 失败原因的可读补充描述，例如协议错误信息、服务端状态或缺失凭据说明 |
| `Debug` | 来自父包注入的 `IIAPStoreContext.DevelopMode == DevelopMode.Debug`；不再取 `EnableAlwaysPaySucceed` |
| 本地业务去重 | `PaySuccess` 按 tableId 去重，不按平台订单号或 purchase token 去重 |

## 8. 存档

`MobileStorePersistData` 是当前 UID 的统一存档容器。

| 字段 | 说明 |
|---|---|
| `OrderRecords` | `Dictionary<long, MobileOrderRecord>`，key = tableId；Android 不持久化 `TransactionId`，iOS 持久化 `TransactionId` |
| `SubscriptionExpireMs` | 订阅到期 Unix 毫秒 |
| `NonConsumeOwnership` | 非消耗品持有标记 |
| `HasQueriedPendingFromServer` | 当前 UID 是否曾成功向服务端同步过未完成订单；不用于阻止后续 QueryPendingOrder |

`MobileOrderRecord` 字段：

| 字段 | 说明 |
|---|---|
| `TransactionId` | 平台订单 ID；Android 运行期可写入 Google `OrderId` 但不落本地存档，iOS 写入 Apple transaction id 并持久化供补单验单使用 |
| `TableId` | 商品配置表行 ID |
| `GoogleToken` | Google Play purchase token；iOS 为空；Google 验单与本地支付成功打点去重使用该字段 |
| `Status` | 当前订单状态 |
| `IsReplenish` | 是否为补单路径 |
| `CustomDataParam` | 业务透传字符串 |

## 9. 错误码

`IAPMobileErrorCode` 是 Mobile 支付过程统一失败原因。0-8 可通过 `IAPResult.ErrorCode` 以 int 返回给业务层；失败打点通过父包 `IAPStoreBase.Track` 写入枚举的 int 值：

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

0-8 是 Mobile Store 自身流程错误；1000-1010 是 Unity IAP `PurchaseFailureReason` 的专用映射号段；2000+ 是验单失败打点细分号段。`TrackLocalPayFailInternal`、`TrackValidateFailInternal` 和 `TrackValidateFailFinishInternal` 都只接收 `IAPMobileErrorCode`，确保支付过程 `nova_reason` 的枚举域统一。

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

当前 Mobile 支付代码按两条失败原因线维护：

| 线 | 枚举 | 用途 |
|---|---|---|
| 初始化失败 | `MobileStoreInitFailureReason` | 初始化阶段失败分类，写入 `IAPInitResult.FailReason` 和 `nova_iap_init.nova_init_failure_reason` |
| 支付过程失败 | `IAPMobileErrorCode` | 支付、平台本地支付失败、验单失败分类，写入 `IAPResult.ErrorCode` 或支付失败打点 `nova_reason` |

支付过程失败统一落到 `IAPMobileErrorCode` 后，`TrackLocalPayFailInternal`、`TrackValidateFailInternal`、`TrackValidateFailFinishInternal` 都不再接受其他失败原因枚举。`nova_reason_detail` 只存可读补充描述，不参与主分类。

订单身份和存档口径：

| 场景 | 当前口径 |
|---|---|
| 本地存档合并 | 以 `tableId` 为 key；服务端 QueryPendingOrder 优先使用 `table_id`，`parameter` 只作旧协议兜底 |
| Apple 平台订单号 | 写入 `TransactionId` 并随本地存档保留 |
| Google 平台订单号 | 运行期写入 `TransactionId`，Android 存档时忽略该字段 |
| Google 验单凭据 | 写入 `GoogleToken` 并持久化 |
| 本地支付成功打点去重 | Apple 使用 `TransactionId`，Google 使用 `GoogleToken` |
| 业务 `PaySuccess` 去重 | 使用 tableId，不使用平台订单号或 purchase token |
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
当前初始化只等待商店连接成功。商品信息在 `OnStoreConnected` 后后台拉取，`OnProductsFetchFailed` 不会回退初始化结果。

**误区 2：直接访问 `StoreController`。**
所有平台调用必须经 `MobileExtendedService`，不要在其他服务中缓存或绕过它访问 Controller。

**误区 3：登录后不做补单扫描。**
`SetUserId` 只负责切换 UID 和加载对应存档；业务层仍需在合适时机调用 `IAPPlugin.CheckLocalOrdersAsync`。

**误区 4：看到 `CanDeliver=false` 仍直接发货。**
`Reissued` 会返回成功但 `CanDeliver=false`，表示奖励已通过其他渠道补发，业务层不应重复发货。`Delivered` 仍会按 `CanDeliver=true` 返回，用于覆盖客户端发出验单协议但未收到响应的补发奖场景，重复平台回调由客户端运行期去重控制。

**误区 5：把 `TransactionId` 当成 Google 订单键。**
Android 运行期允许 `TransactionId` 承载 Google `OrderId`，但它不会写入本地存档，也不能作为 Google 验单或本地支付成功打点去重 key。Google 仍使用 `GoogleToken` 验单和去重。

**误区 6：重新引入独立的验单失败枚举。**
当前支付过程失败原因已经统一到 `IAPMobileErrorCode`。新增验单失败类型时，应扩展 2000+ 号段，而不是新建 `MobileStoreTrackFailureReason` 或父包级失败原因枚举。
