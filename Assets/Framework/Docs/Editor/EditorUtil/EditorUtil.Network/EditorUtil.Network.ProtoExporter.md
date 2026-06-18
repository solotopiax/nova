# EditorUtil.Network.ProtoExporter

**类签名**：`public static class ProtoExporter`（嵌套于 `EditorUtil.Network`）
**命名空间**：`NovaFramework.Editor`

Proto 协议导出工具，读取 `ProtoSettings` 配置，按每个 `ProtoUnitSetting.CSharpExportPath` 独立调用 `EditorUtil.Proto.CliRunner.CompileSingle`；等价于 Inspector"导出所有协议类型"按钮。

---

## §2 文件表

| 文件 | 类 | 说明 |
|------|----|------|
| `Editor/EditorUtil/EditorUtil.Network/EditorUtil.Network.ProtoExporter.cs` | `EditorUtil.Network.ProtoExporter` | Proto 导出工具类 |

---

## §3 继承关系

```
EditorUtil (public static partial class)
  └── EditorUtil.Network (public static partial class)
        └── ProtoExporter (public static class)
```

---

## §4 关键字段表

无字段（静态工具类）。

---

## §5 完整公开 API

```csharp
// 编译 ProtoSettings 中所有已配置的 .proto 文件为 C# 代码
// 每个 ProtoUnitSetting 按其 CSharpExportPath 分别输出（与 Inspector 行为完全一致）
// settings 为 null 或源目录不存在时 Log.Error + return false
// ProtoUnits 为空时 Log.Warning + return true
// 全部成功后调用 AssetDatabase.Refresh()
public static bool ExportAllProtos(ProtoSettings settings);
```

---

## §9 关键算法

```
ExportAllProtos(settings):
  1. settings == null → Log.Error + return false
  2. protoDir = settings.ProtoSourceDirPath；目录不存在 → Log.Error + return false
  3. units = settings.ProtoUnits；空 → Log.Warning + return true
  4. for each unit in units:
     a. SourcePath 或 CSharpExportPath 为空 → 跳过
     b. fullProtoPath = Combine(protoDir.TrimEnd('/','\\'), unit.SourcePath)
     c. 文件不存在 → 跳过
     d. Proto.CliRunner.CompileSingle(fullProtoPath, protoDir, unit.CSharpExportPath) 失败 → allSuccess = false
  5. allSuccess → AssetDatabase.Refresh()
  6. return allSuccess
```

与 `EditorUtil.Proto.CliRunner.CompileAll` 的区别：`CompileAll` 将所有文件输出到同一目录；`ExportAllProtos` 按每个单元的 `CSharpExportPath` 分别输出。

---

## §11 使用示例

```csharp
// NetworkComponentInspector 中批量导出所有协议
bool ok = EditorUtil.Network.ProtoExporter.ExportAllProtos(protoSettings);

// Pipify Step 中导出所有 Proto（#if UNITY_EDITOR 包裹）
bool ok = EditorUtil.Network.ProtoExporter.ExportAllProtos(networkComponent.ProtoSettings);
```

---

## §12 注意事项

- `ProtoSettings` 仅在 `#if UNITY_EDITOR` 下可用，Pipify Step 中取值也需加编译条件
- 按单元独立指定输出目录时，同一 proto 源文件可以输出到不同目录（根据配置）

---

## §13 关联文档

- [ProtoSettings.md](../../../Runtime/Modules/Network/Definitions/ProtoSettings.md)
- [EditorUtil.Proto.CliRunner.md](../EditorUtil.Proto/EditorUtil.Proto.CliRunner.md)
- [PipifySteps.md](../EditorUtil.Pipify/PipifySteps.md)
