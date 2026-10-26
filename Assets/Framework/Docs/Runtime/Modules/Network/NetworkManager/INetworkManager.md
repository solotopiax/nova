# INetworkManager

类签名： public interface INetworkManager  
命名空间： NovaFramework.Runtime

Network 路由管理器公开契约，负责加载 HostKey / NetCmd 表、解析 URL、提供网络工具与服务器时间能力。

## 继承关系

~~~text
INetworkManager
  └── NetworkManagerBase
        └── NetworkManager
~~~

## 公开 API

~~~csharp
void Initialize(NetworkManagerConfig config);

UniTask<bool> LoadNetCmdsAsync();
bool LoadNetCmdsSync();

string GetNetCmdUrl(string tbName, string dtName);
string GetNetCmdUrl<T>(string dtName) where T : class, ITable;
string ResolveNetCmdUrl(INetworkCmdRow cmdRow);
IReadOnlyList<string> ResolveNetCmdUrls(INetworkCmdRow cmdRow);
INetworkCmdRow ResolveNetCmdRow(string cmdName);
INetworkHostKeyRow ResolveHostKeyRow(string hostKeyName);

T GetNetCmd<T>() where T : class, ITable;
ITable GetNetCmd(string tbName);

bool CheckNetworkActive();
string UrlEncode(string str);
UniTask<string> QueryPublicIPAddressAsync();
string QueryLocalIPAddress();

void SetServerTimeFetcher(Func<UniTask<long>> fetcher);
UniTask FetchServerTimeAsync();
long ServerTime { get; }
~~~

ResolveNetCmdUrl 保留单地址兼容入口，返回主地址或唯一有效地址。ResolveNetCmdUrls 返回业务协议使用的零到两个完整 URL：有效主地址在前、有效备用地址在后；无效值和重复地址会被过滤。框架内部的 NetService 使用后者执行主备请求。

`ResolveHostKeyRow(hostKeyName)` 按已加载 HostKey 行的 `Name` 忽略大小写查询，返回原始行。空名称或未命中返回 `null`，非空名称未命中会告警；跨表或表内同名时按已加载表遍历顺序取最后一条。它不触发加载、不修改或重新校验路由缓存；原始 `Value/FallbackValue` 与实际可用路由不同，发送仍使用原有路由解析链。

`GetNetCmd<T>()` / `GetNetCmd(tbName)` 的旧名称保留；它们可读取已加载的 HostKey 和 NetCmd 生成表。`tbName` 是表类型短名；`GetNetCmdUrl` 的 `dtName` 是 `INetworkCmdRow.Name`，两者不能互换。复合键查询仍是表类型名加行名。

自行实现 `INetworkManager` 的项目需要补充 `ResolveHostKeyRow`；既有接口形参名、复合缓存键和 HTTP 契约保持原语义。

## 使用示例

~~~csharp
INetworkCmdRow row = Nova.Network.ResolveNetCmdRow("Login");
string primaryUrl = Nova.Network.ResolveNetCmdUrl(row);
INetworkHostKeyRow host = Nova.Network.ResolveHostKeyRow("Business");

// Framework 内部业务协议会取得主备 URL 并完成切换。
// 业务层通常通过对应 Kit Service 发起请求。
~~~

## 关联文档

- [NetworkManager.md](NetworkManager.md)
- [NetworkComponent.md](../NetworkComponent.md)
- [INetworkHostKeyRow.md](Definitions/INetworkHostKeyRow.md)
- [INetworkCmdRow.md](Definitions/INetworkCmdRow.md)
