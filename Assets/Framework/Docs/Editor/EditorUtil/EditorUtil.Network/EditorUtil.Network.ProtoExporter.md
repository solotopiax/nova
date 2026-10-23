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
// 每个有效 ProtoUnitSetting 按其 CSharpExportPath 分别输出
// settings 为 null 或源目录不存在时 Log.Error + return false
// ProtoUnits 为空时 Log.Warning + return true
// SourcePath/CSharpExportPath 为空或源文件不存在的单元静默跳过，不计失败
// 至少一个 CompileSingle 返回 false 时返回 false；否则返回 true
// 非空单元列表且 allSuccess 为 true 时调用 AssetDatabase.Refresh()
public static bool ExportAllProtos(ProtoSettings settings);
```

---

## §9 关键算法

```
ExportAllProtos(settings):
  1. settings == null → Log.Error + return false
  2. protoDir = settings.ProtoSourceDirPath；目录不存在 → Log.Error + return false
  3. units = settings.ProtoUnits；空 → Log.Warning + return true
  4. rootNorm = protoDir.TrimEnd('/','\\')；allSuccess = true
     for each unit in units:
     a. SourcePath 或 CSharpExportPath 为空 → 跳过
     b. fullProtoPath = Combine(rootNorm, unit.SourcePath)
     c. 文件不存在 → 跳过
     d. Proto.CliRunner.CompileSingle(fullProtoPath, rootNorm, unit.CSharpExportPath) 返回 false → allSuccess = false；继续处理后续单元
  5. allSuccess → AssetDatabase.Refresh()
  6. return allSuccess
```

返回 true 仅表示前置检查通过且没有实际调用的 `CompileSingle` 返回 false；空单元列表或全部单元被跳过也会返回 true，不能据此判断所有配置均已导出。空列表提前返回，不触发 Refresh；非空列表全部跳过时仍会触发 Refresh。null 单元没有防护，访问字段会抛出异常；目录读写等异常也不在此方法中转换为 false。

与 `EditorUtil.Proto.CliRunner.CompileAll` 的区别：`CompileAll` 递归扫描根目录并在统一输出根目录下保留相对层级；`ExportAllProtos` 仅处理配置列表，逐单元使用 `CSharpExportPath`。

### 真实入口与失败边界

- `NetworkComponentInspector.DrawProtoExportButton` 的“导出所有协议类型”按钮调用此方法；按钮依据源目录和序列化列表是否存在决定可用状态，不检查导出返回值。
- Pipify `export.network.proto` 对应 `PipifySteps.RunExportNetworkProto`，先解析 Nova 层级下的 `NetworkComponent`。未找到 Nova 或组件时抛出 `InvalidOperationException`；`ProtoSettings` 为 null 或此方法返回 false 时也抛出异常中断流水线。单元被跳过后返回 true 的情况不会触发该失败判断。
- `nova.project.network.export` 的 `proto` 目标通过 `NetworkExportAction.ExecuteTarget` 复用此方法。其 `TryPlanProto` 会先拒绝空列表、null 单元、空白路径及缺失或越界的源文件，不能将底层导出器的宽松返回语义套用到该 Action 的计划门。当前 Action 是否可调用，以运行中的 Registry 和 MCP 为准。

`CompileSingle` 会扫描根目录内的普通 import，并可能回写其他 `.proto` 源文件。源文件修改和已生成代码不会在失败时回滚；Refresh 也不等于 Unity 编译或运行验证通过。

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
