# Nova Framework - SDK - Firebase 文档索引

> 本包封装 Firebase Analytics / Crashlytics / FCM 的 Nova 侧接入。
> 当前运行时插件只有 `FirebasePlugin`，并通过 `FirebasePluginConfig` 提供少量框架级配置。

## 业务侧公开 API

| 类型 | 说明 | 文档 |
|---|---|---|
| `FirebasePlugin` | Firebase 聚合插件，实现 `IMonetizeTrackPlugin` 与 `IPushPlugin` | [FirebasePlugin.md](./FirebasePlugin.md) |
| `FirebasePluginConfig` | Firebase 插件配置，当前仅承载三方标识上报的 `ReportCmdName` | [FirebasePluginConfig.md](./FirebasePluginConfig.md) |

## 当前能力

- Analytics 事件上报：`TrackEvent(...)`
- 用户标识与属性：`SetUserId(...)`、`SetUserProperty(...)`
- FCM 推送：`GetTokenAsync(...)`、`OnTokenRefreshed`、`SetTopicSubscribed(...)`
- 登录联动：监听 `SDKEventData.UserLogin`，自动上报 Firebase Push Token / Analytics Instance ID

## 平台边界

- 整体受 `#if !UNITY_WEBGL` 保护，WebGL 不编译该插件
- 真正的 Firebase Analytics / Messaging 调用只在 `UNITY_IOS || UNITY_ANDROID` 下生效

## 相关

- 外部依赖管理包：`com.google.external-dependency-manager@1.2.186`
- [FirebasePlugin.md](./FirebasePlugin.md) — Firebase 聚合插件
- [FirebasePluginConfig.md](./FirebasePluginConfig.md) — Firebase 插件配置
