# SDKManagerConfig

**类签名**：`public sealed class SDKManagerConfig`
**命名空间**：`NovaFramework.Runtime`

SDK 管理器构造配置 DTO，由 SDKComponent.Start() 构造后传入 ISDKManager.Initialize。

---

## §2 文件表

| 文件 | 类 | 说明 |
|---|---|---|
| `Managers/Definitions/SDKManagerConfig.cs` | `SDKManagerConfig` | 全部定义 |

---

## §5 完整公开 API

```csharp
public sealed class SDKManagerConfig
{
    /// Inspector 序列化的插件条目列表，来自 SDKComponent.m_PluginEntries。
    /// Manager.Initialize 按此列表进行反射实例化，跳过 Enabled==false 与 IsMissing==true 的条目。
    public IReadOnlyList<SDKPluginEntry> PluginEntries;
}
```

---

## §11 使用示例

```csharp
// SDKComponent.Start() 中构造并传入
private void Start()
{
    m_SDKManager.Initialize(new SDKManagerConfig { PluginEntries = m_PluginEntries });
}
```

---

## §13 关联文档

- [SDKPluginEntry.md](../../Definitions/SDKPluginEntry.md) — 列表元素结构
- [../Interfaces/ISDKManager.md](../Interfaces/ISDKManager.md) — Initialize 方法签名
- [../../SDKComponent.md](../../SDKComponent.md) — 构造并传入 SDKManagerConfig 的调用方
