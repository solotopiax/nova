# IAP 核心包架构文档

> 包名：`com.solotopia.nova.framework.sdk.iap`
> 最后更新：2026-06-11
> 代码入口：`Nova/Scripts/Runtime/**`

## 1. 当前架构

`IAPPlugin` 是 SDK 插件层入口，核心包只负责通用调度与契约，具体支付渠道由子包实现。

```
SDKComponent
  └── IAPPlugin : SDKPluginBase, IIAPStoreEventBridge, IIAPPlugin
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
- 标注 `[IAPStore]`

单个 Store 实例化或初始化失败只记录 Warning 并跳过，不阻断其他 Store。

## 2. 配置模型

`IAPPluginConfig` 只保存序列化数据，不实现 `IIAPProductTable`。

| 字段 / 属性 | 说明 |
|---|---|
| `EnableAlwaysPaySucceed` | 调试开关；Store 可读取 `Context.EnableAlwaysPaySucceed` 跳过真实平台支付 |
| `EnableIAPLog` | IAP 详细日志开关 |
| `RetryValidateMaxNum` | 首次验单失败后的最大重试次数，默认 3 |
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
| `IUIManager` | 默认 Loading 面板加载和显示 |
| `INetworkManager` | 网络可用性与 NetCmd 解析 |
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
- `LoadPersistData<T>` / `SavePersistData<T>` 按 `iap_{storeType}` + `data_{uid}` 读写
- `IAPLoadingGuard` 和默认 Loading 面板绑定
- `AddUnavailableSku` / `IsUnavailableSku`
- `InSubscriptionPeriod` 订阅有效期判断扩展
- `Track*` 系列支付打点方法；子 Store 可通过内部转发方法在自身服务层接入，Mobile 官方内购已接入初始化、购买、平台支付、验单相关事件。`Track*Fail` 的 reason 参数统一为 `Enum`，父包转成 `int` 写入 `nova_reason`，失败描述写入 `nova_reason_detail`。

## 5. 公开调用链

### 初始化

```
SDKManager.InitializePlugin(IAPPlugin)
  └── IAPPlugin.OnInitializeAsync(config, ct)
        ├── 校验 IAPPluginConfig
        ├── 商品表为空则跳过 Store 创建
        ├── BuildStoreContext(config)
        ├── BuildStoreConfigMap(config)
        ├── new IAPProductTableService(config.Products)
        ├── DiscoverAndInitializeStoresAsync(ct)
        └── 订阅 SDKEventData.UserLogin，登录后自动 SetUserId
```

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
SDKEventData.UserLogin → IAPPlugin.SetUserId(uid) → 广播给所有 Store
业务层调用 IAPPlugin.CheckLocalOrdersAsync(ct)
  └── 遍历 Store.CheckLocalOrdersAsync(ct)
```

`CheckLocalOrdersAsync` 可以早于登录事件调用。插件层会把本次调用缓存为延后执行事件，等 `SetUserId` 同步账号 UID 后按入队顺序回放，避免每个 Store 重复实现“等待登录后再跑”的动作缓存逻辑。扫描执行中再次触发时也会缓存一次后续事件，当前扫描结束后继续回放缓存队列。后续其他需要等待账号 UID 或等待当前流程结束的逻辑，应复用同一套事件缓存机制。

## 6. 错误码与打点 reason 分层

错误码统一通过 `IAPResult.ErrorCode` 或 `IAPInitResult.FailReason` 以 `int` 返回。

| 层级 | 枚举 | 使用场景 |
|---|---|---|
| 核心调度层 | `IAPPluginErrorCode` | request 为 null、未找到 Store、Store 未初始化、商品表缺失等核心层失败 |
| Store 层 | 各 Store 自定义 enum | 渠道内部失败，例如 Mobile 的 `IAPMobileErrorCode` |
| 初始化结果 | 各 Store 自定义 init enum | `IAPInitResult.FailReason` 仅透传具体 Store 的 int 失败码 |

核心层不再维护统一的 `IAPInitFailReason`，避免上层与具体 Store 失败原因耦合。

`nova_reason` 是打点字段，不是业务判断字段。`IAPStoreBase` 要求失败 reason 明确为枚举：

| 输入 | 上报值 |
|---|---|
| 失败原因 enum | 枚举整数值写入 `nova_reason` |
| 补充描述字符串 | 写入 `nova_reason_detail` |

因此 Store 侧必须先把失败域收敛到明确枚举，且失败原因枚举由具体 Store 定义，父包不维护跨 Store 的失败原因全集。Mobile 官方内购的支付过程失败统一使用 `IAPMobileErrorCode`：0-8 是业务返回粗粒度错误，1000-1010 是 Unity `PurchaseFailureReason` 映射号段，2000+ 是验单打点细分原因，并可读取 `nova_reason_detail` 做排查。

当前父包应保持以下边界：

- 不新增顶层 `IAPTrackFailureReason` 这类跨 Store 失败原因全集。
- 不把 Apple / Google / 第三方支付的订单号语义写进父包。
- 不把初始化失败原因和支付过程失败原因合并；初始化结果走 `IAPInitResult`，支付结果和支付打点走 Store 自己的支付过程枚举。
- `IAPStoreBase.Track*Fail` 只负责把 Store 传入的 enum 和 detail 写入打点，不做 enum 到 int 或字符串的二次规范化。

## 7. 扩展 Store 的最低要求

1. 定义请求类型，继承 `IAPRequest`。
2. 定义 Store 类型，`public`、非抽象、实现 `IIAPInternalStore`，并标注 `[IAPStore]`。
3. 如需公共模板，继承 `IAPStoreBase`。
4. 定义配置类型，实现 `IIAPStoreConfig`。
5. 在 `IAPStoreType` 增加渠道枚举值。
6. Store 的 `CanHandle` 只处理自己的请求类型。
7. 支付成功或失败必须通过 `Context.EventBridge` 上报到 `IAPPlugin.Events`。
8. Store 专有能力用 `IIAPCapable` 子接口暴露，通过 `IAPPlugin.TryGetCapability<T>` 获取。
9. Store 专属错误码需要写入本包或子包文档；不要让业务侧依赖第三方 SDK 原生 enum 的数值。

## 8. 文档边界

本目录记录当前代码事实和使用方式。历史设计、废弃方案和长期决策应进入 `Assets/Framework/Minds/**` 或子包 `Nova/Doc/plans/**` 归档，不在活跃 API 文档中混写。
