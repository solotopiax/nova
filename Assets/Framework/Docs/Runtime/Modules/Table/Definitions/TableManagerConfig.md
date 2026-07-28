# TableManagerConfig

`TableManagerConfig` 是 `TableComponent.Start()` 传给 `ITableManager.Initialize` 的运行时配置。

```csharp
public sealed class TableManagerConfig
{
    public IReadOnlyList<TableRuntimeBindingSetting> Bindings;
}
```

每条 Binding 独立声明生成 Binding 类型和资源地址前缀。表清单、构造函数 Loader 与 Codec 解码由对应 `ILubanTableBinding` 决定。

## 关联文档

- [TableSettings.md](TableSettings.md)
- [TableManager.md](../TableManager.md)
- [TableComponent.md](../TableComponent.md)
