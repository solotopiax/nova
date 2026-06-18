# TableManagerBase

**类签名**：`internal abstract class TableManagerBase : FrameworkManager, ITableManager`
**命名空间**：`NovaFramework.Runtime`

表格管理器抽象基类，继承 `FrameworkManager` 并实现 `ITableManager` 接口。将所有接口方法声明为 `abstract`，由子类 `TableManager` 提供具体实现。框架管理器优先级为 `14`。

---

## 文件列表

| 文件 | 说明 |
|------|------|
| `Managers/Implements/TableManagerBase.cs` | 抽象基类定义 |

## 继承关系

```
FrameworkManager
  └── TableManagerBase (internal abstract) : ITableManager
        └── TableManager (internal sealed partial)
```

## 关键字段/属性

| 属性 | 类型 | 说明 |
|------|------|------|
| `Priority` | `int` | 框架管理器优先级，值为 `14`。值越小越先 Update、越后 Shutdown |
| `Count` | `int` | 已加载的表格类型数量（abstract，子类实现） |

## 公开 API

```csharp
internal abstract class TableManagerBase : FrameworkManager, ITableManager
{
    // 框架管理器优先级
    public override int Priority => 14;

    // 已加载表格数量
    public abstract int Count { get; }

    // 初始化
    public abstract void Initialize(TableManagerConfig config);

    // 框架管理器轮询
    public abstract override void Update();

    // 关闭并清理
    public abstract override void Shutdown();

    // 两阶段异步加载（Phase 1 并发 AB→JSON 缓存，Phase 2 反射构造 TableTables）
    public abstract UniTask<bool> LoadTablesAsync();
    public abstract bool LoadTablesSync();

    // 统一查询
    public abstract bool HasTable<T>() where T : class, ITable;
    public abstract bool HasTable(Type type);
    public abstract T GetTable<T>() where T : class, ITable;
    public abstract object GetTable(Type type);
}
```

## 关联文档

- [ITableManager.md](../Interfaces/ITableManager.md)（接口定义）
- [TableManager.md](../TableManager.md)（最终实现）
- [TableComponent.md](../TableComponent.md)（持有并调用管理器）
