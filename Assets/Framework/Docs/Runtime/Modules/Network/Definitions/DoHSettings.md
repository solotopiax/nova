# DoHSettings

**类签名**：`[Serializable] public class DoHSettings`
**命名空间**：`NovaFramework.Runtime`

DoH（DNS-over-HTTPS）管理器配置，在 Inspector 中集中管理 DoH 开关与单次查询超时参数。

---

## §2 文件表

| 文件 | 类 | 说明 |
|---|---|---|
| `Network/Definitions/DoHSettings.cs` | `DoHSettings` | UseDoH / DnsTimeoutSeconds 两个序列化字段 |

---

## §5 完整公开 API

```csharp
[Serializable]
public class DoHSettings
{
    public bool UseDoH;               // 是否启用 DoH DNS 解析；false 时不发起 DoH 查询
    public int  DnsTimeoutSeconds = 3; // 每个域名的一次 DoH 查询超时时间（秒），0 表示跳过查询
}
```

每个域名的一次 DoH 查询独立计时。查询期间会按顺序尝试当前配置的候选地址，所有候选地址共用 `DnsTimeoutSeconds`；启动批量预热时，各域名并发执行并分别计时。

---

## §11 使用示例

```csharp
// NetworkComponent.Start() 中映射到 DoHManagerConfig
m_DoHManager.Initialize(new DoHManagerConfig
{
    UseDoH            = m_DoHSettings.UseDoH,
    DnsTimeoutSeconds = m_DoHSettings.DnsTimeoutSeconds,
});
```

---

## §13 关联文档

- [NetworkComponent.md](../NetworkComponent.md)
- [DoHManager.md](../DoHManager/DoHManager.md)
