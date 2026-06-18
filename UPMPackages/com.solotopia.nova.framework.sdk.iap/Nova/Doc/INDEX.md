# Nova Framework - SDK - IAP 文档索引

> 本包是 Nova IAP 核心包，负责调度多渠道 Store、提供通用契约和运行期上下文。
> 当前事实以 `Nova/Scripts/Runtime/**` 与本目录活跃文档为准。

## 业务侧公开 API

| 类型 | 命名空间 | 说明 | 文档 |
|---|---|---|---|
| `IAPPlugin` | `NovaFramework.SDK.IAP.Runtime` | SDK 插件入口；提供支付、恢复购买、补单扫描、Store 启停与能力查询 | [IAPPlugin.md](./IAPPlugin.md) |
| `IAPPluginConfig` | `NovaFramework.SDK.IAP.Runtime` | 插件序列化配置；保存调试开关、Loading 面板路径、Store 配置列表和内联商品表 | [IAPPlugin.md](./IAPPlugin.md) |
| `IAPStoreBase` | `NovaFramework.SDK.IAP.Runtime` | Store 抽象基类；封装防重入、Loading、账号、持久化、SKU 过滤和打点 | [IAP-Architecture.md](./IAP-Architecture.md) |
| `IIAPInternalStore` | `NovaFramework.SDK.IAP.Runtime` | 子 Store 内部统一接口，由 `IAPPlugin` 反射发现和路由 | [IAP-Architecture.md](./IAP-Architecture.md) |
| `IIAPProductTable` | `NovaFramework.SDK.IAP.Runtime` | 运行期商品表查询契约，由 `IAPProductTableService` 实现 | [IAPPlugin.md](./IAPPlugin.md) |
| `IAPResult` / `IAPInitResult` | `NovaFramework.SDK.IAP.Runtime` | 支付结果与初始化结果；失败码以 `int` 透传，具体含义由核心层或 Store 层枚举定义 | [IAPPlugin.md](./IAPPlugin.md) |

## 架构文档

- [IAPPlugin.md](./IAPPlugin.md) — `IAPPlugin` 类规格、配置、公开 API、生命周期和用法。
- [IAP-Architecture.md](./IAP-Architecture.md) — IAP 核心包架构、Store 扩展规则、错误码分层和数据流。

## 当前契约重点

- `IAPResult.ErrorCode` 和 `IAPInitResult.FailReason` 都是 `int` 透传；核心层只定义通用调度错误，渠道 Store 使用自己的错误码枚举。
- 打点字段 `nova_reason` 由父包 `Track*Fail` 方法把 Store 侧传入的明确 `Enum` 转成 `int` 后写入；补充描述写入 `nova_reason_detail`。
- 父包不定义跨 Store 的打点失败原因枚举；`nova_reason` 只要求 Store 侧传入明确 `Enum`，具体枚举下沉到各 Store，上报载荷使用枚举整数值。
- Store 打点 Debug 字段应读取 `IIAPStoreContext.DevelopMode == DevelopMode.Debug`；`EnableAlwaysPaySucceed` 只控制调试支付是否跳过真实平台调用。
- Mobile 官方内购的 Unity IAP 购买失败已纳入 `IAPMobileErrorCode` 的 1000+ 专用号段，避免与 Mobile 通用错误码 0-8 冲突。
- 父包不解释渠道订单号语义；例如 Mobile 的 Google 验单和本地支付成功打点使用 purchase token，Apple 才使用 `TransactionId`。

## 最新支付代码口径

- 父包只负责 Store 发现、商品表、账号同步、支付路由、补单调度、Loading、持久化和通用打点封装。
- 初始化失败原因由具体 Store 定义；Mobile 使用 `MobileStoreInitFailureReason`，通过 `IAPInitResult.FailReason` 以 int 透传。
- 支付过程失败原因由具体 Store 定义；Mobile 统一使用 `IAPMobileErrorCode`，本地支付失败和验单失败都按其整数值写入同一个 `nova_reason` 数值域。
- `nova_reason_detail` 只放补充说明，例如协议错误信息、服务端状态、Google token 缺失等，不再承载主失败分类。
- 业务判断不要依赖 `nova_reason`；业务结果仍以 `IAPResult` / `IAPInitResult` 为准。

## 子包

- `com.solotopia.nova.framework.sdk.iap.mobile`：Google Play + iOS App Store 官方内购 Store。
- `com.solotopia.nova.framework.sdk.iap.thirdpay`：第三方支付 Store。
- `com.solotopia.nova.framework.sdk.iap.voucher`：代金券 Store。
