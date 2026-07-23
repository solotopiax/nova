# EditorUtil.Network.NetCmdExporter

**类签名**：`public static class NetCmdExporter`（嵌套于 `EditorUtil.Network`）

**命名空间**：`NovaFramework.Editor`

`NetCmdExporter` 是 NetCmds 的稳定公共门面。它保持现有表格结构与 API，把导出请求交给内部 `NetworkExporter` 完成暂存和发布。

## 公开 API

```csharp
public static bool ExportNetCmdAll(NetCmdSettings settings);
public static bool ExportNetCmdCode(NetCmdSettings settings);
public static bool ExportNetCmdData(NetCmdSettings settings);
```

NetCmds 不按 DevelopMode 选择 Sheet，也不要求 Debug/Release 配对。预处理仅跳过注释或无效 Sheet；Luban 生成的 JSON/C# 先写入 `_temp/_publish`，验证后再由 `FileSystem.OutputApplier` 替换正式产物。

## 使用示例

```csharp
bool ok = EditorUtil.Network.NetCmdExporter.ExportNetCmdAll(networkSettings.NetCmdSettings);
```

## 关联文档

- [NetworkExporter.md](../../DataPipeline/Implements/Networks/NetworkExporter.md)
- [NetworkExcelPreFilter.md](../../DataPipeline/Implements/Networks/NetworkExcelPreFilter.md)
- [NetworkSettings.md](../../../Runtime/Modules/Network/Definitions/NetworkSettings.md)
