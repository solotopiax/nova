# NetworkComponentInspector

类签名： [CustomEditor(typeof(NetworkComponent))] internal sealed partial class NetworkComponentInspector : BaseComponentInspector  
命名空间： NovaFramework.Editor  
目标组件： NovaFramework.Runtime.NetworkComponent

Network 组件的 Inspector。它提供三个管理器实现类选择器、HostKey / NetCmd 导出、Proto 协议管理，以及 HTTP 和 WebSocket 设置。

## 文件表

| 文件 | 说明 |
|---|---|
| NetworkComponentInspector.cs | OnEnable 属性绑定、OnDisable 清理 FileWatcher、OnInspectorGUI 绘制调度 |
| NetworkComponentInspector.Visitors.cs | SerializedProperty、类型名列表、导出与 FileWatcher 状态 |
| NetworkComponentInspector.Methods.cs | 管理器选择、表格导出、Proto、HTTP 和 WebSocket 绘制方法 |

## 关键绑定

| 字段 | 类型 | 说明 |
|---|---|---|
| m_CurNetworkManagerTypeName | SerializedProperty | Network 管理器实现类选择 |
| m_CurHttpManagerTypeName | SerializedProperty | HTTP 管理器实现类选择 |
| m_CurWebSocketManagerTypeName | SerializedProperty | WebSocket 管理器实现类选择 |
| m_NetworkManagerTypeNames | List<string> | INetworkManager 实现类名列表 |
| m_HttpManagerTypeNames | List<string> | IHttpManager 实现类名列表 |
| m_WebSocketManagerTypeNames | List<string> | IWebSocketManager 实现类名列表 |
| m_Settings | SerializedProperty | NetworkSettings 配置对象 |
| m_HttpSettings | SerializedProperty | UWR 埋点、业务主备策略与 HTTP 请求超时配置 |
| m_WebSocketSettings | SerializedProperty | WebSocket 配置对象 |
| m_ProtoSettings | SerializedProperty | ProtoSettings 配置对象 |

HostKey 和 NetCmd 各自维护源目录、UnitSetting 列表、文件树折叠状态与 _configs 目录 FileWatcher。OnDisable 必须注销两个 FileWatcher，避免已销毁 Inspector 的回调继续执行。

## 绘制顺序

~~~text
OnInspectorGUI()
  ├─ DrawManagerSelectors()
  ├─ DrawDataFormat()
  ├─ DrawHostKeyExport()
  ├─ DrawNetCmdExport()
  ├─ DrawProtoManagement()
  ├─ DrawHttpSettings()
  ├─ DrawWebSocketSettings()
  └─ FinalRefreshInspectorGUI()
~~~

管理器区域只显示 Network、HTTP 与 WebSocket 三个类型选择器。类型列表来自对应接口的实现类型缓存。

## HTTP 设置

HTTP Foldout 按顺序展示“启用 UWR 网络埋点”、“业务请求优先最近成功域名”、“业务主备候选轮数”、“业务请求重试次数”与 HTTP 请求超时。默认分别为 `true`、`true`、`1`、`1` 和 `60`。HelpBox 的现行语义为：

1. HTTP 固定使用 UnityWebRequest 与系统 DNS。
2. HostKey + NetCmd 业务请求可优先使用当前进程内同 HostKey 最近成功的域名。
3. 只有在没有正式 HTTP 响应时才继续下一候选；每轮都执行完整的去重候选顺序。
4. 业务主备候选轮数最小为 1，默认为 1。
5. 全部配置轮数耗尽后才消耗一次重试，每次重试重新执行全部轮次。
6. RequestTimeout 由每次物理请求完整使用，不存在自动推导的链路总超时。

普通 HTTP API 只使用调用方给出的 URL；该 HelpBox 描述的是 HostKey + NetCmd 业务路由，不为单 URL 请求自动增加备用地址。

## HostKey、NetCmd 与 Proto 导出

HostKey / NetCmd 区共享数据格式选项。导出时由 NetworkExporter 使用暂存目录完成数据与 C# 验证，再经 OutputApplier 发布正式产物；切换标准 JSON / Binary 格式会同步单元后缀并清理同名反格式产物。

Proto 文件树仅在 Layout 事件预建 ProtoUnitSetting，避免 Repaint 阶段修改 SerializedProperty。点击全量导出后，ProtoExporter 统一完成协议到 C# 的编译并刷新 AssetDatabase。

## Inspector 布局

~~~text
[Network 管理器]       INetworkManager 实现类
[HTTP 管理器]          IHttpManager 实现类
[WebSocket 管理器]     IWebSocketManager 实现类
─────────────────────────────────────────────────
域名表与指令表导出
Proto 协议导出
─────────────────────────────────────────────────
HTTP 设置              UWR 埋点、最近成功、候选轮数、超时与 HelpBox
─────────────────────────────────────────────────
WebSocket 设置         连接、认证、心跳、重连与运行时通道列表
~~~

WebSocket 设置保留独立的连接超时、认证超时、心跳与自动重连配置；它不属于 HTTP 请求超时配置。

## 关联文档

- [NetworkComponent.md](../../../Runtime/Modules/Network/NetworkComponent.md)
- [HttpSettings.md](../../../Runtime/Modules/Network/Definitions/HttpSettings.md)
- [WebSocketSettings.md](../../../Runtime/Modules/Network/Definitions/WebSocketSettings.md)
- [ProtoSettings.md](../../../Runtime/Modules/Network/Definitions/ProtoSettings.md)
- [EditorUtil.Luban.ConfigSyncer.md](../../EditorUtil/EditorUtil.Luban/EditorUtil.Luban.ConfigSyncer.md)
