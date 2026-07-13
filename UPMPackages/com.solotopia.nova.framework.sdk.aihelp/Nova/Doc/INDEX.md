# AIHelp 对接层文档索引

本目录是 `com.solotopia.nova.framework.sdk.aihelp` 的文档入口，按「先用起来 → 再懂原理 → 遇坑查底层」的顺序阅读：

1. [AIHelpPlugin.md](./AIHelpPlugin.md)
   Nova 对接层的主 API 参考：`AIHelpPlugin` 公开方法清单、接入步骤、后台配置项。这是日常业务接入时最先查的文档。
2. [官方SDK技术文档.md](./官方SDK技术文档.md)
   转述 AIHelp 官方 SDK 的技术要点：初始化参数（`domain` / `appId` / `language`）、`entranceId`、事件类型（`EventType`）等底层概念。当 `AIHelpPlugin.md` 的封装 API 无法满足需求，或需要理解底层行为时查阅。

## 当前状态

包已完成全部实现（Core vendor bundle、Runtime 插件、Editor 构建期处理器、AIHelpDemo Sample），`AIHelpPlugin.md` 已回填完整公开方法清单、事件签名与自动登录说明。
