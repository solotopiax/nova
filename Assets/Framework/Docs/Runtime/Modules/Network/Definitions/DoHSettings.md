# DoHSettings

**类签名**：`[Serializable] public class DoHSettings`
**命名空间**：`NovaFramework.Runtime`

DoH（DNS-over-HTTPS）管理器配置，在 Inspector 中集中管理 DoH 开关与超时参数。

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
    public bool UseDoH;           // 是否启用 DoH DNS 解析；false 时跳过 CollectAllIPAddresses
    public int  DnsTimeoutSeconds; // DNS 查询超时时间（秒），0 表示不限制
}
```

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
