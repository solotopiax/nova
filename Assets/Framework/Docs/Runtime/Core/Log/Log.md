# Log

**类签名**：`public static class Log`
**命名空间**：`NovaFramework.Runtime`

静态日志门面，底层委托可替换的 `ILogHelper`（默认转发至 `UnityEngine.Debug`）。LogTag 随消息用于文本前缀与调试器分类；门面本身不按标签过滤。

Unity Editor 中的全部日志等级始终保留调用，不受运行时日志宏控制；Player 构建继续按照 `ENABLE_LOG`、精确等级宏及 `ENABLE_*_AND_ABOVE_LOG` 宏裁剪调用。

---

## 文件

```csharp
Log.Debug(LogTag.Component, "msg");
Log.Warning(LogTag.Asset, "msg");
Log.Error(LogTag.Table, "msg");
Log.Fatal(LogTag.Network, "msg");
Log.Fatal(LogTag.Network, exception);   // 默认实现文本化后创建新的 Exception，不保留原异常实例堆栈
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

## 行为与边界

五个等级分别提供 object/string 消息重载及 1～16 个泛型格式值重载，没有专用 Exception 或 PrintFatal 接口。消息重载原样转发，不展开内容中的 `{0}`；格式重载按声明顺序把第 j 个值交给零基占位符。泛型没有额外 where 约束，空引用和 nullable 值沿当前 Txt 实现处理。门面在 Helper 为空时静默跳过，也不执行方法体内 Txt.Format；但编译保留的调用，其外部实参表达式已先求值。Helper 有值时，模板错误、参数 ToString 和具体 Helper 的异常均可向外传播。

Conditional 是调用点编译裁剪，多个属性按 OR 生效；所有条件不满足时连实参求值一起去掉，不是运行时开关，反射执行也不自动获得相同过滤。SetHelper 只替换引用，允许 null，不 Initialize、不释放旧实现、不撤销旧实现已排队的输出。

默认 LogHelper 在非 WebGL 初始化时捕获当前 SynchronizationContext 和线程编号，必须由 Unity 主线程初始化链调用；跨线程且存在上下文时才 Post，否则直接输出，不能由类名推断一定切主线程。Post 捕获 message 原引用而非内容快照，无 Shutdown/排队取消机制，回调失败不在原调用方同步栈处理。WebGL 直接调用 Unity 日志接口。

Debug/Info 映射 Log、Warning 映射 LogWarning、Error 映射 LogError，Fatal 用格式后文本新建 Exception 再 LogException，本身不 throw 该异常或停止游戏。未知等级会尝试 message.ToString 后抛 InvalidOperationException，message=null 可能先抛空引用异常。前缀含富文本 color，tag/message 不转义；格式化会创建字符串，不承诺零 GC。LogTagDescriptionAttribute 只允许字段上的单个、不继承描述，构造器允许空/null，不参与日志过滤。

## 关联文档

- [LogTag.md](LogTag.md)
