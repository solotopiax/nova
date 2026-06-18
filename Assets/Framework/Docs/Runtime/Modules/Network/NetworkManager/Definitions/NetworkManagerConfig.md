# NetworkManagerConfig

**类签名**：`public class NetworkManagerConfig`
**命名空间**：`NovaFramework.Runtime`

Network 管理器初始化配置，承载域名表与指令表的 Luban 加载单元列表。

---

## §2 文件表

| 文件 | 类 | 说明 |
|---|---|---|
| `NetworkManagerConfig.cs` | `NetworkManagerConfig` | 配置数据类定义 |

---

## §5 完整公开 API

```csharp
public class NetworkManagerConfig
{
    public List<HostKeyUnitSetting> HostKeyUnitSettings;   // 域名表单元设置列表
    public List<NetCmdUnitSetting>  NetCmdUnitSettings;    // 指令表单元设置列表
}
```

---

## §11 使用示例

```csharp
// NetworkComponent.Start() 中构造并传入
m_NetworkManager.Initialize(new NetworkManagerConfig
{
    HostKeyUnitSettings = m_Settings.HostKeySettings.HostKeyUnits,
    NetCmdUnitSettings  = m_Settings.NetCmdSettings.NetCmdUnits,
});
```

---

## §13 关联文档

- [NetworkManager.md](../NetworkManager.md)
- [INetworkManager.md](../INetworkManager.md)
- [../../Definitions/NetworkSettings.md](../../Definitions/NetworkSettings.md)
