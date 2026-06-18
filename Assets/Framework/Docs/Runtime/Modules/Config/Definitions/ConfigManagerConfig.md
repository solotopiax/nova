# ConfigManagerConfig

**类签名**：`public sealed class ConfigManagerConfig`
**命名空间**：`NovaFramework.Runtime`

ConfigManager 初始化入参；携带 Asset 地址，由 ConfigComponent 在 Start 阶段从 Inspector 字段构造后交给 `ConfigManager.Initialize`。

---

## §2 文件表

| 文件 | 类 | 说明 |
|------|----|------|
| `Managers/Definitions/ConfigManagerConfig.cs` | `ConfigManagerConfig` | 纯数据类，无方法 |

---

## §5 完整公开 API

纯数据类，无方法。

| 字段 | 类型 | 说明 |
|------|------|------|
| `AssetLocation` | `string` | ConfigRuntimeSO 的 Asset 地址；Initialize 时不可为空 |

---

## §11 使用示例

```csharp
// ConfigComponent.Start() 中构建并传入
m_ConfigManager.Initialize(new ConfigManagerConfig
{
    AssetLocation = m_AssetLocation,
});
```

---

## §13 关联文档

- [../ConfigManager.md](../ConfigManager.md)
- [../ConfigComponent.md](../ConfigComponent.md)
- [../Interfaces/IConfigManager.md](../Interfaces/IConfigManager.md)
