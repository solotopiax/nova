# IAP 核心包架构文档

> 包名：`com.solotopia.nova.framework.sdk.iap`
> 最后更新：2026-09-17
> 代码入口：[Nova/Scripts/Runtime/**](../Scripts/Runtime/)

## 1. 当前架构

`IAPPlugin` 是 SDK 插件层入口，核心包只负责通用调度与契约，具体支付渠道由子包实现。

```
SDKComponent
  └── IAPPlugin : SDKPluginBase, IIAPStoreEventBridge, IIAPPlugin,
                  ISDKPauseListener, ISDKFocusListener,
                  ISDKLoginReceiver, ISDKInitializationDependencies
        ├── IAPPluginConfig              // 序列化配置数据
        ├── IAPProductTableService       // 运行期商品表查询服务
        ├── IAPStoreContext              // Store 运行期依赖容器
        └── List<IIAPInternalStore>      // 反射发现的渠道 Store
              ├── MobileStore            // sdk.iap.mobile
              ├── ThirdPayStore          // sdk.iap.thirdpay
              └── VoucherStore           // sdk.iap.voucher
```

`IAPPlugin` 初始化时扫描当前 `AppDomain` 中所有程序集，只实例化满足以下条件的类型：

- `public class`
- 非抽象
- 实现 `IIAPInternalStore`
- 标注 `[IAPStore(IAPStoreType.Xxx)]`，静态声明对应 StoreType

扫描阶段先按 Attribute 中的 StoreType 查询 `IAPPluginConfig.StoreConfigs`；未配置的可选 Store 不执行构造函数或字段初始化。已配置但 `Enabled=false` 的 Store 仍会构造并保留，以维持 `SetStoreEnabled(...)` 的运行时懒启用语义。单个 Store 实例化或初始化的普通异常只记录 Warning 并跳过，不阻断其他 Store；初始化的 `OperationCanceledException` 仍向上传播。

插件通过 `ISDKInitializationDependencies` 声明对 `ITrackPlugin` 的可选依赖：已启用时等待其初始化结果，缺失或失败不作为必需依赖阻断支付。`Priority=70` 用于稳定遍历及释放顺序，不决定初始化先后。账号通过 `ISDKLoginReceiver.OnSDKLogin(userId, sessionId)` 交付，IAP 自身不订阅登录事件。

应用 Pause/Focus 生命周期统一由 `SDKComponent` 采集并广播给 `IAPPlugin`。IAPPlugin 通过 `IIAPStorePauseListener` / `IIAPStoreFocusListener` 可选接口逐 Store 转发，渠道包不再自行创建 `MonoBehaviour` 生命周期代理。转发不按 Store 启用状态过滤，是否响应由 Store 当前会话状态自行判断，避免运行中禁用 Store 时丢失已有会话的返回事件。

## 2. 配置模型

`IAPPluginConfig` 只保存序列化数据，不实现 `IIAPProductTable`。

| 字段 / 属性 | 说明 |
|---|---|
| `EnableAlwaysPaySucceed` | Editor 调试开关；仅 Editor 编译态允许 Store 读取 `Context.EnableAlwaysPaySucceed` 跳过真实平台支付，非 Editor 编译态强制为 false |
| `EnableIAPLog` | IAP 详细日志开关，默认开启；初始化时写入 `IAPLog.SetEnabled` |
| `RetryValidateMaxNum` | Store 共享的首次验单失败后重试次数配置，默认 3；具体 Store 可按验单来源覆盖，例如 ThirdPay 明确支付成功会覆盖完整重试间隔序列，关闭/返回等不明确场景最多 3 次 |
| `SkipLoadingForReplenish` | 启动补单是否跳过 Loading |
| `LoadingPanelPrefab` | 支付期 Loading 面板 Resources 路径，默认 `IAP/IAPLoadingPanel` |
| `StoreConfigs` | `[SerializeReference]` 多态 Store 配置列表 |
| `Products` | 内联商品表条目列表 |

初始化时 `IAPPlugin` 用 `Products` 构建 `IAPProductTableService`，并把该服务以 `IIAPProductTable` 注入各 Store。

## 3. 运行期上下文

`IAPStoreContext` 由 `IAPPlugin.BuildStoreContext` 构造并注入 Store，包含：

| 依赖 | 用途 |
|---|---|
| `IFileFragmentManager` | Store 持久化读写 |
| `ITrackPlugin` | 支付打点 |
| `INetworkManager` | 网络可用性与 NetCmd 解析 |
| `EnableAlwaysPaySucceed` | 仅 Editor 编译态有效的始终支付成功调试开关；非 Editor 编译态由 `IAPPlugin` 强制注入 false |
| `DevelopMode` | 来自 `IConfigManager.DevelopMode`，供 Store 打点 Debug 字段判断 |
| `IIAPStoreEventBridge` | Store 向 `IAPPlugin.Events` 上报初始化、支付、Restore 结果 |

## 4. Store 基类约定

所有 Store 应继承 `IAPStoreBase` 或完整实现 `IIAPInternalStore`。

`IAPStoreBase` 提供：

- `InitializeAsync` 注入 `IIAPProductTable` 和 `IIAPStoreContext`
- `EnableAsync` 懒初始化入口
- `SetEnabled` 运行期启停
- `PayGuardAsync` 支付前置模板：启用状态、初始化状态、防重入、商品表校验
- `SetUserId` 账号 UID 写入
- `GetNetCmdUrl(dtName)` 按已加载 NetCmd 行的 `Name` 解析 URL；不带表名前缀，缺少 NetworkManager、行或 Host 配置时返回 null。该 helper 不发送请求。
- `LoadPersistData<T>` / `SavePersistData<T>` 按 `iap_{storeType}` + `data_{uid}` 读写
- `IAPLoadingGuard` 和默认 Loading 面板绑定
- `IAPLogOwner` 日志收口；Store 和 Service 只声明固定 `LogTag`，日志底层不依赖 `IAPStoreType`
- `AddUnavailableSku` / `IsUnavailableSku`
- `InSubscriptionPeriod` 订阅有效期判断扩展
- 初始化、购买和公共 `PayGuardAsync` 失败打点，以及通用属性构造与发送能力。订单号、验单状态和渠道失败枚举由具体 Store 的 `.Track.cs` 构造，父包不解释渠道订单语义。

`IAPStoreBase.Track.cs` 只提供 `CreateTrackProperties`、`AppendTrackProperties` 和 `EmitTrackEvent` 供 Store 复用，并保留初始化、购买及默认 guard 失败入口。Mobile、ThirdPay、Voucher 各自在自己的 `.Track.cs` 定义业务事件和字段，渠道细分枚举必须写入各自专属属性，不能复用同一个属性承载不同枚举域。

`IAPStoreBase` partial 文件职责固定如下：

| 文件 | 职责 |
|---|---|
| `IAPStoreBase.cs` | public/abstract 调用面、生命周期入口和业务侧可见方法 |
| `IAPStoreBase.Visitors.cs` | 字段、状态属性、protected 属性和常量 |
| `IAPStoreBase.Methods.cs` | protected/private/internal 模板方法与辅助方法，包括 `PayGuardAsync`、持久化模板、SKU 过滤和日志辅助 |
| `IAPStoreBase.Track.cs` | 支付打点封装 |
| `IAPStoreBase.Net.cs` | 通用网络请求能力 |

新增非 public helper 时写入 `.Methods.cs`，新增成员状态时写入 `.Visitors.cs`；无后缀文件不承载 protected/private/internal 辅助方法或成员字段。

## 5. 公开调用链

### 初始化

```
SDKManager.InitializePlugin(IAPPlugin)
  └── IAPPlugin.OnInitializeAsync(config, ct)
        ├── 校验 IAPPluginConfig
        ├── 应用 EnableIAPLog 到 IAPLog.SetEnabled
        ├── 商品表为空则跳过 Store 创建
        ├── BuildStoreContext(config)
        ├── BuildStoreConfigMap(config)
        ├── new IAPProductTableService(config.Products)
        ├── DiscoverAndInitializeStoresAsync(ct)
        └── 返回后由 SDKManager 标记插件 Ready，并补交当前登录账号
```

`Store.InitializeAsync` 的完成语义由各 Store 定义。Mobile Store 在创建服务并启动运行期后台连接后即返回，所以 `Nova.SDK.InitializeTask` 不会等待 Google Play / App Store 的 `Connect()` 回调；Mobile 连接结果由 `IAPPlugin.Events.InitResult` 派发，连接成功前支付由 Store Ready 门禁拦截。

### 支付

```
业务层构造 IIAPRequest
  └── IAPPlugin.PayAsync<T>(request, ct)
        ├── request null → IAPPluginErrorCode.StoreNotAvailable
        ├── FindStore(request as IAPRequest)
        ├── 无匹配 Store → IAPPluginErrorCode.StoreNotAvailable
        └── store.PayAsync(iapRequest, ct)
              └── Store 内部通常经 PayGuardAsync 后进入渠道支付
```

### 补单

```
Nova.SDK.Login(uid, userProperties)
  └── SDKManager（插件 Ready 后交付或补交最新账号）
        └── IAPPlugin.OnSDKLogin(uid, sessionId) → SetUserId(uid) → 广播给所有 Store
业务层调用 IAPPlugin.CheckLocalOrdersAsync(ct)
  └── 遍历 Store.CheckLocalOrdersAsync(ct)
```

`CheckLocalOrdersAsync` 可以早于登录事件调用。插件层会记录一次延后补单请求，等 `SetUserId` 同步账号 UID 后通过 IAPPlugin 后台任务入口自动执行，避免每个 Store 重复实现“等待登录后再跑”的动作缓存逻辑。扫描执行中再次触发时只标记当前轮结束后补跑一轮，不并发启动多轮补单扫描，也不无上限堆积同类补单事件；这项扫描门禁不覆盖用户的 `PayAsync` 或平台迟到回调。`OnDisposeAsync` 会先取消后台任务，再释放各 Store，避免登录后自动补单在插件释放后继续访问旧 Store。

Android ThirdPay 还会在用户重复点击时执行购买前查询。它由 ThirdPay 的开关与时间窗口控制：满足查询条件时，只有服务端明确查询成功且未命中当前商品才继续新购，查询失败会结束本次购买；关闭开关或不在有效窗口时，则清理当前业务旧记录后正常新购。独立后台补单仍合并本地和服务端订单，服务端查询失败时可继续校验本地订单，不能直接复用它来替代严格的购买前查询。两条入口共用验单能力，逐笔成功与批次完成的返回语义见 [ThirdPay 购买前补单规则](../../README.md#跨包文档定位)（`com.solotopia.nova.framework.sdk.iap.thirdpay` 包内 `Nova/Doc/ThirdPayStore.md#android-重复点击补单查询`）。

后台任务统一入口的签名是 `Func<CancellationToken, UniTask>`。返回 `UniTask<T>` 的 Store 内部方法不能直接以方法组接入后台入口；需要用 lambda 或无返回包装方法显式 `await` 并丢弃结果，保证后台任务只承载取消和异常收口，不改变原方法的业务返回语义。

## 6. 错误码与打点 reason 分层

错误码统一通过 `IAPResult.ErrorCode` 或 `IAPInitResult.FailReason` 以 `int` 返回。

| 层级 | 枚举 | 使用场景 |
|---|---|---|
| 核心调度层 | `IAPPluginErrorCode` | request 为 null、未找到 Store、Store 未初始化、商品表缺失等核心层失败 |
| Store 层 | 各 Store 自定义 enum | 渠道内部失败，例如 Mobile 的 `IAPMobileErrorCode` |
| 初始化结果 | 各 Store 自定义 init enum | `IAPInitResult.FailReason` 仅透传具体 Store 的 int 失败码 |

核心层不再维护统一的 `IAPInitFailReason`，避免上层与具体 Store 失败原因耦合。

`nova_reason` 是打点字段，不是业务判断字段。具体 Store 必须先把失败原因收敛到明确枚举，再把枚举整数写入事件：

| 输入 | 上报值 |
|---|---|
| 失败原因 enum | 枚举整数值写入 `nova_reason` |
| 补充描述字符串 | 写入 `nova_reason_detail` |

因此 Store 侧必须先把失败域收敛到明确枚举，且失败原因枚举由具体 Store 定义，父包不维护跨 Store 的失败原因全集。Mobile 官方内购的支付过程失败统一使用 `IAPMobileErrorCode`：0-9 是业务返回粗粒度错误，1000-1010 是 Unity `PurchaseFailureReason` 映射号段，2000+ 是验单打点细分原因，并可读取 `nova_reason_detail` 做排查。

`IAPStoreBase.PayGuardAsync` 对禁用、未就绪、重入和商品表缺失会返回 `ErrorSource.PluginRouter` + `IAPPluginErrorCode`。默认 Store 由基类在 guard 失败完成路径上补打 `nova_iap_local_pay_fail`；需要把 guard 失败映射到自身错误码域的 Store 可以覆写 `ShouldTrackPayGuardFailure`，并在自身 `PayAsync` 返回边界统一上报。

当前父包应保持以下边界：

- 不新增顶层 `IAPTrackFailureReason` 这类跨 Store 失败原因全集。
- 不把 Apple / Google / 第三方支付的订单号语义写进父包。
- 不把初始化失败原因和支付过程失败原因合并；初始化结果走 `IAPInitResult`，支付结果和支付打点走 Store 自己的支付过程枚举。
- 具体 Store 的 `.Track.cs` 负责把渠道 enum 和 detail 写入打点；父包只在默认 guard 失败入口处理通用 `IAPResult`。

### 支付打点三维属性

所有客户端支付事件在 `IAPStoreBase.EmitTrackEvent` 发送前统一写入以下属性；商品属性构造与 `IAPPlugin` 的未匹配 Store 失败分支复用同一构造方法。

| 属性 | 来源 | 字符串取值 |
|---|---|---|
| `nova_platform` | Store 初始化时的 `IConfigManager.Platform` | `Android`、`iOS`、`WebGL` |
| `nova_channel` | Store 初始化时的 `IConfigManager.Channel`，表示包体分发与运营渠道 | `Official`、`Google`、`Apple`、`WeChat`、`TikTok`、`Alipay` |
| `nova_store_type` | 实际 `StoreType`；路由失败时取请求中的类型 | `Mobile`、`ThirdPay`、`Voucher` |

三者使用枚举名称字符串并保留大小写，不发送枚举整数或小写别名。平台和渠道取自已导出的运行配置，不通过编译平台或支付商店推断。

#### 初始化缓存与刷新

- **Store 初始化时缓存三项名称**：`IAPStoreBase.InitializeAsync` 在保存上下文后调用 `CacheTrackDimensions`，读取一次配置，并将平台、运营渠道和商店类型转换为字符串。`CreateTrackProperties` 和 `EmitTrackEvent` 后续只复制缓存值，不查询配置 Manager，也不再转换这三个枚举。
- **插件单独缓存路由坐标**：`IAPPlugin.BuildStoreContext` 缓存平台和运营渠道，用于未匹配到 Store 的失败事件。此时还没有实际 Store，`nova_store_type` 必须取当次请求的类型，不能固定为某个商店；请求为空或没有提供类型时为 `None`。
- **缓存按实例、按初始化周期持有**：显式再次调用 Store 的 `InitializeAsync` 会刷新缓存；插件重新构造运行上下文时刷新自己的缓存。仅调用 `SetEnabled`，或对已经初始化的 Store 调用 `EnableAsync`，不会刷新坐标。不使用跨实例静态缓存。
- **初始化前先加载配置**：初始化时配置缺失或尚未加载，会缓存 `None`，之后发送事件不会自动补读。应沿正常启动链先完成运行配置加载，再初始化支付；配置修正后必须进入新的初始化周期才能获得新快照。


`TrackChannel` 保留为可重写的兼容入口，默认返回初始化时缓存的渠道名称；旧子类即使重写它，也不能覆盖发送时的三维属性。其余事件字段、失败原因、去重与发送时机保持既有语义。分析后台若按原来的 `google`、`ios`、`thirdpay`、`voucher`、`mobile` 或 `router` 筛选 `nova_channel`，应改用新的渠道名称，并用 `nova_store_type` 区分支付方式；历史数据不会自动改写。

集中登记仍在本包 [Nova/Tracks/Tracks.xlsx](../Tracks/Tracks.xlsx)。`pay_success`、`af_purchase` 和 `af_purchase_sandbox_s2s` 属于服务端事件约定；更新客户端不会自动修改服务端的上报实现。

## 7. 扩展 Store 的最低要求

1. 定义请求类型，继承 `IAPRequest`。
2. 定义 Store 类型，`public`、非抽象、实现 `IIAPInternalStore`，并标注 `[IAPStore(IAPStoreType.Xxx)]`。
3. 如需公共模板，继承 `IAPStoreBase`。
4. 定义配置类型，实现 `IIAPStoreConfig`。
5. 在 `IAPStoreType` 增加渠道枚举值。
6. Store 的 `CanHandle` 只处理自己的请求类型。
7. 支付成功或失败必须通过 `Context.EventBridge` 上报到 `IAPPlugin.Events`。
8. Store 专有能力用 `IIAPCapable` 子接口暴露，通过 `IAPPlugin.TryGetCapability<T>` 获取。
9. Store 专属错误码需要写入本包或子包文档；不要让业务侧依赖第三方 SDK 原生 enum 的数值。

## 8. 文档边界

本目录记录当前代码事实和使用方式。新的长期决策由 Nova 源码仓维护者按仓库知识规则处理；消费项目无需开发仓知识目录。当前 API 细节与本轮执行、验收记录不另建归档或写入历史计划目录。

## 9. 基类模板的实际责任边界

`IAPStoreBase.InitializeAsync` 保存 Table/Context、复位支付 ID 和不可用 SKU、设置初始化标志并绑定默认 Loading；它不校验空依赖，也不代表平台连接成功。基类 `IsStoreReady` 默认 true，具体 Store 必须提供自己的门禁。`EnableAsync` 只在未初始化时委托初始化，启用标志由 `SetEnabled` 独立修改；基类 Restore 返回空列表、补单和订阅倒计时默认为空实现、订阅期限默认 0，需要渠道实现覆盖。

`PayGuardAsync` 依次检查禁用、`IsStoreReady`、当前支付中和已配置商品；只有 Table 非空时才检查商品存在。模板不直接读取 ct，不自动设置支付中标志，也不保护 null request/payCore，取消、超时和平台调用由子类承担。失败时先补 StoreType，再发失败事件、按钩子打点，最后执行失败后置钩子；事件或打点异常会中断后续步骤，不能假定必然得到失败返回值。

### Loading 的配对与所有权

`IAPLoadingGuard` 的默认 `UseCommonLoading=true`、`GameStartShowCommonLoading=false`，交互标志决定当前 `ShouldShow`。每次成功 Push 先调用显示回调，再把**当时捕获**的隐藏回调排入队列；Pop 先出队再调用，不是按回调对象实时重新查找。重新 Bind 不改已排队隐藏回调，Clear 只丢弃队列而不逐个隐藏。显示回调抛错不会排入对应 Pop，隐藏回调抛错时该项已经出队；无参 Push/Pop 读取各自调用时的显示策略，调用者需保持策略与配对一致。

默认 `IAPLoadingPanelPresenter` 在第一次 Show 时通过 Resources 懒加载、实例化并 DontDestroyOnLoad；Hide 只隐藏并保留实例，Dispose 才请求 Unity Destroy。缺 Prefab 只警告并返回，其它 Unity/UI 异常会传播；不能据入口调用推断面板已显示或对象已在当前帧销毁。此处使用 Unity 主线程与 Resources 生命周期，不是 YooAsset Handle。

### 从内存到存档及日志

存档键由当前 StoreType 和当前 UID 即时拼接；UID 为空或缺持久化管理器时 Load/Save 不读写匿名账号。`SetUserId` 忽略 null/空值，传空不是登出或清除旧 UID。Load 在反序列化异常时尝试创建空容器并 `EnsureInitialized`，可能在内存迁移集合；Save 先 `SetObject` 再 `Save(classify)`，并不提供设备级原子写、跨对象回滚或账号 generation 隔离。旧异步任务若读取当前 Hub/Store，可能看到切换后的 UID 与存档；接入方须按实际交易归属安排切号和退场。

公共属性合并会覆盖同名已有键；价格打点按支付表 Price 的 InvariantCulture float 解析，失败回 0，该工具本身不拒负数、NaN 或 Infinity，也不读取平台实际结算价。Enum 转 int 未限制底层范围，超出 Int32 可抛错；追踪调用不保证远端接收、去重或重试。IAP 日志总开关会同时关闭 Debug、Warning、Error；存档、协议、票据和回调诊断可能包含原文，不自动脱敏，日志缺失也不能证明流程没有执行。
