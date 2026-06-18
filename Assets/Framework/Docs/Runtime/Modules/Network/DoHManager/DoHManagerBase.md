# DoHManagerBase

**类签名**：`internal abstract class DoHManagerBase : FrameworkManager, IDoHManager`
**命名空间**：`NovaFramework.Runtime`

DoH 管理器抽象基类，继承 `FrameworkManager` 并实现 `IDoHManager` 接口。声明所有 DoH 管理器的抽象方法，`Priority = 11`。由 `DoHManager` 密封实现。

---

## 文件列表

| 文件 | 说明 |
|------|------|
| `DoHManagerBase.cs` | DoH 管理器抽象基类定义 |

## 继承关系

```
FrameworkManager
  └── DoHManagerBase (abstract) : IDoHManager
        └── DoHManager (sealed partial)
```

## 关键字段/属性

| 字段 | 类型 | 说明 |
|------|------|------|
| `Priority` | `int`（override） | 框架管理器优先级，固定为 11 |

## 公开 API

```csharp
// 所有方法均为 abstract，由 DoHManager 实现
void Initialize(DoHManagerConfig config)
UniTask CollectAllIPAddresses(IEnumerable<string> urls)
UniTask DNSQuery(string url)
string GetHostName(string url)
IPAddress[] GetIPAddresses(string hostName)
void Clear()

// 属性
IReadOnlyDictionary<string, List<IPAddress>> AllDomainIPAddresses { get; }
IReadOnlyDictionary<string, List<string>> AllCollectedIPAddresses { get; }
DNSAnswer[] DNSAnswers { get; }

// 生命周期
void Update()
void Shutdown()
```

## 关联文档

- [IDoHManager](IDoHManager.md)
- [DoHManager](DoHManager.md)
