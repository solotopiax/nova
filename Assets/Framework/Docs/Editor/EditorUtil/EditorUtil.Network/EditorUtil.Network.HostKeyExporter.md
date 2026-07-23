# EditorUtil.Network.HostKeyExporter

**类签名**：`public static class HostKeyExporter`（嵌套于 `EditorUtil.Network`）

**命名空间**：`NovaFramework.Editor`

`HostKeyExporter` 是 HostKeys 的稳定公共门面。它不自行处理 Excel 或直写正式文件，而是把请求交给内部 `NetworkExporter`。

## 公开 API

```csharp
public static bool ExportHostKeyAll(HostKeySettings settings);
public static bool ExportHostKeyCode(HostKeySettings settings);
public static bool ExportHostKeyData(HostKeySettings settings);
```

三种入口分别导出数据与类型、仅类型、仅数据。共同规则如下：

- 当前已导出的 `ConfigRuntimeSO` 是 DevelopMode 唯一真相源；不存在时返回 `false` 并提示先导出 Config。
- 所有有效 Sheet 必须按 `xxxxx-Debug` / `xxxxx-Release` 成对存在。
- Luban 只读取当前 DevelopMode 对应的 Sheet，临时表名会去掉模式后缀。
- 生成结果先暂存并验证，再通过 `FileSystem.OutputApplier` 批量发布；失败不会留下部分正式产物。

## 使用示例

```csharp
bool ok = EditorUtil.Network.HostKeyExporter.ExportHostKeyAll(networkSettings.HostKeySettings);
```

## 关联文档

- [NetworkExporter.md](../../DataPipeline/Implements/Networks/NetworkExporter.md)
- [NetworkExcelPreFilter.md](../../DataPipeline/Implements/Networks/NetworkExcelPreFilter.md)
- [NetworkSettings.md](../../../Runtime/Modules/Network/Definitions/NetworkSettings.md)
