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

## 使用示例

~~~csharp
INetworkCmdRow row = Nova.Network.ResolveNetCmdRow("Login");
string primaryUrl = Nova.Network.ResolveNetCmdUrl(row);

// Framework 内部业务协议会取得主备 URL 并完成切换。
// 业务层通常通过对应 Kit Service 发起请求。
~~~

## 关联文档

- [NetworkManager.md](NetworkManager.md)
- [NetworkComponent.md](../NetworkComponent.md)
- [INetworkHostKeyRow.md](Definitions/INetworkHostKeyRow.md)
- [INetworkCmdRow.md](Definitions/INetworkCmdRow.md)
