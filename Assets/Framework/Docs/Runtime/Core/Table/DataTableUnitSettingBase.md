# DataTableUnitSettingBase

**类签名**：`[Serializable] public abstract class DataTableUnitSettingBase : IDataTableUnitSetting`
**命名空间**：`NovaFramework.Runtime`

数据表单元设置抽象基类，提取各模块（Table/Config/Sound/Vibrate/Localization 等）共用的序列化字段与接口显式实现，子类只需提供 `GetMode()`、`GetIndexField()` 两个抽象方法即可完成接入。

---

## § 2 文件表

| 文件 | 类 | 说明 |
|---|---|---|
| `Runtime/Core/Table/DataTableUnitSettingBase.cs` | `DataTableUnitSettingBase` | 抽象基类定义 |

---

## § 3 继承关系

```text
IDataTableUnitSetting  (interface)
  └── DataTableUnitSettingBase  (abstract, [Serializable])
        ├── TableUnitSetting      (Table 模块)
        ├── ConfigUnitSetting     (Config 模块)
        ├── SoundUnitSetting      (Sound 模块)
        ├── VibrateUnitSetting    (Vibrate 模块)
        └── LocalizationUnitSetting (Localization 模块)
```

---

## § 4 关键字段表

| 字段 | 类型 | 默认值 | 说明 |
|---|---|---|---|
| `SourcePath` | `string` | `null` | 相对数据源目录的文件路径，仅 `#if UNITY_EDITOR` 可见 |
| `DatasExportPath` | `string` | `null` | 数据文件导出目标路径，仅 `#if UNITY_EDITOR` 可见 |
| `ClassesExportPath` | `string` | `null` | 类型定义文件导出目标路径，仅 `#if UNITY_EDITOR` 可见 |
| `AssetLocation` | `string` | `null` | 资源的 Asset 地址（运行时字段） |
| `DataTypeNames` | `List<string>` | `new List<string>()` | 数据类型短名称列表（不含命名空间），一个 JSON 可含多个类型 |

---

## § 5 完整公开 API

```csharp
// 编辑器字段（#if UNITY_EDITOR）
public string SourcePath;
public string DatasExportPath;
public string ClassesExportPath;

// 运行时字段
public string AssetLocation;
public List<string> DataTypeNames;

// IDataTableUnitSetting 显式实现（子类透传）
string IDataTableUnitSetting.SourcePath => SourcePath;
string IDataTableUnitSetting.DatasExportPath => DatasExportPath;
string IDataTableUnitSetting.ClassesExportPath => ClassesExportPath;
string IDataTableUnitSetting.LubanInputPath => GetLubanInputPath();
string IDataTableUnitSetting.AssetLocation => AssetLocation;
DataTableMode IDataTableUnitSetting.Mode => GetMode();
string IDataTableUnitSetting.IndexField => GetIndexField();
IReadOnlyList<string> IDataTableUnitSetting.DataTypeNames => DataTypeNames;

// 子类必须实现
protected abstract DataTableMode GetMode();
protected abstract string GetIndexField();

// 子类可 override（默认返回 SourcePath；Config 等需特殊路径时 override）
protected virtual string GetLubanInputPath() => SourcePath;
```

---

## § 9 关键算法

### LubanInputPath 扩展点

`IDataTableUnitSetting.LubanInputPath` 委托给 `GetLubanInputPath()`，默认返回 `SourcePath`。Config 模块的 `ConfigUnitSetting` override 该方法，返回 `_temp/<SourcePath>` 格式，满足预过滤后临时文件的路径规则。其余模块无需 override。

---

## § 11 使用示例

```csharp
// 以 TableUnitSetting 为例（子类实现）
[Serializable]
public class TableUnitSetting : DataTableUnitSettingBase
{
    public string IndexField;
    public DataTableMode Mode;

    protected override DataTableMode GetMode() => Mode;
    protected override string GetIndexField() => IndexField;
}
```

---

## § 13 关联文档

- [IDataTableUnitSetting.md](IDataTableUnitSetting.md)
- [IDataTableSettings.md](IDataTableSettings.md)
- [DataTableMode.md](DataTableMode.md)
- [TableSettings.md](../../Modules/Table/Definitions/TableSettings.md)
- [NetworkSettings.md](../../Modules/Network/Definitions/NetworkSettings.md)
