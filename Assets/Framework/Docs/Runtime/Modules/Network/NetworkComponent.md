# NetworkComponent

**类签名**：`[DisallowMultipleComponent] public sealed partial class NetworkComponent : FrameworkComponent, ICoroutineRunner`
**命名空间**：`NovaFramework.Runtime`
**全局访问**：`Nova.Network`

网络系统对外入口，持有并初始化四个并列管理器（DoH / Http / Network / WebSocket），通过 `ICoroutineRunner` 为 WebSocketManager 提供协程运行环境。

---

## § 2 文件表

| 文件 | 类 | 说明 |
|---|---|---|
| `NetworkComponent.cs` | `sealed partial NetworkComponent` | 主体：Awake（TypeCreator 创建四个 Manager）、Start（Initialize 四个 Manager）、LoadAsync / LoadSync（成功后后台触发一轮 DoH 预热）、OnDestroy、GetCurrentSettings |
| `NetworkComponent.Visitors.cs` | `partial NetworkComponent` | `[SerializeField]` 配置字段、Manager 实例属性、状态属性 |
| `NetworkComponent.Network.cs` | `partial NetworkComponent` | NetworkManager 接口透传：GetNetCmd<T> / GetNetCmd(tbName) / GetNetCmdUrl(tbName, dtName) / ResolveNetCmdUrl / ResolveNetCmdRow / CheckNetworkActive / UrlEncode / QueryLocalIPAddress / SetServerTimeFetcher / FetchServerTimeAsync |
| `NetworkComponent.Http.cs` | `partial NetworkComponent` | HTTP 短连接 API 透传：GetAsync/PostAsync/PostRawDataAsync/PostFileAsync |
| `NetworkComponent.DoH.cs` | `partial NetworkComponent` | DoH DNS 解析 API 透传：CollectAllIPAddresses / DNSQuery / GetHostName / GetIPAddresses / ClearDoH |
| `NetworkComponent.WebSocket.cs` | `partial NetworkComponent` | WebSocket 长连接 API 透传：事件转发、ConnectServer / DisconnectServer / SendMessage 等 |
| `NetworkComponent.Kit.cs` | `partial NetworkComponent` | Kit Service 惰性单例容器：`Kit<T>()` |

---

## § 3 继承关系

```
FrameworkComponent
  └── NetworkComponent (sealed partial) : ICoroutineRunner
        ├── 持有 IDoHManager       → DoHManager (sealed)
        ├── 持有 IHttpManager      → HttpManager (sealed)
        ├── 持有 INetworkManager   → NetworkManager (sealed)
        └── 持有 IWebSocketManager → WebSocketManager (sealed)
```

---

## § 4 关键字段表

**网络配置（`[SerializeField]`）**

| 字段 | 类型 | 默认值 | 说明 |
|---|---|---|---|
| `m_Settings` | `NetworkSettings` | `null` | 单套网络设置（HostKeySettings + NetCmdSettings），不再按地域分组 |

**管理器类型名（`[SerializeField]`，Inspector 下拉）**

| 字段 | 类型 | 说明 |
|---|---|---|
| `m_CurDoHManagerTypeName` | `string` | DoHManager 实现类全名 |
| `m_CurHttpManagerTypeName` | `string` | HttpManager 实现类全名 |
| `m_CurNetworkManagerTypeName` | `string` | NetworkManager 实现类全名 |
| `m_CurWebSocketManagerTypeName` | `string` | WebSocketManager 实现类全名 |

**管理器配置对象（`[SerializeField]`）**

| 字段 | 类型 | 说明 |
|---|---|---|
| `m_DoHSettings` | `DoHSettings` | DoH 管理器参数（UseDoH / DnsTimeoutSeconds） |
| `m_HttpSettings` | `HttpSettings` | HTTP 管理器参数（ConnectTimeout / RequestTimeout） |
| `m_WebSocketSettings` | `WebSocketSettings` | WebSocket 管理器参数（ConnectTimeout 等 7 项） |

**编辑器设置（`[SerializeField]`，`#if UNITY_EDITOR`）**

| 字段 | 类型 | 说明 |
|---|---|---|
| `m_ProtoSettings` | `ProtoSettings` | Protobuf 编辑器设置（全局唯一；.proto 文件路径与 protoc 编译配置） |

**加载状态**

| 字段 | 类型 | 默认值 | 说明 |
|---|---|---|---|
| `m_LoadTcs` | `UniTaskCompletionSource<bool>` | `null` | 防止并发重入的 Task 完成源，LoadAsync 进行中时非 null |
| `IsLoadOver` | `bool` (property) | `false` | NetCmd 数据已加载完成后置 true，LoadAsync 幂等保障；DoH 自动预热不改变其“仅表示路由已加载”的语义 |

**管理器实例（Awake 后有效）**

| 字段 | 类型 | 说明 |
|---|---|---|
| `m_DoHManager` | `IDoHManager` | DoH 管理器实例 |
| `m_HttpManager` | `IHttpManager` | HTTP 管理器实例 |
| `m_NetworkManager` | `INetworkManager` | Network 管理器实例 |
| `m_WebSocketManager` | `IWebSocketManager` | WebSocket 管理器实例 |

**Kit Service 容器**

| 字段 | 类型 | 说明 |
|---|---|---|
| `m_KitInstances` | `Dictionary<Type, object>` | Kit Service 惰性单例缓存（`Type → 实例`），由 `Kit<T>()` 管理生命周期 |

---

## § 5 完整公开 API

```csharp
// --- 生命周期 ---
protected override void Awake()

// --- ICoroutineRunner 实现 ---
Coroutine ICoroutineRunner.StartCoroutine(IEnumerator coroutine)
void ICoroutineRunner.StopCoroutine(Coroutine coroutine)

// --- NetCmd 数据加载 ---
UniTask<bool> LoadAsync()            // 幂等；并发调用时等待同一 TCS；成功后 IsLoadOver = true，并后台触发 DoH 预热（不等待完成）
bool LoadSync()                      // 同步加载，阻塞当前线程直至完成（无重入保护）；成功后同样后台触发 DoH 预热
NetworkSettings GetCurrentSettings() // 返回 m_Settings
T Kit<T>() where T : class, new()   // 获取或惰性创建 Kit Service 单例；子包 Service 通过此方法访问（不引 Kit asmdef）

// --- Kit 扩展（已下沉至主框架，NovaFramework.Runtime 程序集）---
// 扩展方法物理在 NetworkComponentKitExtensions.cs，与 NetService 同程序集，无额外依赖。
void SetDebugMode(bool debugMode)    // 调试模式开关；调试模式下 NetService 跳过 AES 加解密，发送 X-Debug-Plain 头

// --- NetworkManager 透传 (NetworkComponent.Network.cs) ---
T GetNetCmd<T>() where T : class, ITable    // 获取 Luban 表实例，不存在返回 null
ITable GetNetCmd(string tbName)              // 按表类型名获取 Luban 表实例
string GetNetCmdUrl(string tbName, string dtName)             // HostKey URL + Path，不存在返回 null
string ResolveNetCmdUrl(INetworkCmdRow cmdRow)              // 由指令行解析完整 URL
INetworkCmdRow ResolveNetCmdRow(string cmdName)             // 按 INetworkCmdRow.Name 检索指令行
bool CheckNetworkActive()
string UrlEncode(string str)
string QueryLocalIPAddress()
void SetServerTimeFetcher(Func<UniTask<long>> fetcher)   // 注入服务器时间获取委托，由业务层提供实现
UniTask FetchServerTimeAsync()                           // 调用已注入委托获取服务器 UTC0 时间戳，结果写入 ServerTime

// --- 状态属性 ---
bool IsLoadOver { get; }
long ServerTime { get; }
IReadOnlyDictionary<string, List<string>> AllCollectedIPAddresses { get; }
IReadOnlyDictionary<string, List<IPAddress>> AllDomainIPAddresses { get; }
IReadOnlyList<WebSocketScope.NetChannelBase> WebSocketNetChannels { get; }

// --- 管理器实例属性 ---
INetworkManager NetworkManager { get; }
IHttpManager HttpManager { get; }
IDoHManager DoHManager { get; }
IWebSocketManager WebSocketManager { get; }

// --- HTTP 短连接 (NetworkComponent.Http.cs) ---
UniTask<HttpResponse> GetAsync(string url, float requestTimeout = -1f, float connectTimeout = -1f, string headerInfos = null)
UniTask<HttpResponse> PostAsync(string url, string contentString, float requestTimeout = -1f, float connectTimeout = -1f, string headerInfos = null)
UniTask<HttpResponse> PostRawDataAsync(string url, byte[] contentBytes, float requestTimeout = -1f, float connectTimeout = -1f, string headerInfos = null)
UniTask<HttpResponse> PostFileAsync(string url, string bodyJsonData, byte[] fileBytes, string fileName, float requestTimeout = -1f, float connectTimeout = -1f, string headerInfos = null)

// --- DoH DNS 解析 (NetworkComponent.DoH.cs) ---
UniTask CollectAllIPAddresses()
UniTask DNSQuery(string url)
string GetHostName(string url)
IPAddress[] GetIPAddresses(string hostName)
void ClearDoH()

// --- WebSocket 长连接 (NetworkComponent.WebSocket.cs) ---
void ConnectServer(WebSocketScope.NetChannelType channelType, string serverAddress, bool autoReconnect = true)
void ReconnectServer(WebSocketScope.NetChannelType channelType, string serverAddress)
void DisconnectServer(WebSocketScope.NetChannelType channelType, string serverAddress)
void TestDisconnectServerAbnormally(WebSocketScope.NetChannelType channelType, string serverAddress)
bool IsWebSocketConnected(WebSocketScope.NetChannelType channelType, string serverAddress)
bool IsWebSocketAuthenticatedSuccess(WebSocketScope.NetChannelType channelType, string serverAddress)
WebSocketScope.NetMessageBase CreateMessage(WebSocketScope.NetChannelType channelType)
void RecycleMessage(WebSocketScope.NetMessageBase message)
bool SendMessage(WebSocketScope.NetChannelType channelType, string serverAddress, WebSocketScope.NetMessageBase message)

// --- WebSocket 事件 ---
event Action<int, string> OnWebSocketBeginConnect
event Action<int, string> OnWebSocketConnectSuccess
event Action<int, string> OnWebSocketConnectFail
event Action<int, string> OnWebSocketDisconnect
event Action<int, string> OnWebSocketReconnectFailed
event Action<int, string> OnWebSocketAuthenticateSuccess
event Action<int, string> OnWebSocketAuthenticateFail
event Action<WebSocketScope.NetChannelBase, WebSocketScope.NetMessageBase> OnWebSocketReceiveMessage
event Action<WebSocketScope.NetChannelBase, WebSocketScope.NetMessageBase> OnWebSocketSendMessage
```

---

## § 8 初始化时序

```
NetworkComponent.Awake()
  ├─ base.Awake()
  ├─ 1. TypeCreator.Create<IDoHManager>       → DoHManager
  ├─ 2. TypeCreator.Create<IHttpManager>      → HttpManager
  ├─ 3. TypeCreator.Create<INetworkManager>   → NetworkManager
  └─ 4. TypeCreator.Create<IWebSocketManager> → WebSocketManager

NetworkComponent.Start()
  ├─ 校验 m_Settings 非 null（null 抛出 InvalidOperationException）
  ├─ DoHManager.Initialize(DoHManagerConfig { UseDoH, DnsTimeoutSeconds })
  ├─ HttpManager.Initialize(HttpManagerConfig { ConnectTimeout, RequestTimeout, DoHManager })
  ├─ NetworkManager.Initialize(NetworkManagerConfig {
  │       HostKeyUnitSettings ← m_Settings.HostKeySettings.HostKeyUnits
  │       NetCmdUnitSettings  ← m_Settings.NetCmdSettings.NetCmdUnits })
  └─ WebSocketManager.Initialize(WebSocketManagerConfig { ConnectTimeout, AuthenticateTimeout,
           HeartBeatTimeInterval, HeartBeatTimeout, AutoReconnectMaxCounter,
           AutoReconnectTimeInterval, EnableAutoReconnect,
           AutoReconnectFailedUIAssetLocation, CoroutineRunner })

依赖顺序约束：
  DoHManager → HttpManager（HttpManager 注入 IDoHManager）
  NetworkComponent : ICoroutineRunner → WebSocketManager（注入 ICoroutineRunner）
```

---

## § 11 使用示例

```csharp
// 1. Inspector 中选择四个管理器实现类，填写 HostKey / NetCmd / HTTP / DoH / WebSocket 配置

// 2. 等待 NetCmd 数据加载（成功后会自动后台启动一轮 DoH 预热）
bool success = await Nova.Network.LoadAsync();

// 2.1 如需立刻强制重跑预热，可手动再次调用
await Nova.Network.CollectAllIPAddresses();

// 3. URL 路由查询
string url = Nova.Network.GetNetCmdUrl("TbNetCmd", "user.login");

// 4. Luban 表查询
var table = Nova.Network.GetNetCmd<TbNetCmd>();

// 5. 服务器时间（业务层注入具体实现后调用）
Nova.Network.SetServerTimeFetcher(async () =>
{
    var response = await Nova.Network.PostAsync(url, body);
    try
    {
        return ParseTimestampFromResponse(response);
    }
    finally
    {
        ReferencePool.Put(response);
    }
});
await Nova.Network.FetchServerTimeAsync();
long ts = Nova.Network.ServerTime;

// 6. WebSocket 连接与消息收发
Nova.Network.OnWebSocketConnectSuccess += (idx, addr) => Debug.Log($"Channel {idx} connected");
Nova.Network.ConnectServer(WebSocketScope.NetChannelType.TcpPb, "ws://game.server.com:8080/ws");
var msg = Nova.Network.CreateMessage(WebSocketScope.NetChannelType.TcpPb);
Nova.Network.SendMessage(WebSocketScope.NetChannelType.TcpPb, "ws://game.server.com:8080/ws", msg);
```

---

## § 13 关联文档

- [Runtime.md](../../Runtime.md)
- [NetworkComponentInspector.md](../../../Editor/Inspectors/NetworkComponentInspector/NetworkComponentInspector.md)
- [INetworkManager.md](NetworkManager/INetworkManager.md)
- [NetworkSettings.md](Definitions/NetworkSettings.md)
- [IHttpManager.md](HttpManager/IHttpManager.md)
- [IDoHManager.md](DoHManager/IDoHManager.md)
- [IWebSocketManager.md](WebSocketManager/IWebSocketManager.md)
- [IDeviceIdProvider.md](../SDK/Plugins/Device/IDeviceIdProvider.md) — Kit<T>() 访问的 SDK 插件之一；NetService 通过此接口读取设备 ID
- [NetService.md](NetService.md)
- [NetBuilder.md](NetBuilder.md)
- [NetworkComponentKitExtensions.md](NetworkComponentKitExtensions.md)

---

## Sample 演示位置

- 演示 view：`Samples/MainDemo/Scripts/Runtime/UIs/DemoNetworkView/`
- prefab：`Samples/MainDemo/Prefabs/UIs/DemoNetworkView/`（Prefab 期落地）
- 演示 API：`Nova.Network.GetAsync(url)` / `Nova.Network.PostAsync(url, body)` / `Nova.Network.ConnectServer(...)`（详见 [DESIGN.md § 3.2.9](../../../../../Samples/MainDemo/DESIGN.md)）
- 覆盖维度：生命周期（C）/ 重载族（C）/ 异步（F）/ 配置驱动（C）/ 事件回调（F）/ 错误边界（C）
