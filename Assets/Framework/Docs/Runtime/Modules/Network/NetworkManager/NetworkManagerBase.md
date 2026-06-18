# NetworkManagerBase

**类签名**：`internal abstract class NetworkManagerBase : FrameworkManager, INetworkManager`
**命名空间**：`NovaFramework.Runtime`

Network 管理器抽象基类，声明所有 abstract 成员，Priority = 10，由 `NetworkManager` 密封实现。

---

## § 2 文件表

| 文件 | 类 | 说明 |
|---|---|---|
| `NetworkManagerBase.cs` | `abstract NetworkManagerBase` | 基类声明：Priority = 10，全部 abstract 方法 |

---

## § 3 继承关系

```
FrameworkManager
  └── NetworkManagerBase (internal abstract) : INetworkManager   Priority = 10
        └── NetworkManager (internal sealed partial)
```

---

## § 4 关键字段表

| 字段 | 类型 | 默认值 | 说明 |
|---|---|---|---|
| `Priority` | `int`（override） | `10` | 框架管理器优先级（第 10 位 Update，倒数第 10 位 Shutdown） |

---

## § 5 完整公开 API

```csharp
// --- 优先级 ---
public override int Priority => 10

// --- 生命周期（abstract） ---
public abstract void Initialize(NetworkManagerConfig config)
public abstract override void Update()
public abstract override void Shutdown()

// --- 数据加载（abstract） ---
public abstract UniTask<bool> LoadNetCmdsAsync()
public abstract bool LoadNetCmdsSync()

// --- NetCmd 路由（abstract） ---
public abstract string GetNetCmdUrl(string tbName, string dtName)
public abstract string GetNetCmdUrl<T>(string dtName) where T : class, ITable
public abstract string ResolveNetCmdUrl(INetworkCmdRow cmdRow)
public abstract INetworkCmdRow ResolveNetCmdRow(string cmdName)
public abstract IEnumerable<string> GetAllNetCmdUrls()

// --- NetCmd 查询（abstract） ---
public abstract T GetNetCmd<T>() where T : class, ITable
public abstract ITable GetNetCmd(string tbName)

// --- 网络工具（abstract） ---
public abstract bool CheckNetworkActive()
public abstract string UrlEncode(string str)
public abstract UniTask<string> QueryPublicIPAddressAsync()
public abstract string QueryLocalIPAddress()

// --- 服务器时间（abstract） ---
public abstract void SetServerTimeFetcher(Func<UniTask<long>> fetcher)
public abstract UniTask FetchServerTimeAsync()
public abstract long ServerTime { get; }
```

---

## § 11 使用示例

不直接使用基类，通过 `INetworkManager` 接口操作。参见 [NetworkManager.md](NetworkManager.md) 示例。

---

## § 13 关联文档

- [INetworkManager.md](INetworkManager.md)
- [NetworkManager.md](NetworkManager.md)
