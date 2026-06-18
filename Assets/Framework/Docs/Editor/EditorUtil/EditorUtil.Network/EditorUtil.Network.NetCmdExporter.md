# EditorUtil.Network.NetCmdExporter

**类签名**：`public static class NetCmdExporter`（嵌套于 `EditorUtil.Network`）
**命名空间**：`NovaFramework.Editor`

指令表（NetCmd）导出工具，封装数据导出和类型代码生成的一键全量导出入口；内部通过 `NetworkExcelPreFilter` 预过滤后调用 Luban Pipeline。

---

## §2 文件表

| 文件 | 类 | 说明 |
|------|----|------|
| `Editor/EditorUtil/EditorUtil.Network/EditorUtil.Network.NetCmdExporter.cs` | `EditorUtil.Network.NetCmdExporter` | 指令表导出工具类 |

---

## §3 继承关系

```
EditorUtil (public static partial class)
  └── EditorUtil.Network (public static partial class)
        └── NetCmdExporter (public static class)
```

---

## §4 关键字段表

| 字段 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `c_TargetName` | `const string` | `"network-cmd"` | Luban target 名称 |
| `c_ManagerName` | `const string` | `"NetworkTables"` | Luban manager 类名 |

---

## §5 完整公开 API

```csharp
// 导出指令表所有单元的数据和类型代码（等价于 Inspector "导出所有指令表数据和类型"）
// settings 为 null 时 Log.Error + return false
public static bool ExportNetCmdAll(NetCmdSettings settings);

// 仅导出指令类型代码（跳过数据，重新生成 C# 枚举/常量类）
// settings 为 null 时 Log.Error + return false
public static bool ExportNetCmdCode(NetCmdSettings settings);

// 仅导出指令数据（跳过类型代码，重新生成 JSON 数据文件）
// settings 为 null 时 Log.Error + return false
public static bool ExportNetCmdData(NetCmdSettings settings);
```

---

## §9 关键算法

与 `HostKeyExporter` 结构完全一致，差异在于：

- Luban target = `"network-cmd"`，manager = `"NetworkTables"`
- 数据源取自 `settings.SourceDirPath`，单元列表取自 `settings.NetCmdUnits`

内部同样通过 `NetworkExcelPreFilter.FilterAll` 完成 Platform/Channel 过滤，`ctx.RegionUnits = units` 后调用 Pipeline。

---

## §11 使用示例

```csharp
// NetworkComponentInspector 中仅导出指令数据
bool ok = EditorUtil.Network.NetCmdExporter.ExportNetCmdData(networkSettings.NetCmdSettings);

// Pipify Step 中仅导出指令类型
bool ok = EditorUtil.Network.NetCmdExporter.ExportNetCmdCode(networkSettings.NetCmdSettings);
```

---

## §13 关联文档

- [NetworkSettings.md](../../../Runtime/Modules/Network/Definitions/NetworkSettings.md)
- [NetworkExcelPreFilter.md](../../DataPipeline/Implements/Networks/NetworkExcelPreFilter.md)
- [EditorUtil.Network.HostKeyExporter.md](./EditorUtil.Network.HostKeyExporter.md)
- [PipifySteps.md](../EditorUtil.Pipify/PipifySteps.md)
