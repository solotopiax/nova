# Nova Framework - SDK - Firebase 文档索引

> 本包封装 Firebase Analytics / Crashlytics / FCM 的 Nova 侧接入。
> 当前运行时插件只有 `FirebasePlugin`，并通过 `FirebasePluginConfig` 提供少量框架级配置。

## 业务侧公开 API

| 类型 | 说明 | 文档 |
|---|---|---|
| `FirebasePlugin` | Firebase 聚合插件，实现 `IMonetizeTrackPlugin`、`IPushPlugin`、`IFirebasePushTaskPlugin` 与 `ISDKPauseListener` | [FirebasePlugin.md](./FirebasePlugin.md) |
| `IFirebasePushTaskPlugin` | Firebase 专用业务 push task 缓存与批量发送接口 | [FirebasePlugin.md](./FirebasePlugin.md) |
| `FirebasePluginConfig` | Firebase 插件配置，承载标识上报与 push task 批量发送等框架级配置 | [FirebasePluginConfig.md](./FirebasePluginConfig.md) |

## 当前能力

- 初始化门槛：SDK 初始化任务会等待 `FirebaseApp.CheckAndFixDependenciesAsync()` 完成；`IsAvailable == true` 表示 Firebase 依赖已可用且 Messaging 事件已注册
- Analytics 事件上报：`TrackEvent(...)`
- 用户标识与属性：`SetUserId(...)`、`SetUserProperty(...)`
- FCM 推送：`GetTokenAsync(...)`、`OnTokenRefreshed`、`SetTopicSubscribed(...)`
- Push task：`QueuePushTaskAsync(...)` 本地缓存后按 `100` 秒、`5` 条默认阈值或恢复前台触发批量发送，可通过 `FirebasePluginConfig` 调整；发送前要求 Firebase 初始化完成且用户 ID 就绪，取消任务只向服务端发送 `task_key` 与 `cancel`
- 登录联动：监听 `SDKEventData.UserLogin`，自动上报 Firebase Push Token / Analytics Instance ID

## 平台边界

- 整体受 `#if !UNITY_WEBGL` 保护，WebGL 不编译该插件
- 真正的 Firebase Analytics / Messaging 调用只在 `UNITY_IOS || UNITY_ANDROID` 下生效

## 相关

- 外部依赖管理包：`com.google.external-dependency-manager@1.2.186`
- [FirebasePlugin.md](./FirebasePlugin.md) — Firebase 聚合插件
- [FirebasePluginConfig.md](./FirebasePluginConfig.md) — Firebase 插件配置

## SDK 初始化与账号规则

本插件由 ConfigMaster 启用。初始化是否等待其他 SDK 由插件的 `ISDKInitializationDependencies` 能力声明决定，`Priority` 不控制启动先后；业务只需目标能力时使用 `Nova.SDK.WaitForPluginAsync<T>()` 并检查 `Ready`。游戏后端登录成功后调用 `Nova.SDK.Login(uid, userProperties)`，框架会在插件就绪后补交最新账号；切换账号再次调用，登出调用 `EndLoginSession()`。新接入 SDK 应按框架 SDK 插件契约 实现。
