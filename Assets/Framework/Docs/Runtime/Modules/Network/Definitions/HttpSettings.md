# HttpSettings

**类签名**：`[Serializable] public class HttpSettings`
**命名空间**：`NovaFramework.Runtime`

HTTP 管理器配置，在 Inspector 中集中管理 HTTP 连接与请求的超时参数。

---

## §2 文件表

| 文件 | 类 | 说明 |
|---|---|---|
| `Network/Definitions/HttpSettings.cs` | `HttpSettings` | ConnectTimeout / RequestTimeout 两个序列化字段 |

---

## §5 完整公开 API

```csharp
[Serializable]
public class HttpSettings
{
    public float ConnectTimeout = 20f;  // HTTP 连接超时时间（秒）
    public float RequestTimeout = 60f;  // HTTP 请求超时时间（秒）
}
```

---

## §11 使用示例

```csharp
// NetworkComponent.Start() 中映射到 HttpManagerConfig
m_HttpManager.Initialize(new HttpManagerConfig
{
    ConnectTimeout = m_HttpSettings.ConnectTimeout,
    RequestTimeout = m_HttpSettings.RequestTimeout,
    DoHManager     = m_DoHManager,
});
```

---

## §13 关联文档

- [NetworkComponent.md](../NetworkComponent.md)
- [HttpManagerConfig.md](../HttpManager/Definitions/HttpManagerConfig.md)
