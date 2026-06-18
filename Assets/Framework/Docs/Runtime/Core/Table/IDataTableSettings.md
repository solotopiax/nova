# IDataTableSettings

**类签名**：`public interface IDataTableSettings`
**命名空间**：`NovaFramework.Runtime`

数据表设置接口，包含数据源目录和单元设置列表，供 Luban 导出流水线统一消费。

---

## §2 文件表

| 文件 | 类 | 说明 |
|------|----|------|
| `Core/Table/IDataTableSettings.cs` | `IDataTableSettings` | 接口定义 |

---

## §5 完整公开 API

```csharp
public interface IDataTableSettings
{
#if UNITY_EDITOR
    string SourceDirPath { get; }  // 数据源目录路径（仅编辑器使用）
#endif
    IReadOnlyList<IDataTableUnitSetting> Units { get; }  // 所有单元设置列表
}
```

---

## §11 使用示例

```csharp
// Editor 流水线中统一消费
public void SyncLubanConfigs(IDataTableSettings settings)
{
#if UNITY_EDITOR
    foreach (var unit in settings.Units)
        ExportUnit(unit);
#endif
}
```

---

## §13 关联文档

- [IDataTableUnitSetting.md](IDataTableUnitSetting.md)
