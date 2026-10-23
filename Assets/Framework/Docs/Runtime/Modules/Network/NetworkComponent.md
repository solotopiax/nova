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
| NetworkComponent.Kit.cs | Kit Service 惰性单例容器与项目标准 Proto 请求门面 |

## 管理器关系

~~~text
NetworkComponent
  ├── IHttpManager      → HttpManager       Priority = 8
  ├── IWebSocketManager → WebSocketManager  Priority = 9
  └── INetworkManager   → NetworkManager    Priority = 10
~~~

Awake 按 HTTP、Network、WebSocket 的顺序创建管理器；Start 使用 Inspector 配置初始化它们。LoadAsync / LoadSync 只负责加载 HostKey 与 NetCmd 路由数据，成功后将 IsLoadOver 置为 true。

创建失败会抛异常，前面已创建的 Manager 不在 Awake 中回滚；Start 也不是三管理器初始化事务。GetCurrentSettings/配置属性返回活对象，序列化类名不证明实例已创建或配置已生效。LoadAsync 合并进行中的组件调用，失败转为 false 后允许重试；LoadSync 不合并正在执行的异步轮次。OnDestroy 只清共享任务字段、IsLoadOver 与 Kit 字典，不取消底层请求或完成原等待者；迟到异步加载仍可能写回状态。Kit 构造异常不缓存，容器没有锁，Clear 不会 Dispose 实例。

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
UniTask<NetResponse<TResp>> SendProtoAsync<TReq, TResp>(
    INetworkCmdRow cmdRow, Func<PbNetReqHeader, TReq> requestFactory,
    MessageParser<TResp> parser, CancellationToken cancellationToken = default)
    where TReq : class, IMessage<TReq>
    where TResp : class, IMessage<TResp>;

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

UniTask<HttpResponse> GetAsync(
    string url, float requestTimeout, string headerInfos, CancellationToken cancellationToken);
UniTask<HttpResponse> PostAsync(
    string url, string contentString, float requestTimeout, string headerInfos, CancellationToken cancellationToken);
UniTask<HttpResponse> PostRawDataAsync(
    string url, byte[] contentBytes, float requestTimeout, string headerInfos, CancellationToken cancellationToken);
UniTask<HttpResponse> PostFileAsync(
    string url, string bodyJsonData, byte[] fileBytes, string fileName,
    float requestTimeout, string headerInfos, CancellationToken cancellationToken);
~~~

Get/Post/PostRawData/PostFile 只使用调用方给出的单个 URL，不套用 HostKey + NetCmd 主备、轮数或 RetryRequestCount。旧签名没有令牌；新重载在全部原参数之后接收 CancellationToken，原参数必填以保留旧default调用兼容。PostAsync/PostRawDataAsync 默认发送二进制 Content-Type，JSON/form 必须按服务端契约在 headerInfos 的 JSON 键值对中显式覆盖 Content-Type，并正确编码请求体。每次返回的 HttpResponse 都须在 try/finally 中 ReferencePool.Put；先复制所需字段，不跨 UI 代际保留池对象。

新入口要求当前管理器实现公开可选接口 `ICancellableHttpManager`，不支持时发送前抛 `NotSupportedException`，旧 `IHttpManager` 的成员和自定义实现不变。内置实现预取消不发送且返回 `NotReachedServer` 失败响应；在飞取消于 Update 轮询中 Abort，返回失败响应而非抛取消异常。页面/Service拥有者需退出时主动取消传入的令牌，仍等待请求结果并唯一归池；Shutdown及令牌源Dispose不等于取消，取消也不代表服务端回滚。完整契约见 [IHttpManager.md](HttpManager/IHttpManager.md#可选物理取消能力)。

项目自有标准 Protobuf + AES 请求通过 SendProtoAsync 复用内部 ResolveNetCmdUrls → NetService → HttpManager 的完整主备、最近成功优先、候选轮和重试，不自行挑选域名，不直接调用隐藏的 NetService/NetBuilder。该门面只负责前置与转发；不新增业务协议、身份写入或另一套算法。

## 项目标准 Proto 请求

- cmdRow、requestFactory、parser 任一 null，立即抛 ArgumentNullException；随后检查取消令牌，Way 非精确 HTTP_POST 抛 ArgumentException。Network 路由未加载或管理器不支持业务主备链时抛 InvalidOperationException。
- 调用前完成 Nova.Start 的组件绑定、Config.LoadAsync 和 Network.LoadAsync；Config 未加载、AppConfigs 缺失或当前入口未绑定时抛 InvalidOperationException，不隐式加载。标准 Header 继续由原 NetBuilder 从已确认身份、配置、语言和平台取得。
- requestFactory 是同步、无副作用的字段映射，只调用一次。它必须把收到的 Header 绑定到正式请求的 Head，不修改身份；门面不使用反射验证或补写 Head。工厂返回 null 抛 InvalidOperationException，工厂异常直接传播；Header/工厂前后检查取消。
- 工厂完成后委托现 NetService.SendAsync 的 CancellationToken 重载；请求字节、AES 与请求头只由原链构建并冻结。URL_NOT_FOUND、AES_ENCRYPT_FAILED、NETWORK_ERROR、AES_DECRYPT_FAILED、PROTO_PARSE_FAILED 和服务端业务错误/失败业务体沿用原 NetResponse 语义。返回 NetResponse 不需要 ReferencePool.Put，底层 HttpResponse 由原链归还。
- 取消不会撤销服务端已发生的状态变化；标准链得到任何正式 HTTP 响应便停止切换。请求需明确幂等与重放规则；门面不续签、不重发业务错误、不登录或更新 UID/OpenID。登录、绑定和云存档仍优先用已有 Kit 专属入口。

普通 HTTP API 的新增业务 Service 可以由 Agent 在已授权项目源中创建，解析 DTO、错误和有限重试；缺少既有 Service 不构成永久阻断。未知协议、凭据或重放语义仍须明确。单 URL 的业务重试不会成为 Framework 主备链。生命周期取消使用新重载并保留finally归池；若项目仍用旧入口，只能停止后续尝试或拒绝迟到结果。不要以AttachExternalCancellation停止包装等待来替代物理取消，否则底层请求和池化响应可能失去拥有者。

下面使用当前已生成的应用配置协议展示字段映射，不自动替代 Config 模块已有远端刷新流程，也不证明该端点已验收：

~~~csharp
// 项目自己的 Service 方法；调用方已完成启动与正式路由加载。
INetworkCmdRow cmd = Nova.Network.ResolveNetCmdRow(confirmedCmdName);
NetResponse<PbNetAppCustomConfigResp> response = await Nova.Network.SendProtoAsync(
    cmd,
    head => new PbNetAppCustomConfigReq { Head = head, Key = confirmedKey },
    PbNetAppCustomConfigResp.Parser,
    cancellationToken);
~~~

新业务协议沿权威 .proto → network.export 正式生成 message → 项目 Service → UI/Procedure 调用 → 测试响应验收。ProtoExporter 只生成消息，不生成项目 Service；Service 无需网络父类，Kit<T>() 只是按类型缓存无参构造实例，不注入依赖或管理 IDisposable 生命周期。热更新增泛型调用还须按现 HybridCLR 链核对 AOT 泛型、link 与业务 DLL，源码编译不代表 Player/服务器验证。

WebSocketNetChannels、连接、断开、发送消息与事件代理见 [WebSocketManager.md](WebSocketManager/WebSocketManager.md)。

WebSocket 地址门面只拒绝 null/空串，不验证 ws/wss URI。SendMessage 的 true 表示消息所有权已交给通道缓冲，之后不要修改或再次回池；false 时仍由调用者持有。组件 RecycleMessage 拒绝 null，Manager 则忽略 null；都不防止重复归还。事件 add/remove 直接访问 Manager，订阅者负责在 owner 退出时注销，借用消息只在回调期间同步读取。管理器引用不存在时这些薄代理可能抛异常，不能把它们统一当作安全的 false/no-op。

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
