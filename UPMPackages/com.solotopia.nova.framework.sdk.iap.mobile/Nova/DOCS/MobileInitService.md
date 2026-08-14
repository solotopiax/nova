# MobileInitService

**类签名**：`internal sealed partial class MobileInitService`
**命名空间**：`NovaFramework.SDK.IAP.Mobile.Runtime`
**访问方式**：通过 `MobileServiceHub.InitService` 取得；不对外暴露

Unity IAP 5.x 初始化生命周期管理服务，负责三步初始化序列（SetController → RegisterStoreCallbacks → Connect）并在商店连接成功后标记就绪；商品信息拉取在连接成功后异步进行，不阻塞初始化完成。商品拉取状态机已收口到 `MobileProductFetchCoordinator`，InitService 只保留回调委托和初始化生命周期。

> 当前事实以 `Services/Init/*.cs` 为准。

---

## §2 文件表

| 文件 | 类 | 说明 |
|---|---|---|
| `Services/Init/MobileInitService.cs` | `MobileInitService` | 构造器 + public/internal 方法：InitializeAsync、On* 回调、Dispose |
| `Services/Init/MobileInitService.Visitors.cs` | `MobileInitService`（partial） | 字段与属性 |
| `Services/Init/MobileInitService.Methods.cs` | `MobileInitService`（partial） | 私有方法：FailInitialization、ToUnityProductType、OnProductFetchCompleted |
| `Services/Init/MobileRuntimeContext.cs` | `MobileRuntimeContext` | 初始化阶段状态机（连接态 + 初始化状态），InitService 独占使用 |
| `Services/Init/MobileStoreInitFailureReason.cs` | `MobileStoreInitFailureReason` | 初始化失败原因枚举 |
| `Services/Init/MobileStoreInitState.cs` | `MobileStoreInitState` | 初始化阶段枚举 |
| `Services/Init/MobileProductFetchCoordinator.cs` | `MobileProductFetchCoordinator` | `MobileInitService` 内部商品拉取状态机、自动重试、部分成功判定、迟到失败短路、不可用 SKU 校正；重试延迟由 `MobileStoreConfig.ProductFetchRetryDelaysMs` 注入 |
| `Services/Init/MobileProductFetchFailureSnapshot.cs` | `MobileProductFetchFailureSnapshot` | Unity IAP 商品拉取失败回调的内部快照 |
| `Services/Init/MobileProductFetchState.cs` | `MobileProductFetchState` | 商品拉取状态枚举 |

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
| `m_ProductFetchCoordinator` | `MobileProductFetchCoordinator` | 构造器初始化 | `private readonly` | 商品拉取状态机；InitService 通过委托触发和查询 |
| `ProductFetchState` | `MobileProductFetchState` | `None` | `internal get` | 从 `m_ProductFetchCoordinator.State` 读取；初始化成功不代表商品拉取完成 |
| `IsReady` | `bool` | `false` | `internal` | Unity IAP 已成功初始化（OnStoreConnected 后置 true，Dispose 后重置） |

### MobileProductFetchCoordinator

| 字段 / 属性 | 类型 | 默认值 | 访问性 | 说明 |
|---|---|---|---|---|
| `s_DefaultRetryDelaysMs` | `int[]` | `{ 2000, 5000, 10000 }` | `private static readonly` | 默认商品拉取重试延迟表，单位毫秒 |
| `m_IsReady` | `Func<bool>` | 构造器注入 | `private readonly` | 判断 Mobile IAP 当前是否仍处于可发起商品拉取的就绪状态 |
| `m_GetProductDefinitions` | `Func<IReadOnlyList<ProductDefinition>>` | 构造器注入 | `private readonly` | 获取本轮需要提交给 Unity IAP 的商品定义列表 |
| `m_FetchProducts` | `Action<IReadOnlyList<ProductDefinition>>` | 构造器注入 | `private readonly` | 向平台发起商品拉取请求 |
| `m_HasProduct` | `Func<string, bool>` | 构造器注入 | `private readonly` | 判断指定平台商品 ID 当前是否已经存在于 StoreController |
| `m_ClearUnavailableSkus` | `Action` | 构造器注入 | `private readonly` | 清空旧不可用 SKU 缓存 |
| `m_AddUnavailableSku` | `Action<string>` | 构造器注入 | `private readonly` | 写入当前仍缺失 SKU 到不可用集合 |
| `m_OnPostFetchCompleted` | `Action` | 构造器注入 | `private readonly` | 商品首次进入成功态后触发已有购买拉取和延迟权益刷新 |
| `m_DelayAsync` | `Func<int, CancellationToken, UniTask>` | `DefaultDelayAsync` | `private readonly` | 延迟执行商品重试；测试可替换 |
| `m_RetryDelaysMs` | `IReadOnlyList<int>` | `MobileStoreConfig.ProductFetchRetryDelaysMs` 或默认 `2s/5s/10s` | `private readonly` | 当前协调器使用的商品拉取重试延迟表；外部传入为空或包含非正数时回落默认值 |
| `State` | `MobileProductFetchState` | `None` | `internal get` | 当前商品拉取状态 |
| `RetryIndex` | `int` | `0` | `internal get` | 已调度的商品拉取重试次数；默认延迟表为 2s / 5s / 10s |
| `m_ProductFetchTcs` | `UniTaskCompletionSource<MobileProductFetchState>` | `null` | `private` | 商品拉取完成信号，桥接 OnProductsFetched / OnProductsFetchFailed 到等待方 |
| `m_ProductFetchRetryCts` | `CancellationTokenSource` | `null` | `private` | 商品拉取失败后的延迟重试取消源；成功、初始化失败或 Dispose 时取消 |
| `m_HasCompletedPostFetchFlow` | `bool` | `false` | `private` | 确保商品成功后置流程只在首次进入成功态时触发 |

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
// 商店连接成功：标记已连接并完成初始化，随后幂等触发商品拉取
// Fetching / Succeeded 跳过重复请求，None / Failed 允许发起拉取
internal void OnStoreConnected()

// 商店连接断开：初始化期间断开则触发失败流程
internal void OnStoreDisconnected(StoreConnectionFailureDescription description)

// 商品拉取成功：委托 MobileProductFetchCoordinator 处理状态、SKU 校正和后续流程一次性触发
internal void OnProductsFetched(List<Product> products)

// 商品拉取失败：先物化 MobileProductFetchFailureSnapshot，再委托 MobileProductFetchCoordinator 处理
internal void OnProductsFetchFailed(ProductFetchFailed failure)

// 等待商品拉取完成；超时返回当前状态，不抛出超时异常
internal UniTask<MobileProductFetchState> WaitForProductsFetchedAsync(int timeoutMs, CancellationToken ct)
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

MobileProductFetchState 重入规则：
  None       → 发起商品拉取
  Fetching   → 跳过重复请求，保留当前完成信号
  Succeeded  → 跳过重复请求，继续使用已拉取商品
  Failed     → 允许自动重试或后续连接回调重新拉取
```

### 商品拉取失败 SKU 规则

`m_UnavailableSkus` 的语义是“当前 `StoreController` 仍查不到的 SKU”，不是 Unity IAP 失败回调原始列表的镜像：

| 回调顺序 / 场景 | 状态处理 | SKU 处理 | 后续流程 |
|---|---|---|---|
| 全失败 | `ProductFetchState=Failed` | 将 Controller 仍缺失的失败 SKU 写入不可用集合 | 调度下一轮重试，最多 3 次 |
| 失败后成功 | `ProductFetchState=Succeeded` | 成功时清理旧失败 SKU，并按 Controller 状态恢复仍缺失 pending SKU | 触发一次 FetchPurchases 和延迟权益刷新 |
| 成功后迟到失败 | 保持 `Succeeded` | 只补写 Controller 仍缺失的 SKU | 不重试，不重复触发商品成功后置流程 |
| 部分成功（失败数 < 请求数） | 视为 `Succeeded` | 只保留 Controller 仍缺失 SKU | 停止重试并触发 FetchPurchases 和延迟权益刷新 |

该规则避免两类问题：旧失败 SKU 在重试成功后继续拦截购买；Unity IAP 迟到失败回调把已进入 Controller 的商品重新标记为不可买。

商品成功后的后续流程可能补跑之前因商品未就绪而延后的权益刷新。`RefreshEntitlementsAsync` 返回 `UniTask<IReadOnlyList<IAPResult>>`，返回列表只用于 Restore / 权益聚合语义；当它作为后台补跑动作接入 `MobileServiceHub.RunBackgroundTask` 时，必须用 lambda 或无返回包装方法显式 `await` 并丢弃结果，不能直接把方法组传入只接受 `Func<CancellationToken, UniTask>` 的后台任务入口。

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
        ├─ 4. 构建 m_PendingProductDefs（遍历 table.Products → 去重 ProductID → ToUnityProductType 转换）
        │
        ├─ 5. await ExtendedService.Connect()
        │     → 成功 → OnStoreConnected（由 MobileStoreService 路由）
        │               ExtendedService.RegisterProductCallbacks()（StoreService 先注册商品级回调）
        │               MarkConnected()
        │               MarkReady()
        │               IsReady=true
        │               m_InitTcs.TrySetResult(true)
        │               MobileProductFetchCoordinator.StartFetchIfAllowed()
        │               → OnProductsFetched 清理旧失败 SKU，并按 Controller 状态恢复仍缺失的 pending SKU，标记 ProductFetchState=Succeeded 后补跑延迟权益刷新并调用 FetchPurchases()
        │               → OnProductsFetchFailed 先物化失败快照，只记录 StoreController 当前仍缺失的 SKU；整体失败时按 MobileStoreConfig.ProductFetchRetryDelaysMs 自动重试，默认 2s/5s/10s；任一成功回调会取消后续重试
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

商品拉取已经从初始化阻塞链路中拆出，并进一步收口到 `MobileProductFetchCoordinator`。`OnProductsFetchFailed` 不会把已经连接成功的商店回退为初始化失败；它会先物化 Unity IAP 失败回调，再检查 `StoreController` 当前是否已能查询到对应商品，只把仍缺失的 SKU 写入 `m_UnavailableSkus`。整体失败时会按 `MobileStoreConfig.ProductFetchRetryDelaysMs` 自动重试，默认 2s / 5s / 10s 共 3 次。配置为空或包含非正数时回落默认值并打印中文警告日志。若任一轮收到 `OnProductsFetched`，或失败回调中的失败数量小于本轮请求数量，即认为至少有商品信息已可用，取消后续重试并触发 FetchPurchases 和延迟权益刷新。成功态下迟到的失败回调只补记录真实缺失 SKU，不回退状态、不重试、不重复触发商品成功后置流程。启动期不会调用 `RestoreTransactions`，该平台 Restore 入口仅在用户主动恢复购买时触发。

**误区 4：同一个平台 ProductID 需要重复注册给 Unity IAP**

Nova 商品表允许不同 `TableId` 复用同一个 Google Play / App Store 平台 `ProductID`。Unity IAP 只要求传入的 `ProductDefinition.id` 唯一，因此初始化构建商品定义时会跳过空 `ProductID`，并对复用的 `ProductID` 只注册一次平台商品定义。

**误区 5：重复收到 OnStoreConnected 时需要再次并发拉取商品**

Unity IAP 同一时刻只允许一个商品拉取请求。`MobileProductFetchCoordinator.StartFetchIfAllowed` 会在 `ProductFetchState` 为 `Fetching` 或 `Succeeded` 时直接返回，避免替换当前完成信号或重复调用平台；只有 `None` 和 `Failed` 才允许发起请求，因此真实失败后的自动重试或重连回调仍可重新拉取。重新发起拉取前会清理上一轮失败 SKU 缓存，完整成功回调也会清理旧失败 SKU，并立刻按 Controller 状态恢复仍缺失的 pending SKU；随后若 Unity IAP 又发出迟到失败回调，也只会把 Controller 中仍缺失的 SKU 重新标记为不可用。

**误区 6：internal/private 成员可以不写注释**

本模块新增或修改的成员级注释遵循全仓 OPS 红线：类型、枚举、字段、属性、构造器和方法不论访问级别，都要写中文 XML 注释；固定文件头继续沿用仓库英文模板标签，`descrip` 后的说明内容使用中文。运行期日志和测试断言消息也使用中文，代码符号、API 名称和平台名保持原名。简单表达式体、短三元表达式和短属性 getter 在不牺牲可读性时保持一行。

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
