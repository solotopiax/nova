# HttpManagerConfig

**类签名**：`public class HttpManagerConfig`
**命名空间**：`NovaFramework.Runtime`

HTTP 管理器初始化配置；控制连接/请求超时时间及 DoH 管理器引用注入。

---

## §2 文件表

| 文件 | 类 | 说明 |
|---|---|---|
| `Managers/HttpManager/Definitions/HttpManagerConfig.cs` | `HttpManagerConfig` | 纯数据类定义 |

---

## §5 完整公开 API

```csharp
public class HttpManagerConfig
{
    public float      ConnectTimeout = 20f;   // 默认网络连接超时时间（秒）
    public float      RequestTimeout = 60f;   // 默认网络请求超时时间（秒）
    public IDoHManager DoHManager;            // DoH 管理器接口引用，由 NetworkComponent 注入
}
```

---

## §11 使用示例

```csharp
// NetworkComponent.Start() 中构造并传入
m_HttpManager.Initialize(new HttpManagerConfig
{
    ConnectTimeout = m_HttpSettings.ConnectTimeout,
    RequestTimeout = m_HttpSettings.RequestTimeout,
    DoHManager     = m_DoHManager,
});
```

---

## §13 关联文档

- [HttpManager.md](../HttpManager.md)
- [IDoHManager.md](../../DoHManager/IDoHManager.md)
- [NetworkComponent.md](../../NetworkComponent.md)
