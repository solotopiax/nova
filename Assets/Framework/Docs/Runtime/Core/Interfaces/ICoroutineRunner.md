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

## 关联文档

- [Interfaces](Interfaces.md)
