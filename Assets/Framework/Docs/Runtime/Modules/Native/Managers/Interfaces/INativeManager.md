# INativeManager

**类签名**：`public interface INativeManager`  
**命名空间**：`NovaFramework.Runtime`

`INativeManager` 是 Native 模块的公开 Manager 契约。`NativeComponent` 仅通过该接口创建并调用 Manager；它不向业务暴露具体实现，也不承载平台桥接逻辑。

## 完整成员

```csharp
void Initialize(NativeManagerConfig config);

UniTask<NotificationPermissionStatus> GetNotificationPermissionStatusAsync(
    CancellationToken ct = default);

UniTask<NotificationPermissionResult> RequestNotificationPermissionAsync(
    NotificationAuthorizationOptions options = NotificationAuthorizationOptions.Alert |
                                               NotificationAuthorizationOptions.Sound |
                                               NotificationAuthorizationOptions.Badge,
    CancellationToken ct = default);

UniTask<InAppReviewRequestResult> RequestInAppReviewAsync(
    CancellationToken ct = default);

UniTask<bool> OpenAppSettingsAsync();

UniTask<bool> OpenNotificationSettingsAsync();
```

## 契约要求

- `Initialize` 只完成桥接初始化，不能自动查询或请求通知权限。
- 状态查询以操作系统当前状态为准；本地记录只能辅助判断 Android 首次请求状态，不能覆盖系统授权结果。
- 相同选项的并发请求应共享底层系统请求；不同选项必须串行，不得丢弃后续参数。
- 并发 `RequestInAppReviewAsync` 应共享同一次底层原生请求；调用方取消只影响自己的等待。
- `RequestInAppReviewAsync` 只能由业务显式调用，不能在 `Initialize`、框架启动、场景启动或页面打开时自动发起。`RequestDispatched` 仅表示请求已交给系统，不表示提示展示、用户评价或提交完成。
- 调用方取消只影响自己的等待；Manager 关闭时必须取消全部等待者、清空 pending 状态并忽略迟到回调。
- `OpenAppSettingsAsync` 打开当前应用的系统设置根页。
- `OpenNotificationSettingsAsync` 只允许精准跳转到当前应用的通知设置。平台或版本不支持或启动失败时返回 `false`，不得回退为 `OpenAppSettingsAsync`。
- 两个设置入口返回 `true` 都只表示框架已成功发起对应的系统跳转请求，不保证用户已经看到目标页面或修改了任何设置。

实现类型会由 Native Inspector 的类型选择器发现。实现 Native 功能时应把 JNI、P/Invoke 与平台回调保留在 Manager 层，不能把它们放回 `NativeComponent` 或 Editor。

## 关联文档

- [NativeComponent.md](../../NativeComponent.md)
- [NativeManagerBase.md](../Implements/NativeManagerBase.md)
- [NativeManager.md](../Implements/NativeManager.md)
