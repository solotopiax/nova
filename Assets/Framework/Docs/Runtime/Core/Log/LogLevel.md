# LogLevel

**类签名**：`public enum LogLevel : byte`
**命名空间**：`NovaFramework.Runtime`

日志等级枚举，用于标识日志信息的严重程度。从调试信息到致命错误共五个等级，配合 `Log` 静态门面和 `ILogHelper` 实现分级日志输出与过滤。

---

## 文件列表

| 文件 | 说明 |
|------|------|
| `LogLevel.cs` | 枚举定义 |

## 枚举值

| 值 | 说明 |
|------|------|
| `Debug` | 调试信息，通常用于开发阶段排查问题 |
| `Info` | 普通信息，用于记录程序运行状态或重要事件 |
| `Warning` | 警告信息，表示可能存在潜在问题 |
| `Error` | 错误信息，表示程序发生错误但不影响整体运行 |
| `Fatal` | 致命错误，表示严重异常，可能导致程序崩溃或数据丢失 |

## 关联文档

- [Log](Log.md)
- [LogTag](LogTag.md)
- [ILogHelper](Interfaces/ILogHelper.md)
