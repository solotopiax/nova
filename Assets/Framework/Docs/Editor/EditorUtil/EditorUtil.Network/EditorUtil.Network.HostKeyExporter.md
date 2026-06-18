# EditorUtil.Network.HostKeyExporter

**类签名**：`public static class HostKeyExporter`（嵌套于 `EditorUtil.Network`）
**命名空间**：`NovaFramework.Editor`

域名表（HostKey）导出工具，封装数据导出和类型代码生成的一键全量导出入口；内部通过 `NetworkExcelPreFilter` 预过滤后调用 Luban Pipeline。

---

## §2 文件表

| 文件 | 类 | 说明 |
|------|----|------|
| `Editor/EditorUtil/EditorUtil.Network/EditorUtil.Network.HostKeyExporter.cs` | `EditorUtil.Network.HostKeyExporter` | 域名表导出工具类 |

---

## §3 继承关系

```
EditorUtil (public static partial class)
  └── EditorUtil.Network (public static partial class)
        └── HostKeyExporter (public static class)
```

---

## §4 关键字段表

| 字段 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `c_TargetName` | `const string` | `"network-hostkey"` | Luban target 名称 |
| `c_ManagerName` | `const string` | `"HostKeyTables"` | Luban manager 类名 |
| `c_SettingsPropertyName` | `const string` | `"HostKeySettings"` | Luban settings 属性名 |

---

## §5 完整公开 API

```csharp
// 导出域名表所有单元的数据和类型代码（等价于 Inspector "导出所有域名表数据和类型"）
// settings 为 null 时 Log.Error + return false
public static bool ExportHostKeyAll(HostKeySettings settings);

// 仅导出域名类型代码（跳过数据，重新生成 C# 枚举/常量类）
// settings 为 null 时 Log.Error + return false
public static bool ExportHostKeyCode(HostKeySettings settings);

// 仅导出域名数据（跳过类型代码，重新生成 JSON 数据文件）
// settings 为 null 时 Log.Error + return false
public static bool ExportHostKeyData(HostKeySettings settings);
```

---

## §9 关键算法

### 导出流程（以 ExportHostKeyData 为例）

```
ExportHostKeyData(settings):
  1. settings == null → Log.Error + return false
  2. ExportData(settings.SourceDirPath, settings, settings.HostKeyUnits)

ExportData(regionDirPath, settings, units):
  1. regionDirPath 空或目录不存在 → Log.Error + return false
  2. tempDir = ExportHelper.GetPreFilterTempDirPath(regionDirPath)
  3. ConfigSyncer.CleanTempDir(tempDir)
  4. try:
     a. NetworkExcelPreFilter.FilterAll(regionDirPath, tempDir)
        → DevelopValue/PublishValue 合并 + Platform/Channel 过滤
     b. BuildExportContext(regionDirPath, settings, "network-hostkey", "HostKeyTables") → ctx
     c. ctx.RegionUnits = units
     d. Pipeline.ExportData(ctx)
  5. finally: ConfigSyncer.CleanTempDir(tempDir)
  6. return true
```

`ExportHostKeyAll` 和 `ExportHostKeyCode` 除额外设置 `ctx.OutputCodeDir` 外流程相同；ExportAll 还需要提取 classExportPath。

---

## §11 使用示例

```csharp
// NetworkComponentInspector 中一键全量导出
bool ok = EditorUtil.Network.HostKeyExporter.ExportHostKeyAll(networkSettings.HostKeySettings);

// Pipify Step 中仅导出域名数据
bool ok = EditorUtil.Network.HostKeyExporter.ExportHostKeyData(networkSettings.HostKeySettings);
```

---

## §13 关联文档

- [NetworkSettings.md](../../../Runtime/Modules/Network/Definitions/NetworkSettings.md)
- [NetworkExcelPreFilter.md](../../DataPipeline/Implements/Networks/NetworkExcelPreFilter.md)
- [EditorUtil.Network.NetCmdExporter.md](./EditorUtil.Network.NetCmdExporter.md)
- [PipifySteps.md](../EditorUtil.Pipify/PipifySteps.md)
