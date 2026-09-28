# Nova Framework - SDK - AppsFlyer 文档索引

> 本包为 Nova 框架 AppsFlyer 归因埋点插件，提供事件追踪服务。
> 实现 `IAttributionPlugin`，业务侧通过统一归因接口发送事件、获取归因数据，无需直接依赖 AppsFlyer SDK。

---

## 业务侧公开 API

| 类型 | 说明 | 文档 |
|---|---|---|
| `AppsFlyerPlugin` | AppsFlyer SDK 插件，实现 `IAttributionPlugin`；配置由 SDKManager 按 `ConfigType` 自动注入 | [AppsFlyerPlugin.md](./AppsFlyerPlugin.md) |

## 相关

- 外部依赖管理包：`com.google.external-dependency-manager@1.2.186`
- [AppsFlyerPlugin.md](./AppsFlyerPlugin.md) — AppsFlyer 归因埋点插件

## SDK 初始化与账号规则

本插件由 ConfigMaster 启用。初始化是否等待其他 SDK 由插件的 `ISDKInitializationDependencies` 能力声明决定，`Priority` 不控制启动先后；业务只需目标能力时使用 `Nova.SDK.WaitForPluginAsync<T>()` 并检查 `Ready`。游戏后端登录成功后调用 `Nova.SDK.Login(uid, userProperties)`，框架会在插件就绪后补交最新账号；切换账号再次调用，登出调用 `EndLoginSession()`。新接入 SDK 应按框架 SDK 插件契约 实现。
