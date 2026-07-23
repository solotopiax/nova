# DoHClient

**类签名**：`public class DoHClient : IDisposable`
**命名空间**：`NovaFramework.Runtime`

针对单个主机名的 DoH（DNS-over-HTTPS）查询器，内置结果缓存与候选地址轮询机制。每个 `DoHClient` 实例绑定一个主机名，查询时按序尝试 Cloudflare 候选地址，首个成功即返回结果并写入缓存。每个域名的一次查询独立计时，所有候选地址共用调用方传入的超时时间。缓存有效期基于应答 TTL 自动管理，支持并发防重复查询。

---

## 文件列表

| 文件 | 说明 |
|------|------|
| `DoHClient.cs` | DoH 查询器实现 |

## 关键字段/属性

| 字段 | 类型 | 说明 |
|------|------|------|
| `s_EndpointsList` | `private static readonly string[]` | 候选地址列表（Cloudflare IPv4 主备 + 域名），按顺序尝试 |
| `m_HostName` | `readonly string` | 本查询器对应的主机名 |
| `m_AnswersCache` | `DNSCacheEntry` | DNS 结果缓存条目，有效期内直接返回 |
| `m_WaitingTask` | `UniTask<DNSAnswer[]>` | 当前正在执行的等待任务，防止同一主机重复并发查询 |
| `m_Random` | `readonly Random` | 使用加密安全种子的随机数生成器，用于 URL 填充 |

## 公开 API

```csharp
// 构造方法
DoHClient(string hostName)

// 清除本地 DNS 结果缓存，强制下次重新查询
void ClearCache()

// 异步查询 DNS，优先返回有效缓存；若有并发查询则等待其结果；否则轮询端点直到成功
// timeout: 当前域名一次 DoH 查询的超时时间（毫秒），所有候选地址共用；0 跳过 DoH 查询
UniTask<DNSAnswer[]> QueryAsync(int timeout)

// 释放资源
void Dispose()
```

## 查询流程

```
QueryAsync(timeout)
  ├─ timeout <= 0 → 返回 null，不发起 DoH 查询
  ├─ 缓存有效 → 直接返回 m_AnswersCache.Answers
  ├─ 同一域名已有查询 → await m_WaitingTask
  └─ 新查询
       ├─ 计算本次查询的统一截止时间
       └─ foreach endpoint in s_EndpointsList
            ├─ 按截止时间计算当前候选地址的剩余时间
            ├─ CreateRequest(endpoint, remainingTimeout)
            ├─ 异步接收响应和响应体，二者均受剩余时间限制
            ├─ 超时 → Abort 当前请求，尝试下一个候选地址
            ├─ HandleJSONResponse → 解析 Answer 数组 → DNSAnswer[]
            ├─ 成功 → 写入 DNSCacheEntry，返回
            └─ 失败 → 尝试下一个候选地址
       └─ 截止时间已到或全部失败 → 返回 null，并完成所有共享等待者
```

当前候选地址按以下顺序尝试：

1. `https://1.1.1.1/dns-query`
2. `https://1.0.0.1/dns-query`
3. `https://cloudflare-dns.com/dns-query`

## 关联文档

- [DNSAddress](DNSAddress.md)
- [DNSAnswer](DNSAnswer.md)
- [DNSCacheEntry](DNSCacheEntry.md)
- [ResourceRecordType](ResourceRecordType.md)
- [DoHManager](../DoHManager.md)
