# NativeManagerConfig

**类签名**：`[Serializable] public sealed class NativeManagerConfig`  
**命名空间**：`NovaFramework.Runtime`

`NativeManagerConfig` 是 `NativeComponent` 序列化并传入 `INativeManager.Initialize(...)` 的初始化配置容器。

`EnableAndroidAppInstall` 默认 `false`。开启后允许业务显式调用自身 APK 安装，并在实际启用的 Build Scene 中存在已开启的 NativeComponent 时声明 `REQUEST_INSTALL_PACKAGES`；默认不声明该权限，不打开系统来源设置或安装界面。该字段不配置通知、Push Token、AppID 或 Secret。

安装仅支持 Android API 28 及以上。运行时总门与 Inspector、构建读取同一字段；旧 Prefab 未保存该字段时保持关闭，不自动修改项目现有配置。业务仍需系统来源许可与用户安装确认，不能通过开关获得静默安装。

## 当前边界

- 通知权限请求选项由 `RequestNotificationPermissionAsync(...)` 的调用参数提供，不写入该配置。
- APNs / FCM Token 与对应服务配置不属于 Native 模块，不能作为此配置的替代入口。
- 新增字段属于序列化与 Inspector 契约，必须同步检查旧 `Nova.prefab`、Inspector 绑定和文档。

## 关联文档

- [NativeComponent.md](../../NativeComponent.md)
- [INativeManager.md](../Interfaces/INativeManager.md)
- [NativeComponentInspector.md](../../../../../Editor/Inspectors/NativeComponentInspector/NativeComponentInspector.md)
