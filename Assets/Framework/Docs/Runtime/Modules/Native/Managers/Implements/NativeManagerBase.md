# NativeManagerBase

**类签名**：`internal abstract class NativeManagerBase : FrameworkManager, INativeManager`  
**命名空间**：`NovaFramework.Runtime`

`NativeManagerBase` 固定 Native 模块的三层链：`INativeManager → NativeManagerBase → NativeManager`。基类对外隐藏，业务与其他 Runtime 模块只依赖接口或 `Nova.Native` 门面。

## Priority

`Priority` 为 `15`：Native 在 SDK（`16`）之前更新、在 SDK 之后关闭，使 SDK 清理阶段仍可访问原生桥接。

## 抽象成员

除 `FrameworkManager.Update()` / `Shutdown()` 外，基类要求实现：

- `Initialize(NativeManagerConfig config)`
- `GetNotificationPermissionStatusAsync(CancellationToken ct = default)`
- `RequestNotificationPermissionAsync(NotificationAuthorizationOptions options, CancellationToken ct = default)`
- `OpenAppSettingsAsync()`

`Update()` 当前没有周期性工作；平台回调为事件驱动。`Shutdown()` 是权限请求等待者和平台回调引用的统一清理边界。

## 关联文档

- [INativeManager.md](../Interfaces/INativeManager.md)
- [NativeManager.md](NativeManager.md)
- [FrameworkManager.md](../../../FrameworkManager.md)
