# FirebasePlugin

## 1. 简介

`FirebasePlugin` 是 Firebase 聚合插件，实现两个主框架契约：

- `IMonetizeTrackPlugin`：事件埋点与用户属性
- `IPushPlugin`：FCM Token 获取、主题订阅、Token 刷新通知

此外，它会在收到 `SDKEventData.UserLogin` 后，把 Firebase 标识异步上报到业务服务器。

## 2. 公开 API

| 成员 | 说明 |
|---|---|
| `TrackEvent(string, Dictionary<string, object>)` | 上报自定义埋点事件 |
| `TrackEvent(TrackEvent)` | 上报统一事件载荷 |
| `SetUserProperty(string, string)` | 设置用户属性 |
| `SetUserId(string)` | 同步 Analytics / Crashlytics 用户 ID |
| `OnTokenRefreshed` | FCM Token 刷新事件 |
| `GetTokenAsync(...)` | 等待并返回当前 FCM Token |
| `SetTopicSubscribed(string, bool)` | 订阅或退订主题 |
| `SetAnalyticsEnabled(bool)` | 控制 Analytics 收集开关 |
| `GetToken()` | 同步读取已缓存的 FCM Token |
| `GetAnalyticsInstanceId()` | 同步读取已缓存的 Analytics Instance ID |
| `SubscribeAsync(string)` | 直接发起主题订阅 |
| `UnsubscribeAsync(string)` | 直接发起主题退订 |
| `IsInitialized` | 初始化完成标志 |
| `IsNotificationLaunch` | 本次启动是否由推送点击触发 |

## 3. 初始化语义

- `OnInitializeAsync(...)` 会立即返回，但真正的可用时机取决于 `FirebaseApp.CheckAndFixDependenciesAsync()` 的异步回调。
- 只有在依赖检查通过后，`m_InitOver` 才会置为 `true`。
- 大多数公开方法都会在 `m_InitOver == false` 时直接静默返回。
- `GetTokenAsync(...)` 不依赖 `m_InitOver` 直接返回，而是等待 `m_TokenReceived` 非空。

## 4. 配置与上报

当前配置类型是 `FirebasePluginConfig`，只包含一个框架侧字段：

- `ReportCmdName`：登录后向业务服务器上报 Firebase 标识时使用的 NetCmd 名称

上报链路依赖两个内部发布的数据槽位：

- `SDKDataKeys.FirebasePushToken`
- `SDKDataKeys.FirebaseAnalyticsInstanceId`

## 5. 使用示例

```csharp
FirebasePlugin firebase = /* 已从 SDKComponent / SDKManager 取得插件实例 */;

firebase.TrackEvent("level_start", new Dictionary<string, object>
{
    ["level_id"] = 3,
    ["source"] = "main_menu",
});

PushToken token = await firebase.GetTokenAsync();
firebase.SetTopicSubscribed("global_notice", true);
```

## 6. 关联

- 配置类型：[FirebasePluginConfig.md](./FirebasePluginConfig.md)
