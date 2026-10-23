# Nova Framework - SDK - Ad 文档索引

> 本包为 Nova 框架广告聚合插件基类，调度格式来自 `AdFormat`：Rewarded / Interstitial / Banner / AppOpen / Native；实际支持哪些格式由渠道注册与实现决定，当前 MAX 实现前四种，内置打点。
> 具体渠道（MAX、AdMob 等）继承 AdChannelPluginBase 实现各自逻辑；业务侧通过 IAdPlugin 接口调度，与渠道解耦。非基类自定义渠道须自行实现回调线程、取消和占用边界，见[聚合文档](./AdPlugin.md#取消令牌的作用范围)。

---

## 业务侧公开 API

| 类型 | 说明 | 文档 |
|---|---|---|
| `IAdPlugin` | 广告聚合插件公开接口，定义业务侧可调用的所有广告操作 | [IAdPlugin.md](./IAdPlugin.md) |
| `AdPlugin` | 广告聚合调度插件，实现 `IAdPlugin`，管理多渠道切换与降级 | [AdPlugin.md](./AdPlugin.md) |
| `IBannerControl` | Banner 广告控制接口，提供 Show / Hide / Destroy 等精细控制入口 | [IBannerControl.md](./IBannerControl.md) |
| `AdChannelPluginBase` | 广告渠道插件抽象基类，各渠道包继承此类实现具体适配 | [AdChannelPluginBase.md](./AdChannelPluginBase.md) |
| `AdPluginConfig` | 插件序列化配置，承载广告组、开关、超时等参数 | [AdPluginConfig.md](./AdPluginConfig.md) |
| `AdPluginEvents` | 广告事件容器，汇聚初始化、加载、展示、收益与关闭通知 | [AdPluginEvents.md](./AdPluginEvents.md) |
| `AdRequestReason` | 广告请求原因枚举，区分默认请求、手动请求、重试和展示后补量 | [AdRequestReason.md](./AdRequestReason.md) |

基类广告组默认组间并行、组内串行；MAX 保留源表达式拓扑，当前 AdMob 经真实 ID 访问器展开后注册独立单 ID 组，详见[配置文档](./AdPluginConfig.md)。`AdGroupConfigParser` 将 List 每项解析为一组，项内用英文逗号或竖线分隔；单 ID 项自然成为单 ID 组。统一循环重试间隔见 [AdPluginConfig.md](./AdPluginConfig.md)，渠道完整接口、状态与接入说明见 [AdChannelPluginBase.md](./AdChannelPluginBase.md)，请求、展示关键节点及展示后补量（`AutoRefill`）和自动重试（`Retry`）的新轮日志见 [AdPlugin.md](./AdPlugin.md#本地请求与展示日志) 和[基类日志说明](./AdChannelPluginBase.md#本地流程日志)。

## 相关

- [AdPlugin.md](./AdPlugin.md) — 广告聚合调度插件
- [AdPluginConfig.md](./AdPluginConfig.md) — 广告聚合配置
- [AdChannelPluginBase.md](./AdChannelPluginBase.md) — 渠道插件抽象基类

## SDK 初始化与账号规则

正常生命周期由 SDKManager 每轮创建新聚合与新渠道，已释放 AdPlugin 不支持同对象再次初始化；新一轮业务重新获取实例并建立订阅，组件 InitializeTask 的单次缓存不提供重启。初始化 ct 仅关联尚未结束的渠道准备与初始化，成功后不长期控制其事件和业务请求。

本插件由 ConfigMaster 启用；聚合插件就绪表示渠道已创建，不代表全部原厂初始化成功，`InitResult` 是单渠道结果。初始化是否等待其他 SDK 由插件的 `ISDKInitializationDependencies` 能力声明决定，`Priority` 不控制启动先后；业务只需目标能力时使用 `Nova.SDK.WaitForPluginAsync<T>()` 并检查 `Ready`。游戏后端登录成功后调用 `Nova.SDK.Login(uid, userProperties)`，框架会在插件就绪后补交最新账号；切换账号再次调用，登出调用 `EndLoginSession()`。新接入 SDK 按框架 SDK 插件契约实现。现有 `IAdChannelConfig` 保持真实 ID 访问器，具体配置类另外提供组表达式。
