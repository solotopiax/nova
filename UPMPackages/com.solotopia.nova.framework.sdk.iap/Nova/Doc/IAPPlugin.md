# IAPPlugin

> 最后更新：2026-06-12
> 当前代码事实：`UPMPackages/com.solotopia.nova.framework.sdk.iap/Nova/Scripts/Runtime/**`

**类签名**：`public sealed partial class IAPPlugin : SDKPluginBase, IIAPStoreEventBridge, IIAPPlugin`
**命名空间**：`NovaFramework.SDK.IAP.Runtime`
**获取方式**：通过 `SDKComponent.TryGet<IAPPlugin>(out var iap)` 获取；没有独立 `Nova.IAP` 静态门面。

`IAPPlugin` 是 IAP 核心调度插件。它不直接实现某个支付渠道，而是在初始化时反射发现 `[IAPStore]` Store，并把支付、Restore、补单扫描路由给对应 Store。

## 1. 文件表

| 文件 | 类型 | 说明 |
|---|---|---|
| `Runtime/IAPPlugin.cs` | `IAPPlugin` | 生命周期 override、公开 API |
| `Runtime/IAPPlugin.Visitors.cs` | `IAPPlugin` partial | 字段、属性、事件容器 |
| `Runtime/IAPPlugin.Methods.cs` | `IAPPlugin` partial | 反射扫描、Store 初始化、上下文构建、事件桥 |
| `Runtime/IAPPluginConfig.cs` | `IAPPluginConfig`、`IAPStoreConfigList` | 插件序列化配置 |
| `Runtime/IAPProductTableService.cs` | `IAPProductTableService` | 运行期商品表查询与缓存 |
| `Runtime/IAPPluginEvents.cs` | `IAPPluginEvents` | 对外 ReplayEvent 容器 |
| `Runtime/Internal/IAPStoreBase*.cs` | `IAPStoreBase` | Store 抽象基类、Loading、网络、打点 |
| `Runtime/Interfaces/*.cs` | `IIAPInternalStore` 等 | Store、配置、上下文、能力接口 |
| `Runtime/Results/*.cs` | `IAPResult`、`IAPInitResult`、`IAPPluginErrorCode`、`ProductInfo` | 结果与错误码模型 |

## 2. 配置

`IAPPluginConfig` 只保存序列化数据，商品查询由 `IAPProductTableService` 在运行期负责。

| 属性 | 说明 |
|---|---|
| `DisplayName` | 固定为 `IAP 支付` |
| `EnableAlwaysPaySucceed` | 调试开关；为 true 时 Store 可直接返回成功 |
| `EnableIAPLog` | 详细日志开关 |
| `RetryValidateMaxNum` | 首次验单重试次数，默认 3 |
| `SkipLoadingForReplenish` | 补单是否跳过 Loading |
| `LoadingPanelPrefab` | 支付期 Loading 面板 Resources 路径，默认 `IAP/IAPLoadingPanel` |
| `StoreConfigs` | `[SerializeReference]` Store 配置只读列表 |
| `Products` | 内联商品表只读列表 |

## 3. 关键字段

| 字段 / 属性 | 类型 | 说明 |
|---|---|---|
| `m_Stores` | `List<IIAPInternalStore>` | 当前发现的 Store 实例列表 |
| `m_StoreContext` | `IIAPStoreContext` | Store 运行期上下文 |
| `m_StoreConfigMap` | `Dictionary<IAPStoreType, IIAPStoreConfig>` | StoreType 到配置的映射 |
| `m_PurchasesTable` | `IIAPProductTable` | 运行期商品表服务 |
| `m_CurrentUserId` | `string` | 当前已同步到插件层的账号 UID |
| `m_EventCaches` | `List<Func<UniTask>>` | 条件未满足时缓存的延后执行事件 |
| `m_IsReplayingEventCaches` | `bool` | 当前是否正在回放缓存事件 |
| `m_IsCheckingLocalOrders` | `bool` | 当前是否正在执行补单扫描，用于防并发 |
| `ProductTable` | `IIAPProductTable` | 商品表只读视图；初始化前或商品表为空时为 null |
| `Events` | `IAPPluginEvents` | 业务层订阅支付、初始化、Restore 事件 |
| `m_EventManager` | `IEventManager` | 用于订阅 `SDKEventData.UserLogin` 并自动广播 UID |

## 4. 公开 API

```csharp
public override string Name => "IAPPlugin"
public override int Priority => 20
public IIAPProductTable ProductTable { get; }
public IAPPluginEvents Events { get; }

public void SetUserId(string uid)
public UniTask<T> PayAsync<T>(IIAPRequest request, CancellationToken ct = default)
    where T : class, IIAPResult
public UniTask<IReadOnlyList<T>> RestorePurchasesAsync<T>(CancellationToken ct = default)
    where T : class, IIAPResult
public UniTask CheckLocalOrdersAsync(CancellationToken ct = default)
public UniTask SetStoreEnabled(IAPStoreType storeType, bool enabled, CancellationToken ct = default)
public bool TryGetCapability<T>(out T capability) where T : class, IIAPCapable
```

### 事件

```csharp
public readonly ReplayEvent<IAPInitResult> InitResult
public readonly ReplayEvent<IAPResult> PaySuccess
public readonly ReplayEvent<IAPResult> PayFailed
public readonly ReplayEvent<IReadOnlyList<IAPResult>> SubscriptionRestored
public readonly ReplayEvent<IReadOnlyList<IAPResult>> NonConsumeRestored
```

## 5. 生命周期

### 初始化

```
OnInitializeAsync(config, ct)
  ├── config as IAPPluginConfig，失败则 Warning 后返回
  ├── Products 为空则 Warning 后返回，不创建 Store
  ├── BuildStoreContext(config)
  ├── BuildStoreConfigMap(config)
  ├── m_PurchasesTable = new IAPProductTableService(config.Products)
  ├── DiscoverAndInitializeStoresAsync(ct)
  └── 订阅 SDKEventData.UserLogin
```

`DiscoverAndInitializeStoresAsync` 会扫描全部程序集。`config.Enabled == false` 的 Store 会加入 `m_Stores` 但跳过初始化，后续 `SetStoreEnabled(..., true)` 时懒初始化。

`BuildStoreContext` 会从 `IConfigManager.DevelopMode` 读取当前运行模式并写入 `IIAPStoreContext.DevelopMode`。Store 打点里的 Debug 字段应使用该运行模式判断；`EnableAlwaysPaySucceed` 只表示是否跳过真实平台支付，不再作为打点 Debug 依据。

### 释放

```
OnDisposeAsync(ct)
  ├── 注销 SDKEventData.UserLogin
  ├── 清空账号 UID 与延后执行事件缓存状态
  ├── 逐个 await store.DisposeAsync(ct)
  └── 清空 Store、Context、ConfigMap、ProductTable 引用
```

## 6. 使用示例

```csharp
SDKComponent sdk = FrameworkComponentsGroup.GetComponent<SDKComponent>();
if (!sdk.TryGet<IAPPlugin>(out IAPPlugin iap))
    return;

iap.Events.PaySuccess.Subscribe(result =>
{
    if (result.CanDeliver)
        Deliver(result.TableId, result.OrderId, result.CustomData);
});

// IAPPlugin 会监听 SDKEventData.UserLogin 自动同步 UID；
// 手动切账号或登录事件尚未触达时可显式调用。
iap.SetUserId(userId);

// 如果这行早于 SetUserId 调用，IAPPlugin 会缓存一次延后执行事件，并在 SetUserId 后自动补执行。
await iap.CheckLocalOrdersAsync(ct);

var request = new IAPMobileRequest
{
    TableId = 10001,
    CustomData = "shop_entry",
};

IAPResult result = await iap.PayAsync<IAPResult>(request, ct);
if (!result.IsSuccess)
    ShowToast(result.FailReason);
```

## 7. 常见误区

**误区 1：把 `IAPPluginConfig` 当成商品表服务。**
当前配置只保存 `Products` 数据；运行期查询由 `IAPProductTableService` 实现，并通过 `IAPPlugin.ProductTable` 暴露。

**误区 2：认为错误码是全局统一枚举。**
`IAPResult.ErrorCode` 是 int。核心层只定义 `IAPPluginErrorCode`，Store 内部失败使用各 Store 自己的错误码枚举；业务层需要结合 Store 类型解释错误码。

**误区 3：业务层直接调用渠道方法。**
渠道特有能力通过 `TryGetCapability<T>` 获取，例如 Mobile 的 `IIAPMobileQueryCapable` 和 `IIAPMobileSubscriptionCapable`。

**误区 4：登录前不能调用补单扫描。**
业务层可以提前调用 `CheckLocalOrdersAsync`。如果此时 `SetUserId` 尚未执行，`IAPPlugin` 会把本次调用缓存为延后执行事件，并在账号 UID 同步后按入队顺序回放；如果扫描正在执行，再次调用也会缓存一次后续事件，避免并发重复跑。后续其他需要等待账号 UID 或等待当前流程结束的逻辑，也应复用同一套事件缓存机制。

**误区 5：打点 reason 可以直接传任意对象。**
父包 `Track*Fail` 只接收 `Enum` 类型的失败原因，并在上报前转成 `int` 写入 `nova_reason`；可读描述写入 `nova_reason_detail`。Store 侧需要先把失败原因收敛到自己的明确枚举，父包不维护跨 Store 的失败原因全集。Mobile 支付过程失败统一使用 `IAPMobileErrorCode`，初始化失败使用独立的 `MobileStoreInitFailureReason`。
