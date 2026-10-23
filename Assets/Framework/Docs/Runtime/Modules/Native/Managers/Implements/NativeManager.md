# NativeManager

**类签名**：`internal sealed partial class NativeManager : NativeManagerBase, INativeAppInstaller`
**命名空间**：`NovaFramework.Runtime`

`NativeManager` 是 Native 模块的默认实现。它负责平台分发、通知权限与应用内评价请求的并发协调、调用方取消隔离、关闭清理与迟到回调丢弃；`NativeComponent` 不直接持有任何 JNI、P/Invoke 或平台回调细节。设置跳转分为应用设置根页与精准通知设置页两个明确入口，不互相降级。两者返回 `true` 都只表示框架已成功发起系统跳转请求，不保证用户已经看到目标页面或修改了任何设置。

## 初始化与关闭

- `Initialize(NativeManagerConfig)` 记录配置、启用平台桥接，但 Native Manager 自身不查询或请求通知权限；上层 SDK 插件可在自身配置允许时通过 `NativeComponent` 门面显式调用请求接口。
- `EnsureInitialized()` 会拒绝初始化前或 `Shutdown()` 后的公开调用。
- `Shutdown()` 按既有关闭链取消 in-flight 等待者、清空共享请求状态、关闭平台桥接并丢弃迟到回调。
- `Initialize` 先保存同一配置引用、设置已启用标记，再进入平台初始化；平台异常会传播，未提供完整回滚。iOS 只允许一个活动 Manager，第二个实例初始化可在已写本地状态后失败。配置属性不是运行时快照。

## 通知请求并发与取消

| 情况 | 行为 |
|---|---|
| 相同 options 并发请求 | 共享同一底层系统请求及结果。 |
| 不同 options 并发请求 | 等待当前请求结束后，以自己的 options 再发起请求。 |
| 单个调用方取消 | 只取消该调用方的等待，不取消共享系统请求。 |
| Manager Shutdown | 取消全部等待者并清空 pending 状态。 |

请求完成后返回 `NotificationPermissionResult`。平台权限请求的技术异常会转为失败且 `Status == Unknown`；取消仍抛取消异常，初始化和选项校验异常不会转换成权限结果。完成共享 TCS 前先清 in-flight 标记与 options，异参续发重新检查生命周期。通知和评价没有单独超时保证；调用令牌只取消等待，已经预取消的令牌也不能作为“原生动作未发起”的证明。用户拒绝仍是系统请求正常完成。

## 应用内评价请求并发与结果

| 情况 | 行为 |
|---|---|
| 并发请求 | 共享同一次底层平台请求及结果。 |
| 单个调用方取消 | 只取消该调用方等待，不取消已发起的系统请求。 |
| Manager Shutdown | 取消全部等待者，并让迟到回调失效。 |

`InAppReviewRequestResult.Status == RequestDispatched` 只表示平台请求链已交给系统；它不表示系统提示已展示、用户已评价或评价已提交。原生层不保存业务频率、用户历史或评价结果，触发时机由业务层决定。

## 平台实现

### Android

- Android 13（API 33）及以上请求 `android.permission.POST_NOTIFICATIONS`。
- Android 7（API 24）及以上读取 `NotificationManager.areNotificationsEnabled()`，作为应用通知总开关事实。
- 低于 Android 13 时不发起运行时权限请求，只返回当前通知总开关状态。
- Android `options` 通过公共入口的非空/已知位校验，但平台授权方法不按这些位控制通知展示；具体渠道负责展示能力。权限完成方法消费本模块注册的回调，其正文没有再按 `permissionName` 分流。
- 本地 `PlayerPrefs` 标记仅用于区分 Android 13+ 未授权时的首次请求与已请求后拒绝；状态判断仍组合系统运行时权限与通知总开关。
- `OpenAppSettingsAsync` 使用 `ACTION_APPLICATION_DETAILS_SETTINGS` 打开当前应用详情设置页。
- `OpenNotificationSettingsAsync` 仅在 Android 8（API 26）及以上使用 `ACTION_APP_NOTIFICATION_SETTINGS` 与当前包名打开通知设置页；API 26 以下或启动失败均返回 `false`，不回退到应用详情设置页。
- 应用内评价由轻量 Java `.androidlib` 完成两阶段平台请求；原生桥接仅保留短期令牌到流程启动，不向 C# 暴露平台厂商类型。

### iOS

- C# 侧通过 `requestId` 跟踪每个状态、请求和设置页操作，防止回调互相覆盖。
- 原生层调用 `requestAuthorization` 后再次读取 `UNNotificationSettings`，把重新读取的授权状态返回给 C#。
- 原生回调统一派发到 iOS 主队列，托管层并未为这些回调统一切换 Unity PlayerLoop，不能据此承诺任意后台线程调用安全。托管侧将 Apple 授权状态映射为 `NotDetermined`、`Denied`、`Authorized`、`Provisional`、`Ephemeral` 或 `Unknown`。
- `OpenAppSettingsAsync` 始终使用 `UIApplicationOpenSettingsURLString` 打开当前应用设置根页。
- `OpenNotificationSettingsAsync` 仅在 iOS 15.4 及以上使用 `UIApplicationOpenNotificationSettingsURLString` 打开当前应用通知设置；低版本直接返回 `false`，不回退到应用设置页。
- AOT 委托由静态字段保活；各 pending 字典通过非零 requestId 取出并移除后才完成结果，未知或迟到 ID 被忽略。计数跨实例递增而不在 Shutdown 重置，不承诺无限运行无溢出。通知 NSError 字符串只在回调期间有效，托管侧立即复制，不持有或释放原生指针。
- 应用内评价桥接只在 iOS 16 及以上、主线程且存在前台活动 `UIWindowScene` 时请求系统流程；低版本为 `Unsupported`，无前台场景为 `Unavailable`。多窗口下当前实现选择首个前台活动场景。

### Editor 与非移动平台

不初始化原生桥接；通知状态查询为 `Unsupported`，通知请求返回成功完成且 `Status == Unsupported`，应用内评价返回 `Unsupported`，应用设置与通知设置跳转都返回 `false`。

## 构建边界

`NativeBuildProcessor` 负责 Android Manifest 的 `POST_NOTIFICATIONS` 声明，以及 iOS `UserNotifications.framework` / `StoreKit.framework` 到 `UnityFramework` 的链接和 Swift 语言版本设置。Android 评价桥接仅声明一条平台 Maven 依赖及其传递闭包，不引入完整 Unity 评价插件。独立 APK 安装桥接随 Framework 包发布；实际启用的 Build Scene 开启安装总门时才注册 `REQUEST_INSTALL_PACKAGES`，其 Maven 验签依赖与 receiver/R8 输入见安装扩展文档。它不会配置 Push capability、`aps-environment`、APNs Token 或 FCM；这些不属于 Native 通知权限桥接。

## 结果对象与错误边界

结果结构体的构造器保存调用方给出的状态/错误，不重新查询系统，也不校验状态与错误文本是否一致；`null` 文本在显式构造时变成空串，`default(struct)` 的字符串仍可为 null。通知结果的 `IsOperationSuccessful` 与 `Status` 分开：iOS 回调按错误码为 0 且错误域为空计算前者，Denied 仍可为 true。评价和安装的便捷布尔属性仅比较已保存状态，不是额外系统证明；未知原生数字在平台映射处转换，不能把该保护当作 DTO 构造器自动校验。

Android 的短期 `AndroidJavaObject` 由调用点释放；评价/安装代理回调先切回 Unity PlayerLoop，再检查 pending/活动标记。取消安装与通知/评价的共享等待语义不同；取消安装会使代理失效并清理当前原生 request，已经完成的系统安装不能回滚。安装门、路径、IO、并发或生命周期错误可以抛出，原生准备/验签/平台失败也可能以 `Failed` 结果返回，调用方须分别处理。

## 验证边界

本页依据当前源码描述运行时与构建链行为。Android / iOS 真机的授权弹窗、系统开关、应用内评价提示与设置页跳转仍需在目标平台单独验证。

## 关联文档

- [NativeComponent.md](../../NativeComponent.md)
- [NativeManagerBase.md](NativeManagerBase.md)
- [INativeManager.md](../Interfaces/INativeManager.md)

## 独立自身 APK 安装

内置 Manager 另实现可选 `INativeAppInstaller`，由 `NativeComponent` 薄转发。`EnableAndroidAppInstall` 默认关闭；初始化只清理自有遗留，不触发来源授权或系统安装。严格验签、同包名、当前 signer 集合完全一致与递增 `versionCode` 校验在原生暂存件上完成，拒 split 和 signer 轮换；不绑定远端 Check 版本。Manager 重新初始化与关闭取消本轮安装，原生层清理 session、PendingIntent、callback 与暂存文件。详细输入、最终状态、系统用户确认和平台验证边界见 [INativeAppInstaller](../Interfaces/INativeAppInstaller.md)。
