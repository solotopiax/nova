# NetworkSettings

**类签名**：`[Serializable] public class NetworkSettings`
**命名空间**：`NovaFramework.Runtime`

网络设置，包含域名表（HostKey）和指令表（NetCmd）两套独立 Luban 构建单元。`NetworkComponent` 持有单个 `NetworkSettings` 实例，不再按地域索引分组。命名空间由 `IConfigManager.Namespace` 统一提供，不在此类存储。

---

## §2 文件表

| 文件 | 类 | 说明 |
|------|----|------|
| `Runtime/Modules/Network/Definitions/NetworkSettings.cs` | `NetworkSettings` | 顶层容器（持有 HostKeySettings + NetCmdSettings） |
| `Runtime/Modules/Network/Definitions/NetworkSettings.cs` | `HostKeySettings` | 域名表设置，实现 `IDataTableSettings` |
| `Runtime/Modules/Network/Definitions/NetworkSettings.cs` | `HostKeyUnitSetting` | 单个域名表单元设置，实现 `IDataTableUnitSetting` |
| `Runtime/Modules/Network/Definitions/NetworkSettings.cs` | `NetCmdSettings` | 指令表设置，实现 `IDataTableSettings` |
| `Runtime/Modules/Network/Definitions/NetworkSettings.cs` | `NetCmdUnitSetting` | 单个指令表单元设置，实现 `IDataTableUnitSetting` |

---

## §3 继承关系

```
NetworkSettings（[Serializable] class，无继承）
  ├── HostKeySettings : IDataTableSettings
  │     └── List<HostKeyUnitSetting : IDataTableUnitSetting>   HostKeyUnits
  └── NetCmdSettings : IDataTableSettings
        └── List<NetCmdUnitSetting : IDataTableUnitSetting>    NetCmdUnits
```

---

## §4 关键字段表

### NetworkSettings

| 字段 | 类型 | 说明 |
|---|---|---|
| `HostKeySettings` | `HostKeySettings` | 域名表设置（单套单元列表） |
| `NetCmdSettings` | `NetCmdSettings` | 指令表设置（单套单元列表） |

> `Namespace` 字段已移除，运行时通过 `FrameworkManagersGroup.GetManager<IConfigManager>()?.Namespace` 现取。

### HostKeySettings（`IDataTableSettings` 实现）

**仅 Editor 可见（`#if UNITY_EDITOR`）**

| 字段 | 类型 | 默认值 | 说明 |
|---|---|---|---|
| `SourceDirPath` | `string` | `null` | 数据源目录路径；`IDataTableSettings.SourceDirPath` 返回 `SourceDirPath ?? ""` |
| `DatasExportPath` | `string` | `null` | Luban 数据导出目录 |
| `ClassesExportPath` | `string` | `null` | Luban 类型代码导出目录 |

**运行时可见**

| 字段 | 类型 | 默认值 | 说明 |
|---|---|---|---|
| `HostKeyUnits` | `List<HostKeyUnitSetting>` | `new List<>()` | 域名表单元设置列表；`IDataTableSettings.Units` 返回此列表 |

### HostKeyUnitSetting（`IDataTableUnitSetting` 实现）

**仅 Editor 可见（`#if UNITY_EDITOR`）**

| 字段 | 类型 | 说明 |
|---|---|---|
| `SourcePath` | `string` | 相对域名表目录的数据源文件相对路径 |
| `DatasExportPath` | `string` | 该单元数据导出目录 |
| `ClassesExportPath` | `string` | 该单元类型代码导出目录 |

**运行时可见**

| 字段 | 类型 | 说明 |
|---|---|---|
| `AssetLocation` | `string` | 域名表资源的 Asset 地址 |
| `DataTypeNames` | `List<string>` | 数据类型短名称列表（不含命名空间） |

**接口固定值**

| 接口属性 | 值 | 说明 |
|---|---|---|
| `IDataTableUnitSetting.Mode` | `DataTableMode.Map` | Map 模式，以 "Name" 为主键 |
| `IDataTableUnitSetting.IndexField` | `"Name"` | 主键字段 |
| `IDataTableUnitSetting.LubanInputPath` | `"_temp/" + FileName` | Luban 输入临时路径 |

### NetCmdSettings / NetCmdUnitSetting

字段结构与 `HostKeySettings` / `HostKeyUnitSetting` 完全对称，字段名将 `HostKey` / `HostKeyUnits` 改为 `NetCmd` / `NetCmdUnits`。

---

## §5 完整公开 API

```csharp
// NetworkSettings（数据容器，无方法）
public HostKeySettings HostKeySettings;
public NetCmdSettings NetCmdSettings;

// HostKeySettings 接口实现（IDataTableSettings）
string IDataTableSettings.SourceDirPath          // #if UNITY_EDITOR，返回 SourceDirPath ?? ""
IReadOnlyList<IDataTableUnitSetting> IDataTableSettings.Units   // → HostKeyUnits

// HostKeyUnitSetting 接口实现（IDataTableUnitSetting）
string IDataTableUnitSetting.SourcePath          // #if UNITY_EDITOR
string IDataTableUnitSetting.DatasExportPath     // #if UNITY_EDITOR
string IDataTableUnitSetting.ClassesExportPath   // #if UNITY_EDITOR
string IDataTableUnitSetting.LubanInputPath      // #if UNITY_EDITOR，"_temp/" + Path.GetFileName(SourcePath)
string IDataTableUnitSetting.AssetLocation
DataTableMode IDataTableUnitSetting.Mode         // 固定 Map
string IDataTableUnitSetting.IndexField          // 固定 "Name"
IReadOnlyList<string> IDataTableUnitSetting.DataTypeNames
```

---

## §8 命名空间获取

Network 模块不持有 `Namespace` 字段。`NetworkManager.BuildTablesFromCache` 在 Load 阶段通过以下方式现取：

```csharp
IConfigManager configManager = FrameworkManagersGroup.GetManager<IConfigManager>();
if (configManager == null) return false;
string namespace_ = configManager.Namespace;
```

含 null 保护：ConfigManager 未注册时 return false。

---

## §11 使用示例

```csharp
// NetworkComponent.Start() 中直接取单套单元列表传给 NetworkManagerConfig
m_NetworkManager.Initialize(new NetworkManagerConfig
{
    HostKeyUnitSettings = m_Settings.HostKeySettings.HostKeyUnits,
    NetCmdUnitSettings  = m_Settings.NetCmdSettings.NetCmdUnits
});

// Load 阶段 NetworkManager 内部现取 IConfigManager.Namespace
IConfigManager cm = FrameworkManagersGroup.GetManager<IConfigManager>();
string ns = cm?.Namespace;
```

---

## §13 关联文档

- [NetworkComponent.md](../NetworkComponent.md)
- [NetworkManagerConfig.md](../NetworkManager/Definitions/NetworkManagerConfig.md)
- [IDataTableSettings.md](../../../Core/Table/IDataTableSettings.md)
- [IDataTableUnitSetting.md](../../../Core/Table/IDataTableUnitSetting.md)
- [NetworkExcelPreFilter.md](../../../../Editor/DataPipeline/Implements/Networks/NetworkExcelPreFilter.md)
