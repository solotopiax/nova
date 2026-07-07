# SDKPluginEntry

**类签名**：`[Serializable] public sealed class SDKPluginEntry`
**命名空间**：`NovaFramework.Runtime`

SDK 插件条目，Inspector 序列化结构，一条记录对应一个 Plugin 类型。运行时启用由 ConfigMaster.EnabledSDKs 决定，Entry 不再实例化插件，运行时排序使用 ISDKPlugin.Priority。

---

## §2 文件表

| 文件 | 类 | 说明 |
|---|---|---|
| `Managers/Definitions/SDKPluginEntry.cs` | `SDKPluginEntry` | 全部定义 |

---

## §5 完整公开 API

```csharp
[Serializable]
public sealed class SDKPluginEntry
{
    [SerializeField] public string TypeName;   // Plugin AssemblyQualifiedName，用于解析插件元数据
    [SerializeField] public bool   Enabled;    // SDK 面板显式选型标记；不作为运行时启用开关

    [NonSerialized]  public bool   IsMissing;  // 运行时：Type.GetType(TypeName) 失败时为 true，不序列化
}
```

---

## §11 使用示例

```csharp
// SDKComponent.Start() 中构造 SDKManagerConfig
m_SDKManager.Initialize(new SDKManagerConfig
{
    PluginEntries = m_PluginEntries   // List<SDKPluginEntry>，来自 Inspector 序列化
});

// 业务层通过 TryGet<T> 判断能力是否可用
if (!Nova.SDK.TryGet<IAdPlugin>(out _))
{
    Debug.LogWarning("广告能力未安装或初始化失败");
}
```

---

## §13 关联文档

- [SDKManagerConfig.md](../Managers/Definitions/SDKManagerConfig.md) — 持有 `IReadOnlyList<SDKPluginEntry>` 的配置 DTO
- [../SDKComponent.md](../SDKComponent.md) — 持有 `List<SDKPluginEntry>` 的 Inspector 序列化字段
