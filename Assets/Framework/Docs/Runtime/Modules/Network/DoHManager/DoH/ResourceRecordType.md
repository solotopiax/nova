# ResourceRecordType

**类签名**：`public enum ResourceRecordType : Int16`
**命名空间**：`NovaFramework.Runtime`

DNS 资源记录类型枚举，对应标准 DNS 协议中的记录类型编号。在 `DNSAnswer.RecordType` 中使用，用于区分 A、AAAA、CNAME 等不同类型的 DNS 应答。

---

## 文件列表

| 文件 | 说明 |
|------|------|
| `ResourceRecordType.cs` | DNS 资源记录类型枚举定义 |

## 枚举值

| 值 | 编号 | 说明 |
|------|------|------|
| `A` | 1 | IPv4 地址记录 |
| `AAAA` | 28 | IPv6 地址记录 |
| `CNAME` | 5 | 别名规范名称记录 |
| `MX` | 15 | 邮件交换记录 |
| `NS` | 2 | 名称服务器记录 |
| `PTR` | 12 | 指针记录（反向 DNS） |
| `SOA` | 6 | 权威记录起始 |
| `SRV` | 33 | 服务定位记录 |
| `TXT` | 16 | 文本记录 |
| `ALL` | 255 | 通配查询所有记录类型 |
| 其他 | - | 还包含 A6, AFSDB, DNAME, DNSKEY, DS, EUI48, EUI64, HINFO, ISDN, KEY, LOC, NAPTR, NSEC, NXT, RP, RRSIG, RT, SIG, SPF, URI, WKS, X25 等 |

## 关联文档

- [DNSAnswer](DNSAnswer.md)
- [DoHClient](DoHClient.md)
