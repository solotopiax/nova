# PrefabRecordedInstance

**类签名**：`public readonly struct PrefabRecordedInstance`
**命名空间**：`NovaFramework.Runtime`

PrefabManager 当前跟踪的单条实例诊断记录，由 Inspector 诊断面板只读展示。

---

## 文件

| 文件 | 类 | 说明 |
|------|-----|------|
| `Definitions/PrefabRecordedInstance.cs` | `PrefabRecordedInstance` | 只读结构体定义 |

---

## 完整公开 API

```csharp
// 属性
GameObject Instance { get; }
string Location { get; }

// 构造器
PrefabRecordedInstance(GameObject instance, string location)
```

---

## 使用示例

```csharp
// Inspector 诊断面板读取（通过 PrefabComponent）
PrefabComponent prefab = FrameworkComponentsGroup.GetComponent<PrefabComponent>();
IReadOnlyList<PrefabRecordedInstance> records = prefab.RecordedInstances;
foreach (PrefabRecordedInstance rec in records)
{
    Debug.Log($"{rec.Instance.name} <- {rec.Location}");
}
```

---

## 关联文档

- [PrefabComponent.md](../PrefabComponent.md)
- [IPrefabManager.md](../PrefabManager/IPrefabManager.md)
- [PrefabManager.md](../PrefabManager/PrefabManager.md)
