# DNSCacheEntry

**类签名**：`internal class DNSCacheEntry`
**命名空间**：`NovaFramework.Runtime`

DoHClient 内部使用的 DNS 查询结果缓存条目。根据应答集合中最小 TTL 计算过期时间，在缓存有效期内直接返回缓存结果以避免重复查询。

---

## 文件列表

| 文件 | 说明 |
|------|------|
| `DNSCacheEntry.cs` | DNS 缓存条目定义 |

## 关键字段/属性

| 字段 | 类型 | 说明 |
|------|------|------|
| `ExpireTime` | `readonly DateTime` | 缓存过期时间（基于各应答 TTL 的最小值计算） |
| `Answers` | `readonly DNSAnswer[]` | 缓存的 DNS 应答集合 |

## 公开 API

```csharp
// 构造方法，根据应答数组的最小 TTL 计算 ExpireTime
DNSCacheEntry(DNSAnswer[] answers)
```

## 关联文档

- [DNSAnswer](DNSAnswer.md)
- [DoHClient](DoHClient.md)
