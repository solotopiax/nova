# ISDKRegistrationManager

命名空间：`NovaFramework.Runtime`。可选管理器能力提供 `RegisterAndLogin(string userId)`；不扩展既有 `ISDKManager` 实现要求。

业务在服务端明确创建新用户后调用 `Nova.SDK.RegisterAndLogin(uid)`。正式 `SDKManager` 先发 `ISDKRegistrationReceiver.OnSDKRegister`，再发送同会话登录，调用方无需额外 `Login`。每次有效调用均发送注册及一次登录，调用方负责决定是否调用；晚就绪插件只补交当前账号。普通登录 API 保持每次调用新会话的语义。

`SDKManagerBase` 提供虚拟默认实现转发 `Login`。自定义 `ISDKManager` 没有实现注册能力时，`SDKComponent.RegisterAndLogin` 警告并保留 `Login`，不伪造注册。使用新注册或一次性属性能力的子包正式发布时需依赖包含这些接口的 Framework 版本。

关联：[ISDKManager](ISDKManager.md)、[SDKComponent](../../SDKComponent.md)。
