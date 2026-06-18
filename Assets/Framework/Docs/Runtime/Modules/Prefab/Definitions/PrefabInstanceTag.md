# PrefabInstanceTag

**类签名**：`[DisallowMultipleComponent] public sealed class PrefabInstanceTag : MonoBehaviour`
**命名空间**：`NovaFramework.Runtime`

PrefabManager 实例化时自动挂载的钩子组件，OnDestroy 时通知 PrefabManager 释放对应 IAssetHandle（单路径回收机制）。

---

## 文件

| 文件 | 类 | 说明 |
|------|-----|------|
| `Definitions/PrefabInstanceTag.cs` | `PrefabInstanceTag` | 钩子组件，由 PrefabManager.RecordInstance 自动挂载 |

---

## 继承关系

```
MonoBehaviour
  └── PrefabInstanceTag (sealed)
```

---

## 关键字段表

| 字段 | 类型 | 说明 |
|------|------|------|
| `Handle` | `IAssetHandle<GameObject>` | 本实例对应的资源句柄（internal，由 RecordInstance 注入） |
| `OnDestroyed` | `Action<PrefabInstanceTag>` | OnDestroy 触发时的回调（internal，由 RecordInstance 注入） |

---

## 完整公开 API

无公开成员（所有成员 internal/private）。

生命周期钩子：
```csharp
private void OnDestroy()
// 触发时：置 OnDestroyed/Handle 为 null，调用 OnDestroyed 回调通知 PrefabManager
```

---

## 单路径回收铁律

无论通过何种方式销毁 Prefab 实例（`PrefabComponent.Destroy` / 业务 `Object.Destroy` / 父节点销毁链 / 场景切换），
最终都经由 `PrefabInstanceTag.OnDestroy → PrefabManager.OnInstanceDestroyed` 这唯一路径完成 handle 释放，
彻底杜绝引用计数残留。

---

## 注意事项

- 此组件由 PrefabManager 自动挂载，**业务代码不得手动添加或移除**
- `[DisallowMultipleComponent]` 防止重复挂载
- `Shutdown` 兜底清理会先行 Release，`OnInstanceDestroyed` 查不到 key 时幂等返回

---

## 使用示例

```csharp
// 不直接使用，由 PrefabManager.RecordInstance 内部挂载
// 业务代码通过 PrefabComponent.Destroy(go) 销毁即可，无需感知此组件
```

---

## 关联文档

- [PrefabManager.md](../PrefabManager/PrefabManager.md)
- [PrefabComponent.md](../PrefabComponent.md)
