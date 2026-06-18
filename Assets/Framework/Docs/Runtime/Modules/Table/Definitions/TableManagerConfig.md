# TableManagerConfig

**类签名**：`public class TableManagerConfig`
**命名空间**：`NovaFramework.Runtime`

表格管理器配置类，在 `TableComponent.Start()` 阶段向 `ITableManager.Initialize` 传递初始化参数。

---

## §2 文件表

| 文件 | 类 | 说明 |
|---|---|---|
| `Managers/Definitions/TableManagerConfig.cs` | `TableManagerConfig` | 配置类定义 |

---

## §5 完整公开 API

```csharp
public class TableManagerConfig
{
    public List<TableUnitSetting> UnitSettings;   // 表格单元设置列表
}
```

---

## §11 使用示例

```csharp
// TableComponent.Start() 中构造并传入
m_TableManager.Initialize(new TableManagerConfig
{
    UnitSettings = m_TableSettings.Units,
});
```

---

## §13 关联文档

- [TableSettings.md](TableSettings.md) — `TableUnitSetting` 定义所在
- [ITableManager.md](../Interfaces/ITableManager.md) — `Initialize` 方法接收此配置
- [../TableComponent.md](../TableComponent.md) — 构造并传入此配置
