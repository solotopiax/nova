# NetworkManager

**类签名**：`internal sealed partial class NetworkManager : NetworkManagerBase`  
**命名空间**：`NovaFramework.Runtime`  
**对外入口**：`NetworkComponent` / `Nova.Network`

网络路由管理器。它把 HostKey 与 NetCmd 两套 Luban 表加载到本地缓存，再提供 URL 解析、表查询、网络可用性检测与服务器时间获取能力。

## 文件组成

| 文件 | 作用 |
|---|---|
| `NetworkManager.cs` | 初始化、同步/异步加载、URL 与工具 API |
| `NetworkManager.Visitors.cs` | 缓存字段与 `CmdCacheEntry` |
| `NetworkManager.Methods.cs` | 从 `LubanDataCache` 构造 Host/指令缓存 |
| `NetworkManagerBase.cs` | 抽象基类，`Priority = 10` |

## 关键字段

| 字段 | 类型 | 说明 |
|---|---|---|
| `m_AssetManager` | `IAssetManager` | 资源管理器，负责加载 HostKey/NetCmd 数据资源 |
| `m_HttpManager` | `IHttpManager` | 供公网 IP 查询等网络工具使用 |
| `m_HostKeyUnitSettings` | `List<HostKeyUnitSetting>` | HostKey 数据单元列表 |
| `m_NetCmdUnitSettings` | `List<NetCmdUnitSetting>` | NetCmd 数据单元列表 |
| `m_NetworkDatas` | `Dictionary<string, ITable>` | 已构造的 Luban 表实例，键为表类型名 |
| `m_HostKeyCache` | `Dictionary<string, string>` | HostKey 名称到 Host URL 的映射 |
| `m_CmdCache` | `Dictionary<string, CmdCacheEntry>` | 复合键到网络指令缓存项的映射 |
| `m_CmdRowIndex` | `Dictionary<string, INetworkCmdRow>` | 按 `row.Name` 建的快速索引 |
| `m_ServerTimeFetcher` | `Func<UniTask<long>>` | 业务层注入的取时委托 |

## 公开 API

```csharp
public override void Initialize(NetworkManagerConfig config)
public override void Update()
public override void Shutdown()

public override UniTask<bool> LoadNetCmdsAsync()
public override bool LoadNetCmdsSync()

public override string GetNetCmdUrl(string tbName, string dtName)
public override string GetNetCmdUrl<T>(string dtName) where T : class, ITable
public override string ResolveNetCmdUrl(INetworkCmdRow cmdRow)
public override INetworkCmdRow ResolveNetCmdRow(string cmdName)
public override IEnumerable<string> GetAllNetCmdUrls()

public override T GetNetCmd<T>() where T : class, ITable
public override ITable GetNetCmd(string tbName)

public override bool CheckNetworkActive()
public override string UrlEncode(string str)
public override UniTask<string> QueryPublicIPAddressAsync()
public override string QueryLocalIPAddress()

public override void SetServerTimeFetcher(Func<UniTask<long>> fetcher)
public override UniTask FetchServerTimeAsync()
public override long ServerTime { get; }
```

## 加载流程

### Phase 1：读资源，写缓存

1. 通过 `m_AssetManager` 组装 `LoadAssetAsyncFunc` / `LoadAssetSyncFunc`
2. 为每个有效的 `HostKeyUnitSetting` 创建 `LubanDataReceiver`
3. 为每个有效的 `NetCmdUnitSetting` 创建 `LubanDataReceiver`
4. 所有数据统一写入局部 `LubanDataCache`

### Phase 2：构造表与运行时索引

`BuildTablesFromCache` 会：

1. 用 `IConfigManager.Namespace` 构造 `HostKeyTables`
2. 把表实例存入 `m_NetworkDatas`
3. 从实现了 `ITable<INetworkHostKeyRow>` 的表里提取 HostKey 到 `m_HostKeyCache`
4. 用同样方式构造 `NetworkTables`
5. 从实现了 `ITable<INetworkCmdRow>` 的表里提取指令缓存到 `m_CmdCache`

## 路由规则

### 复合键

`m_CmdCache` 的键不是单纯 `row.Name`，而是：

```csharp
string compositeKey = tableTypeName + "." + row.Name;
```

也就是：

```csharp
GetNetCmdUrl("TbUserCmd", "Login")
```

会先命中 `TbUserCmd.Login`，再用缓存项里的 `HostKey + Path` 拼完整 URL。

### 单键索引

`ResolveNetCmdRow(string cmdName)` 走的是 `m_CmdRowIndex`。  
这里按 `row.Name` 单键索引，同名后写会覆盖前写，因此它适合“全局唯一命令名”的业务约束场景，不适合跨表同名精确寻址。

## 使用示例

```csharp
bool success = await Nova.Network.LoadAsync();
if (!success)
{
    return;
}

string loginUrl = Nova.Network.GetNetCmdUrl("TbUserCmd", "Login");
string loginUrl2 = Nova.Network.GetNetCmdUrl<TbUserCmd>("Login");

INetworkCmdRow row = Nova.Network.ResolveNetCmdRow("Login");
string fullUrl = Nova.Network.ResolveNetCmdUrl(row);

TbHostKey hostTable = Nova.Network.GetNetCmd<TbHostKey>();
```

## 注意事项

- 当前实现统一通过 `IAssetManager` 读取 HostKey / NetCmd 数据资源。
- 业务侧通常通过 `Nova.Network` / `NetworkComponent` 使用这些能力，而不是直接接触 `internal` 的 `NetworkManager` 实现。
- `GetNetCmdUrl` 的真实键规则是“表类型名 + 行名”，不是仅靠行名。
- `GetAllNetCmdUrls` 只收集 `HTTP_GET`、`HTTP_POST`、`HTTP_URL` 三类指令。

## 关联文档

- [NetworkComponent.md](../NetworkComponent.md)
- [INetworkManager.md](INetworkManager.md)
- [LubanDataReceiver.md](../../../Core/Table/LubanDataReceiver.md)
- [LubanTablesLoader.md](../../../Core/Table/LubanTablesLoader.md)
