# ILogHelper

**类签名**：`public interface ILogHelper`
**命名空间**：`NovaFramework.Runtime`

日志助手接口，定义了日志记录的统一契约，保证业务模块与具体日志实现解耦。通过实现该接口可以替换底层日志输出方式（如文件日志、远程上报等）。

---

## 文件列表

| 文件 | 说明 |
|------|------|
| `Interfaces/ILogHelper.cs` | 接口定义 |

## 公开 API

```csharp
void Initialize();
void Print(LogLevel level, string tag, object message);
void PrintFatal(string tag, Exception exception);
```

## 关联文档

- [Log](../Log.md)
- [LogLevel](../LogLevel.md)
- [LogHelper](../Implements/LogHelper.md)
