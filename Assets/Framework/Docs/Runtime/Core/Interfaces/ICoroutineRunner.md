# ICoroutineRunner

**类签名**：`public interface ICoroutineRunner`
**命名空间**：`NovaFramework.Runtime`

协程运行器接口，抽象了 Unity 协程的启动与停止操作。通过该接口可以将协程执行能力注入到非 MonoBehaviour 类中，实现协程调用与 MonoBehaviour 的解耦。

---

## 文件列表

| 文件 | 说明 |
|------|------|
| `ICoroutineRunner.cs` | 接口定义 |

## 公开 API

```csharp
Coroutine StartCoroutine(IEnumerator coroutine);
void StopCoroutine(Coroutine coroutine);
```

接口本身不约定空值、宿主激活状态、线程或跨宿主句柄行为。当前 `FrameworkComponent` 显式实现直接转发 Unity `StartCoroutine`，`StopCoroutine(null)` 跳过；这不代表任意自定义 Runner 都执行相同保护，也不提供异步取消令牌或任务完成等待。

## 关联文档

- [Interfaces](Interfaces.md)
