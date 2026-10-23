# IAPPlugin

> 最后更新：2026-08-17
> 当前代码事实：`UPMPackages/com.solotopia.nova.framework.sdk.iap/Nova/Scripts/Runtime/**`

**类签名**：`public sealed partial class IAPPlugin : SDKPluginBase, IIAPStoreEventBridge, IIAPPlugin, ISDKPauseListener, ISDKFocusListener`
**命名空间**：`NovaFramework.SDK.IAP.Runtime`
**获取方式**：通过 `SDKComponent.TryGet<IAPPlugin>(out var iap)` 获取；没有独立 `Nova.IAP` 静态门面。

`IAPPlugin` 是 IAP 核心调度插件。它不直接实现某个支付渠道，而是在初始化时反射发现 `[IAPStore(IAPStoreType.Xxx)]` Store，并把支付、Restore、补单扫描路由给对应 Store。StoreType 元数据会在构造前匹配配置，未配置的可选 Store 不会被构造。

## 1. 文件表

| 文件 | 类型 | 说明 |
|---|---|---|
| `Runtime/IAPPlugin.cs` | `IAPPlugin` | 生命周期 override、公开 API |
| `Runtime/IAPPlugin.Visitors.cs` | `IAPPlugin` partial | 字段、属性、事件容器 |
| `Runtime/IAPPlugin.Methods.cs` | `IAPPlugin` partial | 反射扫描、Store 初始化、上下文构建、事件桥 |
| `Runtime/IAPPlugin.BackgroundTasks.cs` | `IAPPlugin` partial | 登录后自动补单等后台任务的取消与异常收口 |
| `Runtime/IAPPluginConfig.cs` | `IAPPluginConfig`、`IAPStoreConfigList` | 插件序列化配置 |
| `Runtime/IAPProductTableService.cs` | `IAPProductTableService` | 运行期商品表查询与缓存 |
| `Runtime/IAPPluginEvents.cs` | `IAPPluginEvents` | 对外 ReplayEvent 容器 |
| `Runtime/Internal/IAPStoreBase.cs` | `IAPStoreBase` | Store 抽象基类 public/abstract 调用面和生命周期入口 |
| `Runtime/Internal/IAPStoreBase.Visitors.cs` | `IAPStoreBase` partial | Store 基类字段、状态属性、protected 属性和常量 |
| `Runtime/Internal/IAPStoreBase.Methods.cs` | `IAPStoreBase` partial | Store 基类 protected/private 模板方法和辅助方法，包括 `PayGuardAsync` |
| `Runtime/Internal/IAPStoreBase.Track.cs` | `IAPStoreBase` partial | 初始化、购买、默认 guard 失败打点及通用属性构造与发送能力 |
| `Runtime/Internal/IAPStoreBase.Net.cs` | `IAPStoreBase` partial | Store 基类通用网络请求能力 |
| `Runtime/Internal/IAPLog.cs` / `Runtime/Internal/IAPLogOwner.cs` | `IAPLog` / `IAPLogOwner` | IAP 日志统一网关和日志持有者基类；底层只接收 `LogTag + message` |
| `Runtime/Interfaces/*.cs` | `IIAPInternalStore` 等 | Store、配置、上下文、能力接口 |
| `Runtime/Results/*.cs` | `IAPResult`、`IAPInitResult`、`IAPPluginErrorCode`、`ProductInfo` | 结果与错误码模型 |

## 2. 配置

`IAPPluginConfig` 只保存序列化数据，商品查询由 `IAPProductTableService` 在运行期负责。

| 属性 | 说明 |
|---|---|
| `DisplayName` | 固定为 `IAP 支付` |
| `EnableAlwaysPaySucceed` | Editor 调试开关；为 true 时 Editor 下 Store 可直接返回成功，非 Editor 编译态强制关闭 |
| `EnableIAPLog` | 详细日志开关，默认开启；初始化时会传入 `IAPLog.SetEnabled` 控制 IAP 日志统一网关 |
| `RetryValidateMaxNum` | Store 共享的首次验单重试次数配置，默认 3；具体 Store 可按验单来源覆盖，例如 ThirdPay 明确支付成功会覆盖完整重试间隔序列，关闭/返回等不明确场景最多 3 次 |
| `SkipLoadingForReplenish` | 补单是否跳过 Loading |
| `LoadingPanelPrefab` | 支付期 Loading 面板 Resources 路径，默认 `IAP/IAPLoadingPanel` |
| `StoreConfigs` | `[SerializeReference]` Store 配置只读列表 |
| `Products` | 内联商品表只读列表 |

### SKU Excel 导入

IAP Products 支持在 Inspector 中通过 Excel 批量维护：

- `导出 SKU 模板`：从包内 `Nova/Templates/IAPProductsTemplate.xlsx` 复制模板到用户选择的位置。
- `导入 SKU Excel`：读取用户选择的 Excel，校验通过后全量覆盖当前 `IAPPluginConfig.Products`。
- 导入时优先读取历史固定 Sheet `Products`；若不存在，则读取工作簿中的第一个 Sheet。
- 模板使用 `##comment`、`##var`、`##type`、`##comment` 元信息行；`##var` 行字段固定为 `TableId`、`Name`、`ProductID`、`ThirdProductID`、`ProductType`、`SubGroupID`、`Price`、`Currency`、`EditorNote`。
- 为兼容旧表，导入器仍支持第一行直接写上述固定表头的 Excel。
- `ProductType` 只接受 `IAPProductType` 枚举名：`Consumable`、`NonConsumable`、`Subscription`。
- 阻断导入的校验项：`TableId` 必填且必须是 `1~4294967295` 范围内的整数且不重复，`ProductType` 必须是枚举名，`SubGroupID` 非空时必须是整数。
- 其他字段允许为空；`EditorNote` 是商品配置备注，导入后完全以 Excel 为准，并随商品条目导出到 `ConfigRuntimeSO`。

## 3. 关键字段

| 字段 / 属性 | 类型 | 说明 |
|---|---|---|
| `m_Stores` | `List<IIAPInternalStore>` | 当前发现的 Store 实例列表 |
| `m_StoreContext` | `IIAPStoreContext` | Store 运行期上下文 |
| `m_StoreConfigMap` | `Dictionary<IAPStoreType, IIAPStoreConfig>` | StoreType 到配置的映射 |
| `m_PurchasesTable` | `IIAPProductTable` | 运行期商品表服务 |
| `m_CurrentUserId` | `string` | 当前已同步到插件层的账号 UID |
| `m_HasDeferredCheckLocalOrders` | `bool` | 登录前是否收到过补单扫描请求 |
| `m_IsCheckingLocalOrders` | `bool` | 当前是否正在执行补单扫描，用于防并发 |
| `m_PendingCheckLocalOrders` | `bool` | 扫描中再次触发时标记当前轮结束后补跑一次 |
| `m_RuntimeTaskCts` | `CancellationTokenSource` | IAPPlugin 运行期后台任务取消源，Dispose 时取消登录后自动补单等后台任务 |
| `ProductTable` | `IIAPProductTable` | 商品表只读视图；初始化前或商品表为空时为 null |
| `Events` | `IAPPluginEvents` | 业务层订阅支付、初始化、Restore 事件 |
| `m_EventManager` | `IEventManager` | 用于订阅 `SDKEventData.UserLogin` 并自动广播 UID |

## 4. 公开 API

```csharp
public override string Name => "IAPPlugin"
public override int Priority => 70
public IIAPProductTable ProductTable { get; }
public IAPPluginEvents Events { get; }

public void OnPause(bool isPaused)
public void OnFocus(bool hasFocus)
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

`PaySuccess` / `PayFailed` / Restore 事件中的 `IAPResult.StoreType` 表示结果所属商店，可用于区分 `Mobile`、`ThirdPay`、`Voucher`。失败结果未显式传入时会从 `ErrorSource` 推断；`IAPStoreBase.PayGuardAsync` 的公共 guard 失败会由具体 Store 补齐 `StoreType`。

## 5. 生命周期

### 初始化

```
OnInitializeAsync(config, ct)
  ├── 重建 IAPPlugin 运行期后台任务取消源
  ├── config as IAPPluginConfig，缺失或类型不匹配则 Warning 后抛 InvalidOperationException
  ├── 应用 EnableIAPLog 到 IAPLog.SetEnabled
  ├── Products 为空则 Warning 后抛 InvalidOperationException，不创建 Store
  ├── BuildStoreContext(config)
  ├── BuildStoreConfigMap(config)
  ├── m_PurchasesTable = new IAPProductTableService(config.Products)
  ├── DiscoverAndInitializeStoresAsync(ct)
  └── 订阅 SDKEventData.UserLogin
```

`DiscoverAndInitializeStoresAsync` 会扫描全部程序集。`config.Enabled == false` 的 Store 会加入 `m_Stores` 但跳过初始化，后续 `SetStoreEnabled(..., true)` 时懒初始化。

Store 的 `InitializeAsync` 只保证该 Store 已完成自己的启动阶段，不统一保证渠道已经可支付。Mobile Store 会在该方法内启动运行期后台连接后返回，因此 `Nova.SDK.InitializeTask` 不等待 Google Play / App Store 连接；业务若关心 Mobile 就绪结果，应订阅 `IAPPlugin.Events.InitResult`，支付入口本身也会通过 Store Ready 门禁拦截未连接状态。

`BuildStoreContext` 会从 `IConfigManager.DevelopMode` 读取当前运行模式并写入 `IIAPStoreContext.DevelopMode`。Store 打点里的 Debug 字段应使用该运行模式判断；`EnableAlwaysPaySucceed` 只表示 Editor 下是否跳过真实平台支付，不再作为打点 Debug 依据，非 Editor 编译态会被强制注入为 false。

### 应用前后台

`SDKComponent` 将 `OnApplicationPause` / `OnApplicationFocus` 广播给 `IAPPlugin`。IAPPlugin 再遍历当前 Store，仅向实现 `IIAPStorePauseListener` / `IIAPStoreFocusListener` 的 Store 同步转发；单个 Store 抛出异常时只记录错误，不阻断后续 Store。Store 是否处理事件由当前会话状态决定，不以启用标志提前过滤。

### 释放

```
OnDisposeAsync(ct)
  ├── 取消 IAPPlugin 运行期后台任务
  ├── 注销 SDKEventData.UserLogin
  ├── 清空账号 UID 与延后执行事件缓存状态
  ├── 逐个 await store.DisposeAsync(ct)
  ├── 清空 Store、Context、ConfigMap、ProductTable 引用
  └── 释放 IAPPlugin 运行期后台任务取消源
```

`IAPPlugin.RunBackgroundTask` 入口委托固定为 `Func<CancellationToken, UniTask>`，只负责运行期取消和异常日志收口。Store 层如果要把返回 `UniTask<T>` 的方法接入后台任务，必须先用 lambda 或无返回包装方法显式 `await` 并丢弃返回值，不能直接把有返回值方法组传入后台任务入口。

`IAPStoreBase` 的 partial 文件按职责拆分：无后缀文件只保留 public/abstract 调用面；字段、状态属性和 protected 属性放在 `.Visitors.cs`；protected/private/internal 模板与辅助方法放在 `.Methods.cs`；打点和网络能力分别放在 `.Track.cs`、`.Net.cs`。新增基类成员时应保持该布局，避免把非 public helper 或成员状态写回无后缀文件。

## 6. 使用示例

```csharp
SDKComponent sdk = FrameworkComponentsGroup.GetComponent<SDKComponent>();
if (!sdk.TryGet<IAPPlugin>(out IAPPlugin iap))
    return;

iap.Events.PaySuccess.Subscribe(result =>
{
    IAPStoreType storeType = result.StoreType;
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
    ShowToast(result.ErrorDesc);
```

## 7. 常见误区

**误区 1：把 `IAPPluginConfig` 当成商品表服务。**
当前配置只保存 `Products` 数据；运行期查询由 `IAPProductTableService` 实现，并通过 `IAPPlugin.ProductTable` 暴露。

**误区 2：认为错误码是全局统一枚举。**
`IAPResult.ErrorCode` 是 int。核心层只定义 `IAPPluginErrorCode`，Store 内部失败使用各 Store 自己的错误码枚举；业务层必须结合 `(ErrorSource, ErrorCode)` 解码失败类型。

`PayAsync` 只要返回失败 `IAPResult`，都应产生一次 `nova_iap_local_pay_fail`。当失败发生在 `IAPPlugin` 路由层且尚未命中具体 Store 时，`nova_channel` 使用请求的 `StoreType` 小写值；`request == null` 时使用 `router`。这类失败的 `nova_reason` 来自 `IAPPluginErrorCode`，`nova_reason_detail` 会包含 `PluginRouter:<code>` 以便和 Store 自身错误码区分。

当失败发生在 `IAPStoreBase.PayGuardAsync` 公共 guard（禁用、未就绪、重入、商品表缺失）时，`ErrorSource.PluginRouter` 表示错误码属于 `IAPPluginErrorCode`。具体 Store 若需要把 guard 失败映射到自己的打点错误码域，应保留原始 `(ErrorSource, ErrorCode)` 到 `nova_reason_detail`，不要只按整数值解释。

`IAPStoreBase.PayGuardAsync` 会先派发 `PayFailed` 事件，再按 `ShouldTrackPayGuardFailure` 决定是否由基类直接补打失败点。MobileStore 覆写该钩子返回 `false`，因为它需要在 `PayAsync` 返回边界把所有失败 `IAPResult` 统一映射到 `IAPMobileErrorCode` 后上报；其他 Store 默认仍由基类直接上报 guard 失败。

对于已经发送到服务端并取得明确拒绝结果的订单，Store 可以在失败 `IAPResult` 中同时保留 `OrderId` 和 `IsRecoveredOrder`。业务层仍以 `IsSuccess`、`ErrorDesc` 和 `(ErrorSource, ErrorCode)` 判断失败，不应因为存在订单号就视为支付成功。

**误区 3：业务层直接调用渠道方法。**
渠道特有能力通过 `TryGetCapability<T>` 获取，例如 Mobile 的 `IIAPMobileQueryCapable` 和 `IIAPMobileSubscriptionCapable`。

**误区 4：登录前不能调用补单扫描。**
业务层可以提前调用 `CheckLocalOrdersAsync`。如果此时 `SetUserId` 尚未执行，`IAPPlugin` 会记录一次延后补单请求，并在账号 UID 同步后经后台任务入口自动执行；如果扫描正在执行，再次调用只会标记当前轮结束后补跑一轮，避免并发重复跑，也避免无上限堆积同类补单事件。`OnDisposeAsync` 会先取消后台任务，避免插件释放后继续访问 Store。

**误区 5：打点 reason 可以直接传任意对象。**
具体 Store 的 `.Track.cs` 负责把自身失败枚举转成 `int` 写入 `nova_reason`，并把可读描述写入 `nova_reason_detail`。父包不维护跨 Store 的失败原因全集，也不解释订单号和验单状态；它只提供通用属性构造、属性合并和事件发送能力，并在默认 guard 失败入口处理通用 `IAPResult`。Mobile 支付过程失败统一使用 `IAPMobileErrorCode`，初始化失败使用独立的 `MobileStoreInitFailureReason`。

**误区 6：把 `EnableAlwaysPaySucceed` 当成移动端可用的运行时功能。**
该开关只用于 Editor 调试支付链路。移动端编译产物不会包含 Store 的 mock 支付成功分支，且 `IAPPlugin` 构造上下文时会把该值强制置为 false。正式发货仍必须以服务端验单结果为准。

## 8. 查询、配置编辑与失败边界

### 商品表是初始化时的缓存

`IAPProductTableService` 保留传入的商品列表和条目引用，只在构造时建立缓存；`IReadOnlyList` 不代表深度不可变，之后修改原列表或条目不会自动重建索引。`TableId` 为 0 的条目不入主键索引，负值没有在该层拒绝；`ProductID` 为空时不入 SKU 索引，非空值按区分大小写的 Ordinal 比较，重复主键和 SKU 保留首条。`ThirdProductID` 不参与该 SKU 索引。类型桶保留全部非空条目；订阅组桶只收录 `Subscription` 且 `SubGroupID != 0` 的条目，但 `AreInSameSubGroup` 本身只比较两个非空条目的非零组号，不额外验证商品类型。

`IAPProductEntry` 的字符串源码初值为 null、数值为零；这些初值和 getter 不验证当前配置、平台 SKU、金额或币种。`ProductInfo` 仅保存平台展示文本；`IAPResult.IsSuccess`、`CanDeliver`、`OrderId`、到期时间及 `StoreType` 各有独立含义。稳定订单失败仍可携带订单号和补单标记，成功也可禁止发货；`WithStoreType` 修改并返回原结果对象，事件桥也不复制结果或列表。业务必须按结果域与 `CanDeliver` 判断，并自行实现发货幂等。

### Inspector 和 Excel 只改配置源

Excel 单元格统一裁剪首尾空白，列名和商品类型名区分大小写；额外列允许存在。标准九列表头优先，其次查首个 `##var` 行；A 列以任意 `##` 开头的行都会跳过。合法表头但没有商品数据可以得到 `Success=true` 和空列表；该标记只表示错误列表为空，不能证明商品足够初始化。解析器验证 TableId、ProductType、SubGroupID，不验证 SKU 是否已上架、价格正数/有限、币种或订阅组的业务有效性。某行的合法 ID 会先加入去重集合，之后该行其他列失败也不会撤回该 ID，因此后续相同 ID 仍报重复。

导入确认后 `ApplyProducts` 记录 Undo、清空并逐项覆盖数组，随后 Apply/SetDirty；它不重新验证外部构造的商品 DTO，也没有跨字段回滚。中途异常可能留下部分序列化编辑。列表的重复 ID 红色标记只做提示；同数量的外部内容替换不一定使当前 Drawer 的过滤缓存立即失效。模板导出会覆盖用户所选文件，取消文件对话框才是无写入；当前模板路径查询只探项目的 `UPMPackages/...` 和 `Packages/...`，不能从找到开发态模板推断任意 registry 缓存布局已验收。

这些操作不保存或重新导出 `ConfigRuntimeSO`。上传按钮在 Editor delayCall 中读取执行时已导出的 Runtime 配置，可能与 Inspector 当前未保存、未导出的 Products 不同；目录协议和一次提交边界见 [IAPProductCatalogProtocol.md](./IAPProductCatalogProtocol.md)。

### 调度完成不保证所有 Store 完成

Store 配置同类型重复时保留首条；发现顺序没有排序，`FindStore` 和 `TryGetCapability<T>` 取首个匹配实例，能力查询不额外验证启用或 Ready。反射只扫描当前已加载程序集；某程序集 `GetTypes` 出现 `ReflectionTypeLoadException` 时会跳过整个程序集。Store 类型检查不证明可构造、裁剪后仍存在或已经可支付。

`PayAsync<T>` 使用 `as T` 转换结果，T 与实际结果不兼容时可能返回 null。Restore 只聚合能转换成 T 的结果。`SetStoreEnabled` 先改运行期开关再尝试懒初始化，初始化抛错不会自动恢复开关；它不改配置源，也不取消既有交易。后台任务入口只通知取消并捕获任务异常，不等待所有任务退场、不自动重试；取消回调、Store Dispose、事件监听或打点抛错仍可能中断后续收尾。账号切换、交易取消、平台退出及发货是不同动作，不能由一个入口正常返回互相推定。
