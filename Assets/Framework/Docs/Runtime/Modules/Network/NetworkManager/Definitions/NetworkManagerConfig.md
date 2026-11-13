# NetworkManagerConfig

**类签名**：`public class NetworkManagerConfig`
**命名空间**：`NovaFramework.Runtime`

Network 管理器初始化配置，承载域名表与指令表的 Luban 加载单元列表，以及提供给 Config 的启动 GM 单次请求超时。

---

## §2 文件表

| 文件 | 类 | 说明 |
|---|---|---|
| [NetworkManagerConfig.cs](../../../../../../Scripts/Runtime/Modules/Network/Managers/NetworkManager/Definitions/NetworkManagerConfig.cs) | `NetworkManagerConfig` | 配置数据类定义 |

---

## §5 完整公开 API

```csharp
public class NetworkManagerConfig
{
    public LubanDataFormat DataFormat = LubanDataFormat.Json;
    public List<HostKeyUnitSetting> HostKeyUnitSettings;   // 域名表单元设置列表
    public List<NetCmdUnitSetting>  NetCmdUnitSettings;    // 指令表单元设置列表
    public float StartupAppConfigRequestTimeout = 5f;     // 启动 GM 单次请求超时（秒）
}
```

---

## §11 使用示例

```csharp
// NetworkComponent.Start() 中构造并传入
m_NetworkManager.Initialize(new NetworkManagerConfig
{
    DataFormat = m_Settings.DataFormat,
    HostKeyUnitSettings = m_Settings.HostKeySettings.HostKeyUnits,
    NetCmdUnitSettings  = m_Settings.NetCmdSettings.NetCmdUnits,
    StartupAppConfigRequestTimeout = m_HttpSettings.StartupAppConfigRequestTimeout,
});
```

启动单次请求超时来自 [HttpSettings](../../Definitions/HttpSettings.md)，仅通过内部 `INetworkReadySignal` 的只读属性提供给 Config。默认 Manager 初始化时把非正数、NaN 和无穷值回退为 5 秒，不扩张公开 `INetworkManager`，不在 Network 加载入口发起或等待 GM 请求。

---

## §13 关联文档

- [NetworkManager.md](../NetworkManager.md)
- [INetworkManager.md](../INetworkManager.md)
- [../../Definitions/NetworkSettings.md](../../Definitions/NetworkSettings.md)
