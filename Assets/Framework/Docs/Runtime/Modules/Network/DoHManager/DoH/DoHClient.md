# DoHClient

**类签名**：`public class DoHClient : IDisposable`
**命名空间**：`NovaFramework.Runtime`

针对单个主机名的 DoH（DNS-over-HTTPS）查询器，内置按记录类型隔离的结果缓存与进行中任务。`DoHManager` 当前只查询 A 记录，并继续处理响应中的 CNAME；查询内部按顺序尝试 Cloudflare DoH 地址，首个正常响应结束端点轮询，非空结果写入 TTL 缓存。调用方传入原始域名完整解析链的绝对截止时间，因此等待共享查询和后续 CNAME 层都不会重新计时。

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
| `m_AnswersCaches` | `Dictionary<ResourceRecordType, DNSCacheEntry>` | 按记录类型隔离的 TTL 缓存；当前管理器链路只使用 A |
| `m_WaitingTasks` | `Dictionary<ResourceRecordType, UniTask<DNSAnswer[]>>` | 按记录类型隔离的进行中查询；同主机、同类型复用一次请求链 |
| `m_Random` | `readonly Random` | 使用加密安全种子的随机数生成器，用于 URL 填充 |
| `m_DisposeCancellationTokenSource` | `readonly CancellationTokenSource` | 释放查询器时中止仍在等待的网络请求 |
| `m_Disposed` | `bool` | 标记查询器是否已经释放 |

## 公开 API

```csharp
// 构造方法
DoHClient(string hostName)

// 清除本地 DNS 结果缓存，强制下次重新查询
void ClearCache()

// 兼容入口：查询 A 记录，并根据 timeout 创建一次截止时间；小于等于 0 时无限等待
UniTask<DNSAnswer[]> QueryAsync(int timeout)

// 释放资源，并中止仍在等待的网络请求
void Dispose()
```

## 查询流程

```
DoHManager.QueryHostAnswersAsync(host, deadlineUtc)
  └─ QueryAsync(A, deadlineUtc)

单个 QueryAsync(recordType, deadlineUtc)
  ├─ 查询器已释放 → 返回 null
  ├─ 对应记录类型缓存有效 → 直接返回
  ├─ 同主机、同类型已有查询 → 只在当前调用方 deadline 的剩余时间内等待
  └─ 新查询
       └─ foreach endpoint in s_EndpointsList
            ├─ 按绝对截止时间计算剩余时间
            ├─ CreateRequest(endpoint, remainingTimeout, recordType)
            ├─ 响应和响应体读取均受剩余时间限制
            ├─ 有限等待超时 → Abort 当前请求
            ├─ 无限等待时不施加框架超时；端点返回失败后才尝试下一个地址
            ├─ HandleJSONResponse → DNSAnswer[]
            ├─ 正常非空响应 → 写入该记录类型的 DNSCacheEntry，返回
            ├─ 正常空响应 → 表示该类型没有记录，直接返回空数组
            └─ 请求或解析失败 → 尝试下一个候选地址
       └─ 截止时间耗尽、全部端点失败或查询器被释放 → 返回 null，并完成该类型的共享等待者
```

公开的 `QueryAsync(int timeout)` 为兼容入口，默认查询 A；正常管理器链路也只使用内部入口查询 A，并继续处理响应中的 CNAME。`timeout <= 0` 不代表关闭 DoH，而是整条解析链没有时间上限，因此若当前端点一直不返回，也不会自动切到下一个端点；如需停止 DoH 查询，应由上层设置 `UseDoH = false`。`Dispose()` 仍可中止无限等待中的请求。

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
