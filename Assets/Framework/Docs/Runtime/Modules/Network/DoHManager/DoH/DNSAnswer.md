# DNSAnswer

**类签名**：`public class DNSAnswer`
**命名空间**：`NovaFramework.Runtime`

单条 DNS 应答记录，由 `DoHClient` 解析 DoH JSON 响应后生成。包含域名、TTL、数据内容和资源记录类型。通过 `FromJSON` 工厂方法从 JSON 对象解析创建。

---

## 文件列表

| 文件 | 说明 |
|------|------|
| `DNSAnswer.cs` | DNS 应答记录数据类定义 |

## 关键字段/属性

| 字段 | 类型 | 说明 |
|------|------|------|
| `Name` | `string` | 应答名称（域名） |
| `TTL` | `int` | 有效期（秒） |
| `Data` | `string` | 数据内容（IP 地址字符串 / CNAME 域名等） |
| `RecordType` | `ResourceRecordType` | 资源记录类型（A / AAAA / CNAME 等） |

## 公开 API

```csharp
// 从 JSON 节点解析出一条 DNSAnswer 实例（internal 方法）
internal static DNSAnswer FromJSON(JObject jsonAnswer)

// 返回可读字符串，格式为 "name:xxx,type:xxx,ttl:xxx,data:xxx"
string ToString()
```

## 关联文档

- [ResourceRecordType](ResourceRecordType.md)
- [DNSCacheEntry](DNSCacheEntry.md)
- [DoHClient](DoHClient.md)
