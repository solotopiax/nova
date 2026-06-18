# LogTag

**类签名**：`public static partial class LogTag`  
**命名空间**：`NovaFramework.Runtime`

日志标签常量集合，用于统一标记日志来源模块，配合 `Log.Debug/Warning/Error` 做分类输出与过滤。

## § 2 文件表

| 文件 | 类 | 说明 |
|---|---|---|
| `LogTag.cs` | `LogTag` | 日志标签常量定义 |

## § 3 继承关系

`static partial class`，无继承链。

## § 4 关键字段表

| 标签 | 值 | 说明 |
|---|---|---|
| `Editor` | `[Editor]` | 编辑器模块 |
| `Base` | `[Base]` | 基础模块 |
| `Component` | `[Component]` | 组件模块 |
| `App` | `[App]` | App 模块（大版本检查/强更） |
| `Asset` | `[Asset]` | 资源模块 |
| `Prefab` | `[Prefab]` | Prefab 实例化模块 |
| `Config` | `[Config]` | 配置模块 |
| `Event` | `[Event]` | 事件模块 |
| `Hotfix` | `[Hotfix]` | 热更新模块 |
| `Procedure` | `[Procedure]` | 流程模块 |
| `Persist` | `[Persist]` | 持久化模块 |
| `Debug` | `[Debug]` | Debug 模块 |
| `Http` | `[Network][Http]` | HTTP 网络子模块 |
| `WebSocket` | `[Network][WebSocket]` | WebSocket 子模块 |
| `DoH` | `[Network][DoH]` | DoH 子模块 |
| `Network` | `[Network]` | 网络模块 |
| `SDK` | `[SDK]` | SDK 模块 |
| `Table` | `[Table]` | 表格模块 |
| `Sound` | `[Sound]` | 声音模块 |
| `Vibrate` | `[Vibrate]` | 振动模块 |
| `Localization` | `[Localization]` | 本地化模块 |
| `Reference` | `[Reference]` | 引用池模块 |
| `UI` | `[UI]` | UI 模块 |

> 完整列表以 `LogTag.cs` 为准。

## § 5 完整公开 API

`LogTag` 仅包含 `public const string` 标签常量，不包含方法。

## § 11 使用示例

```csharp
Log.Debug(LogTag.Procedure, "版本检查开始。");
Log.Warning(LogTag.Hotfix, "热更下载异常，准备重试。");
Log.Error(LogTag.NetworkHttp, "接口请求失败。");
```

## § 13 关联文档

- [Log.md](Log.md)

