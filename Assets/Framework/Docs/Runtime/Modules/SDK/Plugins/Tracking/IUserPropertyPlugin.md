# IUserPropertyPlugin

命名空间：`NovaFramework.Runtime`。可选能力接口继承 `ISDKPlugin`，提供 `UserSetOnce(Dictionary<string, object> properties)`。

主线程调用，只设置尚无值的属性，保留值类型。提交成功代表 SDK 接受请求，不证明服务端收数。当前 TGAPlugin 使用既有同名方法实现；消费者通过 `ISDKManager.TryGet<IUserPropertyPlugin>` 或 `WaitForPluginAsync<IUserPropertyPlugin>` 查询，SDK 子包无需依赖 TGA 包。

微信引力归因在原厂初始化与游戏 UID 就绪后等待此能力，写入 `nova_` 前缀的一次性属性；沿用 Solar 字符串值语义，过滤凭据、身份、用户内容和非标量数据。等待会在登出、切换账号、插件释放时取消，不成为 SDK 初始化依赖。

关联：[ITrackPlugin](ITrackPlugin.md)、[ISDKPlugin](../../Definitions/ISDKPlugin.md)。
