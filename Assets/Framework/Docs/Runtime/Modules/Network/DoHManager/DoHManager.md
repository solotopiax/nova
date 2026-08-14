# DoHManager

**类签名**：`internal sealed partial class DoHManager : DoHManagerBase`
**命名空间**：`NovaFramework.Runtime`
**全局访问**：`Nova.Network.DoHManager`

`DoHManager` 负责 DNS-over-HTTPS 查询与 IP 收集。每个主机名只查询 A 记录，并继续解析响应中的 CNAME；每个原始域名只创建一次绝对截止时间，根查询和全部 CNAME 层共用。启动预热遍历 `NetworkManager.GetAllHostKeyUrls()` 返回的全部 HostKey URL。DoH 只服务于 `HostKey + NetCmd` 业务请求；资源下载/CDN、热更新、WebSocket 和第三方 SDK 保持各自原有机制。请求复用缓存只保存“原始业务域名 -> 最终 IPv4”，诊断状态则单独保存原始域名、CNAME 子树和失败结果。

---

## § 2 文件表

| 文件 | 说明 |
|---|---|
| `Managers/DoHManager/Implements/DoHManager.cs` | 初始化、批量收集、单次查询、缓存清理 |
| `Managers/DoHManager/Implements/DoHManager.Visitors.cs` | 查询器缓存、结果缓存与配置字段 |
| `Managers/DoHManager/Implements/DoHManager.Methods.cs` | A 查询、CNAME 全链截止时间、解析与缓存提交辅助方法 |
| `Managers/DoHManager/Implements/DoHManagerBase.cs` | 抽象基类，`Priority = 11` |
| `Managers/DoHManager/Definitions/DoHManagerConfig.cs` | `UseDoH` / `DnsTimeoutSeconds` / `MaxIPAddressesPerHost` |
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
| `m_DNSTimeout` | `int` | `0`（初始化前）；`5000`（使用默认配置初始化后） | 单个原始域名完整解析链的超时时间（毫秒）；A 查询与全部 CNAME 层共用一次创建的绝对截止时间。`DnsTimeoutSeconds <= 0` 时转换为 `Timeout.Infinite`，正数安全换算为毫秒并限制在 `int.MaxValue` |
| `m_MaxIPAddressesPerHost` | `int` | `3` | 每个域名最多写入缓存的 IPv4 数量；小于等于 0 时不限制 |
| `m_QueryGeneration` | `int` | `0` | 当前查询代次；`Clear()` / `Shutdown()` 递增该值，使清理前发起的异步查询结果失效 |
| `m_AllCollectedIPAddresses` | `Dictionary<string, List<string>>` | `null` | key = 原始 URL；value = 旧兼容候选 API 使用的 IP URL 快照；业务 HTTPS 不直接使用这类 URL |
| `m_AllDomainIPAddresses` | `Dictionary<string, List<IPAddress>>` | `null` | key = 原始业务域名；value = 可供各请求链路复用的最终 IPAddress 列表，不包含 CNAME 中间域名根项 |
| `m_ResolutionRoots` | `Dictionary<string, DoHResolutionNode>` | `null` | key = 原始业务域名；value = 保留来源、CNAME 层级与失败状态的诊断树根 |
| `m_DNSAnswers` | `DNSAnswer[]` | `null` | 最近一次 `DNSQuery(...)` 的原始应答集合 |

---

## 当前实现说明

- `DoHData` 已不在当前源码中，JSON 解析由 `DoHClient` 与 `DNSAnswer.FromJSON(...)` 直接完成。
- `DnsTimeoutSeconds` 默认 5 秒。每个原始域名只创建一次截止时间；A 查询按顺序尝试 DoH 地址，后续 CNAME 继续使用同一截止时间的剩余部分。配置小于等于 0 时整条解析链无限等待，不会关闭 DoH；如需停止 DoH 查询，必须设置 `UseDoH = false`。
- `MaxIPAddressesPerHost` 默认 3。每个原始域名只保留应答顺序中的前 3 个 IPv4；小于等于 0 时不限制数量。
- 有限等待时，异步请求或响应体读取超时会中止当前请求；无限等待时不施加框架超时，当前端点返回失败后才会尝试下一个端点。`Clear()` / `Shutdown()` 仍会通过 `Dispose()` 中止等待中的请求。未取得 IP 时，候选规划器保留原始 URL，不阻断后续登录流程。
- `Clear()` / `Shutdown()` 会先递增查询代次，再中止查询器并清空缓存；清理前已经发起的 DNS 查询即使稍后返回，也不会重新写入 `DNSAnswers`、IP 缓存或解析诊断树。
- 启动批量预热会并发完成不同原始域名的完整解析链，每个域名分别应用 `DnsTimeoutSeconds`，不是整批域名共享一个计时器。
- `CollectAllIPAddresses(...)` 会让每个原始域名在自己的并发任务内完成 A 与 CNAME 解析，单个域名的意外异常不会中断其余域名；失败域名会以空结果提交并清除旧缓存。
- 启动预热输入是全部 HostKey URL，不再通过 NetCmd 反推，因此未被任何 Cmd 引用的 HostKey 也会被查询。
- `DNSQuery(...)` 与 `CollectAllIPAddresses(...)` 现在都会写入同一份 `host -> IPAddress[]` 缓存；手动查询和批量预热不会再各走各路。
- `GetHostName(...)` 现在接受 HTTP、HTTPS、WS、WSS URL，基于 `Uri.Host` 提取主机名，只返回域名/IP，不带端口。
- `BuildRequestUrlCandidatesAsync(...)` 是保留的旧兼容候选 API：缓存未命中会等待查询并重读缓存，IP 候选后始终追加一次原始 URL。`HostKey + NetCmd` 业务 HTTPS 不使用它直接把 URL 改为 IP，而是保留域名并将 IP 交给传输层；资源下载/CDN、热更新、WebSocket 和第三方 SDK 不调用它。
- `DoHRequestPlanner` 是 Framework 内部纯规划层，通过缓存读取与查询委托获得确定性输入，不创建 Manager，也不直接发起网络请求。
- 当 DoH 应答里出现 `CNAME` 时，`DoHManager` 会把原始域名的同一个绝对截止时间传给别名目标，递归层不会重新获得完整超时时间；最终 IP 整体替换原始域名缓存，CNAME 中间域名仅作为诊断子节点，不会污染根层缓存。
- 非空刷新结果按原始应答顺序去重并整体替换旧列表，同时刷新该主机名下全部 URL 快照；刷新失败、超时、意外异常或没有取得 IP 时删除该主机名的 IP 缓存和全部 URL 快照，避免继续使用过期地址。
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
WhenAll(ResolveUrlSafelyAsync(url)) // urls = NetworkManager.GetAllHostKeyUrls()（全部 HostKey，已去重）
  │
  ├─ 每个原始域名创建一次 deadlineUtc
  ├─ QueryHostAnswersAsync(hostName, deadlineUtc)
  │    └─ QueryAsync(A, deadlineUtc)
  ├─ ResolveIPAddressesAsync(..., deadlineUtc)
  │    ├─ A         → 写入本地 resolvedIPs
  │    └─ CNAME    → 传递同一个 deadlineUtc，添加诊断子节点并递归
  └─ 每个域名形成 HostResolutionResult 后，串行 CommitResolutionResult
       ├─ 写入 ResolutionRoots[hostName]（失败结果也保留）
       ├─ ReplaceCachedIPs(hostName, resolvedIPs) // 非空刷新整体替换；空结果删除旧缓存
       └─ RefreshCachedUrlsForHost(...)          // 同步刷新同一主机名的全部 URL 快照
```

### BuildRequestUrlCandidatesAsync 旧兼容规划

```
GetHostName(originalUrl)                         // 仅 HTTP / HTTPS / WS / WSS
  ├─ DoH 关闭 / URL 无效 / localhost / IP literal → [originalUrl]
  └─ 可查询域名
       ├─ GetIPAddresses(host) 命中 → 使用缓存
       └─ 未命中 → await DNSQuery(originalUrl) → 再次读取缓存
            ├─ canUseIpCandidate = true → [IP URLs..., originalUrl]
            └─ canUseIpCandidate = false → [originalUrl]
```

IP URL 复用同一个替换 helper，保留 scheme、端口、路径与查询字符串；IPv6 host 会按 URI 规则带方括号。该格式仅供旧兼容 API 使用，业务 HTTPS 必须保留原域名 URL。

---

## § 10 常见误区

| 误区 | 正确理解 |
|---|---|
| 以为 m_UseDoH = false 时手动 DNSQuery 仍会去请求 DoH | `UseDoH = false` 时 `CollectAllIPAddresses` 与 `DNSQuery` 都会直接返回，不会写缓存 |
| 以为 DNSAnswers 保留历史记录 | m_DNSAnswers 每次 DNSQuery 都会覆盖，只保留最近一次的结果 |
| 在 Clear() 之后立即读取诊断树或缓存 | Clear 会同时清空 URL 快照、原始域名 IP 缓存和解析诊断树，需重新查询 |
| 以为 m_DoHClients 会自动释放 | `Clear()` 会先使旧查询代次失效，再对每个 `DoHClient` 执行 `Dispose()`，随后清空查询器、URL、IP 与诊断树缓存；当前不是简单把字典置 null |

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
// ips 为解析到的 IPv4 地址数组

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
| 业务 HTTPS 的缓存命中 | `HttpManager` 只在 Inspector 启用 DoH 且传输层支持指定连接 IP 时使用缓存；URL、Host、TLS SNI 与证书校验始终保持原域名 |
| DoH 查询失败的日志 | DNSQuery / CollectAllIPAddresses 查询失败时不会抛断其余域名；失败域名会清除旧缓存，业务请求最终继续使用主、备系统 DNS 兜底 |
| WebGL 平台 DoH 可用性 | `DoHClient` 当前基于 `HttpWebRequest` / `GetResponseAsync()`；不同平台可用性仍需实机验证 |
| 自定义 DoH 服务器 | 当前 DoHClient 使用默认 DoH 服务商（如 Cloudflare），如需更换请修改 DoHClient 内 URL 常量 |

---

## § 13 关联文档

- [NetworkComponent.md](../NetworkComponent.md)
- [HttpManager.md](../HttpManager/HttpManager.md)
