# NativeManagerConfig

**类签名**：`[Serializable] public sealed class NativeManagerConfig`  
**命名空间**：`NovaFramework.Runtime`

`NativeManagerConfig` 是 `NativeComponent` 序列化并传入 `INativeManager.Initialize(...)` 的初始化配置容器。

当前类没有配置字段，保留为后续原生能力的显式扩展位。默认 `NativeManager` 不需要通过 Inspector 填写通知权限、Push Token、AppID 或 Secret。

## 当前边界

- 通知权限请求选项由 `RequestNotificationPermissionAsync(...)` 的调用参数提供，不写入该配置。
- APNs / FCM Token 与对应服务配置不属于 Native 模块，不能作为此配置的替代入口。
- 新增字段属于序列化与 Inspector 契约，必须同步检查旧 `Nova.prefab`、Inspector 绑定和文档。

## 关联文档

- [NativeComponent.md](../../NativeComponent.md)
- [INativeManager.md](../Interfaces/INativeManager.md)
- [NativeComponentInspector.md](../../../../../Editor/Inspectors/NativeComponentInspector/NativeComponentInspector.md)
