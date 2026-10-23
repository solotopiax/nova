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

NetCmds 不按 DevelopMode 选择 Sheet，也不要求 Debug/Release 配对。预处理仅跳过注释或无效 Sheet；Luban 按 `NetworkSettings.DataFormat` 生成 JSON 或 Binary 数据及对应 C#，先写入 `_temp/_publish`，验证后再由 `FileSystem.OutputApplier` 替换正式产物并删除反格式文件及其 `.meta`。HostKey 与 NetCmd 共用同一个格式选项。

## 使用示例

```csharp
bool ok = EditorUtil.Network.NetCmdExporter.ExportNetCmdAll(networkSettings.NetCmdSettings);
```

## 关联文档

- [NetworkExporter.md](../../DataPipeline/Implements/Networks/NetworkExporter.md)
- [NetworkExcelPreFilter.md](../../DataPipeline/Implements/Networks/NetworkExcelPreFilter.md)
- [NetworkSettings.md](../../../Runtime/Modules/Network/Definitions/NetworkSettings.md)

## 公共门面边界

All/Code/Data 原样转内部 NetworkExporter，bool 与异常不做统一转换；HostKey 读取当前导出 Runtime 的 DevelopMode，NetCmd 不具有这一前置。Inspector、Pipify 和 Action 的配置取得与失败处理各有职责；普通门面没有 Action 的确认、冻结和路径授权门，false 不能当作“没有任何工作配置写入”的证明。
