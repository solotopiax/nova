# UISettings

**类签名**：`[Serializable] public class UISettings : IDataTableSettings`
**命名空间**：`NovaFramework.Runtime`

UI 配置设置，实现 `IDataTableSettings`，供 Luban 导出流水线统一消费。由 `UIComponent` 在 Inspector 中序列化配置。

---

## §2 文件表

| 文件 | 类 | 说明 |
|---|---|---|
| `Managers/UIManager/Definitions/UISettings.cs` | `UISettings` | 可序列化配置数据类，含编辑器专用字段（`#if UNITY_EDITOR`） |
| `Managers/UIManager/Definitions/UISettings.cs` | `UIUnitSetting` | 单个 UI 数据源的单元设置，固定 Map 模式，索引字段 "Name" |

---

## §5 完整公开 API

```csharp
// UISettings（IDataTableSettings 显式实现）
[Serializable]
public class UISettings : IDataTableSettings
{
#if UNITY_EDITOR
    [FormerlySerializedAs("ExcelDirPath")]
    public string SourceDirPath;                                          // 数据源目录路径
    string IDataTableSettings.SourceDirPath => SourceDirPath;
    public string TemplatePath;                                           // 模板文件路径
#endif
    public List<UIUnitSetting> UIUnitsSettings = new List<UIUnitSetting>();
    IReadOnlyList<IDataTableUnitSetting> IDataTableSettings.Units => UIUnitsSettings;
}

// UIUnitSetting : DataTableUnitSettingBase
[Serializable]
public class UIUnitSetting : DataTableUnitSettingBase
{
    protected override DataTableMode GetMode() => DataTableMode.Map;
    protected override string GetIndexField() => "Name";
}
```

---

## §11 使用示例

```csharp
// 运行时遍历每个 UIUnit 的 Asset 地址与类型名
foreach (UIUnitSetting unit in m_UISettings.UIUnitsSettings)
{
    string assetLocation = unit.AssetLocation;
}
```

---

## §13 关联文档

- [../../UIComponent.md](../../UIComponent.md)
- [UIManagerConfig.md](UIManagerConfig.md)
- [UIManager.md](../UIManager.md)
