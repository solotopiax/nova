# LogHelper

**类签名**：`internal sealed class LogHelper : ILogHelper`
**命名空间**：`NovaFramework.Runtime`

Unity 平台默认日志实现，实现 `ILogHelper` 接口。支持日志分级输出（Debug/Info/Warning/Error/Fatal），每个等级使用不同颜色的富文本标记。支持跨线程安全打印，非主线程的日志通过 `SynchronizationContext` 转发至主线程执行。

---

## 文件列表

| 文件 | 说明 |
|------|------|
| `Implements/LogHelper.cs` | Unity 默认日志实现 |

## 关键字段

| 字段 | 类型 | 说明 |
|------|------|------|
| `m_SynchronizationContext` | `SynchronizationContext` | Unity 主线程同步上下文（WebGL 下不启用） |
| `m_UnityMainThreadId` | `int` | Unity 主线程 ID |
| `ColorDebug` | `string` | Debug 日志颜色 `#00E7FF` |
| `ColorInfo` | `string` | Info 日志颜色 `#00BF0F` |
| `ColorWarning` | `string` | Warning 日志颜色 `#FDFF00` |
| `ColorError` | `string` | Error 日志颜色 `#FF0000` |
| `ColorFatal` | `string` | Fatal 日志颜色 `#FF00BF` |
| `ColorTag` | `string` | Tag 日志颜色 `#6D6D6D` |

## 公开 API

```csharp
void Initialize();
void Print(LogLevel level, string tag, object message);
```

## 性能说明

`Print` 方法在主线程场景下（>99% 调用场景）直接调用 `PrintInternal`，不创建 `Action` 闭包，消除 GC 分配。仅在跨线程 `SynchronizationContext.Post` 场景下才创建闭包（低频路径，不可避免）。

## 关联文档

- [Log](../Log.md)
- [ILogHelper](../Interfaces/ILogHelper.md)
- [LogLevel](../LogLevel.md)
