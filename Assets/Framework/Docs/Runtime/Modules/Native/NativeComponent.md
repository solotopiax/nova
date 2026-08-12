# NativeComponent

**类签名**：`[DisallowMultipleComponent] public sealed partial class NativeComponent : FrameworkComponent`  
**命名空间**：`NovaFramework.Runtime`  
**全局访问**：`Nova.Native`

`NativeComponent` 是 Nova 访问操作系统原生能力的场景门面。当前正式能力仅覆盖**通知授权状态查询、显式请求、打开应用设置与精准打开通知设置**；JNI、P/Invoke、平台状态映射、并发请求协调与回调清理由 `NativeManager` 承担。

它不负责 APNs / FCM Token 注册、远程消息接收、通知展示策略或业务弹窗策略。这些能力属于对应 SDK 或业务层。

## 文件拆分

| 文件 | 说明 |
|---|---|
| `NativeComponent.cs` | `Awake` / `Start` / `OnDestroy` 与公开通知权限 API |
| `NativeComponent.Visitors.cs` | Manager 类型、初始化配置与私有 Manager 引用 |
| `Managers/Interfaces/INativeManager.cs` | 原生能力 Manager 公共契约 |
| `Managers/Implements/NativeManager*.cs` | 默认实现、并发协调与 Android / iOS / 非移动平台分发 |

## 公开 API

```csharp
public string CurNativeManagerTypeName { get; }
public NativeManagerConfig NativeManagerConfig { get; }

public UniTask<NotificationPermissionStatus> GetNotificationPermissionStatusAsync(
    CancellationToken ct = default);

public UniTask<NotificationPermissionResult> RequestNotificationPermissionAsync(
    NotificationAuthorizationOptions options = NotificationAuthorizationOptions.Alert |
                                               NotificationAuthorizationOptions.Sound |
                                               NotificationAuthorizationOptions.Badge,
    CancellationToken ct = default);

public UniTask<bool> OpenAppSettingsAsync();

public UniTask<bool> OpenNotificationSettingsAsync();
```

- `GetNotificationPermissionStatusAsync` 返回当前操作系统状态。
- `RequestNotificationPermissionAsync` 只能由业务在合适的交互时机显式调用；框架初始化和场景启动不会自动弹窗。
- `OpenAppSettingsAsync` 打开当前应用的系统设置根页。
- `OpenNotificationSettingsAsync` 只打开当前应用的通知设置页；不支持或无法精准跳转时直接返回 `false`，绝不回退到应用设置页。
- 两个设置入口返回 `true` 都只表示框架已成功发起对应的系统跳转请求，不保证用户已经看到目标页面或修改了任何设置。
- `CancellationToken` 只取消当前调用方的等待；它不取消已发起的共享系统请求。

## 通知状态与请求选项

### NotificationPermissionStatus

| 状态 | 含义 |
|---|---|
| `Unknown` | 原生层返回未知值或查询失败。 |
| `Unsupported` | 当前平台不支持该通知权限能力。 |
| `NotDetermined` | 用户尚未作出通知授权选择。 |
| `Denied` | 用户拒绝，或系统通知总开关已关闭。 |
| `Authorized` | 已获得完整通知授权。 |
| `Provisional` | iOS 静默临时授权，通知默认只进入通知中心。 |
| `Ephemeral` | iOS App Clip 临时授权。 |

`NotificationAuthorizationOptions` 支持 `Alert`、`Sound`、`Badge` 与 iOS 专用的 `Provisional`；`None` 及未知位不能用于请求。默认值为 `Alert | Sound | Badge`。

`NotificationPermissionResult` 同时返回原生请求流程是否完成（`IsOperationSuccessful`）、请求后重新读取的权威 `Status`，以及可选的 `ErrorCode`、`ErrorDomain`、`ErrorMessage`。用户拒绝是已完成的系统请求，不等同于原生调用失败。

## 生命周期与并发语义

1. `Awake()` 通过 `Util.TypeCreator` 创建 `INativeManager`，但不查询也不请求权限。
2. `Start()` 调用 `Initialize(NativeManagerConfig)`；初始化同样不会弹窗。
3. 相同 `NotificationAuthorizationOptions` 的并发请求共享同一次底层系统请求。
4. 不同选项的请求会等待前一请求完成后再执行，后续参数不会被静默覆盖。
5. Nova 的 Manager 关闭链负责统一取消等待者并丢弃迟到回调；`NativeComponent.OnDestroy()` 只断开自身引用。

## 平台行为

- **Android**：Android 13（API 33）及以上通过 `POST_NOTIFICATIONS` 请求运行时权限；Android 7（API 24）及以上同时读取应用通知总开关。`OpenAppSettingsAsync` 打开当前应用详情设置，`OpenNotificationSettingsAsync` 仅在 Android 8（API 26）及以上通过 `ACTION_APP_NOTIFICATION_SETTINGS` 精准打开当前应用通知设置。Android 的 `options` 仅用于保持统一 API，具体展示能力仍由通知渠道决定。
- **iOS**：原生桥接在 `requestAuthorization` 完成后再次读取 `UNNotificationSettings`，并将回调切回主队列后进入 Unity 托管层。`OpenAppSettingsAsync` 打开当前应用设置根页；`OpenNotificationSettingsAsync` 仅在 iOS 15.4 及以上精准打开当前应用通知设置。
- **Editor / 非移动平台**：查询返回 `Unsupported`；请求以成功完成且状态为 `Unsupported` 结束；两种设置页入口都返回 `false`。

构建链会向 Android 受控主 Manifest 声明 `POST_NOTIFICATIONS`，并在 iOS 工程链接 `UserNotifications.framework`；它不会注入 Push capability、`aps-environment` 或 APNs 后台模式。

## 使用示例

```csharp
NotificationPermissionStatus status =
    await Nova.Native.GetNotificationPermissionStatusAsync(ct);

if (status == NotificationPermissionStatus.NotDetermined)
{
    NotificationPermissionResult result = await Nova.Native.RequestNotificationPermissionAsync(ct: ct);
    if (!result.IsOperationSuccessful)
    {
        Log.Warning(LogTag.Base, "通知权限请求失败：{0}", result.ErrorMessage);
    }
}
else if (status == NotificationPermissionStatus.Denied)
{
    await Nova.Native.OpenNotificationSettingsAsync();
}
```

上例只演示业务主动触发的路径；是否请求、何时提示用户、拒绝后的产品策略均由业务决定。若业务希望打开应用设置根页而非通知页，应显式调用 `OpenAppSettingsAsync()`；不能把 `false` 自动改为调用后者。

## Sample 演示位置

MainDemo 的 `2.Modules > 2.17 Native` 页面演示查询通知权限、请求 `Alert | Sound | Badge`、请求 iOS `Provisional`、打开当前应用设置根页，以及精准打开当前应用通知设置。精准入口返回 `false` 时 Demo 只显示“不支持或启动失败”，不会自动回退到应用设置；用户可在从系统设置返回后点击“查询通知状态”手动刷新。

- 页面源码：[`DemoNativeView.cs`](../../../../../Samples/MainDemo/Scripts/Runtime/UIs/DemoNativeView/DemoNativeView.cs)
- 页面 Prefab：[`DemoNativeView.prefab`](../../../../../Samples/MainDemo/Prefabs/UIs/DemoNativeView/DemoNativeView.prefab)

Editor 中页面会展示真实的 `Unsupported` 结果，不模拟移动平台授权；系统弹窗与设置页跳转需要在目标设备上验证。

## 验证边界

本文记录当前代码与构建链事实，不宣称已完成 Android 或 iOS 真机授权验证。平台差异、系统弹窗和设置页跳转仍应在目标设备上定向验证。

## 关联文档

- [INativeManager.md](Managers/Interfaces/INativeManager.md)
- [NativeManagerBase.md](Managers/Implements/NativeManagerBase.md)
- [NativeManager.md](Managers/Implements/NativeManager.md)
- [NativeManagerConfig.md](Managers/Definitions/NativeManagerConfig.md)
- [NativeComponentInspector.md](../../../Editor/Inspectors/NativeComponentInspector/NativeComponentInspector.md)
