# AIHelp 对接层文档索引

本目录是 `com.solotopia.nova.framework.sdk.aihelp` 的文档入口，按「先用起来 → 再懂原理 → 遇坑查底层」的顺序阅读：

1. [AIHelpPlugin.md](./AIHelpPlugin.md)
   Nova 对接层的主 API 参考：`AIHelpPlugin` 公开方法清单、接入步骤、后台配置项。这是日常业务接入时最先查的文档。
2. [官方SDK技术文档.md](./官方SDK技术文档.md)
   转述 AIHelp 官方 SDK 的技术要点：初始化参数（`domain` / `appId` / `language`）、`entranceId`、事件类型（`EventType`）等底层概念。当 `AIHelpPlugin.md` 的封装 API 无法满足需求，或需要理解底层行为时查阅。

## 当前状态

包已完成全部实现（Core vendor bundle、Runtime 插件、Editor 构建期处理器、AIHelpDemo Sample），`AIHelpPlugin.md` 已回填完整公开方法清单、事件签名与自动登录说明。

## SDK 初始化与账号规则

本插件由 ConfigMaster 启用。初始化是否等待其他 SDK 由插件的 `ISDKInitializationDependencies` 能力声明决定，`Priority` 不控制启动先后；业务只需目标能力时使用 `Nova.SDK.WaitForPluginAsync<T>()` 并检查 `Ready`。游戏后端登录成功后调用 `Nova.SDK.Login(uid, userProperties)`，框架会在插件就绪后补交最新账号；切换账号再次调用，登出调用 `EndLoginSession()`。新接入 SDK 应按框架 SDK 插件契约 实现。
