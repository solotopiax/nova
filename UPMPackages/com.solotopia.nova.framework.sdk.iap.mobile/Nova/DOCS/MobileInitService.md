# MobileInitService

**类签名**：`internal sealed partial class MobileInitService`
**命名空间**：`NovaFramework.SDK.IAP.Mobile.Runtime`
**访问方式**：通过 `MobileServiceHub.InitService` 取得；不对外暴露

Unity IAP 5.x 初始化生命周期管理服务，负责三步初始化序列（SetController → RegisterStoreCallbacks → Connect）并在商店连接成功后标记就绪；商品信息拉取在连接成功后异步进行，不阻塞初始化完成。

> 当前事实以 `Services/Init/*.cs` 为准。

---

## §2 文件表

| 文件 | 类 | 说明 |
|---|---|---|
| `Services/Init/MobileInitService.cs` | `MobileInitService` | 构造器 + public/internal 方法：InitializeAsync、On* 回调、Dispose |
| `Services/Init/MobileInitService.Visitors.cs` | `MobileInitService`（partial） | 字段与属性 |
| `Services/Init/MobileInitService.Methods.cs` | `MobileInitService`（partial） | 私有方法：FetchProducts、FailInitialization、ToUnityProductType |
| `Services/Init/MobileRuntimeContext.cs` | `MobileRuntimeContext` | 初始化阶段状态机（连接态 + 初始化状态），InitService 独占使用 |
| `Services/Init/MobileStoreInitFailureReason.cs` | `MobileStoreInitFailureReason` | 初始化失败原因枚举 |
| `Services/Init/MobileStoreInitState.cs` | `MobileStoreInitState` | 初始化阶段枚举 |

---

## §3 继承关系

```
internal sealed partial class MobileInitService
    （无继承，通过 MobileServiceHub 接收跨服务依赖）
```

---

## §4 关键字段表

### MobileInitService（MobileInitService.Visitors.cs）

| 字段 / 属性 | 类型 | 默认值 | 访问性 | 说明 |
|---|---|---|---|---|
| `m_Hub` | `MobileServiceHub` | — | `private readonly` | 服务容器，持有共享外部依赖与其他服务引用 |
| `m_RuntimeContext` | `MobileRuntimeContext` | `null` | `private` | 初始化阶段状态机；Dispose 后置 null，阻止后续回调继续执行 |
| `m_InitTcs` | `UniTaskCompletionSource<bool>` | `null` | `private` | 初始化完成信号，桥接 OnStoreConnected / FailInitialization 到 InitializeAsync 的 await 点 |
| `m_PendingProductDefs` | `List<ProductDefinition>` | `null` | `private` | InitializeAsync 阶段构建，OnStoreConnected 后触发 FetchProducts 时使用 |
| `IsReady` | `bool` | `false` | `internal` | Unity IAP 已成功初始化（OnStoreConnected 后置 true，Dispose 后重置） |

### MobileRuntimeContext

| 字段 / 属性 | 类型 | 默认值 | 访问性 | 说明 |
|---|---|---|---|---|
| `Controller` | `StoreController` | `null` | `internal` | Unity IAP 商店控制器，BeginInitialization 写入；已迁移到 ExtendedService，此处仅保留状态上下文 |
| `LastInitFailureReason` | `MobileStoreInitFailureReason` | `None` | `internal` | 最近一次初始化失败原因 |
| `LastInitFailureMessage` | `string` | `string.Empty` | `internal` | 最近一次初始化失败详情 |
| `IsReady` | `bool`（属性） | — | `internal` | `m_InitState == Ready && m_Connected` |
| `IsInitializing` | `bool`（属性） | — | `internal` | `m_InitState == Initializing` |
| `IsFailed` | `bool`（属性） | — | `internal` | `m_InitState == Failed` |

---

## §5 完整公开 API

### MobileInitService — 初始化流程

```csharp
// 流程入口（由 MobileStore.InitializeAsync 调用）
// 三步序列：SetController → RegisterStoreCallbacks → Connect → 后台商品拉取
internal async UniTask<bool> InitializeAsync(IIAPProductTable table, CancellationToken ct)
```

### MobileInitService — 平台事件接收（MobileStoreService 路由过来）

```csharp
// 商店连接成功：标记已连接并完成初始化，随后触发商品拉取
internal void OnStoreConnected()

// 商店连接断开：初始化期间断开则触发失败流程
internal void OnStoreDisconnected(StoreConnectionFailureDescription description)

// 商品拉取成功：商品信息可用后触发一次平台恢复交易和已有购买拉取，不改变初始化结果
internal void OnProductsFetched(List<Product> products)

// 商品拉取失败：记录不可用 SKU，不改变初始化结果
internal void OnProductsFetchFailed(ProductFetchFailed failure)
```

### MobileInitService — 生命周期

```csharp
// 释放服务：通知 ExtendedService 清空 Controller，重置状态，释放 TCS
internal void Dispose()
```

### MobileRuntimeContext — 状态机方法

```csharp
// 开始新一轮初始化，写入 Controller，切换到 Initializing 状态
internal void BeginInitialization(StoreController controller)

// 标记商店连接成功
internal void MarkConnected()

// 标记商店连接断开
internal void MarkDisconnected()

// 标记初始化完成（Ready 状态）
internal void MarkReady()

// 幂等地标记初始化失败；已处于 Ready/Failed 时返回 false
internal bool TryMarkFailed(MobileStoreInitFailureReason reason, string detail)
```

---

## §6 初始化状态机

```
MobileStoreInitState 枚举：
  None        → 初始状态，尚未调用 InitializeAsync
  Initializing→ BeginInitialization 调用后，等待商店连接
  Ready       → OnStoreConnected 触发，商店连接成功
  Failed      → StoreController 创建失败、Connect 异常、初始化期间断连或取消

MobileStoreInitFailureReason 枚举：
  None                       = 0 （未失败）
  PurchasingUnavailable      = 1 （平台内购服务不可用的通用兜底）
  StoreControllerUnavailable = 2 （Unity IAP StoreController 创建失败）
  StoreConnectException      = 3 （Unity IAP Connect 调用抛出异常）
  StoreDisconnected          = 4 （初始化期间商店连接断开）
  InitializationCanceled     = 5 （初始化被取消）
```

### 初始化时序（三步序列）

```
MobileStore.InitializeAsync
  └── MobileInitService.InitializeAsync(table, ct)
        │
        ├─ 1. new MobileRuntimeContext()；new UniTaskCompletionSource<bool>()
        │
        ├─ 2. StoreController controller = UnityIAPServices.StoreController()
        │     m_RuntimeContext.BeginInitialization(controller)
        │
        ├─ 3. ExtendedService.SetController(controller)
        │     ExtendedService.RegisterStoreCallbacks()
        │
        ├─ 4. 构建 m_PendingProductDefs（遍历 table.Products → ToUnityProductType 转换）
        │
        ├─ 5. await ExtendedService.Connect()
        │     → 成功 → OnStoreConnected（由 MobileStoreService 路由）
        │               ExtendedService.RegisterProductCallbacks()（StoreService 先注册商品级回调）
        │               MarkConnected()
        │               MarkReady()
        │               IsReady=true
        │               m_InitTcs.TrySetResult(true)
        │               FetchProducts()（后台商品拉取）
        │               → OnProductsFetched 后调用 RestoreTransactions() + FetchPurchases()
        │               → OnPurchasesFetched 路由到 RestoreService 恢复 PendingOrder 票据
        │     → Connect 抛出异常 → FailInitialization(StoreConnectException)
        │     → 取消 → FailInitialization(InitializationCanceled)
        │
        ├─ 6. 等待 m_InitTcs.Task
        │     → OnStoreConnected 触发：MarkReady()，IsReady=true，m_InitTcs.TrySetResult(true)
        │     → OnStoreDisconnected 初始化期间断连：FailInitialization(StoreDisconnected)
        │
        └─ 返回 bool（true = Ready，false = Failed）
```

---

## §10 常见误区

**误区 1：认为 Controller 由 InitService 持有**

MobileInitService 在旧版中直接持有 `IStoreController / IExtensionProvider`。重构后 StoreController 已完全迁移到 `MobileExtendedService`；InitService 仅通过 `m_RuntimeContext` 管理连接态和初始化状态，不直接调用 Controller 方法。

**误区 2：直接读取 MobileRuntimeContext.Controller**

`MobileRuntimeContext.Controller` 仅在 `BeginInitialization` 写入，供 ExtendedService 注入时参考；其他 Service 应通过 `m_Hub.ExtendedService` 操作 StoreController，不可绕过。

**误区 3：OnProductsFetchFailed 会回退初始化结果**

商品拉取已经从初始化阻塞链路中拆出。`OnProductsFetchFailed` 只记录失败商品到 `m_UnavailableSkus`，不会把已经连接成功的商店回退为初始化失败；具体商品支付时仍由商品可用性检查拦截。

---

## §11 使用示例

```csharp
// 以下为 MobileStore.InitializeAsync 内部调用片段，说明 InitService 的典型用法
// （业务层无需直接使用 InitService，通过 MobileStore 生命周期管理）

// 1. Hub 构建完成后，调用 InitService 启动初始化
bool ok = await m_Hub.InitService.InitializeAsync(table, ct);
if (!ok)
{
    // Unity IAP 初始化失败，MobileStore.IsStoreReady 返回 false
    // 支付调用会被 PayGuardAsync 拦截，返回 StoreInitFailed 错误码
    return;
}

// 2. InitService.IsReady == true 后，ExtendedService.IsAttached 也一定为 true
// MobileStore.IsStoreReady = InitService.IsReady && ExtendedService.IsAttached
```

---

## §13 关联文档

- MobileStore 主类文档：`./MobileStore.md`
- 内部服务架构总览：`./MobileIAP-Architecture.md`
- 旧版设计文档（归档入口）：`./MobileStore-Design.md`
