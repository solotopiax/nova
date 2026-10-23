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

HostKey 的 Value 是主域名，FallbackValue 是备用域名。运行时只接收 HTTP(S) 绝对基础地址，首尾不能有空格，末尾不能带 /；两个地址都有效时保留相同协议的备用地址。这个检查保留原值，不规范化 URI，也未禁止 userinfo、query、fragment 或路径。NetCmd.Path 应为空或以 / 开头，此约束由配置/导出链维护，运行时缓存不重新校验，解析仍直接拼接字符串。

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

同步逐项读取，异步并行读取；资源加载代理取得 TextAsset 后立即 Release 句柄，再供数据接收器处理。Manager 层没有并发合流、取消 token 或加载代际校验；构表逐项写入现有缓存，失败不回滚，也不预先清旧缓存。无效 HostKey 行可能留下同名旧值；HostKey 错误不一定阻止整体 Ready。ConfigManager 不存在时失败，Namespace 为空时则跳过构表仍可能返回成功；成功标志不能替代预期表和路由存在性检查。

HostKey、复合指令键与单 Name 索引用 OrdinalIgnoreCase；表对象字典用默认大小写比较，并以类型短名为键。不同 namespace 同短名或跨表同 Name 可能覆盖。候选 URL 也忽略大小写去重，连 Path 大小写一起比较；它不执行 URI 路径规范化。传入自定义 row 的 null HostKey 仍可能触发字典参数异常，接口本身没有字段校验体。

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

`WaitUntilReadyAsync` 只在未 Ready 时附加调用方取消；已经 Ready 时立即完成。Shutdown 取消原共享 TCS、清缓存，但 Initialize 不重建该信号，也不取消在飞加载/时间请求；迟到加载仍可重新 MarkReady。旧等待者收到取消后，新调用者看到 Ready 的行为可能不同，不能据此复用同一实例跨生命周期。

ServerTime 是委托返回的 UTC0 毫秒快照，Update 不推进时钟；无取消或请求版本保护，旧请求可能后写覆盖。Manager 允许 null 清除 fetcher，组件门面却拒绝 null。CheckNetworkActive 只检查 Unity 接口可达性，不证明 DNS、互联网或后端可用；本地 IP 取首个 Up 的 Wi-Fi/Ethernet IPv4，枚举异常可传播。公网查询使用 `http://api.ipify.org`，复制正文后在 finally 归还响应，失败通常为空串；它无取消参数，也不校验正文是否真为 IP。UrlEncode 编码字符串片段，不是完整 URL 校验；组件会拒绝 null，Manager 则将 null/空串转为空串。

## 关联文档

- [INetworkManager.md](INetworkManager.md)
- [NetworkComponent.md](../NetworkComponent.md)
- [NetworkManagerConfig.md](Definitions/NetworkManagerConfig.md)
