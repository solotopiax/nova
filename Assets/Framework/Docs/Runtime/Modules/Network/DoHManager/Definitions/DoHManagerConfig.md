# DoHManagerConfig

**类签名**：`public class DoHManagerConfig`
**命名空间**：`NovaFramework.Runtime`

DoH 管理器初始化配置；控制 DNS-over-HTTPS 是否启用、每个原始域名的完整解析链超时时间和最多保留的 IPv4 数量。

---

## §2 文件表

| 文件 | 类 | 说明 |
|---|---|---|
| `Managers/DoHManager/Definitions/DoHManagerConfig.cs` | `DoHManagerConfig` | 纯数据类定义 |

---

## §5 完整公开 API

```csharp
public class DoHManagerConfig
{
    public bool UseDoH;               // 是否启用 DoH（DNS-over-HTTPS）解析
    public int  DnsTimeoutSeconds = 5; // 单个原始域名完整 DoH 解析链的超时时间（秒）；小于等于 0 时无限等待
    public int  MaxIPAddressesPerHost = 3; // 单域名最多保留的 DoH IPv4 数量；小于等于 0 时保留全部
}
```

每个原始域名独立创建一次截止时间；A 查询顺序尝试的 DoH 地址与后续全部 CNAME 层共用该截止时间。`DnsTimeoutSeconds <= 0` 表示整条解析链无限等待，不会关闭 DoH；如需停止 DoH 查询，必须设置 `UseDoH = false`。`MaxIPAddressesPerHost` 默认保留前 3 个 IPv4；小于等于 0 时保留全部。

---

## §11 使用示例

```csharp
// NetworkComponent.Start() 中构造并传入
m_DoHManager.Initialize(new DoHManagerConfig
{
    UseDoH            = m_DoHSettings.UseDoH,
    DnsTimeoutSeconds = m_DoHSettings.DnsTimeoutSeconds,
    MaxIPAddressesPerHost = m_DoHSettings.MaxIPAddressesPerHost,
});
```

---

## §13 关联文档

- [DoHManager.md](../DoHManager.md)
- [IDoHManager.md](../IDoHManager.md)
- [NetworkComponent.md](../../NetworkComponent.md)
