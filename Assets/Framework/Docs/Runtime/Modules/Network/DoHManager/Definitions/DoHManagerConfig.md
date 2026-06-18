# DoHManagerConfig

**类签名**：`public class DoHManagerConfig`
**命名空间**：`NovaFramework.Runtime`

DoH 管理器初始化配置；控制 DNS-over-HTTPS 是否启用及 DNS 查询超时时间。

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
    public bool UseDoH;           // 是否启用 DoH（DNS-over-HTTPS）解析
    public int  DnsTimeoutSeconds; // DNS 查询超时时间（秒），0 表示不限制超时
}
```

---

## §11 使用示例

```csharp
// NetworkComponent.Start() 中构造并传入
m_DoHManager.Initialize(new DoHManagerConfig
{
    UseDoH            = m_DoHSettings.UseDoH,
    DnsTimeoutSeconds = m_DoHSettings.DnsTimeoutSeconds,
});
```

---

## §13 关联文档

- [DoHManager.md](../DoHManager.md)
- [IDoHManager.md](../IDoHManager.md)
- [NetworkComponent.md](../../NetworkComponent.md)
