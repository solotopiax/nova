# IUIViewRow

**类签名**：`public interface IUIViewRow`
**命名空间**：`NovaFramework.Runtime`

UI 视图数据行接口，Luban 生成的 UI 视图 bean 类须实现此接口，框架侧通过接口直接访问字段，彻底消除反射，替代已删除的 `UIViewEntry` 中间类型。

---

## §2 文件表

| 文件 | 类 | 说明 |
|------|-----|------|
| `Runtime/Modules/UI/Managers/UIManager/Definitions/IUIViewRow.cs` | `interface IUIViewRow` | UI 视图数据行接口定义 |

---

## §3 继承关系

```
IUIViewRow (interface)
```

---

## §5 完整公开 API

```csharp
public interface IUIViewRow
{
    /// <summary>视图类名（作为注册表 Key，与 typeof(T).Name 对齐）。</summary>
    string Name { get; }

    /// <summary>视图资源地址（格式由业务层约定，框架侧透传）。</summary>
    string AssetLocation { get; }

    /// <summary>视图所属的视图分组名称。</summary>
    string UIGroupName { get; }

    /// <summary>是否暂停被当前视图覆盖的下层视图。</summary>
    bool PauseCoveredUIView { get; }

    /// <summary>是否启用对象池缓存。true 关闭后回收到对象池等待复用，false 关闭后直接销毁。</summary>
    bool InObjectPools { get; }
}
```

---

## §11 使用示例

```csharp
// Luban 生成的 bean 类实现接口
public partial class TbUIView : IUIViewRow
{
    public string Name { get; set; }
    public string AssetLocation { get; set; }
    public string UIGroupName { get; set; }
    public bool PauseCoveredUIView { get; set; }
}

// UIManager 通过 m_UIViewRegistry 注册表直接访问
if (m_UIViewRegistry.TryGetValue(typeof(T).Name, out IUIViewRow row))
{
    int serialID = OpenUIViewAsync(row.AssetLocation, row.UIGroupName, row.PauseCoveredUIView);
}
```

---

## §13 关联文档

- [UIManager.md](../UIManager.md)（消费者，持有 `Dictionary<string, IUIViewRow>` 注册表）
- [ITable.md](../../../../Core/Table/ITable.md)（表容器接口）
