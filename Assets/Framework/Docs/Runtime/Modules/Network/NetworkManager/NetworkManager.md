# NetworkManager

类签名： internal sealed partial class NetworkManager : NetworkManagerBase  
命名空间： NovaFramework.Runtime  
对外入口： NetworkComponent / Nova.Network

Network 路由管理器加载 HostKey 与 NetCmd 两套 Luban 表，构建运行时缓存，并提供 URL 解析、网络状态和服务器时间能力。

## 文件组成

| 文件 | 作用 |
|---|---|
| NetworkManager.cs | 初始化、同步/异步加载、URL 与网络工具 API |
| NetworkManager.Visitors.cs | 缓存字段与 CmdCacheEntry |
| NetworkManager.Methods.cs | 从 LubanDataCache 构建 HostKey / NetCmd 缓存 |
| NetworkManagerBase.cs | 抽象基类，Priority = 10 |

## 路由数据

| 缓存 | 作用 |
|---|---|
| m_HostKeyCache | HostKey 名称到主、备基础地址的映射 |
| m_CmdCache | 表类型名 + 指令行名到 HostKey、Path 的映射 |
| m_CmdRowIndex | 指令行 Name 到 INetworkCmdRow 的索引 |

HostKey 的 Value 是主域名，FallbackValue 是备用域名。非空地址必须是 HTTP(S) 基础地址，首尾不能有空格，末尾不能带 /；两个地址都有效时必须使用相同协议。NetCmd.Path 可为空，非空时必须以 / 开头。

## URL 解析

~~~text
ResolveNetCmdUrls(cmdRow)
  ├─ 主地址 + Path
  └─ 备用地址 + Path
~~~

运行时按顺序保留有效地址：主地址无效时备用地址成为唯一候选；备用地址无效时忽略；主备相同时去重。ResolveNetCmdUrl 只返回列表中的首项，以保持已有单 URL 调用方式。

NetService 使用 ResolveNetCmdUrls 的完整顺序发送业务请求。每次请求仍由 HTTP 管理器用系统 DNS 解析，服务器给出任意正式 HTTP 响应后不再切换备用域名。

## 加载流程

1. 对所有有效 HostKeyUnitSetting 与 NetCmdUnitSetting 加载 Luban 数据资源。
2. 先在局部 LubanDataCache 中汇总读取结果。
3. 构造 HostKey 表、NetCmd 表与三份运行时索引。
4. 构造成功后标记 Network Ready。

## 使用示例

~~~csharp
if (!await Nova.Network.LoadAsync())
{
    return;
}

string url = Nova.Network.GetNetCmdUrl("TbUserCmd", "Login");
INetworkCmdRow row = Nova.Network.ResolveNetCmdRow("Login");
~~~

## 注意事项

- GetNetCmdUrl 的复合键是 表类型名 + "." + 指令行名，不是单独的行名。
- ResolveNetCmdRow 按行名建立单键索引；跨表同名时后写入项覆盖先写入项。
- QueryPublicIPAddressAsync 使用 HTTP 管理器；业务代码通常通过 Nova.Network 使用公开能力，不直接依赖此 internal 实现。

## 关联文档

- [INetworkManager.md](INetworkManager.md)
- [NetworkComponent.md](../NetworkComponent.md)
- [NetworkManagerConfig.md](Definitions/NetworkManagerConfig.md)
