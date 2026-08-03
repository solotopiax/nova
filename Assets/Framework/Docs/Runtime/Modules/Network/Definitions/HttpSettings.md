# HttpSettings

**类签名**：`[Serializable] public class HttpSettings`
**命名空间**：`NovaFramework.Runtime`

HTTP 管理器配置，在 Inspector 中集中管理 HTTP 连接、请求超时与 BestHTTP 网络埋点开关。

---

## §2 文件表

| 文件 | 类 | 说明 |
|---|---|---|
| `Network/Definitions/HttpSettings.cs` | `HttpSettings` | BestHTTP 埋点开关及 ConnectTimeout / RequestTimeout 序列化字段 |

---

## §5 完整公开 API

```csharp
[Serializable]
public class HttpSettings
{
    public bool EnableBestHttpTelemetry = true; // 是否把 BestHTTP 结构化网络遥测转发到 ITrackPlugin
    public float ConnectTimeout = 20f;  // HTTP 连接超时时间（秒）
    public float RequestTimeout = 60f;  // HTTP 请求超时时间（秒）
}
```

`EnableBestHttpTelemetry` 仅在安装了包含结构化遥测契约的 BestHTTP 商业库与 Nova BestHTTP 适配包时产生运行时效果；未检测到该能力时 Network Inspector 会将开关置灰且不可点击。

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
