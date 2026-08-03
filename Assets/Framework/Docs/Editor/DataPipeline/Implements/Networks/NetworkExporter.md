# NetworkExporter

**类签名**：`internal static class NetworkExporter`

**命名空间**：`NovaFramework.Editor`

`NetworkExporter` 是 Network 模块的数据、类型与单文件导出编排器。它把 HostKeys/NetCmds 的预处理、Luban 暂存生成、产物验证和正式发布串成一条失败安全的链路。

## 调用关系

```text
Inspector / Pipify
  -> EditorUtil.Network.HostKeyExporter / NetCmdExporter（公共门面）
  -> NetworkExporter（预处理与导出编排）
  -> EditorUtil.FileSystem.OutputApplier（批量应用与失败回滚）
```

Network 没有额外的 `NetworkOutputApplier`：它不需要模块级重入保护或特殊应用语义，因此直接复用通用 `FileSystem.OutputApplier`，避免增加只转发调用的脚本。

## HostKeys 流程

```text
读取当前 ConfigRuntimeSO.DevelopMode
  -> NetworkExcelPreFilter 校验全部 Debug/Release Sheet 配对
  -> 只把当前模式投影到源目录 _temp
  -> Luban 输出当前格式数据/C# 到 _temp/_publish
  -> 验证目标数据、SchemaManifest 与预期 C# 文件
  -> Luban.GeneratedOutput 写入第一行单行所有权标记并登记安全过期删除
  -> OutputApplier 一次性替换正式产物
  -> finally 清理 _temp 并刷新 AssetDatabase
```

当前 ConfigRuntime 不存在时立即终止并提示先导出 Config。Network 不维护第二份 DevelopMode，避免环境选择与 Config 漂移。

## NetCmds 流程

NetCmds 使用同一套暂存发布流程，但预处理保持原 Sheet 结构，不读取或筛选 DevelopMode。

全量、仅数据、仅类型及 Inspector 单文件导出都经过本编排器。代码导出前会把当前格式的正式数据复制到暂存区，供 Map 属性生成读取；Pipeline 返回失败或暂存产物缺失时，不登记正式替换。

## 失败语义

- 应用前失败：正式数据/C# 保持不变。
- 应用中失败：`FileSystem.OutputApplier` 通过备份逆序恢复。
- 成功或失败后都会清理 `_temp`、`_publish` 与备份目录。
- `ExportOperations` 只提供定向测试替换点，不构成新的生产抽象层。
- 全量代码清理不读取本地清单作删除裁决；只有 Profile、Source 与正文 Hash 均匹配的过期文件可删除。

## 关联文档

- [NetworkExcelPreFilter.md](NetworkExcelPreFilter.md)
- [EditorUtil.Network.HostKeyExporter.md](../../../EditorUtil/EditorUtil.Network/EditorUtil.Network.HostKeyExporter.md)
- [EditorUtil.Network.NetCmdExporter.md](../../../EditorUtil/EditorUtil.Network/EditorUtil.Network.NetCmdExporter.md)
- [EditorUtil.FileSystem.OutputApplier.md](../../../EditorUtil/EditorUtil.FileSystem/EditorUtil.FileSystem.OutputApplier.md)
- [EditorUtil.Luban.GeneratedOutput.md](../../../EditorUtil/EditorUtil.Luban/EditorUtil.Luban.GeneratedOutput.md)
- [DataPipeline.md](../../DataPipeline.md)
