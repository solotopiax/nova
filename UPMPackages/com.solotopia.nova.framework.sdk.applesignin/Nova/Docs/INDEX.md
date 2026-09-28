# Apple Sign-In SDK 文档索引

| API | 说明 | 文档 |
| --- | --- | --- |
| `AppleSignInPlugin` | Nova Apple 登录插件，实现 `IAuthPlugin`。 | [AppleSignInPlugin.md](./AppleSignInPlugin.md) |
| `AppleSignInPluginConfig` | Apple 登录运行时配置。 | [AppleSignInPluginConfig.md](./AppleSignInPluginConfig.md) |
| `AppleSignInUserData` | Apple 登录用户数据。 | [AppleSignInUserData.md](./AppleSignInUserData.md) |
## Nova SDK 生命周期

AppleSignIn 只提供第三方身份验证，不需要游戏 UID，因此不实现登录资料接收接口。它与无依赖插件并发初始化；业务需在插件状态为 `Ready` 后使用。取得 Apple 身份后仍须由游戏后端确认登录，再调用 `Nova.SDK.Login(gameUid, userProperties)` 同步其他 SDK。
