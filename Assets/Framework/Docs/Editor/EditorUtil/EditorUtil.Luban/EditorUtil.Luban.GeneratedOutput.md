# EditorUtil.Luban.GeneratedOutput

**类签名**：`internal static class GeneratedOutput`（嵌套于 `EditorUtil.Luban`）

**命名空间**：`NovaFramework.Editor`

`GeneratedOutput` 是非 Table 模块共用的 Luban C# 产物所有权基础设施。它根据本次 `SchemaManifest` 验证预期代码、写入可自证的文件头，并把替换和安全过期删除登记给 `FileSystem.OutputApplier`；它不解释任何模块的 Excel 结构或导出时序。

## 第一行所有权标记

每个正式生成 C# 的第一行固定为一条完整单行标记，无论字段内容多长都不换行：

```csharp
// <nova-generated profile="sound" source="Assets/Samples/MainDemo/Excels/Sounds" artifact="Sound.cs" content-hash="sha256:..." />
```

- `profile`：Luban ExportProfile 的稳定 ID。
- `source`：项目内使用标准化项目相对路径；项目外测试路径使用标准化完整路径。
- `artifact`：当前生成文件名。
- `content-hash`：移除所有权标记后的正文 SHA-256；计算前把 CRLF/CR 统一为 LF。
- 重复写入时先识别并移除旧标记，因此文件头不会累积。

## 安全过期删除

全量代码发布先从当前 `SchemaManifest` 计算预期文件集合。正式目录中不在集合内的 `.cs` 只有同时满足以下条件才会删除：

1. 第一行标记可解析；
2. `profile` 与当前导出完全一致；
3. `source` 与当前数据源完全一致；
4. 当前正文 Hash 与标记中的 Hash 一致。

无标记、跨 Profile/Source 或正文被人工修改的文件一律保留并输出 Warning。对应 `.meta` 只会随已通过上述判断的 `.cs` 一起登记删除。

## 产物清单缓存

全量代码发布会更新：

```text
{SourceDir}/_configs/output-manifests/{ProfileId}.json
```

该文件记录最近一次 Profile、Source、代码输出目录和预期文件名，便于排查与重建。它不是删除依据：即使缓存缺失、过期或由其他维护者留下，安全删除仍只依据当前 SchemaManifest 和正式 C# 自身的所有权标记与正文 Hash。

## 关联文档

- [EditorUtil.Luban.SchemaManifest.md](EditorUtil.Luban.SchemaManifest.md)
- [EditorUtil.Luban.ExportProfile.md](EditorUtil.Luban.ExportProfile.md)
- [EditorUtil.FileSystem.OutputApplier.md](../EditorUtil.FileSystem/EditorUtil.FileSystem.OutputApplier.md)
- [DataPipeline.md](../../DataPipeline/DataPipeline.md)
