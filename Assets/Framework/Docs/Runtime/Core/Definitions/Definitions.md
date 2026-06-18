# Runtime/Core/Definitions

框架级基础枚举定义，供框架层和业务代码共同使用。

| 文件 | 枚举/类 | 说明 |
|------|---------|------|
| `ChannelType.cs` | `ChannelType` | 业务渠道类型（Official / Google / Appstore / WX / DY / Alipay） |
| `DevelopMode.cs` | `DevelopMode` | 开发/发布模式枚举（Debug / Publish），Config 第三维度 |
| `Language.cs` | `Language`, `LanguageInfo`, `LanguageMetadata` | 游戏语言枚举及描述 / Flag 元数据 |
| `LanguageSelectionWay.cs` | `LanguageSelectionWay` | 已移除，保留历史兼容说明页 |
| `PlatformType.cs` | `PlatformType` | 目标平台类型（Android / iOS / PC / WebGL / Mini），Config 第一维度 |

`Language`、`LanguageInfo` 与 `LanguageMetadata` 一起定义在 `Runtime/Core/Definitions/`，因为根组件 `Nova.Visitors.cs` 直接持有 `Language` 类型字段（避免根组件依赖子模块产生逆向依赖）。

`ChannelType`、`PlatformType`、`DevelopMode` 三维共同构成 Config 模块的配置索引。`BusinessRegionType` 枚举已移除，各模块通过 `IConfigManager.Channel` / `IConfigManager.Platform` 读取运行时渠道/平台信息。
