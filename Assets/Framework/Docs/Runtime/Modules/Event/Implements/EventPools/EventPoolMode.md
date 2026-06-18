# EventPoolMode

**类签名**：`[Flags] public enum EventPoolMode : byte`
**命名空间**：`NovaFramework.Runtime`

事件池行为模式枚举，使用 `[Flags]` 特性支持按位组合。控制事件池在无处理函数、多处理函数、重复处理函数等场景下的行为策略。

---

## 文件列表

| 文件 | 说明 |
|------|------|
| `Managers/Implements/EventPools/EventPoolMode.cs` | 枚举定义 |

## 枚举值

| 值 | 名称 | 说明 |
|-----|------|------|
| `0` | `Default` | 默认模式：必须存在有且只有一个事件处理函数，无 handler 时 `Fire` 抛异常 |
| `1` | `AllowNoHandler` | 允许不存在事件处理函数，`Fire` 时静默忽略 |
| `2` | `AllowMultiHandler` | 允许同一事件 ID 注册多个事件处理函数 |
| `4` | `AllowDuplicateHandler` | 允许同一处理函数被重复注册到同一事件 ID |

## 组合示例

```csharp
// 允许无订阅者 + 允许多订阅者（最常用组合）
EventPoolMode.AllowNoHandler | EventPoolMode.AllowMultiHandler

// 完全宽松模式
EventPoolMode.AllowNoHandler | EventPoolMode.AllowMultiHandler | EventPoolMode.AllowDuplicateHandler
```

## 关联文档

- [EventPool.md](EventPool.md)
- [EventManager.md](../../EventManager.md)
