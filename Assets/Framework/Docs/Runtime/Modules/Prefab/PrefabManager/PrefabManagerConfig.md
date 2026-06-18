# PrefabManagerConfig

**类签名**：`[Serializable] public class PrefabManagerConfig`
**命名空间**：`NovaFramework.Runtime`

PrefabManager 启动配置。当前为空扩展位，采用"每实例一 handle + PrefabInstanceTag 单路径回收"策略，无额外配置项。

---

## 文件

| 文件 | 类 | 说明 |
|------|-----|------|
| `Managers/PrefabManager/Definitions/PrefabManagerConfig.cs` | `PrefabManagerConfig` | 配置容器（扩展位） |

---

## 完整公开 API

无公开成员（扩展位，后续可追加配置字段）。

---

## 使用示例

```csharp
// Inspector 序列化后自动注入
m_PrefabManager.Initialize(m_PrefabManagerConfig);
```

---

## 关联文档

- [PrefabComponent.md](../PrefabComponent.md)
- [PrefabManager.md](PrefabManager.md)
