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
- 数据格式来自 `NetworkSettings.DataFormat`，与 NetCmd 共用一个选项，默认 JSON；Binary 使用 `.bytes`。
- 所有有效 Sheet 必须按 `xxxxx-Debug` / `xxxxx-Release` 成对存在。
- Luban 只读取当前 DevelopMode 对应的 Sheet，临时表名会去掉模式后缀。
- `Value` 主域名列必须存在且有效行必须填写；`FallbackValue` 备用域名可为空。旧表缺少备用列时按空备用处理，仍校验主域名；缺少主域名列会在临时输入写入前明确失败。
- 非空地址必须是无首尾空格、无末尾斜杠的 HTTP(S) 基础地址，同时填写时协议必须相同。官方模板提供空备用列，`XXXXX` 占位主域名必须替换为实际地址再导出。
- NetCmd 的 `Path` 可以为空；非空时必须以 `/` 开头。
- 生成结果先暂存并验证，再通过 `FileSystem.OutputApplier` 批量发布；失败时尝试补偿，IO 或回滚异常仍可能留下部分正式产物。
- 发布数据时会在同一事务中删除同名反格式文件及其 `.meta`。

## 使用示例

```csharp
bool ok = EditorUtil.Network.HostKeyExporter.ExportHostKeyAll(networkSettings.HostKeySettings);
```

## 关联文档

- [NetworkExporter.md](../../DataPipeline/Implements/Networks/NetworkExporter.md)
- [NetworkExcelPreFilter.md](../../DataPipeline/Implements/Networks/NetworkExcelPreFilter.md)
- [NetworkSettings.md](../../../Runtime/Modules/Network/Definitions/NetworkSettings.md)

## 公共门面边界

All/Code/Data 原样转内部 NetworkExporter，bool 与异常不做统一转换；HostKey 读取当前导出 Runtime 的 DevelopMode，NetCmd 不具有这一前置。Inspector、Pipify 和 Action 的配置取得与失败处理各有职责；普通门面没有 Action 的确认、冻结和路径授权门，false 不能当作“没有任何工作配置写入”的证明。
