# Log

**类签名**：`public static class Log`
**命名空间**：`NovaFramework.Runtime`

静态日志门面，底层委托可替换的 `ILogHelper`（默认转发至 `UnityEngine.Debug`）。支持按 `LogTag` 分类过滤，便于运行时开关特定模块的日志输出。

Unity Editor 中的全部日志等级始终保留调用，不受运行时日志宏控制；Player 构建继续按照 `ENABLE_LOG`、精确等级宏及 `ENABLE_*_AND_ABOVE_LOG` 宏裁剪调用。

---

## 文件

```csharp
Log.Debug(LogTag.Component, "msg");
Log.Warning(LogTag.Asset, "msg");
Log.Error(LogTag.Table, "msg");
Log.Fatal(LogTag.Network, "msg");
Log.Fatal(LogTag.Network, exception);   // 保留原始异常堆栈
```

底层委托给可替换的 `ILogHelper`，默认实现 `LogHelper` 转发至 `UnityEngine.Debug`。

## 文件列表

| 文件 | 说明 |
|------|------|
| `Log.cs` | 静态门面 |
| `LogLevel.cs` | 枚举：`Debug / Info / Warning / Error / Fatal` |
| `LogTag.cs` | 静态类（`const string` 成员）：`Component / Asset / UI / Table / Event / Object` 等分类标签 |
| `Interfaces/ILogHelper.cs` | 日志助手接口 |
| `Implements/LogHelper.cs` | 默认实现（`UnityEngine.Debug` 转发） |

## 关联文档

- [LogTag.md](LogTag.md)
