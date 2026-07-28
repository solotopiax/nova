# DataTableUnitSettingBase

**类签名**：`[Serializable] public abstract class DataTableUnitSettingBase : IDataTableUnitSetting`
**命名空间**：`NovaFramework.Runtime`

数据表单元设置抽象基类，提取仍采用 Unit 导出链的模块（Config/Sound/Vibrate/Localization/Network 等）共用字段。Table 已不再继承此体系。

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
        ├── ConfigUnitSetting     (Config 模块)
        ├── SoundUnitSetting      (Sound 模块)
        ├── VibrateUnitSetting    (Vibrate 模块)
        ├── LocalizationUnitSetting (Localization 模块)
        ├── HostKeyUnitSetting    (Network 模块)
        └── NetCmdUnitSetting     (Network 模块)
```

---

## § 4 关键字段表

| 字段 | 类型 | 默认值 | 说明 |
|---|---|---|---|
| `SourcePath` | `string` | `null` | 相对数据源目录的文件路径，仅 `#if UNITY_EDITOR` 可见 |
| `DatasExportPath` | `string` | `null` | 数据文件导出目标路径，仅 `#if UNITY_EDITOR` 可见 |
| `ClassesExportPath` | `string` | `null` | 类型定义文件导出目标路径，仅 `#if UNITY_EDITOR` 可见 |
| `AssetLocation` | `string` | `null` | 资源的 Asset 地址（运行时字段） |

---

## § 5 完整公开 API

```csharp
// 编辑器字段（#if UNITY_EDITOR）
public string SourcePath;
public string DatasExportPath;
public string ClassesExportPath;

// 运行时字段
public string AssetLocation;

// IDataTableUnitSetting 显式实现（子类透传）
string IDataTableUnitSetting.SourcePath => SourcePath;
string IDataTableUnitSetting.DatasExportPath => DatasExportPath;
string IDataTableUnitSetting.ClassesExportPath => ClassesExportPath;
string IDataTableUnitSetting.LubanInputPath => GetLubanInputPath();
string IDataTableUnitSetting.AssetLocation => AssetLocation;
DataTableMode IDataTableUnitSetting.Mode => GetMode();
string IDataTableUnitSetting.IndexField => GetIndexField();

// 子类必须实现
protected abstract DataTableMode GetMode();
protected abstract string GetIndexField();

// 子类可 override（默认返回 SourcePath；Config 等需特殊路径时 override）
protected virtual string GetLubanInputPath() => SourcePath;
```

---

## § 9 关键算法

### LubanInputPath 扩展点

`IDataTableUnitSetting.LubanInputPath` 委托给 `GetLubanInputPath()`，默认返回 `SourcePath`。Config 与 Network 的单元设置 override 该方法，返回 `_temp/<不含扩展名的文件名>`，满足预过滤后临时文件的路径规则。其余模块无需 override。

### Editor 与 Runtime 分层

源文件、导出目录和 Luban 输入只参与编辑器导出，因此全部位于 `#if UNITY_EDITOR`。Player 中的共享基类只保留 `AssetLocation`，并通过 `Mode`、`IndexField` 告诉运行时如何装载 JSON。Excel Sheet 结构不再序列化到该基类，而由 Editor 导出前扫描。

---

## § 11 使用示例

```csharp
// 以 SoundUnitSetting 为例（子类实现）
[Serializable]
public class SoundUnitSetting : DataTableUnitSettingBase
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
