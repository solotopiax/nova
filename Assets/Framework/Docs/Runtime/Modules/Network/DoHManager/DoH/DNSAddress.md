# DNSAddress

**类签名**：`public static class DNSAddress`
**命名空间**：`NovaFramework.Runtime`

预定义 DoH（DNS-over-HTTPS）端点地址常量集合。包含 Cloudflare 和 Google 两套 DoH 服务端点，每套提供域名 URL、IPv4 主备端点和 IPv6 主备端点。供 `DoHClient` 在构建查询请求时轮询使用。

---

## 文件列表

| 文件 | 说明 |
|------|------|
| `DNSAddress.cs` | DoH 端点地址常量定义 |

## 关键字段/属性

| 字段 | 类型 | 说明 |
|------|------|------|
| `Cloudflare.URL` | `static readonly string` | Cloudflare DoH 域名端点：`https://cloudflare-dns.com/dns-query` |
| `Cloudflare.IPv4.Primary` | `static readonly string` | Cloudflare 主 IPv4 端点：`https://1.1.1.1/dns-query` |
| `Cloudflare.IPv4.Secondary` | `static readonly string` | Cloudflare 备 IPv4 端点：`https://1.0.0.1/dns-query` |
| `Cloudflare.IPv6.Primary` | `static readonly string` | Cloudflare 主 IPv6 端点 |
| `Cloudflare.IPv6.Secondary` | `static readonly string` | Cloudflare 备 IPv6 端点 |
| `Google.URL` | `static readonly string` | Google DoH 域名端点：`https://dns.google/resolve` |
| `Google.IPv4.Primary` | `static readonly string` | Google 主 IPv4 端点：`https://8.8.8.8/resolve` |
| `Google.IPv4.Secondary` | `static readonly string` | Google 备 IPv4 端点：`https://8.8.4.4/resolve` |
| `Google.IPv6.Primary` | `static readonly string` | Google 主 IPv6 端点 |
| `Google.IPv6.Secondary` | `static readonly string` | Google 备 IPv6 端点 |

## 公开 API

```csharp
// 静态常量类，无方法。通过嵌套类访问：
DNSAddress.Cloudflare.URL
DNSAddress.Cloudflare.IPv4.Primary
DNSAddress.Cloudflare.IPv4.Secondary
DNSAddress.Cloudflare.IPv6.Primary
DNSAddress.Cloudflare.IPv6.Secondary
DNSAddress.Google.URL
DNSAddress.Google.IPv4.Primary
DNSAddress.Google.IPv4.Secondary
DNSAddress.Google.IPv6.Primary
DNSAddress.Google.IPv6.Secondary
```

## 关联文档

- [DoHClient](DoHClient.md)
- [DoHManager](../DoHManager.md)
