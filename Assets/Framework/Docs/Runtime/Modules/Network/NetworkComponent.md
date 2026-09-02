# NetworkComponent

类签名： [DisallowMultipleComponent] public sealed partial class NetworkComponent : FrameworkComponent, ICoroutineRunner  
命名空间： NovaFramework.Runtime  
全局访问： Nova.Network

网络系统对外入口，创建并初始化 NetworkManager、HttpManager 与 WebSocketManager 三个管理器，并为 WebSocket 提供协程运行环境。

## 文件表

| 文件 | 说明 |
|---|---|
| NetworkComponent.cs | 创建和初始化三个管理器，加载 HostKey / NetCmd 数据 |
| NetworkComponent.Visitors.cs | 序列化配置、管理器实例与状态属性 |
| NetworkComponent.Network.cs | NetCmd 路由、网络状态与服务器时间代理 |
| NetworkComponent.Http.cs | HTTP 短连接代理 |
| NetworkComponent.WebSocket.cs | WebSocket 连接、消息与事件代理 |
| NetworkComponent.Kit.cs | Kit Service 惰性单例容器 |

## 管理器关系

~~~text
NetworkComponent
  ├── IHttpManager      → HttpManager       Priority = 8
  ├── IWebSocketManager → WebSocketManager  Priority = 9
  └── INetworkManager   → NetworkManager    Priority = 10
~~~

Awake 按 HTTP、Network、WebSocket 的顺序创建管理器；Start 使用 Inspector 配置初始化它们。LoadAsync / LoadSync 只负责加载 HostKey 与 NetCmd 路由数据，成功后将 IsLoadOver 置为 true。

## 序列化配置

| 字段 | 类型 | 说明 |
|---|---|---|
| m_CurNetworkManagerTypeName | string | INetworkManager 实现类名 |
| m_CurHttpManagerTypeName | string | IHttpManager 实现类名 |
| m_CurWebSocketManagerTypeName | string | IWebSocketManager 实现类名 |
| m_Settings | NetworkSettings | HostKey 与 NetCmd 数据单元 |
| m_HttpSettings | HttpSettings | UWR 埋点、HostKey + NetCmd 最近成功优先、完整轮数、重试次数与单次请求超时 |
| m_WebSocketSettings | WebSocketSettings | WebSocket 连接、认证、心跳和重连设置 |
| m_ProtoSettings | ProtoSettings | 仅 Editor 的协议导出设置 |

## 公开 API 摘要

~~~csharp
UniTask<bool> LoadAsync();
bool LoadSync();
NetworkSettings GetCurrentSettings();
T Kit<T>() where T : class, new();

T GetNetCmd<T>() where T : class, ITable;
ITable GetNetCmd(string tbName);
string GetNetCmdUrl(string tbName, string dtName);
string GetNetCmdUrl<T>(string dtName) where T : class, ITable;
string ResolveNetCmdUrl(INetworkCmdRow cmdRow);
INetworkCmdRow ResolveNetCmdRow(string cmdName);
bool CheckNetworkActive();
string UrlEncode(string str);
string QueryLocalIPAddress();
void SetServerTimeFetcher(Func<UniTask<long>> fetcher);
UniTask FetchServerTimeAsync();

UniTask<HttpResponse> GetAsync(
    string url, float requestTimeout = -1f, string headerInfos = null);
UniTask<HttpResponse> PostAsync(
    string url, string contentString, float requestTimeout = -1f, string headerInfos = null);
UniTask<HttpResponse> PostRawDataAsync(
    string url, byte[] contentBytes, float requestTimeout = -1f, string headerInfos = null);
UniTask<HttpResponse> PostFileAsync(
    string url, string bodyJsonData, byte[] fileBytes, string fileName,
    float requestTimeout = -1f, string headerInfos = null);
~~~

公开 HTTP API 只使用调用方给出的 URL。HostKey + NetCmd 业务协议的主备 URL 解析由内部 ResolveNetCmdUrls 与 NetService 协作完成，HttpManager 再按 Inspector 配置执行最近成功优先、完整候选轮与重试；业务 Service 不需要自行挑选域名。

WebSocketNetChannels、连接、断开、发送消息与事件代理见 [WebSocketManager.md](WebSocketManager/WebSocketManager.md)。

## 使用示例

~~~csharp
if (!await Nova.Network.LoadAsync())
{
    return;
}

// 读取主地址兼容 URL；业务 Kit 的 SendAsync 会使用内部主备链。
string url = Nova.Network.GetNetCmdUrl("TbUserCmd", "Login");
~~~

## 关联文档

- [NetworkManager.md](NetworkManager/NetworkManager.md)
- [HttpManager.md](HttpManager/HttpManager.md)
- [WebSocketManager.md](WebSocketManager/WebSocketManager.md)
- [NetworkComponentInspector.md](../../../Editor/Inspectors/NetworkComponentInspector/NetworkComponentInspector.md)
