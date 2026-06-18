# DoHManager

**类签名**：`internal sealed partial class DoHManager : DoHManagerBase`
**命名空间**：`NovaFramework.Runtime`
**全局访问**：`Nova.Network.DoHManager`

`DoHManager` 负责 DNS-over-HTTPS 查询与 IP 收集。当前实现会遍历 `NetworkManager.GetAllNetCmdUrls()` 提供的 URL，异步查询 DNS 结果，并把所有结果统一缓存到 `主机名 -> IPAddress 列表` 映射表中；同时为已查询过的原始 URL 维护一份“替换 host 后的候选 URL 列表”快照，供调试与运行时观察。

---

## § 2 文件表

| 文件 | 说明 |
|---|---|
| `Managers/DoHManager/Implements/DoHManager.cs` | 初始化、批量收集、单次查询、缓存清理 |
| `Managers/DoHManager/Implements/DoHManager.Visitors.cs` | 查询器缓存、结果缓存与配置字段 |
| `Managers/DoHManager/Implements/DoHManager.Methods.cs` | `GetDoHClient`、CNAME 递归解析、缓存写入辅助方法 |
| `Managers/DoHManager/Implements/DoHManagerBase.cs` | 抽象基类，`Priority = 11` |
| `Managers/DoHManager/Definitions/DoHManagerConfig.cs` | `UseDoH` / `DnsTimeoutSeconds` |
| `Managers/DoHManager/DoH/DoHClient.cs` | 单主机名 DoH 查询器 |
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
| `m_DNSTimeout` | `int` | `0` | DNS 查询超时（毫秒），0 表示不限制，由 DnsTimeoutSeconds * 1000 计算得出 |
| `m_AllCollectedIPAddresses` | `Dictionary<string, List<string>>` | `null` | key = 原始 URL；value = 按当前域名缓存生成的“IP 直连候选 URL 列表”快照 |
| `m_AllDomainIPAddresses` | `Dictionary<string, List<IPAddress>>` | `null` | key = 主机名；value = 当前有效的可用 IPAddress 列表（DoH 运行期单一真相源） |
| `m_DNSAnswers` | `DNSAnswer[]` | `null` | 最近一次 `DNSQuery(...)` 的原始应答集合 |

---

## 当前实现说明

- `DoHData` 已不在当前源码中，JSON 解析由 `DoHClient` 与 `DNSAnswer.FromJSON(...)` 直接完成。
- `CollectAllIPAddresses(...)` 会并发查询，再串行写入缓存字典，避免竞态。
- `DNSQuery(...)` 与 `CollectAllIPAddresses(...)` 现在都会写入同一份 `host -> IPAddress[]` 缓存；手动查询和批量预热不会再各走各路。
- `GetHostName(...)` 现在基于 `Uri.Host` 提取主机名，只返回域名/IP，不带端口。
- 当 DoH 应答里出现 `CNAME` 且没有直接 `A/AAAA` 时，`DoHManager` 会继续递归查询别名目标，把最终解析出的 IP 合并回原始主机名缓存。
- `DNSAddress` 现在是 DoH 服务端点常量定义，不是“收集到的单个 IP 地址封装”。

---

## § 5 完整公开 API

```csharp
// --- 生命周期 ---
void Initialize(DoHManagerConfig config)
void Update()
void Shutdown()      // 调用 Clear()

// --- DoH 核心接口 ---
UniTask CollectAllIPAddresses(IEnumerable<string> urls)     // urls 由 NetworkManager.GetAllNetCmdUrls() 提供
UniTask DNSQuery(string url)
string GetHostName(string url)
IPAddress[] GetIPAddresses(string hostName)
void Clear()

// --- 状态访问 ---
IReadOnlyDictionary<string, List<string>> AllCollectedIPAddresses { get; }
IReadOnlyDictionary<string, List<IPAddress>> AllDomainIPAddresses { get; }
DNSAnswer[] DNSAnswers { get; }
```

---

## § 9 关键算法

### CollectAllIPAddresses / DNSQuery 统一缓存写入

```
WhenAll(QueryDNSResultAsync(url))   // urls = NetworkManager.GetAllNetCmdUrls()（已过滤 HTTP 类型，已去重）
  │
  ├─ 并发查询每个 URL 对应的 DNSAnswer[]
  └─ 查询完成后串行写入缓存
       ├─ hostName = GetHostName(url)            // Uri.Host，不带端口
       ├─ ResolveIPAddressesAsync(hostName, answers, visitedHosts)
       │    ├─ A / AAAA → 直接写入 resolvedIPs
       │    └─ CNAME    → 递归 QueryHostAnswersAsync(cnameHost)
       ├─ MergeCachedIPs(hostName, resolvedIPs)  // host -> IPAddress[] 单一真相源
       └─ CacheCollectedUrls(url, cachedIPs)     // 刷新该 URL 的候选 URL 快照
```

---

## § 10 常见误区

| 误区 | 正确理解 |
|---|---|
| 以为 m_UseDoH = false 时手动 DNSQuery 仍会去请求 DoH | `UseDoH = false` 时 `CollectAllIPAddresses` 与 `DNSQuery` 都会直接返回，不会写缓存 |
| 以为 DNSAnswers 保留历史记录 | m_DNSAnswers 每次 DNSQuery 都会覆盖，只保留最近一次的结果 |
| 在 Clear() 之后立即读取 AllCollectedIPAddresses | Clear 会清空所有缓存，需重新调用 CollectAllIPAddresses |
| 以为 m_DoHClients 会自动释放 | `Clear()` 会对每个 `DoHClient` 执行 `Dispose()`，随后 `Clear()` 两个缓存字典；当前不是简单把字典置 null |

---

## § 11 使用示例

```csharp
// 1. NetworkComponent.Awake 中由框架自动完成 DI 初始化（UseDoH、DnsTimeoutSeconds 由 Inspector 配置）

// 2. LoadNetCmds 完成后，NetworkComponent.LoadAsync / LoadSync 会自动在后台启动一轮 DoH 预热
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
| HttpManager 请求期的缓存命中 | `HttpManager` 会优先读 `host -> IP` 缓存；命中后按顺序尝试 IP 直连，未命中时会先触发一次 `DNSQuery(url)` |
| DoH 查询失败的日志 | DNSQuery / CollectAllIPAddresses 查询失败时不会抛断整个流程；请求层会继续退回原始 URL 兜底 |
| WebGL 平台 DoH 可用性 | `DoHClient` 当前基于 `HttpWebRequest` / `GetResponseAsync()`；不同平台可用性仍需实机验证 |
| 自定义 DoH 服务器 | 当前 DoHClient 使用默认 DoH 服务商（如 Cloudflare），如需更换请修改 DoHClient 内 URL 常量 |

---

## § 13 关联文档

- [NetworkComponent.md](../NetworkComponent.md)
- [HttpManager.md](../HttpManager/HttpManager.md)
