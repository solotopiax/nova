# DoHManager

**类签名**：`internal sealed partial class DoHManager : DoHManagerBase`
**命名空间**：`NovaFramework.Runtime`
**全局访问**：`Nova.Network.DoHManager`

`DoHManager` 负责 DNS-over-HTTPS 查询与 IP 收集。启动预热遍历 `NetworkManager.GetAllHostKeyUrls()` 返回的全部 HostKey URL；HostKey 范围外的 HTTP、Asset、WebSocket URL 在运行时缓存未命中时按需查询。请求复用缓存只保存“原始业务域名 -> 最终 IP”，诊断状态则单独保存原始域名、CNAME 子树和失败结果。

---

## § 2 文件表

| 文件 | 说明 |
|---|---|
| `Managers/DoHManager/Implements/DoHManager.cs` | 初始化、批量收集、单次查询、缓存清理 |
| `Managers/DoHManager/Implements/DoHManager.Visitors.cs` | 查询器缓存、结果缓存与配置字段 |
| `Managers/DoHManager/Implements/DoHManager.Methods.cs` | `GetDoHClient`、CNAME 递归解析、缓存写入辅助方法 |
| `Managers/DoHManager/Implements/DoHManagerBase.cs` | 抽象基类，`Priority = 11` |
| `Managers/DoHManager/Definitions/DoHManagerConfig.cs` | `UseDoH` / `DnsTimeoutSeconds` |
| `Managers/DoHManager/Definitions/DoHResolutionNode.cs` | DoH 查询来源、原始域名与 CNAME 诊断树节点 |
| `Managers/DoHManager/DoH/DoHClient.cs` | 单主机名 DoH 查询器 |
| `Managers/DoHManager/DoH/DoHRequestPlanner.cs` | 纯请求候选规划器，统一 scheme 校验、缓存未命中查询与原始 URL 兜底 |
| `Managers/DoHManager/DoH/DNSAnswer.cs` | 单条 DNS 应答记录 |
| `Managers/DoHManager/DoH/DNSCacheEntry.cs` | DoH 查询缓存条目 |
| `Managers/DoHManager/DoH/DNSAddress.cs` | Cloudflare / Google DoH 端点常量 |
| `Managers/DoHManager/DoH/ResourceRecordType.cs` | DNS 资源记录类型枚举 |

---

## § 3 继承关系

```
FrameworkManager
  └── DoHManagerBase (abstract) : IDoHManager   Priority = 11
        └── DoHManager (sealed partial)
```

---

## § 4 关键字段表

| 字段 | 类型 | 默认值 | 说明 |
|---|---|---|---|
| `m_DoHClients` | `Dictionary<string, DoHClient>` | `null` | 实例内的 DoH 查询器缓存，key = 主机名 |
| `m_UseDoH` | `bool` | `false` | false 时 CollectAllIPAddresses 直接返回，不执行 DNS 查询 |
| `m_DNSTimeout` | `int` | `0（初始化后默认 3000）` | 每个域名的一次 DoH 查询超时时间（毫秒）；所有候选地址共用，0 跳过查询，由 DnsTimeoutSeconds * 1000 计算得出 |
| `m_AllCollectedIPAddresses` | `Dictionary<string, List<string>>` | `null` | key = 原始 URL；value = 按当前域名缓存生成的“IP 直连候选 URL 列表”快照 |
| `m_AllDomainIPAddresses` | `Dictionary<string, List<IPAddress>>` | `null` | key = 原始业务域名；value = 可供各请求链路复用的最终 IPAddress 列表，不包含 CNAME 中间域名根项 |
| `m_ResolutionRoots` | `Dictionary<string, DoHResolutionNode>` | `null` | key = 原始业务域名；value = 保留来源、CNAME 层级与失败状态的诊断树根 |
| `m_DNSAnswers` | `DNSAnswer[]` | `null` | 最近一次 `DNSQuery(...)` 的原始应答集合 |

---

## 当前实现说明

- `DoHData` 已不在当前源码中，JSON 解析由 `DoHClient` 与 `DNSAnswer.FromJSON(...)` 直接完成。
- `DnsTimeoutSeconds` 默认 3 秒。每个域名的一次 DoH 查询独立计时，同次查询的所有候选地址共用该超时时间；配置为 0 时跳过 DoH 查询。异步请求或响应体读取超时会中止当前请求；未取得 IP 时，候选规划器保留原始 URL，不阻断后续登录流程。
- 启动批量预热会并发查询不同域名，每个域名分别应用 `DnsTimeoutSeconds`，不是整批域名共享一个计时器。
- `CollectAllIPAddresses(...)` 会并发查询，再串行写入缓存字典，避免竞态。
- 启动预热输入是全部 HostKey URL，不再通过 NetCmd 反推，因此未被任何 Cmd 引用的 HostKey 也会被查询。
- `DNSQuery(...)` 与 `CollectAllIPAddresses(...)` 现在都会写入同一份 `host -> IPAddress[]` 缓存；手动查询和批量预热不会再各走各路。
- `GetHostName(...)` 现在接受 HTTP、HTTPS、WS、WSS URL，基于 `Uri.Host` 提取主机名，只返回域名/IP，不带端口。
- `BuildRequestUrlCandidatesAsync(...)` 提供共享的 DoH 候选规划 API：缓存未命中会等待查询并重读缓存，IP 候选后始终追加一次原始 URL；HTTP / WebSocket 调用方的迁移由各自链路独立完成。
- `DoHRequestPlanner` 是 Framework 内部纯规划层，通过缓存读取与查询委托获得确定性输入，不创建 Manager，也不直接发起网络请求。
- 当 DoH 应答里出现 `CNAME` 时，`DoHManager` 会递归查询别名目标，把最终 IP 合并回原始域名的请求缓存；CNAME 中间域名仅作为该原始域名的诊断子节点，不会污染根层缓存。
- 失败、超时、NXDOMAIN 或无 IP 的查询仍会保留诊断根节点，并标记为“未获取 IP”。
- `DNSAddress` 现在是 DoH 服务端点常量定义，不是“收集到的单个 IP 地址封装”。

---

## § 5 完整公开 API

```csharp
// --- 生命周期 ---
void Initialize(DoHManagerConfig config)
void Update()
void Shutdown()      // 调用 Clear()

// --- DoH 核心接口 ---
UniTask CollectAllIPAddresses(IEnumerable<string> urls)     // urls 由 NetworkManager.GetAllHostKeyUrls() 提供
UniTask DNSQuery(string url)
UniTask<IReadOnlyList<string>> BuildRequestUrlCandidatesAsync(string originalUrl, bool canUseIpCandidate)
string GetHostName(string url)
IPAddress[] GetIPAddresses(string hostName)
void Clear()

// --- 状态访问 ---
IReadOnlyDictionary<string, List<string>> AllCollectedIPAddresses { get; }
IReadOnlyDictionary<string, List<IPAddress>> AllDomainIPAddresses { get; }
IReadOnlyDictionary<string, DoHResolutionNode> ResolutionRoots { get; }
DNSAnswer[] DNSAnswers { get; }
```

---

## § 9 关键算法

### CollectAllIPAddresses / DNSQuery 统一缓存写入

```
WhenAll(QueryDNSResultAsync(url))   // urls = NetworkManager.GetAllHostKeyUrls()（全部 HostKey，已去重）
  │
  ├─ 并发查询每个 URL 对应的 DNSAnswer[]
  └─ 查询完成后串行写入缓存
       ├─ hostName = GetHostName(url)            // Uri.Host，不带端口
       ├─ 创建或刷新 ResolutionRoots[hostName]（失败结果也保留）
       ├─ ResolveIPAddressesAsync(hostName, answers, visitedHosts, root)
       │    ├─ A / AAAA → 直接写入 resolvedIPs
       │    └─ CNAME    → root.Children 添加子节点并递归查询
       ├─ MergeCachedIPs(hostName, resolvedIPs)  // host -> IPAddress[] 单一真相源
       └─ CacheCollectedUrls(url, cachedIPs)     // 刷新该 URL 的候选 URL 快照
```

### BuildRequestUrlCandidatesAsync 请求期规划

```
GetHostName(originalUrl)                         // 仅 HTTP / HTTPS / WS / WSS
  ├─ DoH 关闭 / URL 无效 / localhost / IP literal → [originalUrl]
  └─ 可查询域名
       ├─ GetIPAddresses(host) 命中 → 使用缓存
       └─ 未命中 → await DNSQuery(originalUrl) → 再次读取缓存
            ├─ canUseIpCandidate = true → [IP URLs..., originalUrl]
            └─ canUseIpCandidate = false → [originalUrl]
```

IP URL 复用同一个替换 helper，保留 scheme、端口、路径与查询字符串；IPv6 host 会按 URI 规则带方括号。

---

## § 10 常见误区

| 误区 | 正确理解 |
|---|---|
| 以为 m_UseDoH = false 时手动 DNSQuery 仍会去请求 DoH | `UseDoH = false` 时 `CollectAllIPAddresses` 与 `DNSQuery` 都会直接返回，不会写缓存 |
| 以为 DNSAnswers 保留历史记录 | m_DNSAnswers 每次 DNSQuery 都会覆盖，只保留最近一次的结果 |
| 在 Clear() 之后立即读取诊断树或缓存 | Clear 会同时清空 URL 快照、原始域名 IP 缓存和解析诊断树，需重新查询 |
| 以为 m_DoHClients 会自动释放 | `Clear()` 会对每个 `DoHClient` 执行 `Dispose()`，随后 `Clear()` 两个缓存字典；当前不是简单把字典置 null |

---

## § 11 使用示例

```csharp
// 1. NetworkComponent.Awake 中由框架自动完成 DI 初始化（UseDoH、DnsTimeoutSeconds 由 Inspector 配置）

// 2. HostKey / NetCmd 加载完成后，NetworkComponent.LoadAsync / LoadSync 会自动后台预热全部 HostKey
bool success = await Nova.Network.LoadAsync();

// 3. 如需立刻刷新或显式重跑，可手动再次触发
await Nova.Network.CollectAllIPAddresses();

// 4. 获取某主机名对应的 IP 列表
string host = Nova.Network.GetHostName("https://api.example.com/login");
// host = "api.example.com"
IPAddress[] ips = Nova.Network.GetIPAddresses(host);
// ips 为解析到的 IPv4/IPv6 地址数组

// 5. 单独查询某 URL 的 DNS（按需）；查询结果同样会写入 host -> IP 缓存
await Nova.Network.DNSQuery("https://cdn.example.com/resource");
DNSAnswer[] answers = Nova.Network.DoHManager.DNSAnswers;

// 6. 清空缓存（例如切换服务器环境时）
Nova.Network.ClearDoH();
```

---

## § 12 注意事项

| 场景 | 正确做法 |
|---|---|
| HttpManager 请求期的缓存命中 | `HttpManager` 统一调用共享候选规划；DoH 启用时先读缓存、未命中则查询，只有传输后端声明支持时才使用 IP 候选 |
| DoH 查询失败的日志 | DNSQuery / CollectAllIPAddresses 查询失败时不会抛断整个流程；请求层会继续退回原始 URL 兜底 |
| WebGL 平台 DoH 可用性 | `DoHClient` 当前基于 `HttpWebRequest` / `GetResponseAsync()`；不同平台可用性仍需实机验证 |
| 自定义 DoH 服务器 | 当前 DoHClient 使用默认 DoH 服务商（如 Cloudflare），如需更换请修改 DoHClient 内 URL 常量 |

---

## § 13 关联文档

- [NetworkComponent.md](../NetworkComponent.md)
- [HttpManager.md](../HttpManager/HttpManager.md)
