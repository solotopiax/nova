# NativeManager

**类签名**：`internal sealed partial class NativeManager : NativeManagerBase`  
**命名空间**：`NovaFramework.Runtime`

`NativeManager` 是 Native 模块的默认实现。它负责平台分发、通知权限请求的并发协调、调用方取消隔离、关闭清理与迟到回调丢弃；`NativeComponent` 不直接持有任何 JNI、P/Invoke 或平台回调细节。设置跳转分为应用设置根页与精准通知设置页两个明确入口，不互相降级。两者返回 `true` 都只表示框架已成功发起系统跳转请求，不保证用户已经看到目标页面或修改了任何设置。

## 初始化与关闭

- `Initialize(NativeManagerConfig)` 记录配置、启用平台桥接，但不查询或请求通知权限。
- `EnsureInitialized()` 会拒绝初始化前或 `Shutdown()` 后的公开调用。
- `Shutdown()` 取消 in-flight 等待者、清空共享请求状态、关闭平台桥接并丢弃迟到回调。

## 通知请求并发与取消

| 情况 | 行为 |
|---|---|
| 相同 options 并发请求 | 共享同一底层系统请求及结果。 |
| 不同 options 并发请求 | 等待当前请求结束后，以自己的 options 再发起请求。 |
| 单个调用方取消 | 只取消该调用方的等待，不取消共享系统请求。 |
| Manager Shutdown | 取消全部等待者并清空 pending 状态。 |

请求完成后返回 `NotificationPermissionResult`。异常会转为 `Unknown` 状态及错误文本；用户拒绝仍是系统请求正常完成。

## 平台实现

### Android

- Android 13（API 33）及以上请求 `android.permission.POST_NOTIFICATIONS`。
- Android 7（API 24）及以上读取 `NotificationManager.areNotificationsEnabled()`，作为应用通知总开关事实。
- 低于 Android 13 时不发起运行时权限请求，只返回当前通知总开关状态。
- 本地 `PlayerPrefs` 标记仅用于区分 Android 13+ 未授权时的首次请求与已请求后拒绝；状态判断仍组合系统运行时权限与通知总开关。
- `OpenAppSettingsAsync` 使用 `ACTION_APPLICATION_DETAILS_SETTINGS` 打开当前应用详情设置页。
- `OpenNotificationSettingsAsync` 仅在 Android 8（API 26）及以上使用 `ACTION_APP_NOTIFICATION_SETTINGS` 与当前包名打开通知设置页；API 26 以下或启动失败均返回 `false`，不回退到应用详情设置页。

### iOS

- C# 侧通过 `requestId` 跟踪每个状态、请求和设置页操作，防止回调互相覆盖。
- 原生层调用 `requestAuthorization` 后再次读取 `UNNotificationSettings`，把重新读取的授权状态返回给 C#。
- 原生回调统一派发到 iOS 主队列，托管侧将 Apple 授权状态映射为 `NotDetermined`、`Denied`、`Authorized`、`Provisional`、`Ephemeral` 或 `Unknown`。
- `OpenAppSettingsAsync` 始终使用 `UIApplicationOpenSettingsURLString` 打开当前应用设置根页。
- `OpenNotificationSettingsAsync` 仅在 iOS 15.4 及以上使用 `UIApplicationOpenNotificationSettingsURLString` 打开当前应用通知设置；低版本直接返回 `false`，不回退到应用设置页。

### Editor 与非移动平台

不初始化原生桥接；状态查询为 `Unsupported`，请求返回成功完成且 `Status == Unsupported`，应用设置与通知设置跳转都返回 `false`。

## 构建边界

`NativeBuildProcessor` 负责 Android Manifest 的 `POST_NOTIFICATIONS` 声明和 iOS `UserNotifications.framework` 链接。它不会配置 Push capability、`aps-environment`、APNs Token 或 FCM；这些不属于 Native 通知权限桥接。

## 验证边界

本页依据当前源码描述运行时与构建链行为。Android / iOS 真机的授权弹窗、系统开关与设置页跳转仍需在目标平台单独验证。

## 关联文档

- [NativeComponent.md](../../NativeComponent.md)
- [NativeManagerBase.md](NativeManagerBase.md)
- [INativeManager.md](../Interfaces/INativeManager.md)
