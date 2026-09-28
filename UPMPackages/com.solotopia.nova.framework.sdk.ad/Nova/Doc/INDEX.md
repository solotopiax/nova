# Nova Framework - SDK - Ad 文档索引

> 本包为 Nova 框架广告聚合插件基类，支持 RV / Inter / Banner / AppOpen / InGameDisplay 五种广告类型，内置打点。
> 具体渠道（MAX 等）继承 `AdChannelPluginBase` 实现各自逻辑；业务侧通过 `IAdPlugin` 接口调度，与渠道解耦。

---

## 业务侧公开 API

| 类型 | 说明 | 文档 |
|---|---|---|
| `IAdPlugin` | 广告聚合插件公开接口，定义业务侧可调用的所有广告操作 | [IAdPlugin.md](./IAdPlugin.md) |
| `AdPlugin` | 广告聚合调度插件，实现 `IAdPlugin`，管理多渠道切换与降级 | [AdPlugin.md](./AdPlugin.md) |
| `IBannerControl` | Banner 广告控制接口，提供 Show / Hide / Destroy 等精细控制入口 | [IBannerControl.md](./IBannerControl.md) |
| `AdChannelPluginBase` | 广告渠道插件抽象基类，各渠道包继承此类实现具体适配 | [AdChannelPluginBase.md](./AdChannelPluginBase.md) |
| `AdPluginConfig` | 插件序列化配置，承载渠道 ID、开关、超时等参数 | [AdPluginConfig.md](./AdPluginConfig.md) |
| `AdPluginEvents` | 广告事件容器，汇聚各类型广告的加载、展示、点击、关闭回调 | [AdPluginEvents.md](./AdPluginEvents.md) |
| `AdRequestReason` | 广告请求原因枚举，用于区分首次加载、重试、强制刷新等场景 | [AdRequestReason.md](./AdRequestReason.md) |

## 相关

- [AdPlugin.md](./AdPlugin.md) — 广告聚合调度插件
- [AdPluginConfig.md](./AdPluginConfig.md) — 广告聚合配置
- [AdChannelPluginBase.md](./AdChannelPluginBase.md) — 渠道插件抽象基类

## SDK 初始化与账号规则

本插件由 ConfigMaster 启用。初始化是否等待其他 SDK 由插件的 `ISDKInitializationDependencies` 能力声明决定，`Priority` 不控制启动先后；业务只需目标能力时使用 `Nova.SDK.WaitForPluginAsync<T>()` 并检查 `Ready`。游戏后端登录成功后调用 `Nova.SDK.Login(uid, userProperties)`，框架会在插件就绪后补交最新账号；切换账号再次调用，登出调用 `EndLoginSession()`。新接入 SDK 应按框架 SDK 插件契约 实现。
