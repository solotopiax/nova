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

构造器原样保存两个引用，不校验空值、资源地址或实例归属，也不加载或持有资源句柄；`default` 结构体的两项都为 `null`。记录及其属性只用于诊断，既不证明资源仍可加载，也不使 Unity 对象保持存活。Manager 暴露的是实时只读列表，已有记录副本不会随销毁自动变成新记录。

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
