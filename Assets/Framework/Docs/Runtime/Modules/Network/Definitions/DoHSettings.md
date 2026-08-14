# DoHSettings

**类签名**：`[Serializable] public class DoHSettings`
**命名空间**：`NovaFramework.Runtime`

DoH（DNS-over-HTTPS）管理器配置，在 Inspector 中集中管理 DoH 开关与单个原始域名的完整解析链超时参数。

---

## §2 文件表

| 文件 | 类 | 说明 |
|---|---|---|
| `Network/Definitions/DoHSettings.cs` | `DoHSettings` | UseDoH / DnsTimeoutSeconds / MaxIPAddressesPerHost 三个序列化字段 |

---

## §5 完整公开 API

```csharp
[Serializable]
public class DoHSettings
{
    public bool UseDoH;               // 是否启用 DoH DNS 解析；false 时不发起 DoH 查询
    public int  DnsTimeoutSeconds = 5; // 单个原始域名完整 DoH 解析链的超时时间（秒）；小于等于 0 时无限等待
    public int  MaxIPAddressesPerHost = 3; // 单域名最多保留的 DoH IPv4 数量；小于等于 0 时保留全部
}
```

每个原始域名独立创建一次截止时间。该域名只查询 A 记录，并继续解析响应中的 CNAME；查询会按顺序尝试 DoH 地址，后续全部 CNAME 层继续使用同一个截止时间的剩余部分。启动批量预热时，各原始域名并发执行并分别计时。`DnsTimeoutSeconds <= 0` 表示整条解析链无限等待，不会关闭 DoH；如需停止 DoH 查询，必须设置 `UseDoH = false`。`MaxIPAddressesPerHost` 默认保留前 3 个 IPv4；小于等于 0 时保留全部。

---

## §11 使用示例

```csharp
// NetworkComponent.Start() 中映射到 DoHManagerConfig
m_DoHManager.Initialize(new DoHManagerConfig
{
    UseDoH            = m_DoHSettings.UseDoH,
    DnsTimeoutSeconds = m_DoHSettings.DnsTimeoutSeconds,
    MaxIPAddressesPerHost = m_DoHSettings.MaxIPAddressesPerHost,
});
```

---

## §13 关联文档

- [NetworkComponent.md](../NetworkComponent.md)
- [DoHManager.md](../DoHManager/DoHManager.md)
