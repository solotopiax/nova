# ILubanTableBinding

`ILubanTableBinding` 是 Luban 生成代码与 Nova 可选资源加载能力之间的最小适配接口。

```csharp
public interface ILubanTableBinding
{
    IReadOnlyList<string> DataFiles { get; }
    ILubanTables Create(Func<string, byte[]> loader);
}
```

- `DataFiles` 来自 Luban 的 `output_data_file`，用于异步预加载原始单表资源。
- `Create` 把原始字节转换为目标 Codec 的 Loader，并调用对应生成 `Tables` 构造函数。
- 一个项目可以配置任意数量的 Binding；自定义 Luban target 可通过自定义模板或业务实现接入。

Nova 的 Table 专用 JSON、Binary 和 Protobuf 模板会生成同名 `*TablesBinding`。JSON Binding 同时识别 JSON 与 MsgPack 数据，Protobuf Binding 同时支持 Binary 与 JSON 数据。
模板生成的 Binding 带有 `UnityEngine.Scripting.Preserve`。业务自行实现且只通过 `BindingTypeName` 引用的类型也需要保留裁剪标记。

## 关联文档

- [ILubanTables.md](ILubanTables.md)
- [TableManager.md](../../Modules/Table/TableManager.md)
- [TableSettings.md](../../Modules/Table/Definitions/TableSettings.md)
