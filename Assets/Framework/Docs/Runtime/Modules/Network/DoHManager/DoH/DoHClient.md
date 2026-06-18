# DoHClient

**类签名**：`public class DoHClient : IDisposable`
**命名空间**：`NovaFramework.Runtime`

针对单个主机名的 DoH（DNS-over-HTTPS）查询器，内置结果缓存与多端点轮询机制。每个 `DoHClient` 实例绑定一个主机名，查询时按序尝试多个 DoH 端点（默认为 Cloudflare），首个成功即返回结果并写入缓存。缓存有效期基于应答 TTL 自动管理，支持并发防重复查询。

---

## 文件列表

| 文件 | 说明 |
|------|------|
| `DoHClient.cs` | DoH 查询器实现 |

## 关键字段/属性

| 字段 | 类型 | 说明 |
|------|------|------|
| `s_EndpointsList` | `private static readonly string[]` | 查询端点列表（默认 Cloudflare IPv4 主备 + 域名），按顺序尝试 |
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
// timeout: 每次端点请求超时时间（毫秒），0 表示不限制
UniTask<DNSAnswer[]> QueryAsync(int timeout)

// 释放资源
void Dispose()
```

## 查询流程

```
QueryAsync(timeout)
  ├─ 缓存有效 → 直接返回 m_AnswersCache.Answers
  ├─ 已有并发任务 → await m_WaitingTask
  └─ 新查询
       └─ foreach endpoint in m_EndpointsList
            ├─ CreateRequest(endpoint, timeout)   // 构建 HttpWebRequest，TLS 1.2
            ├─ 发送 GET 请求，接收 JSON 响应
            ├─ HandleJSONResponse → 解析 Answer 数组 → DNSAnswer[]
            ├─ 成功 → 写入 DNSCacheEntry，返回
            └─ 失败 → 尝试下一个端点
       └─ 全部失败 → 返回 null
```

## 关联文档

- [DNSAddress](DNSAddress.md)
- [DNSAnswer](DNSAnswer.md)
- [DNSCacheEntry](DNSCacheEntry.md)
- [ResourceRecordType](ResourceRecordType.md)
- [DoHManager](../DoHManager.md)
