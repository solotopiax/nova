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

Network 直接复用 `FileSystem.AcquireWorkspace` 的当前 Editor 进程内租约与 `FileSystem.OutputApplier` 的文件补偿，不增加模块级 `NetworkOutputApplier`。

## HostKeys 流程

```text
读取当前 ConfigRuntimeSO.DevelopMode
  -> NetworkExcelPreFilter 校验全部 Debug/Release Sheet 配对
  -> 只把当前模式投影到源目录 _temp
  -> Luban 输出当前格式数据/C# 到 _temp/_publish
  -> 验证目标数据、SchemaManifest 与预期 C# 文件
  -> Luban.GeneratedOutput 写入第一行单行所有权标记并登记安全过期删除
  -> OutputApplier 一次性替换正式产物
  -> 持有工作区租约清理 _temp 并刷新 AssetDatabase，最后释放租约
```

当前 ConfigRuntime 不存在时立即终止并提示先导出 Config。Network 不维护第二份 DevelopMode，避免环境选择与 Config 漂移。

## NetCmds 流程

NetCmds 使用同一套暂存发布流程，但预处理保持原 Sheet 结构，不读取或筛选 DevelopMode。

全量、仅数据、仅类型及 Inspector 单文件导出都经过本编排器。代码导出前会把当前格式的正式数据复制到暂存区，供 Map 属性生成读取；Pipeline 返回失败或暂存产物缺失时，不登记正式替换。

## 失败语义

- 工作区租约获取失败：返回失败，不清理已有 `_temp` 或 `.meta`，不运行预处理、Pipeline 或刷新；HostKeys 与 NetCmds 若使用同一源目录，共享该租约保护。
- 成功取得租约后，预处理、暂存、发布、清场和刷新一直处于同一租约内。最外层 `finally` 释放租约，刷新抛错也不会遗留租约；保护只在当前 Editor 进程生效。
- 应用前失败：正式数据/C# 保持不变。
- 应用中失败：`FileSystem.OutputApplier` 通过备份逆序恢复。
- 租约持有者在成功或失败后尝试清理 `_temp`、`_publish` 与备份目录；删除失败记录警告，不能据返回值保证文件系统清场或补偿全部成功。
- `ExportOperations` 只提供定向测试替换点，不构成新的生产抽象层。
- 全量代码清理不读取本地清单作删除裁决；只有 Profile、Source 与正文 Hash 均匹配的过期文件可删除。

## 关联文档

- [NetworkExcelPreFilter.md](NetworkExcelPreFilter.md)
- [EditorUtil.Network.HostKeyExporter.md](../../../EditorUtil/EditorUtil.Network/EditorUtil.Network.HostKeyExporter.md)
- [EditorUtil.Network.NetCmdExporter.md](../../../EditorUtil/EditorUtil.Network/EditorUtil.Network.NetCmdExporter.md)
- [EditorUtil.FileSystem.OutputApplier.md](../../../EditorUtil/EditorUtil.FileSystem/EditorUtil.FileSystem.OutputApplier.md)
- [EditorUtil.Luban.GeneratedOutput.md](../../../EditorUtil/EditorUtil.Luban/EditorUtil.Luban.GeneratedOutput.md)
- [DataPipeline.md](../../DataPipeline.md)

## 全源验证与普通 API 的范围

单目标仍可能预过滤整个源根，其他 workbook 非法可阻目标；目标 Unit 按引用属于正式列表，不用等值副本替代。普通校验不完整核源归属、实际文件、后缀、格式枚举或规范化后的数据目标碰撞。NetCmd 不读 Runtime 模式，HostKey 在进入 Execute 前取得模式，前置校验/回调可外抛。代码从旧正式数据种入，缺种子可跳；空正式数据路径可能跳发布，真实结果不能只看 bool。
