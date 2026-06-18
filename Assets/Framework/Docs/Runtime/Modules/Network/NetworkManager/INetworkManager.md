# INetworkManager

**类签名**：`public interface INetworkManager`
**命名空间**：`NovaFramework.Runtime`

Network 管理器公开契约接口，定义 NetCmd URL 路由、Luban 表查询、网络状态检测与服务器时间获取的全部方法。

---

## § 2 文件表

| 文件 | 类 | 说明 |
|---|---|---|
| `INetworkManager.cs` | `INetworkManager` | 接口定义 |

---

## § 3 继承关系

```
INetworkManager（public interface）
  └── NetworkManagerBase (abstract) : FrameworkManager, INetworkManager
        └── NetworkManager (sealed partial)
```

---

## § 5 完整公开 API

```csharp
// --- 初始化 ---
void Initialize(NetworkManagerConfig config)

// --- 数据加载 ---
UniTask<bool> LoadNetCmdsAsync()              // 两阶段 Luban 加载：并行 AB → BuildTablesFromCache
bool LoadNetCmdsSync()                        // 两阶段 Luban 加载：串行 AB → BuildTablesFromCache

// --- NetCmd 路由 ---
string GetNetCmdUrl(string tbName, string dtName)              // HostKey URL + Path，不存在返回 null
string GetNetCmdUrl<T>(string dtName) where T : class, ITable  // 泛型版本，提供编译期类型约束
string ResolveNetCmdUrl(INetworkCmdRow cmdRow)              // 由指令行解析完整 URL，HostKey 缺失返回 null
INetworkCmdRow ResolveNetCmdRow(string cmdName)             // 按 INetworkCmdRow.Name 检索指令行，未找到返回 null
IEnumerable<string> GetAllNetCmdUrls()        // 所有 HTTP 类型 URL（去重），供 DoH 使用

// --- Luban 表查询 ---
T GetNetCmd<T>() where T : class, ITable     // 按类型查找 Luban 表实例，不存在返回 null
ITable GetNetCmd(string tbName)              // 按表类型名查找 Luban 表实例，不存在返回 null

// --- 网络工具 ---
bool CheckNetworkActive()
string UrlEncode(string str)
UniTask<string> QueryPublicIPAddressAsync()
string QueryLocalIPAddress()

// --- 服务器时间 ---
void SetServerTimeFetcher(Func<UniTask<long>> fetcher)   // 注入服务器时间获取委托，由业务层提供实现
UniTask FetchServerTimeAsync()                           // 调用已注入委托获取 UTC0 时间戳，写入 ServerTime

// --- 状态属性 ---
long ServerTime { get; }                      // UTC0 毫秒时间戳
```

---

## § 11 使用示例

```csharp
// 路由查询（LoadNetCmdsAsync 完成后才可调用）
string url = Nova.Network.GetNetCmdUrl("TbNetCmd", "user.login");

// 按泛型类型查询 Luban 表实例
var table = Nova.Network.GetNetCmd<TbNetCmd>();

// 按表类型名查询 Luban 表实例（动态场景，类型名来自配置或反射）
ITable table2 = Nova.Network.GetNetCmd("TbNetCmd");
```

---

## § 13 关联文档

- [NetworkManager.md](NetworkManager.md)
- [NetworkManagerBase.md](NetworkManagerBase.md)
- [NetworkManagerConfig.md](Definitions/NetworkManagerConfig.md)
