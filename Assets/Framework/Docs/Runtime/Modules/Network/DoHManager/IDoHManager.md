# IDoHManager

**类签名**：`public interface IDoHManager`
**命名空间**：`NovaFramework.Runtime`

DoH 管理器公开接口，定义 DNS-over-HTTPS 查询与 IP 地址收集的全部契约。外部通过此接口与 DoH 模块交互，避免直接依赖内部实现类。

---

## 文件列表

| 文件 | 说明 |
|------|------|
| `IDoHManager.cs` | DoH 管理器接口定义 |

## 公开 API

```csharp
// 初始化
void Initialize(DoHManagerConfig config)

// 遍历全部 HostKey URL，异步预热各域名的 IP 地址
UniTask CollectAllIPAddresses(IEnumerable<string> urls)

// 对指定 URL 进行 DoH DNS 查询，结果写入内部缓存
UniTask DNSQuery(string url)

// 根据 DoH 缓存与即时查询结果构造候选；顺序为 IP 候选 -> 原始 URL
UniTask<IReadOnlyList<string>> BuildRequestUrlCandidatesAsync(string originalUrl, bool canUseIpCandidate)

// 从 URL 中提取主机名（域名部分）
string GetHostName(string url)

// 通过主机名获取已收集的 IP 地址数组，未收集时返回 null
IPAddress[] GetIPAddresses(string hostName)

// 清空所有已收集的 IP 地址与 DNS 缓存
void Clear()

// 所有已收集的 IP 地址，<原始 URL, 替换 IP 后的 URL 列表>
IReadOnlyDictionary<string, List<string>> AllCollectedIPAddresses { get; }

// 所有域名对应的 IP 地址，<主机名, IPAddress 列表>
IReadOnlyDictionary<string, List<IPAddress>> AllDomainIPAddresses { get; }

// 原始业务域名到解析诊断树根；CNAME 只作为子节点出现
IReadOnlyDictionary<string, DoHResolutionNode> ResolutionRoots { get; }

// 最近一次 DNSQuery 返回的 DNS 应答集合
DNSAnswer[] DNSAnswers { get; }
```

`DNSQuery` / `GetHostName` / `BuildRequestUrlCandidatesAsync` 接受 HTTP、HTTPS、WS、WSS 绝对 URL。
候选规划在 DoH 关闭、URL 无效、host 为 `localhost` 或 IP literal 时只返回原始 URL；可查询域名会优先读取缓存，未命中时等待一次 `DNSQuery` 并重读缓存。只有 `canUseIpCandidate = true` 才会生成 IP URL，原始 URL 始终在列表末尾保留一次。该 API 为兼容既有调用而保留，业务 HTTPS 不应用它直接改写 URL 为 IP。

启动预热覆盖全部 HostKey，不要求 HostKey 已被 NetCmd 引用。Framework 的 DoH 路由范围仅为 `HostKey + NetCmd` 业务请求；资源下载/CDN、热更新、WebSocket 和第三方 SDK 不会因本接口进入 DoH 路由。

## 关联文档

- [DoHManager](DoHManager.md)
- [DoHManagerBase](DoHManagerBase.md)
- [DoHManagerConfig](Definitions/DoHManagerConfig.md)
